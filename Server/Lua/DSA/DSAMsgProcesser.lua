local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DSAMsgProcesser", baseClass)

local json = require("json")
local MsgIds = require("_NetMsg.MsgId")
local moon = require("moon")
local socket = require "moon.socket"
require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")
require("Config.ErrorCode")
require("DSA.DSState")

require("LuaPanda").start("127.0.0.1", ServerData.Debug)

--------------------------------------------- 客户端发送服务器协议处理 --------------------

--- 客户端发送给服务器请求协议处理
local ClientToServerMsgProcess = {
    [MsgIds.CM_DS_Ready] = function (self, msg, socket, fd)
        local ip, port = GetIpAndPort(socket, fd)
        local token = GenerateToken2(ip, port)
        local ds = _MOE.DSMap[token]
        if ds and ds.dsData then
            ds.dsData.isLocalDS = msg.isLocalDS -- 是否是Local DS
            ds.dsData.port = msg.port
            ds.dsData.scene = msg.scene
            -- ds.dsData.ip = msg.ip
            ds.dsData.ip = ip
            ds.dsData.state = _MOE.DsStatus.WaitingPlayersConnect -- 等待玩家连接
            ds.freeTime = os.time() -- 更新下空闲时间
            print(string.format("[DSA] token: %s isLocalDS: %s ip: %s dsPort: %d", token, tostring(msg.isLocalDS), ip, msg.port))
            if msg.isLocalDS then
                _MOE.LocalDS = ds -- 设置Local DS的数据
            end
            local loginSrvMsg = {
                result = _MOE.ErrorCode.NOERROR,
                dsData = ds.dsData,
                clients = msg.clients,
                dsToken = ds.token,
                clientLoginTokens = msg.clientLoginTokens,
            }
            ---- 发送异步给LoginSrv
            self:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.SM_DSReady, loginSrvMsg)
            return true
        end
        return false
    end,
    [MsgIds.CM_DS_RunOk] = function (self, msg, socket, fd)
        return true
    end,
    [MsgIds.CM_DS_PlayerLogin] = function (self, msg, socket, fd)
        if not _MOE.DSMap or not msg.ownerClientId or not msg.loginToken then
            return false
        end
        local token = GenerateToken(socket, fd)
        local ds = _MOE.DSMap[token]
        if not ds then
            return false
        end
        ds.players = ds.players or {}
        local dsPlayer = {
            dsClientId = msg.ownerClientId,
            dsToken = token,
            loginToken = msg.loginToken,
        }
        local isFound = false
        for idx, player in ipairs(ds.players) do
            if player.dsClientId == msg.ownerClientId then
                isFound = true
                ds.players[idx] = dsPlayer
                break
            end
        end
        if not isFound then
            table.insert(ds.players, dsPlayer)
        end
        self:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.CM_DS_PlayerLogin, dsPlayer) -- 通知LoginSrv
        return true
    end,
    [MsgIds.CM_DS_PlayerLoginOut] = function (self, msg, socket, fd)
        if not _MOE.DSMap then
            return false
        end
        local token = GenerateToken(socket, fd)
        local ds = _MOE.DSMap[token]
        if not ds then
            return false
        end
        local dsClientId = msg.ownerClientId
        local dsPlayer = nil
        if ds.players then
            for idx, player in ipairs(ds.players) do
                if player.dsClientId == dsClientId then
                    dsPlayer = player
                    table.remove(ds.players, idx)
                    break
                end
            end
        end

        if dsPlayer then
            self:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.CM_DS_PlayerLoginOut, dsPlayer) -- 通知LoginSrv
        end

        return true
    end
}
-----------------------------

RegisterClientMsgProcess(ClientToServerMsgProcess)

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    -- 请求空闲的DS
    [_MOE.ServerMsgIds.CM_ReqDS] = function (msg)
        if _MOE.LocalDS ~= nil then
            -- 本地DS
            local dsData = _MOE.LocalDS.dsData
            MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_DSReady, {result = _MOE.ErrorCode.NOERROR, dsData = dsData,
                clients = {msg.client},
                clientLoginTokens = {msg.loginToken},
            })
        else
            -- 申请服务器
            print("Request DS ...")
            --[[
            local freeIp, freePort = GetFreeAdress()
            if not freeIp or not freePort then
                print("Request DS: NO FreeIP or FreePort")
                MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_DSReady, {result = _MOE.ErrorCode.DSA_REQ_DS_ERROR, client = msg.client})
            else
                local dsData = {ip = freeIp, port = freePort, isLocalDS = false}
                MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_DSReady, {result = _MOE.ErrorCode.NOERROR, dsData = dsData, client = msg.client})
            end
            ]]
            --- 启动DS，DS内部有获取Free的地址
            local platform = moon.GetPlatForm()
            local exePath = nil
            if platform == 1 then
                -- windows
                exePath = "Start ../../outPath/DS/Server.exe"
            elseif platform == 2 then
                -- linux
                exePath = "../../outPath/DS_Linux/Server"
            end
            -- "{'dsData':{'ip':'127.0.0.1','port':7777, 'scene': 'MultiScene'},'GsData':{'ip':'127.0.0.1','port':1991}}"
            exePath = string.format("%s '{\\\"clients\\\":[%d],\\\"clientLoginTokens\\\":[\\\"%s\\\"],\\\"dsData\\\":{\\\"ip\\\":\\\"%s\\\",\\\"scene\\\":\\\"%s\\\"},\\\"GsData\\\":{\\\"ip\\\":\\\"%s\\\",\\\"port\\\":%d}}'",
                exePath, msg.client, msg.loginToken, ServerData.ip, msg.sceneName, ServerData.ip, ServerData.port)
            print("[DSA] RunCmd: " .. exePath)
            moon.RunCmd(exePath)
        end
    end,
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