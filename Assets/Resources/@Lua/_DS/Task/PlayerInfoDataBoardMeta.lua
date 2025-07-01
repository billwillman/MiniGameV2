------------------------- 数据面板定义 ------------------------------------
local MCGConfig = require("Feature.CHASE.Script.Config.MCGGameConfig")

local AccessType = {
    Server = 0, -- 只在DS存在
    Both = 1, -- Client 和 DS都可访问
}

-- 法宝枚举
local ChangeMagicEnum = {
    WuKong = 0, -- 悟空 OK
    ShaSeng = 1, -- 沙僧 OK
    ZhuBaJie = 2, -- 猪八戒 OK
    TangSeng = 3, -- 唐僧/安琪儿 OK
    LangRen = 4, -- 狼人 OK
    SuanNiHen = 5, -- 蒜你恨 OK
    ShiZijia = 6, -- 驱魔十字架
}

local DataName = {
    TaskInfos = "TaskInfos", -- 数据面板数据定义
    UseSkillNum = "UseSkillNum", -- 使用主技能次数
    HitSkillNum = "HitSkillNum", -- 使用主技能击中次数
    SideMeetingTaskCount = "SideMeetingTaskCount", -- 阵营Meeting任务统计次数
    GetInRecorderArea = "GetInRecorderArea",--进入BP_PlayerGetInRecorderBase框出的区域
    Boss = {
        ------------------Boss身份通用--------------------
        HitDownCount = "HitDownCount", -- 击倒星宝次数
        HitPlayerCount = "HitPlayerCount", -- 击中星宝次数
        HitPlayerCountPercent = "HitPlayerCountPercent", -- 击中星宝次数在所有击中的百分比（整型）
        DestroyTrapCount = "DestroyTrapCount", -- 摧毁机关次数
        KillCount = "KillCount", -- 击杀次数
        DamageValue = "DamageValue", -- 伤害量
        ExilePlayerSuccessCount = "ExilePlayerSuccessCount", -- 放逐星宝次数
        EnterAngryTime = "EnterAngryTime", --进入觉醒状态距离DS开局的时间(-1：从未进入觉醒状态)
        ------------------Boss身份特性----------------------
        [1014] = {
            LizaFullnessCount = "LizaFullnessCount",--伊丽莎白饱食度上升记录
        },
        [1012] = {
            LamiaSlowDownTangsengOrAngel = "LamiaSlowDownTangsengOrAngel",--拉弥娅魔核是否减速过唐僧/安琪儿
            BossLamiaExecutionCount = "BossLamiaExecutionCount", -- 拉米亚处决
        },
        [1013] = {
            DraculaEndSkillNearPlayer = "DraculaEndSkillNearPlayer",--德古拉在星宝附近变回原样
        }
    },
    Player = {
        RescueTeammateCount = "RescueTeammateCount", -- 解救数目
        TotalAssistantCount = "TotalAssistantCount", -- 辅助次数
        TotalAssistantScore = "TotalAssistantScore", -- 辅助分数
        DecodeStarChartProgress = "DecodeStarChartProgress", -- 星路仪检索进度
        DecodeStarChartProgressPercent = "DecodeStarChartProgressPercent", -- 星路仪搜索进度百分比
        BeingHuntedCount = "BeingHuntedCount", -- 牵制时长
        BeingHuntedCountPercent = "BeingHuntedCountPercent", -- 牵制时长百分比
        DecodeStarChartCount = "DecodeStarChartCount", -- 成功搜索星路仪台数
        SearchStarChartIDArray = "SearchStarChartIDArray", -- 搜索过的星路仪唯一ID数组（只要搜过就算）
        SearchStarChartIDNum = "SearchStarChartIDNum", -- 搜索过的星路仪的数量，来自：SearchStarChartIDArray
        HittedDownCount = "HittedDownCount", -- 被击倒次数
        BeExilecutedCount = "BeExilecutedCount", -- 被放逐次数
        BeDeadCount = "BeDeadCount", -- 死亡次数
        OpenEscapeGateCount = "OpenEscapeGateCount", -- 传送门开启次数
        BeDamageValue = "BeDamageValue", -- 承受伤害量
        CureTeammateValue = "CureTeammateValue", -- 治疗伤害量
        GetMagicEnumArray = "GetMagicEnumArray", -- 获得过法宝获取枚举数组
        BoardStubBossCount = "BoardStubBossCount", -- 机关砸晕BOSS次数
    }
}

------------------------------------------------- 通用定义 -----------------------------------------------------------------------------------------
local CommonDefine = {
    [DataName.TaskInfos] = {
        --[[
            任务数据定义：
            任务ID
            任务类型：透传任务
            参数1：关联任务ID
            参数2：当前数量
        --]]
        Access = AccessType.Both,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            if PlayerInfo then
                local Character = PlayerInfo:GetCharacter()
                if Character then
                    Character.TaskInfos = value
                    -- 单机版本
                    if _MCG.IsSinglePlay and Character.OnRep_TaskInfos then
                        Character:OnRep_TaskInfos()
                    end
                    ----
                end
            end
        end,
        GetFunc = function (PlayerInfo, name, dataBoard)
            if PlayerInfo then
                local Character = PlayerInfo:GetCharacter()
                if Character then
                    return Character.TaskInfos
                end
            end
        end,
    },
    [DataName.UseSkillNum] = { -- 使用主技能次数
        AccessType = AccessType.Server, -- 只允许DS调用
        DSUpdateEvent = "CHASE_UseSkillNum_Update",
    },
    [DataName.HitSkillNum] = { -- 使用主技能击中次数
        AccessType = AccessType.Server, -- 只允许DS调用
        DSUpdateEvent = "CHASE_HitSkillNum_Update",
    },
    [DataName.GetInRecorderArea] = {
        ---数据格式：
        ---{
        ---     RecordKey1 = GetInNum1,
        ---     RecordKey2 = GetInNum2,
        ---}
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_GetInRecorderArea_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or { }
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or { }
            return true
        end,
    },
    [DataName.SideMeetingTaskCount] = {
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_SideMeetingTaskCount_Update",
    },
}

local function GetAllPlayerDataBoards(exIncludePlayerUID, SideId)
    if not _MCG or not _MCG.GameEntityManager then
        return
    end
    local GameInfo = _MCG.GameEntityManager:GetGameInfo()
    if not GameInfo or not GameInfo:IsValid() then
        return
    end
    local PlayerUIDs = GameInfo:GetAllPlayerUIDs()
    if not PlayerUIDs then
        return
    end
    local len = PlayerUIDs:Length()
    if len <= 0 then
        return
    end
    local ret = nil
    for idx = 1, len do
        local PlayerUID = PlayerUIDs:Get(idx)
        if PlayerUID and PlayerUID ~= exIncludePlayerUID then
            if SideId ~= nil then
                local Info = GameInfo:GetPlayerInfoByPlayerUID(PlayerUID)
                if Info == nil or Info:GetSideId() ~= SideId then
                    goto continue
                end
            end
            local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
            if DataBoard then
                ret = ret or {}
                table.insert(ret, DataBoard)
            end
        end
        ::continue::
    end
    return ret
end

local function GetPlayerDataPercent(FieldName, MyPlayerUID, SideId)
    if not FieldName or not MyPlayerUID then
        return 0
    end
    if not _MCG or not _MCG.PlayerDataBoardEntityManager then
        return 0
    end
    local MyDataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(MyPlayerUID)
    if not MyDataBoard then
        return 0
    end
    local MyValue = MyDataBoard:GetDataValue(FieldName)
    if MyValue == nil or type(MyValue) ~= "number" then
        return 0
    end
    local OhterDataBoards = GetAllPlayerDataBoards(MyPlayerUID, SideId)
    if not OhterDataBoards or #OhterDataBoards <= 0 then
        return MyValue > 0 and 100 or 0
    end
    local SumValue = MyValue
    for _, board in ipairs(OhterDataBoards) do
        local count = board:GetDataValue(FieldName) or 0
        SumValue = SumValue + count
    end
    if SumValue <= 0 then
        return 0
    end
    local ret = math.floor((MyValue/SumValue) * 100)
    return ret
end

local function GetPlayerInfoDataPercent(FieldName, PlayerInfo, useSideId)
    if not FieldName or not PlayerInfo then
        return 0
    end
    if useSideId == nil then
        useSideId = true
    end
    local MyPlayerUID = PlayerInfo:GetPlayerUID()
    local SideId = nil
    if useSideId then
        SideId = PlayerInfo:GetSideId()
    end
    return GetPlayerDataPercent(FieldName, MyPlayerUID, SideId)
end

---------------------------------------------- BOSS专用定义 ---------------------------------------------------------------------------------
local BossDefine = {
    -- 击倒星宝次数
    [DataName.Boss.HitDownCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_HitDownCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.HitDownCount or 0
        end
    },
    -- 击中星宝次数
    [DataName.Boss.HitPlayerCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = {"CHASE_HitPlayerCount_Update", "CHASE_HitPlayerCountPercent_Update"},
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.HitPlayerCount or 0
        end
    },
    -- 击中星宝次数百分比
    [DataName.Boss.HitPlayerCountPercent] = {
        ReadOnly = true, -- 只读属性
        AccessType = AccessType.Server,
        DSUpdateEvent = "CHASE_HitPlayerCountPercent_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            local ret = GetPlayerInfoDataPercent(DataName.Boss.HitPlayerCount, PlayerInfo) or 0
            return ret
        end
    },
    -- 摧毁机关次数
    [DataName.Boss.DestroyTrapCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_DestroyTrapCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.DestroyTrapCount or 0
        end
    },
    -- 击杀次数
    [DataName.Boss.KillCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_KillCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            local ExilePlayerSuccessCount = PlayerInfo.ExilePlayerSuccessCount or 0
            local BossLamiaExecutionCount = PlayerInfo.BossLamiaExecutionCount or 0
            return ExilePlayerSuccessCount + BossLamiaExecutionCount
        end
    },
    -- 拉米亚处决
    [DataName.Boss[1012].BossLamiaExecutionCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = {"CHASE_BossLamiaExecutionCount_Update", "CHASE_KillCount_Update"},
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.BossLamiaExecutionCount or 0
        end
    },
    -- 伤害量
    [DataName.Boss.DamageValue] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_DamageValue_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.DamageValue or 0
        end
    },
    -- 放逐星宝次数
    [DataName.Boss.ExilePlayerSuccessCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = {"CHASE_ExilePlayerSuccessCount_Update", "CHASE_KillCount_Update"},
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.ExilePlayerSuccessCount or 0
        end
    },
    -- 伊丽莎白饱食度
    [DataName.Boss[1014].LizaFullnessCount] = {
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_Liza_FullnessCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or {
                MidFullnessCount = 0,
                HighFullnessCount = 0,
                LowFullnessCount = 0
            }
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            local MidFullnessCount = value.MidFullnessCount or 0
            local HighFullnessCount = value.HighFullnessCount or 0
            local LowFullnessCount = value.LowFullnessCount or 0
            local NewFullnessCountList = {
                MidFullnessCount = MidFullnessCount,
                HighFullnessCount = HighFullnessCount,
                LowFullnessCount = LowFullnessCount
            }
            dataBoard[name] = NewFullnessCountList
            return true
        end,
    },
    -- Boss进入觉醒状态距离开局的时间(-1：从未进入觉醒状态)
    [DataName.Boss.EnterAngryTime] = {
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_EnterAngryTime_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or -1
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or -1
            return true
        end,
    },
    -- 拉弥娅魔核是否减速过唐僧
    [DataName.Boss[1012].LamiaSlowDownTangsengOrAngel] = {
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_Lamia_SlowDownTangsengOrAngel_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or false
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or false
            return true
        end,
    },
    --德古拉在星宝附近变回原样
    [DataName.Boss[1013].DraculaEndSkillNearPlayer] = {
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_Dracula_EndSkillNearPlayer_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or 0
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or dataBoard[name]
            return true
        end,
    },
}
setmetatable(BossDefine, {__index = CommonDefine})
-------------------------------------------------- 星宝专用定义 --------------------------------------------------------------------------------
local PlayerDefine = {
    [DataName.Player.RescueTeammateCount] = { -- 解救数目
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_RescueTeammateCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo:GetTotalRescueTeammateCount() or 0
        end
    },
    [DataName.Player.TotalAssistantCount] = { -- 辅助次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_TotalAssistantCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo:GetTotalAssistantCount() or 0
        end
    },
    [DataName.Player.TotalAssistantScore] = { -- 辅助分数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_TotalAssistantScore_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo:GetTotalAssistantScore() or 0
        end
    },
    [DataName.Player.DecodeStarChartProgress] = { -- 星路仪检索进度
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = {"CHASE_DecodeStarChartProgress_Update", "CHASE_DecodeStarChartProgressPercent_Update"},
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.DecodeStarChartProgress or 0
        end
    },
    [DataName.Player.DecodeStarChartProgressPercent] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_DecodeStarChartProgressPercent_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            local ret = GetPlayerInfoDataPercent(DataName.Boss.DecodeStarChartProgress, PlayerInfo) or 0
            return ret
        end
    },
    [DataName.Player.BeingHuntedCount] = { -- 牵制BOSS次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = {"CHASE_BeingHuntedCount_Update", "CHASE_BeingHuntedCountPercent_Update"},
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.BeingHuntedCount or 0
        end
    },
    [DataName.Player.BeingHuntedCountPercent] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Server,
        DSUpdateEvent = "CHASE_BeingHuntedCountPercent_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            local ret = GetPlayerInfoDataPercent(DataName.Player.BeingHuntedCount, PlayerInfo) or 0
            return ret
        end
    },
    [DataName.Player.DecodeStarChartCount] = { -- 成功搜索星路仪台数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_DecodeStarChartCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.DecodeStarChartCount or 0
        end
    },
    [DataName.Player.HittedDownCount] = { -- 被击倒次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_HittedDownCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.HittedDownCount or 0
        end
    },
    [DataName.Player.BeExilecutedCount] = { -- 被放逐次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_BeExilecutedCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.BeExilecutedCount or 0
        end
    },
    [DataName.Player.BeDeadCount] = { -- 死亡次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_BeDeadCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.BeExilecutedCount or 0
        end
    },
    [DataName.Player.OpenEscapeGateCount] = { -- 传送门开启次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_OpenEscapeGateCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.OpenEscapeGateCount or 0
        end
    },
    [DataName.Player.BeDamageValue] = { -- 承受伤害量
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_BeDamageValue_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.BeDamageValue or 0
        end
    },
    [DataName.Player.CureTeammateValue] = { -- 治疗伤害量
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_CureTeammateValue_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.CureTeammateValue or 0
        end
    },
    [DataName.Player.SearchStarChartIDArray] = {
        AccessType = AccessType.Server, -- 只允许DS访问
        DSUpdateEvent = {"CHASE_SearchStarChartIDArray_Update", "CHASE_SearchStarChartIDNum_Update"}, -- 需要同时通知更新了IDNum
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or {}
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or {}
        end,
    },
    [DataName.Player.SearchStarChartIDNum] = {
        AccessType = AccessType.Server, -- 只允许DS访问
        DSUpdateEvent = "CHASE_SearchStarChartIDNum_Update",
        ReadOnly = true,
        GetFunc = function (PlayerInfo, name, dataBoard)
            local arr = dataBoard[DataName.Player.SearchStarChartIDArray] or {}
            local ret = #arr
            return ret
        end
    },
    [DataName.Player.GetMagicEnumArray] = { -- 获取法宝变身数组
        AccessType = AccessType.Server, -- 只允许DS访问
        DSUpdateEvent = "CHASE_GetMagicEnumArray_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or {}
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or {}
        end
    },
    [DataName.Player.BoardStubBossCount] = { -- 机关砸晕BOSS次数
        AccessType = AccessType.Server, -- 只允许DS访问
        DSUpdateEvent = "CHASE_BoardStubBossCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or 0
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or 0
        end
    }
}
setmetatable(PlayerDefine, {__index = CommonDefine})

local _M = {

    ------ BOSS数据面板
    [MCGConfig.ConstValue.BossSideId] = BossDefine,
    [MCGConfig.ConstValue.PlayerSideId] = PlayerDefine,
}

_M.DataName = DataName
_M.AccessType = AccessType
_M.ChangeMagicEnum = ChangeMagicEnum

return _M