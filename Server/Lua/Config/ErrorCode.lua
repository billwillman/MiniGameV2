_MOE = _MOE or {}

_MOE.ErrorCode = {
    NOERROR = 0, -- 无错误（通用）
    LOGIN_INVAILD_PARAM = -1, -- 登录参数错误
    LOGIN_USER_LOCKED = -2, -- 账号被锁定
}

return _MOE.ErrorCode