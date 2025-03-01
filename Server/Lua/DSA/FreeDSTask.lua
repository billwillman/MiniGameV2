local Task = _MOE.class("FreeDSTask")

local moon = require("moon")

local function TaskTick()
    while true do
        if _MOE.DSFreeList == nil then
            moon.sleep(0)
        else
            local currtime = os.time()
            for i = 1, 50 do
                local firstDs = _MOE.DSFreeList:GetFirst()
                _MOE.DSFreeList:remove_first()
                if firstDs then
                    if not firstDs.players or #firstDs.players <= 0 then
                        -- 说明是空闲的
                        if currtime - firstDs.freeTime > 60 then -- 大于60秒杀进程
                            -- 杀进程
                            --- 暂时还是放最后
                            _MOE.DSFreeList:insert_last(firstDs)
                            ---
                        else
                            --- 添加到后面去
                            _MOE.DSFreeList:insert_last(firstDs)
                        end
                    else
                        ---- 移到BUSY列表
                        firstDs.freeTime = currtime
                        _MOE.DSBusyList:insert_last(firstDs)
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

return Task