local ConditionConfig = _MOE.class("CHASE_ConditionConfig")
local ConditionClass = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Condition.Condition")
local baseActionClass = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Condition.BaseAction")

local function RegisterClassMap(nameMap, classTable)
    if not nameMap or not classTable then
        return
    end
    for _, class in pairs(classTable) do
        local className = class.__cname
        if className then
            nameMap[className] = class
        end
    end
end

local function InitVars(self)
    self.ConditionInstanceKeyMap = {} -- 条件实例MAP
    self.TickConditionInstanceMap = {}
    self.TickEndOnceConditionInstanecMap = {}
    self.DoActionStack = {}
    self.DoActionStacnMaxNum = 6 -- 最大支持的数量

    self.ActionMap = {}
    self.IsConfigLoaded = false
end

local function CallActionAndConditonFunc(self, funcName)
    if not funcName or string.len(funcName) <= 0 then
        return
    end
    xpcall(function()
        for _, action in pairs(self.ActionMap) do
            if action and action[funcName] then
                action[funcName](action)
            end
        end
        for _, condition in pairs(self.ConditionInstanceKeyMap) do
            if condition and condition[funcName] then
                condition[funcName](condition)
            end
        end
    end,
    _G.ErrorHandler
    )
end

function ConditionConfig:Ctor(...)
    self.ConditionNameClassMap = {}
    InitVars(self)
    self:OnInit(...)
end

function ConditionConfig:RegisterConditionClass(className, classModel)
    if className and string.len(className) > 0 and classModel then
        self.ConditionNameClassMap[className] = classModel
    end
end

local function TickCondition(tickConditionMap, eventName)
    if not tickConditionMap or not eventName then
        return
    end
    if tickConditionMap then
        for _, condition in pairs(tickConditionMap) do
            if condition and condition[eventName] then
                condition[eventName](condition)
            end
        end
    end
end

local function OnActionStackSort(action1, action2)
    if action1.time < action2.time then
        return true
    else
        return false
    end
end

local function PushToActionStack(self, action)
    if not action or not _MOE or not _MOE.Models.HeartBeatModel then
        return
    end
    local id = action:GetID()
    -- 最优的方案是用 LinkedList + Map的形式，这样效率最高，但目前项目LUA没有实现LinkedList
    local isFound = false
    for _, data in ipairs(self.DoActionStack) do
        if data.id == id then
            data.time = _MOE.Models.HeartBeatModel:GetClientPassTimeMs()
            isFound = true
            break
        end
    end
    if isFound then
        table.sort(self.DoActionStack, OnActionStackSort)
        return
    end
    if #self.DoActionStack == self.DoActionStacnMaxNum then
        table.remove(self.DoActionStack, 1) -- 位置1为时间最早的
    end
    local data = {time = _MOE.Models.HeartBeatModel:GetClientPassTimeMs(), id = id}
    table.insert(self.DoActionStack, data)
end

function ConditionConfig:GetActionStack()
    return self.DoActionStack
end

function ConditionConfig:CanTick()
    return true
end

function ConditionConfig:_OnTick()
    if not self:CanTick() then
        return
    end
    self:OnPreTick()

    -- 暂时需要TICK的条件比较少，暂时一次TICK执行全部，后续如果过多可以优化，一帧执行多少个
    TickCondition(self.TickConditionInstanceMap, "OnTick")
    local removeActions = nil
    for _, action in pairs(self.ActionMap) do
        if action then
            if action.OnTick then
                if action:OnTick() then
                    -- 记录ID
                    PushToActionStack(self, action)
                end
            end
            local status = action:GetStatus()
            if status == baseActionClass.StateType.Remove then
                if not removeActions then
                    removeActions = {}
                end
                table.insert(removeActions, action)
            end
        end
    end
    if removeActions then
        for _, action in ipairs(removeActions) do
            self:RemoveAction(action)
        end
    end
    TickCondition(self.TickEndOnceConditionInstanecMap, "OnTickEndOnce")
    self:_ClearTickEndOnceConditionInstanecMap()

    self:OnPostTick()
end

function ConditionConfig:_OnRegisterConditionTickEndCallOnce(condition)
    if not condition then
        return
    end
    self.TickEndOnceConditionInstanecMap[condition] = condition
end

local function CreateHashKey(className, ...)
    local params = {...}
    if #params <= 0 then
        return className
    end
    local ps = nil
    for i, p in ipairs(params) do
        if not ps then
            ps = {}
            ps[#ps + 1] = className
        end
        ps[#ps + 1] = "_"
        ps[#ps + 1] = tostring(p)
    end
    if ps then
        return table.concat(ps)
    end
    return className
end

local function GetParamUnpack(paramValue)
    if paramValue == nil then
        return
    end
    local typeStr = type(paramValue)
    if typeStr == "table" then
        local ret = table.unpack(paramValue)
        if ret then
            return ret
        end
        ret = _MOE.Utils.TableUtils.Serialize(paramValue)
        return ret
    else
        return tostring(paramValue)
    end
end

function ConditionConfig:_RegisterConditionInstance(className, paramValue)
    if not className then
        _MOE.Logger.LogWarning("[ConditionConfig] RegisterCondition: className is nil")
        return
    end
    local targetClass = self.ConditionNameClassMap[className]
    if not targetClass then
        _MOE.Logger.LogWarning("[ConditionConfig] RegisterCondition: not found className: ", className)
        return
    end
    local ret = nil
    local isNewCreate = false
    local key = CreateHashKey(className, GetParamUnpack(paramValue))
    if key then
        ret = self.ConditionInstanceKeyMap[key]
        if not ret then
            xpcall(
                function (...)
                    ret = targetClass.New(...)
                    ret.HashKey = key
                    isNewCreate = true
                end,
                _G.ErrorHandler,
                self, paramValue
            )
        end
    end
    if not ret or not key then
        _MOE.Logger.LogError("[ConditionConfig] RegisterCondition: CreateObj is nil")
    elseif isNewCreate then
        if ret.IsTickMode and ret:IsTickMode() then
            self.TickConditionInstanceMap[ret] = ret
        end
        self.ConditionInstanceKeyMap[key] = ret
    end
    return ret
end

function ConditionConfig:RegisterAction(action)
    if not action then
        return false
    end
    if action.CanRegister and not action:CanRegister() then
        return false
    end
    self.ActionMap[action:GetID()] = action
    return true
end

function ConditionConfig:RemoveAction(action)
    if not action then
        return false
    end
    local ID = action:GetID()
    return self:RemoveActionByID(ID)
end

function ConditionConfig:RemoveActionByID(ID)
    if ID == nil or self.ActionMap == nil then
        return false
    end
    local action = self.ActionMap[ID]
    if action and action.DoRemove then
        action:DoRemove()
    end
    self.ActionMap[ID] = nil
    return action ~= nil
end

function ConditionConfig:GetConditionNameClassMap()
    return self.ConditionNameClassMap
end

function ConditionConfig:GetConditionInstanceKeyMap()
    return self.ConditionInstanceKeyMap
end

function ConditionConfig:GetTickConditionInstanceMap()
    return self.TickConditionInstanceMap
end

function ConditionConfig:GetActionMap()
    return self.ActionMap
end

function ConditionConfig:GetActionByID(ID)
    if not self.ActionMap or not ID then
        return
    end
    local ret = self.ActionMap[ID]
    return ret
end

function ConditionConfig:_ClearTickEndOnceConditionInstanecMap()
    local hasData = next(self.TickEndOnceConditionInstanecMap) ~= nil
    if hasData then
        self.TickEndOnceConditionInstanecMap = {}
    end
end

--------------------------------------------------------------- 外部可调用方法 ---------------------------------------

function ConditionConfig:Dispose()
    CallActionAndConditonFunc(self, "OnClear")
    self:OnClear()
    InitVars(self)
    self:Stop()
    _MOE.EventManager:UnRegisterEventsOfRef(self)
end

function ConditionConfig:RegisterStartConditions(StartConditions)
    local conditions = nil
    if StartConditions ~= nil and string.len(StartConditions) > 0 then
        local final, configtable = pcall(load("return " .. StartConditions))
        if final then
            for _, conditionTable in ipairs(configtable) do
                for className, paramValue in pairs(conditionTable) do
                    local c = self:_RegisterConditionInstance(className, paramValue)
                    if c then
                        conditions = conditions or {}
                        table.insert(conditions, c)
                    end
                end
            end
        else
            _MOE.Logger.LogWarning("[NewGuideSystem] StartConditions Error:", StartConditions)
        end
    end
    return conditions
end

---------------------------------------------------------------- 外部继承的方法 --------------------------------------

function ConditionConfig:OnInit(...)
end

function ConditionConfig:OnClear()
end

function ConditionConfig:Start()
    if self.Timer == nil then
        self.Timer = _MOE.TimerManager:AddLoopTimer(0.3, self, self._OnTick)
        _MOE.EventManager:RegisterEvent(ConditionClass.RegisterConditionTickEndCallOnce_EvetName, self, self._OnRegisterConditionTickEndCallOnce)
    end
end

function ConditionConfig:Stop()
    if self.Timer ~= nil then
        _MOE.TimerManager:RemoveTimer(self.Timer)
        self.Timer = nil
        _MOE.EventManager:UnRegisterEvent(ConditionClass.RegisterConditionTickEndCallOnce_EvetName, self)
    end
end

function ConditionConfig:OnPreTick()
end

function ConditionConfig:OnPostTick()
end

return ConditionConfig