local _M = _MOE.class("CommonMsgProcesser")
local MsgIds = require("_NetMsg.MsgId")
local json = require("json")
local moon = require("moon")
local socket = require "moon.socket"
require("ServerCommon.ServerMsgIds")

_M.MsgDispatch = {
    [MsgIds.CM_Heart] = function (self, msg, socket, fd)
        self:SendHeartMsg(socket, fd)
    end
}

local listenfd = nil

_M.SERVER_COMMAND_PROCESS_IS_CALLMODE = {
    [_MOE.ServicesCall.InitDB] = true,
    [_MOE.ServicesCall.SaveAndQuit] = true,
    [_MOE.ServicesCall.Shutdown]  = true,
    [_MOE.ServicesCall.Listen] = true,
    [_MOE.ServicesCall.Start] = true
}

local ServerCommandProcessCallMode = _M.SERVER_COMMAND_PROCESS_IS_CALLMODE
local ServerClientDispatch = _M.MsgDispatch
local ServerToServerDispatch = _M.SERVER_COMMAND_PROCESS

moon.exports.IsServerCommandCallModel = function (funcName)
    if not funcName or not ServerCommandProcessCallMode then
        return false
    end
    return ServerCommandProcessCallMode[funcName]
end

_M.SERVER_COMMAND_PROCESS = {
    [_MOE.ServicesCall.InitDB] = function ()
        print(string.format("[%d] %s", moon.id, _MOE.ServicesCall.InitDB))
        return true
    end,
    [_MOE.ServicesCall.SaveAndQuit] = function ()
        if ServerData and ServerData.isSaveQuit and listenfd then
            print(string.format("[%d] %s", moon.id, _MOE.ServicesCall.SaveAndQuit))
            socket.close(listenfd)
            listenfd = nil
        end
        return true
    end,
    [_MOE.ServicesCall.Shutdown] = function ()
        if ServerData and not ServerData.isSaveQuit and listenfd then
            print(string.format("[%d] %s", moon.id, _MOE.ServicesCall.Shutdown))
            socket.close(listenfd)
            listenfd = nil
        end
        return true
    end,
    [_MOE.ServicesCall.Listen] = function ()
        print(string.format("[%d] %s", moon.id, _MOE.ServicesCall.Listen))
        if ServerData then
            listenfd = socket.listen(ServerData.ip, ServerData.port, moon.PTYPE_SOCKET_MOON)
            return true
        else
            print(string.format("[%d] %s ERROR~!", moon.id, _MOE.ServicesCall.Listen))
            return false
        end
    end,
    [_MOE.ServicesCall.Start] = function ()
        if ServerData and listenfd then
            print(string.format("[%d] %s", moon.id, _MOE.ServicesCall.Start))
            socket.start(listenfd)--auto accept
            --注册网络事件
            if OnAccept then
                socket.on("accept", OnAccept)
            end
            if OnClose then
                socket.on("close", OnClose)
            end
            if OnMessage then
                socket.on("message", OnMessage)
            end
            return true
        else
            print(string.format("[%d] %s ERROR", moon.id, _MOE.ServicesCall.Start))
            return false
        end
    end
}

function _M:SendHeartMsg(socket, fd)
    local sendMsg = {msgId = MsgIds.SM_Heart}
    self:SendTableToJson(socket, fd, sendMsg)
end

---异步跨服务器请求数据
---@param serverName string
---@param msgId string
---@param ... any
function _M:SendServerMsgAsync(serverName, msgId, ...)
    if not serverName or not msgId then
        return false
    end
    local serverId = moon.queryservice(serverName)
    if not serverId or serverId <= 0 then
        return false
    end
    moon.send("lua", serverId, msgId, ...)
end

---同步跨服务器请求数据
---@param serverName string
---@param msgId string
---@param ... unknown
---@return boolean
function _M:SendServerMsgSync(serverName, msgId, ...)
    if not serverName or not msgId then
        return false
    end
    local serverId = moon.queryservice(serverName)
    if not serverId or serverId <= 0 then
        return false
    end
    return moon.call("lua", serverId, msgId, ...)
end

function _M:SendTableToJson2(socket, fd, msgId, msg)
    msg = msg or {}
    msg.msgId = msgId
    self:SendTableToJson(socket, fd, msg)
end

function _M:SendTableToJson(socket, fd, sendMsg)
    if not socket or not fd or not sendMsg or not sendMsg.msgId then
        return false
    end
    local str = json.encode(sendMsg)
    --socket.write(fd, string.pack(">H",#str)..str)
    socket.write(fd, str)
    return true
end

function _M:_OnMsg(msg, socket, fd)
    if ServerClientDispatch then
        -- print(_MOE.TableUtils.Serialize(msg))
        local func = ServerClientDispatch[msg.msgId]
        if func then
            -- func(self, msg, socket, fd)
            moon.async(func, self, msg, socket, fd)
            return true
        end
    end
    print("[OnMsg Error] MsgId: " .. msg.msgId)
    return false
end

function _M:OnMsg(msg, socket, fd)
    local data = moon.decode(msg, "Z")
    if not data then
        print("[OnMsg] socket close: not data")
        -- 关闭Socket
        socket.close(fd)
        return false
    end
    msg = json.decode(data)
    if not msg.msgId then
        print("[OnMsg] socket close: not msg.msgId")
        socket.close(fd)
        return false
    end
    if not self:_OnMsg(msg, socket, fd) then
        print("[OnMsg] socket close: not _OnMsg")
        socket.close(fd)
        return false
    end
    return true
end

moon.exports.RegisterServerCommandProcess = function (table)
    if not table or type(table) ~= "table"  then
        return false
    end
    if table ~= _M.SERVER_COMMAND_PROCESS then
        setmetatable(table, {__index = _M.SERVER_COMMAND_PROCESS})
    end
    ServerToServerDispatch = table
    moon.dispatch("lua", function(sender, session, cmd, ...)
        -- 处理 cmd
        local OnProcess = ServerToServerDispatch[cmd]
        if OnProcess then
            if IsServerCommandCallModel(cmd) then
                -- print(string.format("[%s] callModel...", cmd))
                
                moon.response("lua", sender, session, OnProcess(...))
            else
                OnProcess(...)
            end
        end
    end)
    return true
end

-- 注册消息是同步的
moon.exports.RegisterServerCommandSync= function (table)
    if not table or type(table) ~= "table"  then
        return false
    end
    if table ~= ServerCommandProcessCallMode then
        if table ~= _M.SERVER_COMMAND_PROCESS_IS_CALLMODE then
            setmetatable(table, {__index = _M.SERVER_COMMAND_PROCESS_IS_CALLMODE})
            ServerCommandProcessCallMode = table
        else
            ServerCommandProcessCallMode = table
        end
    end
    return true
end

moon.exports.RegisterClientMsgProcess = function (table)
    -- ServerClientDispatch
    if not table or type(table) ~= "table"  then
        return false
    end
    if table ~= ServerClientDispatch then
        if table ~= _M.MsgDispatch then
            setmetatable(table, {__index = _M.MsgDispatch})
            ServerClientDispatch = table
        else
            ServerClientDispatch = table
        end
    end
    return true
end

moon.exports.RegisterDefaultServerCommandProcess = function ()
    ServerToServerDispatch = _M.SERVER_COMMAND_PROCESS
    moon.dispatch("lua", function(sender, session, cmd, ...)
        -- 处理 cmd
        local OnProcess = ServerToServerDispatch[cmd]
        if OnProcess then
            if IsServerCommandCallModel(cmd) then
                moon.response("lua", sender, session, OnProcess(...))
            else
                OnProcess(...)
            end
        end
    end)
    return true
end

------------------------------------------- 服务器交互协议 -----------------------

return _M