_MOE = _MOE or {}

_MOE.ErrorCode = {
    NOERROR = 0, -- 无错误（通用）
    LOGIN_INVAILD_PARAM = -1, -- 登录参数错误
    LOGIN_USER_LOCKED = -2, -- 账号被锁定
    LOGIN_KICKOFF_OTHER_LOGIN = -3, -- 账号在另外一个地方登录
    LOGIN_EXIST_LOGINED = -4,   -- 已经登录过了
    DSA_REQ_DS_ERROR = -5,  -- DSA分配请求DS失败
    LOGIN_REQ_DS_EXIST = -6, -- 已经请求过了DS
}

return _MOE.ErrorCode