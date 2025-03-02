local BaseClass = require("_Common.ModelContainer")
local _M = _MOE.class("ClientPlayerControllerContainer", BaseClass)

_M[".superctor"] = true -- 会调用父类方法

require("_Common.Net.DSMsgDefine")

-------------------------------- 外部调用 -------------------------------

function _M:OnLoginDS()
    local PlayerController = _MOE.GameApp:GetLocalPlayerController()
    local userInfo = _MOE.Models.RootAttr:GetUserInfo()
    if userInfo and userInfo.token then
        PlayerController:DispatchServer_Reliable(_MOE.DS.ClientMsgIds.ClientLoginDS, userInfo.token) -- 发送Toekn给DS
    end
end

function _M:OnLoginOutDS()
end

-------------------------------------------------------------------------

return _M