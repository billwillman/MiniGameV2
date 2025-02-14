local M = {}

local userTableFmt = "CREATE TABLE `user_%d_%d` (" ..
	"`id` VARCHAR(50) NOT NULL DEFAULT (UUID()) COMMENT '用户标识' COLLATE 'utf8mb4_0900_ai_ci'," ..
	"`username` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '登录名' COLLATE 'utf8mb4_0900_ai_ci'," ..
	"`password` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '登录密码' COLLATE 'utf8mb4_0900_ai_ci'," ..
	"`isLock` BIT(1) NOT NULL DEFAULT (0) COMMENT '是否锁定'," ..
	"`lockEndTime` DATETIME NOT NULL DEFAULT '0' COMMENT '锁定到的时间(isLock=1)'" ..
    ")" ..
    "COMMENT='用户登录表'" ..
    "COLLATE='utf8mb4_0900_ai_ci'" ..
    "ENGINE=InnoDB" ..
    ";"

M.CreateUserTableSQL = function (startIndex)
    startIndex = startIndex or 1
    local sql = string.format(userTableFmt, (startIndex - 1) * 10000 + 1, startIndex * 10000)
    return sql
end

-- pgsql port 5432

return M