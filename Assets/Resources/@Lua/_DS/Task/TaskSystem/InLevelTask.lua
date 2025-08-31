local InLevelTask = _MOE.class("CHASE_InLevelTask")

local Meta = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Task.PlayerInfoDataBoardMeta")

local TaskParamType = {
    FromDataBoard = 0,
    ConstValue = 1,
    ConstIntArray = 2,
}

local TaskState = {
    NoComplete = 0,
    Complete = 1,
}

InLevelTask.TaskState = TaskState

function InLevelTask:Ctor(PlayerInfo, config)
    self.PlayerInfo = PlayerInfo
    self.Config = config
    self.State = TaskState.NoComplete
    ----- 只有Server端才需要关注DSUPDATE事件
    if _MOE.Utils.WorldUtils:IsServer() and self.Config then
        ---- 只有局内需要显示的任务才需要注册事件 -----------
        if self:IsClientInLevelView() then
            local SideId = self:GetOwnerSideId()
            if SideId then
                local MetaGroup = Meta[SideId]
                if MetaGroup then
                    if self:IsTaskPram1IsFromDataBoard() then
                        local MetaInfo = MetaGroup[config.TaskParam1]
                        if MetaInfo and MetaInfo.DSUpdateEvent then
                            local eventName = nil
                            if type(MetaInfo.DSUpdateEvent) == "table" then
                                if #MetaInfo.DSUpdateEvent > 0 then
                                    eventName = MetaInfo.DSUpdateEvent[1]
                                end
                            elseif string.len(MetaInfo.DSUpdateEvent) > 0 then
                                eventName = MetaInfo.DSUpdateEvent
                            end
                            if eventName and string.len(eventName) > 0 then
                                _MOE.EventManager:RegisterEvent(eventName, self, self._OnTaskParam1Update)
                            end
                        end
                    end
                    if self:IsTaskParam2FromDataBoard() then
                        local MetaInfo = MetaGroup[config.TaskParam2]
                        if MetaInfo and MetaInfo.DSUpdateEvent then
                            local eventName = nil
                            if type(MetaInfo.DSUpdateEvent) == "table" then
                                if #MetaInfo.DSUpdateEvent > 0 then
                                    eventName = MetaInfo.DSUpdateEvent[1]
                                end
                            elseif string.len(MetaInfo.DSUpdateEvent) > 0 then
                                eventName = MetaInfo.DSUpdateEvent
                            end
                            if eventName and string.len(eventName) > 0 then
                                _MOE.EventManager:RegisterEvent(eventName, self, self._OnTaskParam2Update)
                            end
                        end
                    end
                end
            end
        end
    end
end

function InLevelTask:GetPlayerInfo()
    return self.PlayerInfo
end

--- 客户端局内是否可以显示
function InLevelTask:IsClientInLevelView()
    local ret = self.Config ~= nil and self.Config.TaskResultType == 2
    return ret
end

------ Server

function InLevelTask:ServerUpdateTaskToSystem()
    if _MCG and _MCG.InLevelTaskSystem then
        _MCG.InLevelTaskSystem:UpdateTaskInfoCurrentValue(self)
    end
end

function InLevelTask:_OnTaskParam1Update(SourcePlayerInfo)
    if self.PlayerInfo ~= SourcePlayerInfo then
        return
    end
    self:CheckComplete()
    self:ServerUpdateTaskToSystem()
end

function InLevelTask:_OnTaskParam2Update(SourcePlayerInfo)
    if self.PlayerInfo ~= SourcePlayerInfo then
        return
    end
    self:CheckComplete()
    -- self:ServerUpdateTaskToSystem()
end

--- DS获得积分
function InLevelTask:ServerGetScore()
    if not self:IsComplete() or not self.Config or not self.Config.Scores then
        return 0
    end
    local currentValue = self:GetCurrentValueOne() or 0
    if currentValue <= 0 then
        return 0
    end
    local targetValue = self:GetTargetValue() or 0
    if type(targetValue) == "table" then
        if next(targetValue) == nil then
            return 0
        end
        local idx = nil
        for i, t in ipairs(targetValue) do
            if currentValue >= t then
                idx = i
            else
                break
            end
        end
        if idx == nil then
            return 0
        end
        return self.Config.Scores[idx] or 0
    else
        if targetValue <= 0 then
            return 0
        end
        if currentValue >= targetValue then
            return self.Config.Scores[1] or 0
        end
    end
end

--------------------------------------

function InLevelTask:GetTaskID()
    if self.Config then
        return self.Config.ID
    end
end

function InLevelTask:GetOwnerSideId()
    if self.SideId then
        return self.SideId
    end
    if self.PlayerInfo then
        self.SideId = self.PlayerInfo:GetSideId()
    end
    return self.SideId
end

function InLevelTask:GetOwnerPlayerUID()
    if self.PlayerUID then
        return self.PlayerUID
    end
    if self.PlayerInfo then
        local ret = self.PlayerInfo:GetPlayerUID()
        if ret and ret ~= 0 then
            self.PlayerUID = ret
        end
    end
    return self.PlayerUID
end

function InLevelTask:IsTaskPram1IsFromDataBoard()
    local ret = self.Config and self.Config.TaskParamType1 and self.Config.TaskParamType1 == TaskParamType.FromDataBoard and 
                self.Config.TaskParam1 and string.len(self.Config.TaskParam1) > 0
    return ret
end

function InLevelTask:IsTaskParam2FromDataBoard()
    local ret = self.Config and self.Config.TaskParamType2 and
        self.Config.TaskParamType2 == TaskParamType.FromDataBoard and 
        self.Config.TaskParam2 and string.len(self.Config.TaskParam2) > 0
    return ret
end

---- 当前值
function InLevelTask:GetCurrentValue()
    if _MOE.Utils.WorldUtils:IsServer() then
        if self.Config and self.Config.TaskParamType1 then
            --- 数据面板
            if self:IsTaskPram1IsFromDataBoard() then
                local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(self:GetOwnerPlayerUID())
                if DataBoard then
                    local ret = math.floor(tonumber(DataBoard:GetDataValue(self.Config.TaskParam1)) or 0)
                    return ret
                end
            elseif self.Config.TaskParamType1 == TaskParamType.ConstValue then
                if self.CurrentConstValue ~= nil then
                    return self.CurrentConstValue
                end
                self.CurrentConstValue = math.floor(tonumber(self.Config.TaskParam1) or 0)
                return self.CurrentConstValue
            elseif self.Config.TaskParamType1 == TaskParamType.ConstIntArray then
                if self.CurrentConstValues ~= nil then
                    return self.CurrentConstValues
                end
                local strs = _MOE.Utils.StringUtils.SplitString(self.Config.TaskParam1, ",")
                if strs == nil then
                    self.CurrentConstValues = {}
                else
                    self.CurrentConstValues = {}
                    for _, str in ipairs(strs) do
                        local n = math.floor(tonumber(str) or 0)
                        table.insert(self.CurrentConstValues, n)
                    end
                end
                return self.CurrentConstValues
            end
        end
    else
        return math.floor(tonumber(self.CurrentValue) or 0) -- Client端读取
    end
end

function InLevelTask:ClientSetTaskCurrentValue(value)
    if (not _MCG.IsSinglePlay) and _MOE.Utils.WorldUtils:IsServer() then
        return false
    end
    if self.CurrentValue == value then
        return false
    end
    self.CurrentValue = value
    return true
end

function InLevelTask:GetCurrentValueOne()
    local currentValue = self:GetCurrentValue()
    if currentValue == nil then
        return 0
    end
    if type(currentValue) == "table" then
        currentValue = currentValue[1] or 0
    end
    return currentValue
end

function InLevelTask:GetTargetValueOne()
    local targetValue = self:GetTargetValue()
    if targetValue == nil then
        return 0
    end
    if type(targetValue) == "table" then
        targetValue = targetValue[1] or 0
    end
    return targetValue
end

function InLevelTask:CheckComplete()
    local currentValue = self:GetCurrentValueOne()
    local targetValue = self:GetTargetValueOne()
    if currentValue == nil or targetValue == nil or targetValue <= 0 then -- targetValue不应该为0
        return
    end
    if currentValue >= targetValue then
        self.State = TaskState.Complete
    else
        self.State = TaskState.NoComplete
    end
end

--- 获得显示的任务文本
function InLevelTask:GetDisplayText()
    if self.Config then
        local ret = self.Config.TaskContentFormat
        local currStr = tostring(self:GetCurrentValueOne() or 0)
        local targetStr = tostring(self:GetTargetValueOne() or 0)
        ret = string.gsub(ret, "{TaskParam1}", currStr)
        ret = string.gsub(ret, "{TaskParam2}", targetStr)
        return ret
    end
    return ""
end

--- 局内显示任务文本
function InLevelTask:GetInLevelTaskText()
    if self.Config then
        local ret = self.Config.TaskContentFormat
        local currCount = self:GetCurrentValueOne() or 0
        local targetCount = self:GetTargetValueOne() or 0
        currCount = math.min(currCount, targetCount)
        ret = string.gsub(ret, "{TaskParam1}/{TaskParam2}", "<ChaseTaskList>" .. tostring(currCount) .. "/" .. tostring(targetCount) .. "</>")
        return ret
    end
    return ""
end

function InLevelTask:IsTaskOver()
    local currCount = self:GetCurrentValueOne() or 0
    local targetCount = self:GetTargetValueOne() or 0
    return currCount > targetCount
end

--- 目标值
function InLevelTask:GetTargetValue()
    if self.Config and self.Config.TaskParamType2 then
        --- 数据面板
        if self:IsTaskParam2FromDataBoard() then
            local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(self:GetOwnerPlayerUID())
            if DataBoard then
                local ret = math.floor(tonumber(DataBoard:GetDataValue(self.Config.TaskParam2)) or 0)
                return ret
            end
        elseif self.Config.TaskParamType2 == TaskParamType.ConstValue then
            if self.TargetConstValue ~= nil then
                return self.TargetConstValue
            end
            self.TargetConstValue = math.floor(tonumber(self.Config.TaskParam2) or 0)
            return self.TargetConstValue
        elseif self.Config.TaskParamType2 == TaskParamType.ConstIntArray then
            if self.TargetConstValues ~= nil then
                return self.TargetConstValues
            end
            local strs = _MOE.Utils.StringUtils.SplitString(self.Config.TaskParam2, ",")
            if strs == nil then
                self.TargetConstValues = {}
            else
                self.TargetConstValues = {}
                for _, str in ipairs(strs) do
                    local n = math.floor(tonumber(str)) or 0
                    table.insert(self.TargetConstValues, n)
                end
            end
            return self.TargetConstValues
        end
    end
end

---- 是否是完成状态
function InLevelTask:IsComplete()
    return self.State == TaskState.Complete
end

function InLevelTask:Dispose()
    _MOE.EventManager:UnRegisterEventsOfRef(self)
end

return InLevelTask