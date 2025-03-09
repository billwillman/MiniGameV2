local moon = require("moon")

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
        freeTime = os.time()
    }
    self.SessionTempDataMap[uuid] = Player
    _MOE.FreeSessionList:insert_last(Player)
end

return Task