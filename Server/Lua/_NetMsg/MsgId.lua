local MsgIds = {
    CM_Heart = 1, -- 心跳包(通用)
    CM_DS_Ready = 2, -- DS准备好了
    CM_ReqDS = 3, -- 请求DS
    CM_Login = 4, -- 登录
    CM_DS_RunOk = 5, -- DS运行启动OK
    CM_DS_PlayerConnect = 6, -- DS上玩家链接
    CM_DS_PlayerDisConnect = 7,-- DS玩家断开
---------------------------------------------------------
    SM_Heart = 1,
    SM_DS_Info = 2,
    SM_LOGIN_RET = 3,
    SM_LOGIN_KICKOFF = 4, -- LoginSrv踢人(带Reason)
    SM_DS_QUIT = 5, -- 关闭DS(要带Reason)
}

_MOE.MsgIds = MsgIds

return MsgIds
