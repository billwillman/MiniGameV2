local moon = require("moon")
require("ServerCommon.ServerMsgIds")

local Task = _MOE.class("FreeSessionTask")

local function TaskTick()
    while true do
        moon.sleep(0)
    end
end

function Task:Ctor()
    self.SessionTempDataMap = {}
end

function Task:Start()
    moon.async(TaskTick, self.__cname)
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

function Task:RemoveFreeSession(session, isSendDSA)
    if not session then
        return false
    end
    local uid = session:GetUUID()
    if not uid then
        return false
    end
    local Player = self.SessionTempDataMap[uid]
    if not Player then
        return false
    end
    self.SessionTempDataMap[uid] = nil
    _MOE.FreeSessionList:remove(Player)
    if isSendDSA then
        -- 发送给DSA
        MsgProcesser:SendServerMsgAsync("DBSrv", _MOE.ServerMsgIds.SM_GS_DS_PlayerKickOff, Player)
    end
    return true
end

return Task