_MOE = _MOE or {}
_MOE.ServerMsgIds = {
    CM_ReqDS = 1, -- 请求一个新的DS服务器
    SM_DSReady = 2, -- DS 准备好了
    SM_DS_STATUS = 3, -- DS 状态更新
    CM_Login = 4,   -- 登录请求
    SM_DS_RUNOK = 5, -- DS运行起来了
    SM_LS_Exist_PLAYERINFO = "SM_LS_Exist_PLAYERINFO", -- 根据LS的Token查询是否存在PlayerInfo
    SM_LS_DS_Enter = "SM_LS_DS_Enter",  -- 通过LoginSrv通知Client连接DS
    SM_DSA_Exist_DS = "SM_DSA_Exist_DS",
    SM_Login_Ret = "SM_Login_Ret",
    CM_DS_PlayerConnect = "CM_DS_PlayerConnect", -- 玩家DS链接
    CM_DS_PlayerDisConnect = "CM_DS_PlayerDisConnect", -- 玩家DS断开链接
    SM_GS_DS_PlayerKickOff = "SM_GS_DS_PlayerKickOff",  -- LoginSrv到DSA，DS踢掉某个玩家
    SM_LS_DSA_CheckPlayerDS = "SM_LS_DSA_CheckPlayerDS",
}

_MOE.ServicesCall = {
----------------------------- 默认事件 --------------------
    InitDB = "InitDB",
    Start = "Start",
    Listen = "Listen",
    Shutdown = "Shutdown", -- 非DB服务器先关闭端口
    SaveAndQuit = "save_then_quit", -- redisd用
----------------------------- LoginSrv ----------------
}