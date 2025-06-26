------------------------- 数据面板定义 ------------------------------------
local MCGConfig = require("Feature.CHASE.Script.Config.MCGGameConfig")

local AccessType = {
    Server = 0, -- 只在DS存在
    Both = 1, -- Client 和 DS都可访问
}

local DataName = {
    TaskInfos = "TaskInfos", -- 数据面板数据定义
    UseMainSkill = "UseMainSkillNum", -- 使用主技能次数
    Boss = {
        HitDownPlayerCount = "HitDownPlayerCount", -- 击倒星宝次数
        HitPlayerCount = "HitPlayerCount", -- 击中星宝次数
        DestroyTrapCount = "DestroyTrapCount", -- 摧毁机关次数
        KillCount = "KillCount", -- 击杀次数
        DamageValue = "DamageValue", -- 伤害量
        ExilePlayerSuccessCount = "ExilePlayerSuccessCount", -- 放逐星宝次数
        
    },
    Player = {
        RescueTeammateCount = "RescueTeammateCount", -- 解救数目
        TotalAssistantCount = "TotalAssistantCount", -- 辅助次数
        TotalAssistantScore = "TotalAssistantScore", -- 辅助分数
        DecodeStarChartProgress = "DecodeStarChartProgress", -- 星路仪检索进度
        BeingHuntedCount = "BeingHuntedCount", -- 牵制时长
        DecodeStarChartCount = "DecodeStarChartCount", -- 成功搜索星路仪台数
        SearchStarChartIDArray = "SearchStarChartIDArray", -- 搜索过的星路仪唯一ID数组（只要搜过就算）
        SearchStarChartIDNum = "SearchStarChartIDNum", -- 搜索过的星路仪的数量，来自：SearchStarChartIDArray
        HittedDownCount = "HittedDownCount", -- 被击倒次数
        BeExilecutedCount = "BeExilecutedCount", -- 被放逐次数
        BeDeadCount = "BeDeadCount", -- 死亡次数
        OpenEscapeGateCount = "OpenEscapeGateCount", -- 传送门开启次数
        BeDamageValue = "BeDamageValue", -- 承受伤害量
        CureTeammateValue = "CureTeammateValue", -- 治疗伤害量
        ChangeMagicCount = "ChangeMagicCount", -- 法宝变身次数
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
    }
}

---------------------------------------------- BOSS专用定义 ---------------------------------------------------------------------------------
local BossDefine = {
    -- 击倒星宝次数
    [DataName.Boss.HitDownPlayerCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_HitDownPlayerCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.HitDownCount or 0
        end
    },
    -- 击中星宝次数
    [DataName.Boss.HitPlayerCount] = {
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_HitPlayerCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.HitPlayerCount or 0
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
            return (PlayerInfo.ExilePlayerSuccessCount + PlayerInfo.BossLamiaExecutionCount) or 0
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
        DSUpdateEvent = "CHASE_ExilePlayerSuccessCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.ExilePlayerSuccessCount or 0
        end
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
            return PlayerInfo.GetTotalRescueTeammateCount() or 0
        end
    },
    [DataName.Player.TotalAssistantCount] = { -- 辅助次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_TotalAssistantCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.GetTotalAssistantCount() or 0
        end
    },
    [DataName.Player.TotalAssistantScore] = { -- 辅助分数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_TotalAssistantScore_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.GetTotalAssistantScore() or 0
        end
    },
    [DataName.Player.DecodeStarChartProgress] = { -- 星路仪检索进度
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_DecodeStarChartProgress_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.DecodeStarChartProgress or 0
        end
    },
    [DataName.Player.BeingHuntedCount] = { -- 牵制BOSS次数
        ReadOnly = true, -- 只读属性
        Access = AccessType.Both,
        DSUpdateEvent = "CHASE_BeingHuntedCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            return PlayerInfo.BeingHuntedCount or 0
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
    [DataName.Player.ChangeMagicCount] = { -- 法宝变身次数
        AccessType = AccessType.Server, -- 只允许DS访问
        DSUpdateEvent = "CHASE_ChangeMagicCount_Update",
        GetFunc = function (PlayerInfo, name, dataBoard)
            dataBoard[name] = dataBoard[name] or 0
            local ret = dataBoard[name]
            return ret
        end,
        SetFunc = function (PlayerInfo, name, value, dataBoard)
            dataBoard[name] = value or 0
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

return _M