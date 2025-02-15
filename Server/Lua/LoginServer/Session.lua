local Session = _MOE.class("Session")

require("ServerCommon.GlobalFuncs")

function Session:Ctor(clientIp, clientPort, client_uuid)
    self.loginToken = GenerateToken2(clientIp, clientPort)
    self.uuid = client_uuid
end

function Session:GetLoginToken()
    return self.loginToken
end

function Session:GetUUID()
    return self.uuid
end

return Session