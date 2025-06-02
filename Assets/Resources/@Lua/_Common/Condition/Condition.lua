local ConditionBase = _MOE.class("CHASE_ConditionBase")

-- 条件是否有效, virtual方法，默认返回false
function ConditionBase:IsVaild()
    return false
end

-- 是否需要Tick判断
function ConditionBase:IsTickMode()
    return false
end

-- 当TickMode是true会调用
function ConditionBase:OnTick()
end

-- 清理回调
function ConditionBase:OnClear()
end

function ConditionBase:GetClassName()
    return self.__cname
end

function ConditionBase:OnTickEndOnce()
end

ConditionBase.RegisterConditionTickEndCallOnce_EvetName = "CHASE_RegisterConditionTickEndCallOnce"

-------------------------------------------- 不允许继承 --------------------------------------
--- 需要执行的时候注册，在TickEnd执行后执行一次
function ConditionBase:RegisterFrameEndCallOnce()
    -- 在判断一次后实行OnTickOnceEnd
    if _MOE and _MOE.EventManager then
        _MOE.EventManager:DispatchEvent(ConditionBase.RegisterConditionTickEndCallOnce_EvetName, self)
    end
end

-- 用来做注册用，复用条件（每个条件都要有一个HashKey）
function ConditionBase:GetHashKey()
    if self.HashKey then
        return self.HashKey
    else
        return self:GetClassName()
    end
end
---------------------------------------------------------------------------------------------



return ConditionBase