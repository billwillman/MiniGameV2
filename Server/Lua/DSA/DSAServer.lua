require("ServerCommon.GlobalServerConfig")
require("InitGlobalVars")

local ServerData = GetServerConfig("DSA")
require("LuaPanda").start("127.0.0.1", ServerData.Debug)

local moon = require("moon")
local socket = require "moon.socket"

local MsgProcesser = require("DSA/DSAMsgProcesser").New()

moon.exports.MsgProcesser = MsgProcesser
moon.exports.ServerData = ServerData

moon.exports.OnAccept = function(fd, msg)
    print("accept ", fd, moon.decode(msg, "Z"))
    socket.settimeout(fd, 10)

    ---------------- DS数据存储 --------------------------
    _MOE.DSMap = _MOE.DSMap or {}
    local ip, port = GetIpAndPort(socket, fd)
    local token = GenerateToken2(ip, port)
    _MOE.DSMap[token] = {
        fd = fd,
        dsData = {
            ip = ip,
        }
    }
    print(string.format("[DSA] Accept DS => token: %s ip: %s port: %d", token, ip, port))
    -----------------------------------------------------
end

moon.exports.OnMessage = function(fd, msg)
    MsgProcesser:OnMsg(msg, socket, fd)
end

moon.exports.OnClose = function(fd, msg)
    print("close ", fd, moon.decode(msg, "Z"))

    if _MOE.DSMap then
        local ip, port = GetIpAndPort(socket, fd)
        local token = GenerateToken2(ip, port)
        _MOE.DSMap[token] = nil
        print(string.format("[DSA] Close DS => token: %s ip: %s port: %d", token, ip, port))
    end
end