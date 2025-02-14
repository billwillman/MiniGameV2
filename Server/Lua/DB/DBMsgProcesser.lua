local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DBServerMsgProcesser", baseClass)

local mysql = require("moon.db.mysql") -- mysql

local PlayerManager = require("DB.DBPlayerManager")

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    --[[
    [_MOE.ServicesCall.InitDB] = function ()
        -- 初始化连接DB
        local DB = ServerData.DB
        local db = mysql.connect(DB)
        if db.code then
            print_r(db.code)
            return false
        end
        moon.exports.db = db -- db数据库
        return true
    end,
    ]]
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
}

---- 服务器间同步消息定义
local _SERVER_SYNC_MSG = {
}
------------------------

RegisterServerCommandSync(_SERVER_SYNC_MSG)
RegisterServerCommandProcess(_OtherServerToMyServer)

return _M