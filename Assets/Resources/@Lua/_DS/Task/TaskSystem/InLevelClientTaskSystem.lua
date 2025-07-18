---- 客户端本人Task任务
local InLevelClientTaskSystem = _MOE.class("CHASE_InLevelClientTaskSystem")

local TaskClass = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Task.TaskSystem.InLevelTask")
local ChaseGameLocalDataUtil = require("Feature.CHASE.Script.Utils.ChaseGameLocalDataUtil")
--------------------------------------- 外部可以调用 -----------------------------------------

local function ResetVar(self)
    self.TaskList = {}
    self.TaskMap = {}
end

function InLevelClientTaskSystem:Ctor()
    ResetVar(self)
end

function InLevelClientTaskSystem:Dispose()
    ResetVar(self)
end

function InLevelClientTaskSystem:IsShow()
    local SaveData = ChaseGameLocalDataUtil.GetLocalData(ChaseGameLocalDataUtil.SaveKey.HUDShowTask)
    if SaveData then
        return SaveData == "1"
    end
    return true
end

function InLevelClientTaskSystem:SaveLocalHUDShowMap(IsShow)
    local SaveStr = "0"
    if IsShow then
        SaveStr = "1"
    end
    ChaseGameLocalDataUtil.SaveLocalData(ChaseGameLocalDataUtil.SaveKey.HUDShowTask, SaveStr)
end

function InLevelClientTaskSystem:GetTaskList()
    return self.TaskList or {}
end

function InLevelClientTaskSystem:GetTaskById(id)
    if not id or not self.TaskMap then
        return false
    end
    local ret = self.TaskMap[id]
    return ret
end

function InLevelClientTaskSystem:ClientUpdateTaskInfos(DSTaskInfos)
    if DSTaskInfos == nil or DSTaskInfos.Tasks == nil then
        ResetVar(self)
        return
    end
    local len = DSTaskInfos.Tasks:Length()
    if len <= 0 then
        ResetVar(self)
        return
    end
    local world = _MOE.Utils.WorldUtils:GetCurrentWorld()
    local GameClient = UE4.UMoeGameLibrary.GetCurrentRoundGameClient(world)
    if not GameClient then
        return
    end
    local MyPlayerInfo = GameClient.PlayerInfo
    if not MyPlayerInfo then
        return
    end
    self.TaskList = self.TaskList or {}
    self.TaskMap = self.TaskMap or {}
    local MCGGameConfig = _MCG.MCGGameConfig
    for idx = 1, len do
        local DStask = DSTaskInfos.Tasks:GetRef(idx)
        if DStask and DStask.TaskID then
            local taskID = DStask.TaskID
            local isNew = false
            if not self.TaskMap[taskID] then
                local config = _MOE.Config.Chase_ChaseInLevelTaskInfo_Chase:GetDataByKey(taskID)
                if config then
                    local task = TaskClass.New(MyPlayerInfo, config)
                    self.TaskMap[taskID] = task
                    table.insert(self.TaskList, task)
                    isNew = true
                end
            end
            local task = self.TaskMap[taskID]
            if task then
                if task:ClientSetTaskCurrentValue(DStask.CurrentValue) then
                    task:CheckComplete()
                    _MOE.Logger.Log("[InTaskSystem] Update id:", taskID, "CurrentValue:", task:GetCurrentValue())
                    _MOE.EventManager:DispatchEvent(MCGGameConfig.Events.ON_CLIENT_INLEVEL_TASKUPDATE, taskID, task) -- 任务数据更新
                elseif isNew then
                    _MOE.Logger.Log("[InTaskSystem] Update id:", taskID, "CurrentValue:", task:GetCurrentValue())
                    _MOE.EventManager:DispatchEvent(MCGGameConfig.Events.ON_CLIENT_INLEVEL_TASKUPDATE, taskID, task) -- 任务数据更新
                end
            end
        end
    end
end

return InLevelClientTaskSystem