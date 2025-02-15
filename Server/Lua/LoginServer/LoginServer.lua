require("ServerCommon.GlobalServerConfig")
require("InitGlobalVars")

local ServerData = GetServerConfig("LoginSrv")

local moon = require("moon")
local socket = require "moon.socket"

moon.exports.ServerData = ServerData

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
    print("close ", fd, moon.decode(msg, "Z"))
end