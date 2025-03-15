local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("LoginMsgProcesser", baseClass)

local json = require("json")
local MsgIds = require("_NetMsg.MsgId")
local moon = require("moon")
local socket = require "moon.socket"
require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")
require("Config.ErrorCode")
require("LoginServer.SessionState")

local SessionManager = require("LoginServer.SessionManager")
local SessionClass = require("LoginServer.Session")


require("LuaPanda").start("127.0.0.1", ServerData.Debug)

--------------------------------------------- 客户端发送服务器协议处理 --------------------

--- 客户端发送给服务器请求协议处理
local ClientToServerMsgProcess = {
    [MsgIds.CM_ReqDS] = function (self, msg, socket, fd)
        -- 请求DS地图
        local Session = SessionManager:GetSession(socket, fd)
        if not Session then
            return
        end
        if not Session:CanReqDS() then
            -- 通知客户端报错
            self:SendTableToJson2(socket, fd, MsgIds.SM_DS_Info, {result = _MOE.ErrorCode.LOGIN_REQ_DS_EXIST})
            return
        end
        -- 获取Token
        if not self:SendServerMsgAsync("DSA", _MOE.ServerMsgIds.CM_ReqDS,
            {serverName = "LoginSrv", sceneName = msg.sceneName, client = fd, uid = Session:GetUUID(), loginToken = Session:GetLoginToken()}) then
            self:SendTableToJson2(socket, fd, MsgIds.SM_DS_Info, {result = _MOE.ErrorCode.DSA_REQ_DS_ERROR})
        else
            Session:SetState(_MOE.SessionState.ReqDS) -- 请求DS
        end
    end,
    [MsgIds.CM_Login] = function (self, msg, socket, fd)
        -- 登录账号

        local loginToken = GenerateToken(socket, fd)
        if not loginToken then
            return
        end
        if SessionManager:ExistsByLoginToken(loginToken) then
            self:SendTableToJson2(socket, fd, MsgIds.SM_LOGIN_RET, {result = _MOE.ErrorCode.LOGIN_EXIST_LOGINED})
            return
        end


        if not msg.userName or string.len(msg.userName) <= 0 then
            self:SendTableToJson2(socket, fd, MsgIds.SM_LOGIN_RET, {result = _MOE.ErrorCode.LOGIN_INVAILD_PARAM})
            return
        end

        -- self:PrintMsg(msg)

        self:SendServerMsgAsync("DBSrv", _MOE.ServerMsgIds.CM_Login, {userName = msg.userName,
            password = msg.password, client = fd, serverName = "LoginSrv"})
    end
}
-----------------------------

RegisterClientMsgProcess(ClientToServerMsgProcess)

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    [_MOE.ServicesCall.InitDB] = function ()
        ----------- 连接Redis，用Redis来做客户端发包频率记录限制 ----------------------------------
        return true
    end,


    [_MOE.ServerMsgIds.SM_DSReady] = function (msg)
        local clients = msg.clients
        if clients then
            local clientMsg = {
                result = msg.result,
                dsData = msg.dsData,
                dsToken = msg.dsToken,
            }
            for _, fd in ipairs(clients) do
                local Session = SessionManager:GetSession(socket, fd)
                if Session then
                    Session:SetState(_MOE.SessionState.LoginDS)
                    print("[SM_DSReady] client:", fd)
                    MsgProcesser:SendTableToJson2(socket, fd, MsgIds.SM_DS_Info, clientMsg) -- 通知客户端
                end
            end
        end
    end,
    [_MOE.ServerMsgIds.SM_Login_Ret] = function (msg) -- DB 返回登录数据
        local fd = msg.client
        local result = msg.result
        if result == _MOE.ErrorCode.NOERROR then
            local clientIp, clientPort = GetIpAndPort(socket, fd)
            -- print("clientIp", clientIp, "clientPort", clientPort)

            --- 关闭之前已经连接的Session并删除
            SessionManager:CloseSocketAndRemove(msg.user.uuid, _MOE.ErrorCode.LOGIN_KICKOFF_OTHER_LOGIN)
            -----------------------------------

            local Session = SessionClass.New(clientIp, clientPort, msg.user.uuid, fd)
            SessionManager:AddSession(Session) -- 增加Session
            local retMsg = {
                result = msg.result,
                user = msg.user,
            }

            -- 附上上次登录的缓存信息
            local isHasLocalDs = false
            if _MOE.FreeSessionTask:AttachSession(Session) then
                -- 判断DS是否存在，不存在直接删除
                isHasLocalDs = Session:HasdsData()
                if isHasLocalDs then
                    -- 检查DS是否是有效的
                    local dsData = Session.dsData
                    if not MsgProcesser:SendServerMsgSync("DSA", _MOE.ServerMsgIds.SM_LS_DSA_CheckPlayerDS,
                        {dsToken = dsData.dsToken}) then
                        Session.dsData = nil
                        isHasLocalDs = false
                    else
                        retMsg.dsData = dsData
                    end
                end
            end
            -- 删除掉原来的
            _MOE.FreeSessionTask:RemoveFreeSession(Session, not isHasLocalDs)

            retMsg.user.token = Session:GetLoginToken()

            -- _MOE.TableUtils.PrintTable2(retMsg)

            MsgProcesser:SendTableToJson2(socket, fd, MsgIds.SM_LOGIN_RET, retMsg)
        else
            -- 通知客户端失败
            MsgProcesser:SendTableToJson2(socket, fd, MsgIds.SM_LOGIN_RET, {
                result = msg.result
            })
            ----------------
        end
    end,
    [_MOE.ServerMsgIds.CM_DS_PlayerConnect] = function (msg)
        print("[CM_DS_PlayerConnect]")
        local loginToken = msg.loginToken
        if not loginToken then
            print("[CM_DS_PlayerConnect] Error: not loginToken")
            return
        end
        local session = SessionManager:GetSessionByToken(loginToken)
        if not session then
            print("[CM_DS_PlayerConnect] Error: session not found")
            return
        end
        session:LoginInDS(msg)
    end,
    [_MOE.ServerMsgIds.CM_DS_PlayerDisConnect] = function (msg)
        print("[CM_DS_PlayerDisConnect]")
        local loginToken = msg.loginToken
        if not loginToken then
            print("[CM_DS_PlayerDisConnect] Error: not loginToken")
            return
        end
        local session = SessionManager:GetSessionByToken(loginToken)
        if not session then
            print("[CM_DS_PlayerDisConnect] Error: session not found")
            return
        end
        if session:LoginoutDS(msg) then
            _MOE.FreeSessionTask:AddFreeSession(session)
        end
    end,
}
----------------------------

---- 服务器间同步消息定义
local _SERVER_SYNC_MSG = {
    [_MOE.ServerMsgIds.SM_LS_DSA_CheckPlayerDS] = true,
}
-----------------------------------------

RegisterServerCommandSync(_SERVER_SYNC_MSG)
RegisterServerCommandProcess(_OtherServerToMyServer)

return _M