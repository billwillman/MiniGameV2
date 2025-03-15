require("ServerCommon.GlobalServerConfig")
require("InitGlobalVars")
local json = require("json")

local ServerData = GetServerConfig("LoginSrv")

local moon = require("moon")
local socket = require "moon.socket"

moon.exports.ServerData = ServerData

local ListClass = require("_Common.LinkedList")
_MOE.FreeSessionList = ListClass:new() -- 空闲DS

local FreeSessionTask = require("LoginServer.SessionFreeTask").New()
FreeSessionTask:Start()
_MOE.FreeSessionTask = FreeSessionTask

local SessionManager = require("LoginServer.SessionManager")
local MsgProcesser = require("LoginServer/LoginMsgProcesser").New()

moon.exports.MsgProcesser = MsgProcesser
--moon.exports.PlayerManager = require("LoginServer.PlayerManager").New()

moon.exports.OnAccept = function(fd, msg)
    print("accept ", fd, moon.decode(msg, "Z"))
    socket.settimeout(fd, 10)
end

moon.exports.OnMessage = function(fd, msg)
    MsgProcesser:OnMsg(msg, socket, fd)
end

moon.exports.OnClose = function(fd, msg)
    local str = moon.decode(msg, "Z")
    -- print("close ", fd, str)

    xpcall(function ()
        local data = json.decode(str)
        local token = moon.md5(data.addr)
        local Session = SessionManager:GetSessionByToken(token)
        SessionManager:RemoveSession(token)
        if Session then
            _MOE.FreeSessionTask:AddFreeSession(Session)
        end
    end, function () end)
end