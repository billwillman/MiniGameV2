local moon = require("moon")
local _MOE = _MOE or {}
moon.exports._MOE = _MOE
local io = require("io")
local json = require("json")

local serverCfgStr = io.readfile("./Config/Server.json")
_MOE.ServerConfig = json.decode(serverCfgStr)

moon.exports.GetServerConfig = function (serverName)
    if not serverName then
        return
    end
    return _MOE.ServerConfig[serverName]
end