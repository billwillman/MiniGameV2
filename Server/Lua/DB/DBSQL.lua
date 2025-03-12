local M = {}

-- pgsql port 5432
-- uuid字段 自动生成uuid表达式： gen_random_uuid()

----- 修改sha256登录加密方式更改为md5模式，moon只支持md5模式
-- SHOW password_encryption;
-- SET password_encryption='md5';
-- ALTER USER postgres with password 'GameBryo1122';

local QueryUserLoginFormat = "Select * from userlogin where username='%s' and password='%s'"

M.QueryUserLogin = function (userName, password)
    if not userName then
        return false
    end
    password = password or ""
    local sql = string.format(QueryUserLoginFormat, userName, password)
    return sql
end

M.MongoDB_QueryUserLogin = function (db, userName, password)
    if not db.userlogin then
        return
    end
    local ret = db.userlogin:findOne({username = userName, password = password})
    return ret
end

local pg = require("moon.db.pg")

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

local function QueryDB(sql)
    CheckAndConnectDB()
    local result = db:query(sql)
    if result and result.code == "SOCKET" then
        CheckAndConnectDB()
        result = db:query(sql)
    end
    return result
end

M.PostSQL_QueryUserLogin = function (db, userName, password)
    local sql = M.QueryUserLogin(userName, password)
    local result = QueryDB(sql)
    if not result or not result.data or next(result.data) == nil then
        return nil
    end
    if #result.data > 1 then
        -- 冗余数据
        print("[DB ERROR] CM_Login data num > 1 sql:", sql)
        return nil
    end
    result = result.data[1]
    return result
end

return M