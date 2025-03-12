local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DBServerMsgProcesser", baseClass)

local moon = require("moon")

require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")

-- local mysql = require("moon.db.mysql") -- mysql
local pg = require("moon.db.pg") -- pgSql
local mongo = require "moon.db.mongo" -- mongo db数据库

require("LuaPanda").start("127.0.0.1", ServerData.Debug)

local PlayerManager = require("DB.DBPlayerManager")

local SQL = require("DB.DBSQL")
require("Config.ErrorCode")

local isUseMongoDB = true

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    --------------------------------------- 内部系统调用 -----------------------------
    [_MOE.ServicesCall.InitDB] = function ()
        -- 初始化连接DB
        if not isUseMongoDB then
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
            print("[DB] connect PostSQL DB Success~!")
            moon.exports.db = db -- db数据库
        end

        if isUseMongoDB then
            local MongoDB = ServerData.MongoDB
            local connectStr = _MOE.TableUtils.Serialize(MongoDB)
            print("[DB] Connect => ", connectStr)
            local db =  mongo.client(MongoDB)
            if not db or not db["minigame"] then
                return false
            end
            db = db["minigame"]
            db:auth(MongoDB.user, MongoDB.password)
            print("[DB] connect Mongo DB Success~!")
            moon.exports.mongodb = db
        end
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
        if mongodb then
            PlayerManager:SaveAllToDB(mongodb)
        end
        return true
    end,
    --------------------------------------------------------------------------------------
    [_MOE.ServerMsgIds.CM_Login] = function (msg)
        if not msg.userName then
            return
        end
        local userName = msg.userName
        local password = msg.password
        local client = msg.client
        local result
        if isUseMongoDB then
            result = SQL.MongoDB_QueryUserLogin(mongodb, userName, password)
        else
            result = SQL.PostSQL_QueryUserLogin(db, userName, password)
        end

        local OnError = function (isLock)
            MsgProcesser:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.SM_Login_Ret,
            {
                result = isLock and _MOE.ErrorCode.LOGIN_USER_LOCKED or _MOE.ErrorCode.LOGIN_INVAILD_PARAM,
                client = client
            }
            )
        end

        MsgProcesser:PrintMsg(result)

        if not result then
            -- 失败
            OnError()
            return
        end
        if result.isLock ~= "0" and  result.isLock ~= false then
            OnError(true)
            return
        end
        MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_Login_Ret,
        {
            result = _MOE.ErrorCode.NOERROR,
            client = client,
            user = {
                uuid = result.id,
            }
        })
    end
}

---- 服务器间同步消息定义
local _SERVER_SYNC_MSG = {
}
------------------------

RegisterServerCommandSync(_SERVER_SYNC_MSG)
RegisterServerCommandProcess(_OtherServerToMyServer)

return _M