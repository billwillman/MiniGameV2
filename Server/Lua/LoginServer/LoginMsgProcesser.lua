local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("LoginMsgProcesser", baseClass)

local json = require("json")
local MsgIds = require("_NetMsg.MsgId")
local moon = require("moon")
local socket = require "moon.socket"
require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")


require("LuaPanda").start("127.0.0.1", ServerData.Debug)

--------------------------------------------- 客户端发送服务器协议处理 --------------------

--- 客户端发送给服务器请求协议处理
local ClientToServerMsgProcess = {
    [MsgIds.CM_ReqDS] = function (self, msg, socket, fd)
        -- 请求DS地图
        self:SendServerMsgAsync("DSA", _MOE.ServerMsgIds.CM_ReqDS, {serverName = "LoginSrv", client = fd})
    end,
    [MsgIds.CM_Login] = function (self, msg, socket, fd)
        -- 登录账号

        if not msg.userName or string.len(msg.userName) <= 0 then
            self:SendTableToJson2(socket, fd, MsgIds.SM_LOGIN_RET, {result = _MOE.ErrorCode.LOGIN_INVAILD_PARAM})
            return
        end

        self:SendServerMsgAsync("DBSrv", _MOE.ServerMsgIds.CM_Login, {userName = msg.userName,
            password = msg.password, client = fd})
    end
}
-----------------------------

RegisterClientMsgProcess(ClientToServerMsgProcess)

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    [_MOE.ServerMsgIds.SM_DSReady] = function (msg)
        local fd = msg.client
        local dsData = msg.dsData
        print("[SM_DSReady] client:", fd)
        MsgProcesser:SendTableToJson2(socket, fd, MsgIds.SM_DS_Info, dsData) -- 通知客户端
    end,
    [_MOE.ServerMsgIds.SM_Login_Ret] = function (msg) -- DB 返回登录数据
        local fd = msg.client
    end
}
----------------------------

---- 服务器间同步消息定义
local _SERVER_SYNC_MSG = {
    -- [_MOE.ServerMsgIds.SM_LS_Exist_PLAYERINFO] = true,
}
-----------------------------------------

RegisterServerCommandSync(_SERVER_SYNC_MSG)
RegisterServerCommandProcess(_OtherServerToMyServer)

return _M