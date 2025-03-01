_MOE = _MOE or {}
-- 状态
_MOE.SessionState = {
    Free = 0, -- 空闲状态
    ReqDS = 1, -- 请求分配DS
    LoginDS = 2, -- 等待登录DS
    QuitDS = 3, -- 等待退出DS
    InDS = 4, -- 已经在DS中
}