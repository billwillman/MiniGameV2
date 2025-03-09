local moon = require("moon")

local Task = _MOE.class("FreeSessionTask")

local function TaskTick()
    while true do
        moon.sleep(0)
    end
end

function Task:Start()
    moon.async(TaskTick, self.__cname)
end

return Task