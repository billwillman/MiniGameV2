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

local function RemoveDsPlayer(Player)
    if not Player or not Player.dsData then
        return
    end

    local dsToken = Player.dsData.dsToken
    if not dsToken then
        return
    end
    local PlayerUID = Player.dsData.uid
    local PlayerClientId = Player.dsData.dsClientId
    if not PlayerUID or not PlayerClientId then
        return
    end
    local ds = _MOE.DSMap[dsToken]
    if ds and ds.players and next(ds.players) ~= nil then
        print(1)
        for idx = 1, #ds.players do
            local P = ds.players[idx]
            _MOE.TableUtils.PrintTable2(P)
            if P.uid == PlayerUID --[[and P.dsClientId == PlayerClientId--]] then
                table.remove(ds.players, idx)
                ----- 用户数据为 0
                if #ds.players <= 0 then
                    ds.state = _MOE.DsStatus.DSACloseFreeDS
                    -- 从列表中删除
                    local fd = ds.fd
                    RemoveDS(ds)
                    -- 关闭socket
                    if fd then
                        CloseSocket(socket, fd)
                    end
                end
                print(string.format("[remove] idx: %d dsPlayerCount: %d", idx, #ds.players))
                break
            end
        end
    end
end

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
    [MsgIds.CM_DS_PlayerConnect] = function (self, msg, socket, fd)
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
            uid = msg.uid,
        }
        local isFound = false
        for idx, player in ipairs(ds.players) do
            if player.uid == msg.uid then
                isFound = true
                ds.players[idx] = dsPlayer
                break
            end
        end
        if not isFound then
            table.insert(ds.players, dsPlayer)
        end
        print(string.format("[DS] dsToken: %s playerNum: %d", token, #ds.players))
        self:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.CM_DS_PlayerConnect, dsPlayer) -- 通知LoginSrv
        return true
    end,
    [MsgIds.CM_DS_PlayerDisConnect] = function (self, msg, socket, fd)
        if not _MOE.DSMap then
            return false
        end
        local token = GenerateToken(socket, fd)
        local ds = _MOE.DSMap[token]
        if not ds then
            return false
        end
        local uid = msg.uid
        local dsPlayer = nil
        if ds.players then
            for idx, player in ipairs(ds.players) do
                if player.uid == uid then
                    dsPlayer = player
                    -- 不删除（需要LoginSrv来处理保活一段时间在DS)
                    if ds.isLocalDS then -- LocalDS就直接删除
                        table.remove(ds.players, idx)
                    end
                    break
                end
            end
        end

        if dsPlayer then
            self:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.CM_DS_PlayerDisConnect, dsPlayer) -- 通知LoginSrv
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
                uids = {msg.uid},
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
            exePath = string.format("%s '{\\\"clients\\\":[%d],\\\"uids\\\":[\\\"%s\\\"],\\\"clientLoginTokens\\\":[\\\"%s\\\"],\\\"dsData\\\":{\\\"ip\\\":\\\"%s\\\",\\\"scene\\\":\\\"%s\\\"},\\\"GsData\\\":{\\\"ip\\\":\\\"%s\\\",\\\"port\\\":%d}}'",
                exePath, msg.client, msg.uid, msg.loginToken,
                ServerData.ip, msg.sceneName, ServerData.ip, ServerData.port)
            print("[DSA] RunCmd: " .. exePath)
            moon.RunCmd(exePath)
        end
    end,
    [_MOE.ServerMsgIds.SM_GS_DS_PlayerKickOff] = function (Player)
        if not Player or not Player.dsData then
            return
        end
        ---- 处理真正删除Players的对应Player
        print(string.format("SM_GS_DS_PlayerKickOff => dsToken: %s uid: %s clientId: %d", Player.dsData.dsToken or nil,
            Player.dsData.uid or 0, Player.dsData.dsClientId or 0))
        -- _MOE.TableUtils.PrintTable2(Player)
        -----------------------------------
        RemoveDsPlayer(Player)
    end,
    [_MOE.ServerMsgIds.SM_LS_DSA_CheckPlayerDS] = function (Player)
        return true
    end,
    [_MOE.ServerMsgIds.SM_LS_DSA_CheckPlayerDS] = function (Session)
        if Session and Session.dsToken then
            local dsToken = Session.dsToken
            local ds = _MOE.DSMap[dsToken]
            if ds and ds.dsData then
                local ret = {
                    dsToken = ds.dsData.dsToken,
                    ip = ds.dsData.ip,
                    port = ds.dsData.port,
                    scene = ds.dsData.scene,
                }
                return ret
            end
        end
        return nil
    end
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