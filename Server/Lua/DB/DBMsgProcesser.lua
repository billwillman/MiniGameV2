local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DBServerMsgProcesser", baseClass)

local moon = require("moon")

require("ServerCommon.ServerMsgIds")
require("ServerCommon.GlobalFuncs")

-- local mysql = require("moon.db.mysql") -- mysql
local pg = require("moon.db.pg") -- pgSql

require("LuaPanda").start("127.0.0.1", ServerData.Debug)

local PlayerManager = require("DB.DBPlayerManager")

local SQL = require("DB.DBSQL")
require("Config.ErrorCode")

local function CheckAndConnectDB()
    if not db then
        return
    end
    if not db["sock"] then
        local DB = ServerData.DB
        local db = pg.connect(DB)
        if db.code then
            print_r("[DB] db.code: ", db.code)
            return false
        end
        moon.exports.db = db
    end
end

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
        if not msg.userName then
            return
        end

        CheckAndConnectDB()

        local userName = msg.userName
        local password = msg.password
        local client = msg.client
        local sql = SQL.QueryUserLogin(userName, password)
        local result = db:query(sql)

        local OnError = function (isLock)
            MsgProcesser:SendServerMsgAsync("LoginSrv", _MOE.ServerMsgIds.SM_Login_Ret,
            {
                result = isLock and _MOE.ErrorCode.LOGIN_USER_LOCKED or _MOE.ErrorCode.LOGIN_INVAILD_PARAM,
                client = client
            }
            )
        end


        if not result or not result.data or next(result.data) == nil then
            -- 失败
            OnError()
            return
        end
        if #result.data > 1 then
            -- 冗余数据
            print("[DB ERROR] CM_Login data num > 1 sql:", sql)
            OnError()
            return
        end
        local userData = result.data[1]
        if userData.isLock then
            OnError(true)
            return
        end
        MsgProcesser:SendServerMsgAsync(msg.serverName, _MOE.ServerMsgIds.SM_Login_Ret,
        {
            result = _MOE.ErrorCode.NOERROR,
            client = client,
            user = {
                uuid = userData.id,
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