local SessionManager = {
    uuidToSessionMap = {},
    loginTokenToSessinMap = {},
}
_MOE.SessionManager = SessionManager

function SessionManager:AddSession(session)
    if not session then
        return
    end
    local uuid = session:GetUUID()
    if not uuid then
        return
    end
    local loginToken = session:GetLoginToken()
    if not loginToken then
        return
    end
    self.uuidToSessionMap[uuid] = session
    self.loginTokenToSessinMap[loginToken] = session
end

function SessionManager:RemoveSession(loginToken)
    if not loginToken then
        return
    end
    local session = self.loginTokenToSessinMap[loginToken]
    if not session then
        return
    end
    self.loginTokenToSessinMap[loginToken] = nil
    local uuid = session:GetUUID()
    if uuid then
        self.uuidToSessionMap[uuid] = nil
    end
end

return SessionManager