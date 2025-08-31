--local BaseClass = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Condition.ConditionConfig")
local InLevelTaskSystem = _MOE.class("CHASE_InLevelTaskSystem")

local TaskClass = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Task.TaskSystem.InLevelTask")

local function IsVaildTaskCfg(cfgItem)
    if cfgItem and cfgItem.TaskParamType1 and cfgItem.TaskParam1 and string.len(cfgItem.TaskParam1) > 0 
        and cfgItem.TaskParamType2 and cfgItem.TaskParam2 and string.len(cfgItem.TaskParam2) > 0 then
        return true
    end
    return false
end

---- 加载配置(DS全局就一个)
function InLevelTaskSystem:_LoadConfig()
    --- 从配置表建立关联
    if self.Config ~= nil then
        return
    end
    local AllConfig = _MOE.Config.Chase_ChaseInLevelTaskInfo_Chase:GetAllData()
    if AllConfig  == nil then
        self.Config = {}
        return
    end
    self.Config = {}
    local MCGGameConfig = _MCG.MCGGameConfig
    local matchType = MCGGameConfig:MCGGetMatchType()
    for _, cfgItem in pairs(AllConfig) do
        if cfgItem and (cfgItem.MatchType == 0 or cfgItem.MatchType == matchType) then
            if IsVaildTaskCfg(cfgItem) then -- 有效配置
                local UnitID = cfgItem.UnitID or 0
                local UnitTasks
                if UnitID == 0 then
                    self.Config.CommonTasks = self.Config.CommonTasks or {}
                    UnitTasks = self.Config.CommonTasks
                else
                    self.Config[UnitID] = self.Config[UnitID] or {}
                    UnitTasks = self.Config[UnitID]
                end
                if cfgItem.SideId == MCGGameConfig.ConstValue.PlayerSideId or cfgItem.SideId == MCGGameConfig.ConstValue.BossSideId then
                    UnitTasks[cfgItem.SideId] = UnitTasks[cfgItem.SideId] or {}
                    table.insert(UnitTasks[cfgItem.SideId], cfgItem)
                end
            end
        end
    end
end

function InLevelTaskSystem:GetTaskInfoByID(id)
    if not id then
        return
    end
    local ret = _MOE.Config.Chase_ChaseInLevelTaskInfo_Chase:GetDataByKey(id)
    return ret
end

--- UnitID 暗星是BOSSTYPE，星宝是PropID
function InLevelTaskSystem:GetTasksByUnitID(PlayerInfo, UnitID)
    if UnitID == nil or PlayerInfo == nil then
        return
    end
    self:_LoadConfig()
    if self.Config == nil then --- 配置读取
        return
    end
    local ret = {}
    local SideId = PlayerInfo:GetSideId()
    --- 1.先获取通用仅能
    if self.Config.CommonTasks then
        local taskList = self.Config.CommonTasks[SideId]
        if taskList then
            for _, cfg in ipairs(taskList) do
                if cfg and cfg.ID then
                    local task = TaskClass.New(PlayerInfo, cfg)
                    table.insert(ret, task)
                end
            end
        end
    end
    --- 2.然后添加特定角色
    local config = self.Config[UnitID]
    if config then
        local taskList = config[SideId]
        if taskList then
            for _, cfg in ipairs(taskList) do
                local task = TaskClass.New(PlayerInfo, cfg)
                table.insert(ret, task)
            end 
        end
    end
    return ret
end

function InLevelTaskSystem:_DisposePlayerTasks()
    if self.PlayerTasksMap ~= nil then
        for _, taskList in pairs(self.PlayerTasksMap) do
            if taskList then
                for _, task in ipairs(taskList) do
                    if task then
                        task:Dispose()
                    end
                end
            end
        end
    end
end

function InLevelTaskSystem:_OnReceiveUseProp(Character)
    if not Character or type(Character) == "string" then
        return
    end
    local UID = Character:GetUID()
    if not UID then
        return
    end
    --[[
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return
    end
    ]]
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(UID)
    if not DataBoard then
        return
    end
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.UseSkillNum) or 0
    count = count + 1 -- 技能使用增加
    DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.UseSkillNum, count)
end

------------------------------------------------ 对外调用 ------------------------------------------------

function InLevelTaskSystem:Dispose()
    self:_DisposePlayerTasks()
    _MOE.EventManager:UnRegisterEventsOfRef(self)
    self.Config = nil
    self.Game = nil
    self.PlayerTasksMap = nil 
end

function InLevelTaskSystem:Ctor(Game)
    self.Game = Game
    _MOE.EventManager:RegisterEvent(_MOE.EventEnum.ON_USE_PROP, self, self._OnReceiveUseProp)
end

function InLevelTaskSystem:GetTaskList(PlayerInfo)
    if not self.PlayerTasksMap or not PlayerInfo then
        return
    end
    local PlayerUID = PlayerInfo:GetPlayerUID()
    if not PlayerUID then
        return
    end
    local ret = self.PlayerTasksMap[PlayerUID]
    return ret
end

function InLevelTaskSystem:GetTaskListByUID(PlayerUID)
    if not self.PlayerTasksMap or not PlayerUID then
        return
    end
    local ret = self.PlayerTasksMap[PlayerUID]
    return ret
end

--- 填充局内任务
function InLevelTaskSystem:FillPlayerInLevelTasks(PlayerInfo)
    if not PlayerInfo then
        return false
    end
    local Character = PlayerInfo:GetCharacter()
    if not Character then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    local PlayerUID = PlayerInfo:GetPlayerUID()
    local UnitID = nil
    if SideId == _MCG.MCGGameConfig.ConstValue.BossSideId then
        local CharTypeComponent = Character.BP_MCGCharTypeInfo
        if not CharTypeComponent then
            return false
        end
        UnitID = CharTypeComponent.BossType -- BossType
    elseif SideId == _MCG.MCGGameConfig.ConstValue.PlayerSideId then
        local SkillComponent = Character.BP_MCGSkillPropComponent
        if not SkillComponent then
            local SkillComponentClass =
            UE4.LoadClass(
                "Blueprint'/Game/Feature/CHASE/Gameplay/Boss/Components/BP_MCGSkillPropComponent.BP_MCGSkillPropComponent_C'"
            )
            if SkillComponentClass == nil then
                return false
            end
            SkillComponent = Character:GetComponentByClass(SkillComponentClass)
            if not SkillComponent then
                return false
            end
        end
        local SkillPropArray = SkillComponent:GetSkillPropArray()
        if not SkillPropArray or SkillPropArray:Length() <= 0 then
            return false
        end
        local Skill = SkillPropArray:GetRef(1)
        if not Skill then
            return false
        end
        UnitID = Skill.SkillID -- 星宝技能
    end
    if UnitID == nil then
        return false
    end
    local taskList = self:GetTasksByUnitID(PlayerInfo, UnitID)
    if not taskList or next(taskList) == nil then
        return false
    end

    self.PlayerTasksMap = self.PlayerTasksMap or {}
    self.PlayerTasksMap[PlayerUID] = taskList

    ---- 过滤出ClientTaskView
    local clientTaskViewList = {}
    for _, task in ipairs(taskList) do
        if task and task:IsClientInLevelView() then
            table.insert(clientTaskViewList, task)
        end
    end

    if next(clientTaskViewList) == nil then
        return false
    end

    --- 写入数据面板
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if DataBoard then
        local TaskInfos = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.TaskInfos)
        if TaskInfos and TaskInfos.Tasks then
            local num = #clientTaskViewList
            TaskInfos.Tasks:Resize(num)
            for idx, task in ipairs(clientTaskViewList) do
                local DStaskInfo = TaskInfos.Tasks:GetRef(idx)
                DStaskInfo.TaskID = task:GetTaskID()
                DStaskInfo.CurrentValue = 0
            end
            DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.TaskInfos, TaskInfos)
        end
    end
    ------------------------
end

function InLevelTaskSystem:UpdateTaskInfoCurrentValue(InLevelTask)
    if not InLevelTask then
        return
    end
    --- 只有特殊任务需要下发给Client端
    if not InLevelTask:IsClientInLevelView() then
        return
    end
    --------------------------------
    local PlayerInfo = InLevelTask:GetPlayerInfo()
    if not PlayerInfo then
        return
    end
    local PlayerUID = PlayerInfo:GetPlayerUID()
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if DataBoard then
        local TaskInfos = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.TaskInfos)
        if TaskInfos and TaskInfos.Tasks then
            local isDirty = false
            for idx = 1, TaskInfos.Tasks:Length() do
                local DStaskInfo = TaskInfos.Tasks:GetRef(idx)
                if DStaskInfo and DStaskInfo.TaskID == InLevelTask:GetTaskID() then
                    DStaskInfo.CurrentValue = InLevelTask:GetCurrentValue()
                    isDirty = true
                    break
                end
            end
            if isDirty then
                DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.TaskInfos, TaskInfos)
            end
        end
    end
end

function InLevelTaskSystem:FillInLevelTasks()
    local GameInfo = self.Game.GameInfo
    if GameInfo then
        local PlayerInfos = GameInfo:GetAllPlayerInfos()
        if PlayerInfos then
            for idx = 1, PlayerInfos:Length() do
                local PlayerInfo = PlayerInfos:GetRef(idx)
                if PlayerInfo then
                    self:FillPlayerInLevelTasks(PlayerInfo) -- 填充PlayerInfo任务
                end
            end
        end
    end
end

----------- DS上报局内任务
function InLevelTaskSystem:ReportInLevelTasks(PlayerInfo, InlevelTasks)
    if PlayerInfo then
        --- 真人才填充局内任务数据
        local taskList = self:GetTaskList(PlayerInfo)
        local PlayerUID = PlayerInfo:GetPlayerUID()
        if taskList and next(taskList) ~= nil then
            InlevelTasks = InlevelTasks or {}
            for _, task in ipairs(taskList) do
                task:CheckComplete() -- 这个主动调用一下
                if task and task:IsComplete() then
                    local taskID = task:GetTaskID()
                    local score = task:ServerGetScore()
                    local currentValue = task:GetCurrentValueOne() or 0
                    if taskID and score then
                        -- InlevelTask[taskID] = score -- 积分上报
                        local InlevelTask = {id = taskID, current = currentValue, score = score}
                        table.insert(InlevelTasks, InlevelTask)
                        _MOE.Logger.Log("[InLevelTask] Report: taskId=", taskID, "score=", score, "currentValue=", currentValue, "PlayerUID=", PlayerUID)
                    end
                end
            end
        end
        ------------------------
    end
    --- 任务排序上报
    if InlevelTasks ~= nil and next(InlevelTasks) ~= nil then
        local TaskSortFunc = function (a, b)
            local ret = a.id < b.id
            return ret
        end
        table.sort(InlevelTasks, TaskSortFunc)
    end
    return InlevelTasks
end

return InLevelTaskSystem