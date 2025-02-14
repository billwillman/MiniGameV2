local baseClass = require("ServerCommon.CommonMsgProcesser")
local _M = _MOE.class("DBServerMsgProcesser", baseClass)

local PlayerManager = require("DB.DBPlayerManager")

----------------------------------------------- 服务器间通信 -------------------------------

-- 其他服务器发送本服务器处理
local _OtherServerToMyServer = {
    [_MOE.ServicesCall.InitDB] = function ()
        -- 初始化连接DB
        return true
    end
}

---- 服务器间同步消息定义
local _SERVER_SYNC_MSG = {
}
------------------------

RegisterServerCommandSync(_SERVER_SYNC_MSG)
RegisterServerCommandProcess(_OtherServerToMyServer)

return _M