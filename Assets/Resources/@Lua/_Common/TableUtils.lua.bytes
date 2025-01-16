---@class TableUtils
local TableUtils = {}

local ignore_keys =
{
    --[[
    battleRecords = 1,
    lastBattleData = 1,
    protodata = 1,
    workData = 1,
    fileContentList = 1,
    fileData = 1,
    specModInfo = 1,
    req_body = 1,
    rsp_body = 1,
    safeSdkData = 1,
    snapshot = 1,
    global_record = 1,
    dbMail = 1,
    frameBattleEndNtf = 1,
    globalRecord = 1,
    snapshotRecord = 1,
    mapInfo = 1
    ]]
}

local function _InnerTablePrint(data, cstring, pfunc, depth, maxDepth)
    if data == nil then
        pfunc("data is nil")
    end
    if depth == 0 then
        return
    end
    local cs = cstring
    local strTabBracket = string.rep("\t", math.max(maxDepth - depth, 0))
    local strTab = string.rep("\t", maxDepth - depth + 1)

    pfunc(table.concat({strTabBracket, cstring, "{"}))
    if type(data) == "table" then
        for k, v in pairs(data) do
            if ignore_keys[k] == nil then
                pfunc(table.concat({strTab, cs, tostring(k), " = ", tostring(v)}))
                if type(v) == "table" and k ~= "__index" then
                    _InnerTablePrint(v, cs, pfunc, depth - 1, maxDepth)
                end
            else
                pfunc("data is bytes, ignore")
            end
        end
    else
        pfunc(table.concat({cs, tostring(data), type(data)}))
    end
    pfunc(table.concat({strTabBracket, cstring, "}"}))
end

function TableUtils.IsEmpty(tb)
    if tb == nil then
        return true
    end
    for _, _v in pairs(tb) do
        return false
    end
    return true
end

---@private
---TableUtils.PrintTable 打印Table
---@param data  table
---@param pfunc function
---@param depth int 最大打印层数
function TableUtils.PrintTable(data, pfunc, depth)
    local cstring = ""
    pfunc = pfunc or print
    depth = depth or 12
    local maxDepth = depth
    _InnerTablePrint(data, cstring, pfunc, depth, maxDepth)
end

function TableUtils.Serialize(obj, serializeMeta)
    if serializeMeta == nil then
        serializeMeta = false
    end
    local s = ""
    local t = type(obj)
    if t == "number" then
        s = table.concat({s, obj})
    elseif t == "boolean" then
        s = table.concat({s,tostring(obj)})
    elseif t == "string" then
        s = table.concat({s, string.format("%q", obj)})
    elseif t == "table" then
        s = table.concat({s, "{\n"})
        for k, v in pairs(obj) do
            s = table.concat({s , "[" , TableUtils.Serialize(k) , "]=" , TableUtils.Serialize(v) , ",\n"})
        end
        if serializeMeta then
            local metatable = getmetatable(obj)
            if metatable ~= nil and type(metatable.__index) == "table" then
                for k, v in pairs(metatable.__index) do
                    s = table.concat({s, "[", TableUtils.Serialize(k), "]=", TableUtils.Serialize(v) , ",\n"})
                end
            end
        end
        s = table.concat({s, "}"})
    elseif t == "nil" then
        return "nil"
    else
        print("can not serialize a type="..t)
    end
    return s
end

---@private
---TableUtils.SerializeLite
---Liteba版的序列化, 一般用于日志输出
---默认只输出1层table内容, 不输出metatable
---单行内容, 不换行
---@param obj        Type Description
---@param max_depth  Type Description
---@param curr_depth Type Description
---@return  Type Description
function TableUtils.SerializeLite(obj, max_depth, curr_depth)
    max_depth = max_depth or 2

    if curr_depth == nil then
        curr_depth = 0
    end

    if curr_depth > max_depth then
        return "..."
    end

    local s = ""
    local t = type(obj)
    if t == "number" then
        s = table.concat({s, obj})
    elseif t == "boolean" then
        s = table.concat({s, tostring(obj)})
    elseif t == "string" then
        s = table.concat({s, string.format("%q", obj)})
    elseif t == "function" then
        s = table.concat({s, "<Function>"})
    elseif t == "table" then
        curr_depth = curr_depth + 1

        s = table.concat({s, "{ "})
        for k, v in pairs(obj) do
            s = table.concat(
                {
                    s,
                    "[",
                    TableUtils.SerializeLite(k, max_depth, (curr_depth - 1)),
                    "]=",
                    TableUtils.SerializeLite(v, max_depth, curr_depth),
                    ", "
                }
            )
        end

        s = table.concat({s, "} "})
    elseif t == "nil" then
        return "nil"
    else
        error("can not serialize a type.")
    end
    return s
end

function TableUtils.UnSerialize(s)
    local code = string.format("local tmp = %s; return tmp", s)
    local l = load(code)
    if l ~= nil then
        return l()
    else
        return nil
    end
end

---@public
---TableUtils.ReadOnlyTable 设置table只读
---@param table table Description
function TableUtils.ReadOnlyTable(table)
    return setmetatable(
        {},
        {
            __index = table,
            __newindex = function(intable, key, value)
                error("Attempt to modify read-only table")
            end,
            __metatable = false
        }
    )
end

function TableUtils.Copy(obj, seen)
    if type(obj) ~= "table" then
        return obj
    end
    if seen and seen[obj] then
        return seen[obj]
    end
    local s = seen or {}
    local metaTable = getmetatable(obj)
    local res
    -- metaTable有可能是false，因为目前配置表转成lua，item的元表做了保护会对__metatable设置为false
    -- 然后Lua源码setmetatable对非nil和table的数据会触发类型检查异常（具体看：luaB_setmetatable源码）
    if metaTable then
        res = setmetatable({}, metaTable)
    else
        res = {}
    end
    s[obj] = res
    for k, v in pairs(obj) do
        res[TableUtils.Copy(k, s)] = TableUtils.Copy(v, s)
    end
    return res
end

function TableUtils.Tablelength(table)
    if (table == nil or type(table) ~= "table") then
        return 0
    end
    local count = 0
    for _ in pairs(table) do
        count = count + 1
    end
    return count
end

function TableUtils.CombineTable(target, source, isOverride)
    if not target or not source then
        return target
    end
    for k, v in pairs(source) do
        if isOverride then
            target[k] = v
        elseif target[k] == nil then
            target[k] = v
        end
    end
    return target
end

function TableUtils.RemoveData(tb, conditionFunc)
    if type(tb) ~= "table" then
        return
    end
    for i = #tb, 1, -1 do
        if not conditionFunc or conditionFunc(tb[i]) then
            table.remove(tb, i)
        end
    end
end

function TableUtils.RemoveElement(tb, element)
    if type(tb) ~= "table" then
        return
    end
    for i = 1, #tb do
        if tb[i] == element then
            table.remove(tb, i)
            break
        end
    end
end

-- 二分查找找到合适位置
function  TableUtils.FindInserListIndex(list, value, valueFunc, startIdx, endIdx)
    if not list or not value or not valueFunc then
        return -1
    end
    local num = #list
    if num <= 0 then
        return 1
    end
    if num == 1 then
        local t = list[1].X
        if value < t then
            return 1
        end
        return 2
    end
    startIdx = startIdx or 1
    endIdx = endIdx or num

    if (endIdx - startIdx) <= 1 then
        local startValue =  valueFunc(list[startIdx])
        local endValue = valueFunc(list[endIdx])
        local ret
        if value < startValue then
            ret = startIdx
        elseif value > endValue then
            ret = endIdx + 1
        else
            ret = endIdx
        end
        return ret
    end

    local midIdx = startIdx + math.floor((endIdx - startIdx)/2)
    local midT = valueFunc(list[midIdx])
    if value < midT then
        endIdx = midIdx
    elseif value > midT then
        startIdx = midIdx
    else
        return midIdx
    end
    return TableUtils.FindInserListIndex(list, value, valueFunc, startIdx, endIdx)
end

function TableUtils.ReverseTable(tb)
    local tmp = {}
    local key = #tb
    for i = 1, #tb do
        tmp[i] = tb[key]
        key = key - 1
    end
    return tmp
end

function TableUtils.Clear(tb)
    for i = #tb, 1, -1 do
        table.remove(tb, i)
    end
end

function TableUtils.ClearTable(tbl)
    if TableUtils.IsTable(tbl) then
        for k,_ in pairs(tbl) do
            tbl[k] = nil
        end
    end
end

function TableUtils.Contains(tb, value)
    if value and tb and TableUtils.IsTable(tb) and #tb > 0 then
        for i = 1, #tb do
            if tb[i] == value then
              return i
            end
        end
    end
    return nil
end

function TableUtils.ContainsValue(tb, value)
    if value and tb and TableUtils.IsTable(tb) then
        for _, val in pairs(tb) do
            if (val == value) then
                return true
            end
        end
    end
    return false
end

function TableUtils.IsTable(var)
    return type(var) == "table"
end

function TableUtils.Merge(dst, src)
    if not (TableUtils.IsTable(dst) and TableUtils.IsTable(src)) then
        return false
    end
    for key, value in pairs(src) do
        dst[key] = value
    end
    return true
end

--判断是不是空表
---@param valueTable table
---@return boolean
function TableUtils.TableIsNil(valueTable)
    if valueTable == nil or next(valueTable) == nil then
        return true
    end
    return false
end

function TableUtils.Append(tbl, value)
    assert (TableUtils.IsTable(tbl), type(tbl))
    if type(value) == "table" then
        local isList = TableUtils.TableIsList(value)
        if isList then
            for i, v in ipairs(value) do
                table.insert(tbl, v)
            end
        else
            for k, v in pairs(value) do
                tbl[k] = v
            end
        end
    else
        table.insert(tbl, value)
    end
end

function TableUtils.AddUnique(tbl, value)
    assert (TableUtils.IsTable(tbl), type(tbl))

    tbl = tbl or {}

    if not TableUtils.ContainsValue(tbl, value) then
        table.insert(tbl, value)
    end
end


function TableUtils.TableIsList(t)
    if type(t) ~= "table" then return false end
    local n = 0
    for k, v in pairs(t) do
        if type(k) ~= "number" then return false end
        if k <= 0 or k > #t or math.floor(k) ~= k then return false end
        n = n + 1
    end
    return n == #t
end

--- 时间转换成秒分
function TableUtils.NormilizeLeftTimeNew(Time)
    if Time==nil then
        return "00:00"
    end
    local min = Time / 1000/60
    local sec = Time /1000 % 60
    return string.format("%02d:%02d", math.floor(min), math.floor(sec))
end

function TableUtils.TableToString(tbl, indent)
    indent = indent or 0
    local lineIndent = string.rep("  ", indent)
    local str = lineIndent.."{\n"
    for key, value in pairs(tbl) do
        lineIndent = string.rep("  ", indent + 1)
        local keyValueStr = ""

        if type(key) == "number" then
            keyValueStr = "[" .. key .. "]"
        else
            keyValueStr = key
        end

        if type(value) == "table" then
            str = str .. lineIndent .. keyValueStr .. " = \n" .. TableUtils.TableToString(value, indent + 1) .. ",\n"
        elseif type(value) == "string" then
            str = str .. lineIndent .. keyValueStr .. " = '" .. value .. "',\n"
        else
            str = str .. lineIndent .. keyValueStr .. " = " .. tostring(value) .. ",\n"
        end
    end

    str = str .. string.rep("  ", indent) .. "}"
    return str
end

function TableUtils.TableToStringWithoutLineBreak(tbl, indent)
    if tbl == nil then
        return "{}"
    end
    indent = indent or 0
    local lineIndent = string.rep("  ", indent)
    local str = lineIndent.."{"
    for key, value in pairs(tbl) do
        lineIndent = string.rep("  ", indent + 1)
        local keyValueStr = ""

        if type(key) == "number" then
            keyValueStr = "[" .. key .. "]"
        else
            keyValueStr = key
        end

        if type(value) == "table" then
            str = str .. lineIndent .. keyValueStr .. " = " .. TableUtils.TableToString(value, indent + 1) .. ","
        elseif type(value) == "string" then
            str = str .. lineIndent .. keyValueStr .. " = '" .. value .. "',"
        else
            str = str .. lineIndent .. keyValueStr .. " = " .. tostring(value) .. ","
        end
    end

    str = str .. string.rep("  ", indent) .. "}"
    return str
end


---二分查找，在非递减数组array中查找从左到右第一个大于或等于x的位置，找不到时返回nil，即查找x在数组中的上界
---@param array table 数组，下标从1开始，需满足非递减，允许重复
---@param x 待查找的值
---@return 返回索引
function TableUtils.GreaterLowerBound(array, x)
    local first = 1
    local len = #array
    local half, middle

    while(len > 0) do
        half = len >> 1
        middle = first + half
        if(array[middle] < x) then --在右子序列中查找
            first = middle + 1
            len = len - half - 1
        else --在左子序列中查找
            len = half
        end
    end
    return (array[first] and array[first] >= x) and first or nil
end

---二分查找，在非递减数组array中查找从左到右第一个大于x的位置，找不到时返回nil，即查找x在数组中的上界
---@param array table 数组，下标从1开始，需满足非递减，允许重复
---@param x 待查找的值
---@return 返回索引
function TableUtils.GreaterUpperBound(array, x)
    local first = 1
    local len = #array
    local half, middle

    while(len > 0) do
        half = len >> 1
        middle = first + half
        if(array[middle] > x) then --在左子序列中查找
            len = half
        else --在右子序列中查找
            first = middle + 1
            len = len - half - 1
        end
    end
    return (array[first] and array[first] > x) and first or nil
end

---二分查找，在非递减数组array中查找从左到右最后一个小于x的位置，找不到时返回nil，即查找x在数组中的下界
---@param array table 数组，下标从1开始，需满足非递减, 允许重复
---@param x 待查找的值
---@return 返回索引
function TableUtils.LessLowerBound(array, x)
    local last = #array
    local len = #array
    local half, middle

    while(len > 0) do
        half = len >> 1
        middle = last - half
        if(array[middle] < x) then --在右子序列中查找
            len = half
        else --在左子序列中查找
            last = middle - 1
            len = len - half - 1
        end
    end
    return (array[last] and array[last] < x) and last or nil
end

---二分查找，在非递减数组array中查找从左到右最后一个小于或等于x的位置，找不到时返回nil，即查找x在数组中的下界
---@param array table 数组，下标从1开始，需满足非递减, 允许重复
---@param x 待查找的值
---@return 返回索引
function TableUtils.LessUpperBound(array, x)
    local last = #array
    local len = #array
    local half, middle

    while(len > 0) do
        half = len >> 1
        middle = last - half
        if(array[middle] > x) then --在左子序列中查找
            last = middle - 1
            len = len - half - 1
        else --在右子序列中查找
            len = half
        end
    end
    return (array[last] and array[last] <= x) and last or nil
end

--- 数组切片
---@param array table 数组，下标从1开始，需满足非递减, 允许重复
---@param first 待查找的起始位置
---@param last 待查找的结束位置
---@return table
function TableUtils.Slice(array, first, last)
    if first == nil then
       first = 1
    end
    if last == nil then
       last = #array
    end
    if first > last then
       return {}
    end

    local sub_table = {}
    for i = first, last do
       table.insert(sub_table, array[i])
    end
    return sub_table
end

return TableUtils
