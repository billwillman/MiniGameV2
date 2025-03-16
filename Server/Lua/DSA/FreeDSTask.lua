local Task = _MOE.class("FreeDSTask")

local moon = require("moon")

require("ServerCommon.GlobalFuncs")
require("DSA.DSState")
local MsgIds = require("_NetMsg.MsgId")
local socket = require "moon.socket"

-- 关闭长时间不用的DS
local function CloseFreeDS(ds)
    if not ds then
        return
    end
    local fd = ds.fd
    if not fd then
        return
    end
    print("[Close Free DS: Start]")
    ds.state = _MOE.DsStatus.DSACloseFreeDS -- DSA关闭空闲的DS
    --_MOE.TableUtils.PrintTable2(ds)
    RemoveDS(ds)
    MsgProcesser:SendTableToJson2(socket, fd, MsgIds.SM_DS_QUIT, {reason = _MOE.DsStatus.DSACloseFreeDS})
    CloseSocket(socket, fd)
    print("[Close Free DS: end]")
end

local function TaskTick()
    while true do
        if _MOE.DSFreeList == nil or _MOE.DSFreeList:GetLength() <= 0 then
            moon.sleep(0)
        else
            local currtime = os.time()
            local currentCnt = _MOE.DSFreeList:GetLength()
            local currentIdx = 1
            for i = 1, 50 do
                local firstDs = _MOE.DSFreeList:GetFirst()
                _MOE.DSFreeList:remove_first()
                if firstDs then
                    if not firstDs:IsBusy() then
                        -- 说明是空闲的
						-- 判断是否是LocalDS
						local isLocalDS = firstDs.dsData and firstDs.dsData.isLocalDS or false
                        if not isLocalDS and currtime - firstDs.freeTime > 60 then -- 大于60秒杀进程
                            -- 杀进程(采用关闭端口)
                            CloseFreeDS(firstDs)
                        else
                            --- 添加到后面去
                            _MOE.DSFreeList:insert_last(firstDs)
                        end
                    else
                        ---- 移到BUSY列表
                       -- print("[FreeDSTask] to DSBusyList: ")
                       -- _MOE.TableUtils.PrintTable2(firstDs)
                       -- print("-----------------------------")
                        firstDs.freeTime = currtime
                        _MOE.DSBusyList:insert_last(firstDs)
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

return Task