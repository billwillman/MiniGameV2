local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DBServerMsgProcesser", baseClass)

local moon = require("moon")

-- local mysql = require("moon.db.mysql") -- mysql
local pg = require("moon.db.pg") -- pgSql

local PlayerManager = require("DB.DBPlayerManager")

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    --------------------------------------- 内部系统调用 -----------------------------
    [_MOE.ServicesCall.InitDB] = function ()
        -- 初始化连接DB
        local DB = ServerData.DB
        local connectStr = _MOE.TableUtils.Serialize(DB)
        print("[DB] Connect => ", connectStr)
        --[[
        local db = mysql.connect(DB)
        ]]
        local db = pg.connect(DB)
        if db.code then
            print_r("[DB] db.code: ", db.code)
            return false
        end
        print("[DB] connect DB Success~!")
        moon.exports.db = db -- db数据库
        return true
    end,
    [_MOE.ServicesCall.Start] = function ()
        return true
    end,
    [_MOE.ServicesCall.Listen] = function ()
        return true
    end,
    [_MOE.ServicesCall.Shutdown] = function ()
        return true
    end,
    [_MOE.ServicesCall.SaveAndQuit] = function ()
        -- db数据存储
        if db then
            -- Player存储
            PlayerManager:SaveAllToDB(db)
        end
        return true
    end,
    --------------------------------------------------------------------------------------
    [_MOE.ServerMsgIds.CM_Login] = function (msg)
    end
}

---- 服务器间同步消息定义
local _SERVER_SYNC_MSG = {
}
------------------------

RegisterServerCommandSync(_SERVER_SYNC_MSG)
RegisterServerCommandProcess(_OtherServerToMyServer)

return _M