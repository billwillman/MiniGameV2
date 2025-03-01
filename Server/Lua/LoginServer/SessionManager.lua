local SessionManager = {
    uuidToSessionMap = {},
    loginTokenToSessinMap = {},
}
_MOE.SessionManager = SessionManager

require("ServerCommon.GlobalFuncs")

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

function SessionManager:GetSession(socket, fd)
    local loginToken = GenerateToken(socket, fd)
    if not loginToken then
        return
    end
    return self:GetSessionByToken(loginToken)
end

function SessionManager:GetSessionByToken(token)
    local ret = self.loginTokenToSessinMap[token]
    return ret
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

function SessionManager:ExistsByLoginToken(loginToken)
    if not loginToken then
        return false
    end
    local ret = self.loginTokenToSessinMap[loginToken] ~= nil
    return ret
end

-- 主动关闭Socket并删除
function SessionManager:CloseSocketAndRemove(uuid, quitReason)
    if not uuid then
        return false
    end
    local session = self.uuidToSessionMap[uuid]
    if not session then
        return false
    end
    self.uuidToSessionMap[uuid] = nil
    local loginToken = session:GetLoginToken()
    if loginToken then
        self.loginTokenToSessinMap[loginToken] = nil
    end
    xpcall( function()
        session:CloseSocket(quitReason)
    end, _MOE.ErrorHandler )
    return true
end

return SessionManager