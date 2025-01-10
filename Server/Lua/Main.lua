-- 启动入口脚本

---__init__---
if _G["__init__"] then
    local arg = ... --- command line args
    return {
        thread = 16,
        enable_stdout = true,
        logfile = string.format("log/moon-%s-%s.log", arg[1], os.date("%Y-%m-%d-%H-%M-%S")),
        loglevel = 'DEBUG',
    }
end

-- Define lua module search dir, all services use same lua search path
-- 注意工作目录时当前脚本所在路径
local path = table.concat({
    "./?.lua",
    "./?/init.lua",
    "../lualib/?.lua",   -- moon lualib 搜索路径
    "../service/?.lua",  -- moon 自带的服务搜索路径，需要用到redisd服务
    -- Append your lua module search path
}, ";")

package.path = path .. ";"

local moon = require("moon")

require("LuaPanda").start("127.0.0.1", 9999)

local arg = moon.args()

--保存为env所有服务共享PATH配置
moon.env("PATH", string.format("package.path='%s'", package.path))

moon.env("NODE", arg[1])
-------------------------------以上代码是固定写法--------------------------------------
--[[
-- 启动HttpPayServer
moon.async(function ()
    local id = moon.new_service(
        {
            name = "PayHttpServer",
            file = "PayHttpServer.lua",
            unique = true
        }
    )
    assert(id > 0, "Create PayHttpServer Fail")
end)
]]

require("ServerCommon.GlobalServerConfig")
require("ServerCommon.GlobalFuncs")
require("ServerCommon.ServerMsgIds")
local TableUtils = require("_Common.TableUtils")

local server_ok = false

local Services = {
    -- 登录服务器
    {
        name = "LoginSrv",
        file = "./LoginServer/LoginServer.lua",
        unique = true
    },
    -- 区服服务器
    {
        name = "ServerListSrv",
        file = "ServerListServer.lua",
        unique = true,
        NoInitCallFunc = true
    },
    -- 战斗服务器
    {
        name = "BattleSrv",
        file = "./DsBattleServer/DsBattleServer.lua",
        unique = true
    },
    -- DSA服务器：用来拉取和分配DS的服务器
    {
        name = "DSA",
        file = "./DSAServer/DSAServer.lua",
        unique = true
    },
    -- GM服务器
    {
        name = "GM",
        file = "./GMServer/GMServer.lua",
        unique = true
    },
    --[[
    -- Cluster集群支持
    {
        name = "Cluster",
        file = "../service/cluster.lua",
        unique = true,
        url = clusterUrl
    }
    ]]
}

local RuntimeServerIds = {}

--[[
moon.async(function ()
    local id = moon.new_service(
        {
            name = "HttpFileStatic",
            file = "HttpFileStatic.lua",
            unique = true
        }
    )
    assert(id > 0, "Create HttpFileStatic Fail")
end)
]]

local function CallServerIds_Func(funcName, ...)
    if not RuntimeServerIds or not next(RuntimeServerIds) or not funcName then
        return
    end
    for _, id in ipairs(RuntimeServerIds) do
        assert(moon.call("lua", id, funcName, ...))
    end
end

local function Start()
    RuntimeServerIds = {}
    for _, service in ipairs(Services) do
        local id = moon.new_service(service)
        assert(id > 0, string.format("Create %s Fail", service.name))
        if not service.NoInitCallFunc then
            table.insert(RuntimeServerIds, id)
        end
        print(string.format("[%d] %s new service", id, service.name))
    end
    -- print (TableUtils.Serialize(RuntimeServerIds))

    -- 初始化DB
    CallServerIds_Func(_MOE.ServicesCall.InitDB)
    -- 开始监听
    CallServerIds_Func(_MOE.ServicesCall.Listen)

    CallServerIds_Func(_MOE.ServicesCall.Start)

    -- moon.call("lua", GetLoginSrvId(), _MOE.ServerMsgIds.SM_LS_Exist_PLAYERINFO, {token = "111"})
end

moon.async(
    function ()
        local ok, err = xpcall(Start, debug.traceback)
        if not ok then
            moon.error("server will abort, init error\n", err)
            moon.exit(-1)
            return
        end
        server_ok = true
    end
)


moon.shutdown(function ()
    print("receive shutdown")
    moon.async(function ()
        if server_ok then
            -- 无DB的服务关闭
            CallServerIds_Func( _MOE.ServicesCall.Shutdown)
            -- 等待
            --[[
            local i = 5
            while i > 0 do
                moon.sleep(1000)
                print(i .. "......")
                i = i - 1
            end
            ]]
            -- 有DB的服务关闭
            CallServerIds_Func(_MOE.ServicesCall.SaveAndQuit)
        else
            moon.exit(-1)
        end
        RuntimeServerIds = {}
        ---wait all service quit
        while true do
            local size = moon.server_stats("service.count")
            if size == 1 then
                break
            end
            moon.sleep(200)
            print("bootstrap wait all service quit, now count:", size)
        end
        moon.quit()
    end)
end)