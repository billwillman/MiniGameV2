local Task = _MOE.class("BusyDSTask")

local moon = require("moon")

require("ServerCommon.GlobalFuncs")
require("DSA.DSState")


local function TaskTick()
    while true do
        if _MOE.DSBusyList == nil or _MOE.DSBusyList:GetLength() <= 0 then
            moon.sleep(0)
        else
            local currtime = os.time()
            local currentCnt = _MOE.DSBusyList:GetLength()
            local currentIdx = 1
            for i = 1, 50 do
                local firstDs = _MOE.DSBusyList:GetFirst()
                _MOE.DSBusyList:remove_first()
                if firstDs then
                    if firstDs:IsBusy() then
                        _MOE.DSBusyList:insert_last(firstDs)
                    else
                        ---- 移到Free列表
                        firstDs.freeTime = currtime
                        _MOE.DSFreeList:insert_last(firstDs)
                    end
                    currentIdx = currentIdx + 1
                    if currentIdx >= currentCnt then
                        break
                    end
                else
                    break
                end
            end
            moon.sleep(0)
        end
    end
end



function Task:Start()
    moon.async(TaskTick, self.__cname)
end