local Session = _MOE.class("Session")

require("ServerCommon.GlobalFuncs")

local socket = require "moon.socket"

function Session:Ctor(clientIp, clientPort, client_uuid, fd)
    self.loginToken = GenerateToken2(clientIp, clientPort)
    self.uuid = client_uuid
    self.fd = fd
    self.lastLoginTime = os.time()
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
    if self.fd then
        socket.close(self.fd)
        self.fd = nil
    end
end

return Session