--- class基类
local setmetatableindex_
setmetatableindex_ = function(t, index)
    local mt = getmetatable(t)
    if not mt then mt = {} end
    if not mt.__index then
        mt.__index = index
        setmetatable(t, mt)
    elseif mt.__index ~= index then
        setmetatableindex_(mt, index)
    end
end

local setmetatableindex = setmetatableindex_

-- 为了跟UE4.Class 统一，所以添加了这个逻辑
local function CallSuper(t, k, ...)
    return t.super[k](...)
end


local function class(className, ...)
    local cls = {__cname = className}

    local supers = {...}
    for _, super in ipairs(supers) do
        local superType = type(super)
        if superType == "function" then
            cls.__create = super
        elseif superType == "table" then
            if super[".isclass"] then
                cls.__create = function() return super:Create() end
            else
                cls.__supers = cls.__supers or {}
                cls.__supers[#cls.__supers + 1] = super
                if not cls.super then
                    cls.super = super
                end
            end
        end
    end

    cls.__index = cls
    if not cls.__supers or #cls.__supers == 1 then
        setmetatable(cls, {__index = cls.super})
    else
        setmetatable(cls, {__index = function(_, key)
            local temp_supers = cls.__supers
            for i = 1, #temp_supers do
                local super = temp_supers[i]
                if super[key] then return super[key] end
            end
        end})
    end

    if not cls.Ctor then
        -- add default constructor
        cls.Ctor = function() end
    end
    cls.New = function(...)
        local instance
        if cls.__create then
            instance = cls.__create(...)
        else
            instance = {}
        end
        setmetatableindex(instance, cls)
        instance.class = cls
        instance.className = className

        -- 默认不执行父类Ctor
        if cls[".superctor"] and cls.__supers then
            local stack = {cls}
            local pc = 1
            while pc > 0 do
                local from = #stack - pc + 1
                pc = 0
                for i = from, #stack do
                    local super = stack[i]
                    if super.__supers then
                        for _, grandSuper in ipairs(super.__supers) do
                            pc = pc + 1
                            table.insert(stack, grandSuper)
                        end
                    end
                end
            end
            for i = #stack, 1, -1 do
                local super = stack[i]
                super.Ctor(instance, ...)
            end
        else
            instance:Ctor(...)
        end

        return instance
    end
    cls.CallSuper = CallSuper
    cls.Create = function(_, ...)
        return cls.New(...)
    end

    return cls
end

-- local function getclassname(obj)
--     if obj.className ~= nil then
--         return obj.className
--     end
--     local cls = obj
--     return cls.__cname
-- end

---example
-- local ClassA = class("ClassA")
-- local ClassB = class("ClassB", ClassA)

return class

