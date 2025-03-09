local Session = _MOE.class("Session")

require("ServerCommon.GlobalFuncs")
require("LoginServer.SessionState")

local socket = require "moon.socket"

function Session:Ctor(clientIp, clientPort, client_uuid, fd)
    self.loginToken = GenerateToken2(clientIp, clientPort)
    self.uuid = client_uuid
    self.fd = fd
    self.lastLoginTime = os.time()
    self.state = _MOE.SessionState.Free
end

function Session:GetState()
    return self.state
end

function Session:SetState(state)
    if state == nil then
        return
    end
    self.state = state
end

function Session:CanReqDS()
    local ret = self.state == _MOE.SessionState.Free
    return ret
end

function Session:LoginInDS(dsPlayer)
    if not dsPlayer or not dsPlayer.dsToken or not dsPlayer.ownerClientId then
        return false
    end
    self.dsData = {
        dsToken = dsPlayer.dsToken,
        dsClientId = dsPlayer.ownerClientId,
    }
    self:SetState(_MOE.SessionState.InDS)
    print("[Session] LoginInDS => dsToken: ", dsPlayer.dsToken, "dsClientId:", dsPlayer.dsClientId)
    return true
end

function Session:LoginoutDS(dsPlayer)
    if not dsPlayer or not dsPlayer.dsToken or not dsPlayer.ownerClientId then
        return false
    end
    if self.dsData then
        if self.dsData.dsToken == dsPlayer.dsToken and self.dsData.dsClientId == dsPlayer.ownerClientId then
            self.dsData = {}
            self:SetState(_MOE.SessionState.Free) -- 空闲状态
            print("[Session] LoginOutInDS => dsToken: ", dsPlayer.dsToken, "dsClientId:", dsPlayer.dsClientId)
            return true
        end
    end
    return false
end

function Session:GetLoginToken()
    return self.loginToken
end

function Session:GetUUID()
    return self.uuid
end

function Session:GetLastLoginTime()
    return self.lastLoginTime
end

function Session:UpdateLoginTime()
    self.lastLoginTime = os.time()
end

function Session:GetFd()
    return self.fd
end

function Session:CloseSocket(quitReason)
    if quitReason and self.fd then
        MsgProcesser:SendTableToJson2(socket, self.fd, _MOE.MsgIds.SM_LOGIN_KICKOFF,
        {
            uuid = self.uuid,
            token = self.loginToken,
            reason = quitReason,
        })
    end
    if CloseSocket(socket, self.fd) then
        self.fd = nil
    end
end

return Session