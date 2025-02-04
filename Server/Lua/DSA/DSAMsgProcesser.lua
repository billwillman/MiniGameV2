local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DSAMsgProcesser", baseClass)

local json = require("json")
local MsgIds = require("_NetMsg.MsgId")
local moon = require("moon")
local socket = require "moon.socket"
require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")

--------------------------------------------- 客户端发送服务器协议处理 --------------------

--- 客户端发送给服务器请求协议处理
local ClientToServerMsgProcess = {
    [MsgIds.CM_DS_Ready] = function (self, msg, socket, fd)
        local ip, port = GetIpAndPort(socket, fd)
        local token = GenerateToken2(ip, port)
        local ds = _MOE.DSMap[token]
        if ds and ds.dsData then
            ds.dsData.isLocalDS = msg.isLocalDS -- 是否是Local DS
            print(string.format("[DSA] token: %s isLocalDS: %s", token, tostring(msg.isLocalDS)))
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