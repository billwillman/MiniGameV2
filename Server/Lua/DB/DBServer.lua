require("ServerCommon.GlobalServerConfig")
require("InitGlobalVars")
local json = require("json")

local ServerData = GetServerConfig("DBSrv")

local moon = require("moon")
local socket = require "moon.socket"

local MsgProcesser = require("DB/DBMsgProcesser").New()

moon.exports.MsgProcesser = MsgProcesser
moon.exports.ServerData = ServerData

--[[
local mysql = require("moon.db.mysql")
moon.async(function()
    local db = mysql.connect(ServerData.DB)
    if db.code then
        print_r(db.code)
        return false
    end
end)
]]

--[[
moon.exports.OnAccept = function(fd, msg)
    print("accept ", fd, moon.decode(msg, "Z"))
    socket.settimeout(fd, 10)
end

moon.exports.OnMessage = function(fd, msg)
    MsgProcesser:OnMsg(msg, socket, fd)
end

moon.exports.OnClose = function(fd, msg)
    local str = moon.decode(msg, "Z")
    print("close ", fd, str)
end
]]