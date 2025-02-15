local M = {}

-- pgsql port 5432
-- uuid字段 自动生成uuid表达式： gen_random_uuid()

----- 修改sha256登录加密方式更改为md5模式，moon只支持md5模式
-- SHOW password_encryption;
-- SET password_encryption='md5';
-- ALTER USER postgres with password 'GameBryo1122';

local QueryUserLoginFormat = "Select id, isLock, lockEndTime from userlogin where username='%s' and password='%s'"

M.QueryUserLogin = function (userName, password)
    if not userName then
        return false
    end
    password = password or ""
    local sql = string.format(QueryUserLoginFormat, userName, password)
    return sql
end

return M