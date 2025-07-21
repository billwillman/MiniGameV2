local _M = _MOE.class("CHASE_BaseAction")

-- 状态定义
_M.StateType = {
    None = 0, -- 无触发
    Runing = 1, -- 可触发
    Stoped = 2, -- 可以结束了
    Remove = 3, -- 删除
}

function _M:Ctor(id, ...)
    self.ID = id
    self.StartCondition = {}
    self.StopCondition = {}
    self.Status = _M.StateType.None
    self:Init(...)
end

-- 唯一标识
function _M:GetID()
    return self.ID
end

-- 注册启动条件
function _M:RegisterStartCondition(condition)
    self.StartCondition[condition] = condition
end

function _M:RegisterStartConditions(conditions)
    if not conditions then
        return
    end
    for _, condition in pairs(conditions) do
        self.StartCondition[condition] = condition
    end
end

-- 注册冻结事件
function _M:RegisterStopCondition(condition)
    -- 需要考虑兜底
    self.StopCondition[condition] = condition
end

-- 检查是否可以开始
function _M:_CheckCanStart()
    for _, condition in pairs(self.StartCondition) do
        if not condition:IsVaild() then
            return false
        end
    end
    return true
end

-- 检查可以结束Tips
function _M:_CheckCanStop()
    for _, condition in pairs(self.StopCondition) do
        if not condition:IsVaild() then
            return false
        end
    end
    return true
end

function _M:GetClassName()
    return self.__cname
end

-- 获得当前状态
function _M:GetStatus()
    return self.Status
end

function _M:_ResetStopStatus(isForce)
    if self.Status == _M.StateType.Stoped or isForce then
        self.Status = _M.StateType.None
    end
end

--- 手动设置为Remove状态
function _M:CustomMarkRemove()
    self.Status = _M.StateType.Remove
end

-- 返回：如果执行了DoAction返回true
function _M:OnTick()
    if not self:CanRegister() then
        self.Status = _M.StateType.Remove
        return false
    end
    local ret = false
    if self.Status == _M.StateType.None then
        -- 走一次TICK条件
        if self:_CheckCanStart() and self:CustomCanStart() then
            self.Status = _M.StateType.Runing
            self:DoAction(false) -- 是否是TickCall
            ret = true
        end
    elseif self.Status == _M.StateType.Runing then
        if self:_CheckCanStop() then
            self.Status = _M.StateType.Stoped
            return false
        end
        if self:IsTickAction() then
            self:DoAction(true)
            ret = true
        end
    elseif self.Status == _M.StateType.Stoped then
        if not self:_CheckCanStart() then
            self:_ResetStopStatus()
        end
    end
    return ret
end

function _M:OnClear()
    self:Clear()
    self.StartCondition = {}
    self.StartTickCondtion = nil
    self.StopCondition = {}
    self.StopTickCondition = nil
end

---------------------------------------------------------- 可以覆写的方法
---例如打开教程
-- isTickCall 是否是从TICK调过来的
function _M:DoAction(isTickCall)
end

function _M:DoRemove()
end

-- 自身条件，例如，一天触发了3次不让触发了，优先级最高
function _M:CanRegister()
    return true
end

function _M:CustomCanStart()
    return true
end

-- 是否是持续动作
function _M:IsTickAction()
    return false
end

function _M:Init()
end

-- 被清理
function _M:Clear()
end

-----------------------------------------------------------


return _M