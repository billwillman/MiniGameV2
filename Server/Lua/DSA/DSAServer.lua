require("ServerCommon.GlobalServerConfig")
require("InitGlobalVars")
local json = require("json")

local ServerData = GetServerConfig("DSA")

local moon = require("moon")
local socket = require "moon.socket"

moon.exports.ServerData = ServerData

local MsgProcesser = require("DSA/DSAMsgProcesser").New()

moon.exports.MsgProcesser = MsgProcesser

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
        },
        token = token
    }
    print(string.format("[DSA] Accept DS => token: %s ip: %s port: %d", token, ip, port))
    -----------------------------------------------------
end

moon.exports.OnMessage = function(fd, msg)
    MsgProcesser:OnMsg(msg, socket, fd)
end

moon.exports.OnClose = function(fd, msg)
    local str = moon.decode(msg, "Z")
    print("close ", fd, str)

    if _MOE.DSMap then
        local data = json.decode(str)
        local token = moon.md5(data.addr)
        print(string.format("[DSA] Close DS => token: %s", token))
        local ds = _MOE.DSMap[token]
        _MOE.DSMap[token] = nil
        if _MOE.LocalDS == ds then
            _MOE.LocalDS = nil
        end
    end
end