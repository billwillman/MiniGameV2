require("ServerCommon.GlobalServerConfig")
require("InitGlobalVars")
require("DSA.DSState")
local json = require("json")

local ServerData = GetServerConfig("DSA")

local moon = require("moon")
local socket = require "moon.socket"
local ListClass = require("_Common.LinkedList")

_MOE.DSFreeList = ListClass:new() -- 空闲DS
_MOE.DSBusyList = ListClass:new() -- 有玩家的DS

moon.exports.RemoveDS = function (ds)
    if not ds then
        return
    end
    local token = ds.token
    if _MOE.DSMap then
        ds = _MOE.DSMap[token]
        _MOE.DSMap[token] = nil
    end
    --[[
    if ds:IsInList() then
        if ds:IsBusy() then
            _MOE.DSBusyList:remove(ds)
        else
            _MOE.DSFreeList:remove(ds)
        end
    end
    ]]
    _MOE.DSBusyList:remove(ds)
    _MOE.DSFreeList:remove(ds)
    if _MOE.LocalDS == ds then
        _MOE.LocalDS = nil
    end
end

local FreeDSTask = require("DSA.FreeDSTask").New()
FreeDSTask:Start()
local BusyDSTask = require("DSA.BusyDSTask").New()
BusyDSTask:Start()

moon.exports.ServerData = ServerData

local MsgProcesser = require("DSA/DSAMsgProcesser").New()

moon.exports.MsgProcesser = MsgProcesser

local function DS_IsBusy(ds)
    if not ds then
        return false
    end
    if ds.isLocalDS then -- LocalDS永远是BUSY的
        return true
    end
    local players = ds.players
    if not players or #players <= 0 then
        return false
    end
    return true
end

local function DS_InFreeList(ds)
    if not ds then
        return false
    end
    local ret = ds._list ~= nil and ds._list == _MOE.DSFreeList
    return ret
end

local function DS_InBusyList(ds)
    if not ds then
        return false
    end
    local ret = ds._list ~= nil and ds._list == _MOE.DSBusyList
    return ret
end

local function DS_IsInList(ds)
    local ret = DS_InFreeList(ds) or DS_InBusyList(ds)
    return ret
end

moon.exports.OnAccept = function(fd, msg)
    print("accept ", fd, moon.decode(msg, "Z"))
    socket.settimeout(fd, 10)

    ---------------- DS数据存储 --------------------------
    _MOE.DSMap = _MOE.DSMap or {}
    local ip, port = GetIpAndPort(socket, fd)
    local token = GenerateToken2(ip, port)
    if ServerData.extIp then
        ip = ServerData.extIp
    end
    local dsData = {
        fd = fd,
        dsData = {
            ip = ip,
        },
        token = token,
        state = _MOE.DsStatus.None,
        freeTime = os.time(), -- 空闲时间
        IsBusy = DS_IsBusy,
        IsInList = DS_IsInList,
        IsInFreeList = DS_InFreeList,
        IsInBusyList = DS_InBusyList,
    }
    _MOE.DSFreeList:insert_last(dsData)
    _MOE.DSMap[token] = dsData
    print(string.format("[DSA] Accept DS => token: %s ip: %s port: %d", token, ip, port))
    -----------------------------------------------------
end

moon.exports.OnMessage = function(fd, msg)
    MsgProcesser:OnMsg(msg, socket, fd)
end

moon.exports.OnClose = function(fd, msg)
    local str = moon.decode(msg, "Z")
    print("close ", fd, str)

    local doRemoveDS = function ()
        if _MOE.DSMap then
            local data = json.decode(str)
            local token = moon.md5(data.addr)
            print(string.format("[DSA] Close DS => token: %s", token))
            local ds = _MOE.DSMap[token]
            RemoveDS(ds)
        end
    end
    xpcall(doRemoveDS, function () end)
end