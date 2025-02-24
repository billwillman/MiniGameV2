local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DSAMsgProcesser", baseClass)

local json = require("json")
local MsgIds = require("_NetMsg.MsgId")
local moon = require("moon")
local socket = require "moon.socket"
require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")
require("Config.ErrorCode")

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
            ds.dsData.isReady = true -- 准备好了
            print(string.format("[DSA] token: %s isLocalDS: %s ip: %s dsPort: %d", token, tostring(msg.isLocalDS), ip, msg.port))
            if msg.isLocalDS then
                _MOE.LocalDS = ds -- 设置Local DS的数据
            end
            return true
        end
        return false
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
            -- 说明包含DS
            local dsData = _MOE.LocalDS.dsData
            MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_DSReady, {result = _MOE.ErrorCode.NOERROR, dsData = dsData, client = msg.client})
        else
            -- 申请服务器
            print("Request DS ...")
            local freeIp, freePort = GetFreeAdress()
            if not freeIp or not freePort then
                print("Request DS: NO FreeIP or FreePort")
                MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_DSReady, {result = _MOE.ErrorCode.DSA_REQ_DS_ERROR, client = msg.client})
            else
                local dsData = {ip = freeIp, port = freePort, isLocalDS = false}
                MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_DSReady, {result = _MOE.ErrorCode.NOERROR, dsData = dsData, client = msg.client})
            end
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