local Session = _MOE.class("Session")

require("ServerCommon.GlobalFuncs")

function Session:Ctor(clientIp, clientPort, client_uuid)
    self.loginToken = GenerateToken2(clientIp, clientPort)
    self.uuid = client_uuid
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

return Session