local moon = require("moon")
require("ServerCommon.ServerMsgIds")

local Task = _MOE.class("FreeSessionTask")

local function TaskTick()
    while true do
        if _MOE.FreeSessionList and next(_MOE.FreeSessionList) ~= nil then
            local currtime = os.time()
            local currentCnt = _MOE.FreeSessionList:GetLength()
            local currentIdx = 1
            for i = 1, 50 do
                local FreeSession = _MOE.FreeSessionList:GetFirst()
                _MOE.FreeSessionList:remove_first()
                if FreeSession then
                    if currtime - FreeSession.freeTime <= 60 then
                        _MOE.FreeSessionList:insert_last(FreeSession)
                    else
                        ---- 移到Free列表
                        _MOE.FreeSessionTask:RemoveFreeSessionByUUID(FreeSession.uid, true)
                    end
                    currentIdx = currentIdx + 1
                    if currentIdx >= currentCnt then
                        break
                    end
                else
                    break
                end
            end
        end
        moon.sleep(0)
    end
end

function Task:Ctor()
    self.SessionTempDataMap = {}
end

function Task:Start()
    moon.async(TaskTick, self.__cname)
end

function Task:AttachSession(session)
    if not session then
        return false
    end
    local uuid = session:GetUUID()
    if not uuid then
        return false
    end
    local Player = self.SessionTempDataMap[uuid]
    if Player and Player.dsData and next(Player.dsData) ~= nil then
        session.dsData = Player.dsData
        return true
    end
    return false
end

function Task:AddFreeSession(session)
    if not session then
        return false
    end
    local uuid = session:GetUUID()
    if not uuid then
        return false
    end
    local Player = self.SessionTempDataMap[uuid]
    if Player then
        --- 更新即可
        Player.dsData = session.dsData
        Player.freeTime = os.time()
        ----
        return true
    end
    Player = {
        uid = uuid,
        dsData = session.dsData,
        freeTime = os.time(),
        serverName = "LoginSrv",
    }
    self.SessionTempDataMap[uuid] = Player
    _MOE.FreeSessionList:insert_last(Player)
    return true
end

function Task:RemoveFreeSessionByUUID(uid, isSendDSA)
    if not uid then
        return false
    end
    local Player = self.SessionTempDataMap[uid]
    if not Player then
        return false
    end
    self.SessionTempDataMap[uid] = nil
    _MOE.FreeSessionList:remove(Player)

    ----- 处理DS
    if isSendDSA and Player.dsData and next(Player.dsData) ~= nil then
        -- 发送给DSA
        MsgProcesser:SendServerMsgAsync("DSA", _MOE.ServerMsgIds.SM_GS_DS_PlayerKickOff, Player)
    end
    return true
end

function Task:RemoveFreeSession(session, isSendDSA)
    if not session then
        return false
    end
    local uid = session:GetUUID()
    return self:RemoveFreeSessionByUUID(uid, isSendDSA)
end

return Task