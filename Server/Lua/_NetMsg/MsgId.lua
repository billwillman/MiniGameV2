local MsgIds = {
    CM_Heart = 1, -- 心跳包(通用)
    CM_DS_Ready = 2, -- DS准备好了
    CM_ReqDS = 3, -- 请求DS
    CM_Login = 4, -- 登录
---------------------------------------------------------
    SM_Heart = 1,
    SM_DS_Info = 2,
    SM_LOGIN_RET = 3,
}

_MOE.MsgIds = MsgIds

return MsgIds
