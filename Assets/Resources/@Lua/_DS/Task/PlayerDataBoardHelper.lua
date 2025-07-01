----------------- 数据面板帮助函数 ---------------------------
local helper = {}
local MCGGameConfig = require("Feature.CHASE.Script.Config.MCGGameConfig")

--- 记录搜寻过星路仪的ID
function helper:RecordSearchStarChartID(Character, StarChart)
    if not Character or not StarChart then
        return false
    end
    local StarChartID = StarChart.StarChartID
    if not StarChartID or StarChartID == 0 then -- StarChartID 为 0 说明是没有标记编号的星路仪
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    if not SideId or SideId ~= _MCG.MCGGameConfig.ConstValue.PlayerSideId then
        return false
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local StarChartIDArray = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Player.SearchStarChartIDArray)
    if not StarChartIDArray then
        return false
    end
    local isFound = false
    for _, id in ipairs(StarChartIDArray) do
        if id == StarChartID then
            isFound = true
            break
        end
    end
    if isFound then
        return true
    end
    table.insert(StarChartIDArray, StarChartID)
    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Player.SearchStarChartIDArray, StarChartIDArray)
end

--- 星宝获取某个法宝
function helper:RecordPlayerGetMagicEnum(Character, magicEnum)
    if not Character or not magicEnum then
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    if not SideId or SideId ~= _MCG.MCGGameConfig.ConstValue.PlayerSideId then
        return false
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local arr = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Player.GetMagicEnumArray)
    if arr == nil then
        return false
    end
    local isFound = false
    for _, e in ipairs(arr) do
        if e == magicEnum then
            isFound = true
            break
        end
    end
    if isFound then
        return true
    end
    table.insert(arr, magicEnum)
    DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Player.GetMagicEnumArray, arr)
    return true
end

--- 星宝砸晕BOSS统计
function helper:IncPlayerBoardStubBossCount(Character)
    if not Character then
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    if not SideId or SideId ~= _MCG.MCGGameConfig.ConstValue.PlayerSideId then
        return false
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Player.BoardStubBossCount)
    if count == nil then
        return false
    end
    count = count + 1
    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Player.BoardStubBossCount, count)
end

--- 星宝统计使用技能次数
function helper:IncPlayerUseSkillNum(Character)
    if not Character then
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    if not SideId or SideId ~= _MCG.MCGGameConfig.ConstValue.PlayerSideId then
        return false
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.UseSkillNum) or 0
    count = count + 1
    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.UseSkillNum, count)
end

--检查是否是特定的Boss类型
local function CheckSpecificBossType(Character,BossType)
    if not Character then
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    if not SideId or SideId ~= _MCG.MCGGameConfig.ConstValue.BossSideId then
        return false
    end
    local BossComponent = Character:GetComponentByClass(UE4.UMCGBossComponent.StaticClass())
    if not UE4.UKismetSystemLibrary.IsValid(BossComponent) then
        return false
    end
    if BossComponent.BossType ~= BossType then
        return false
    end

    return PlayerInfo
end

--- 更新伊丽莎白精神值进入次数
function helper:IncLizaFullnessCount(Character,NewFullnessLevel)
    local PlayerInfo = CheckSpecificBossType(Character,1014)
    if not PlayerInfo then
        return false
    end
    local PlayerUID = Character:GetUID()

    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Boss[1014].LizaFullnessCount)
    if count == nil or not next(count) then
        return false
    end
    if NewFullnessLevel == 1 then
        count.LowFullnessCount = count.LowFullnessCount + 1
    elseif NewFullnessLevel == 2 then
        count.MidFullnessCount = count.MidFullnessCount + 1
    elseif NewFullnessLevel == 3 then
        count.HighFullnessCount = count.HighFullnessCount + 1
    end
    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Boss[1014].LizaFullnessCount, count)
end

--- 更新Boss进入觉醒状态的时间
function helper:RecordBossEnterAngryTime(Character)
    if not Character then
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local SideId = PlayerInfo:GetSideId()
    if not SideId or SideId ~= _MCG.MCGGameConfig.ConstValue.BossSideId then
        return false
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end

    if not _MCG.InGameGlobalVars.PlayerLoopStartTime then
        return false
    end
    local EnterAngryTime = os.time() - _MCG.InGameGlobalVars.PlayerLoopStartTime
    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Boss.EnterAngryTime, EnterAngryTime)
end

--- 更新拉弥娅减速唐僧/安琪儿
---@param Character 拉弥娅
---@param OtherCharacter 被减速的角色
function helper:UpdateLamiaSlowDownTangsengOrAngel(Character,OtherCharacter)
    local PlayerInfo = CheckSpecificBossType(Character,1012)
    if not PlayerInfo then
        return false
    end
    local PlayerUID = Character:GetUID()

    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local HasSlowDown = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Boss[1012].LamiaSlowDownTangsengOrAngel)
    --已经减速过了，后面的逻辑都不用判定了
    if HasSlowDown then
        return true
    end
    --判断一下减速的对象是不是唐僧/安琪儿
    if not OtherCharacter then
        return false
    end

    local CharPropComponent = OtherCharacter:GetCharPropComponent()
    local CurrentProp = CharPropComponent and CharPropComponent:GetCurrentProp()

    if CurrentProp then
        if CurrentProp.SkillID == MCGGameConfig.SkillConfig.Prop_Tangseng_Active.SkillID then
            return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Boss[1012].LamiaSlowDownTangsengOrAngel,true)
        end
    end

    if CurrentProp then
        if CurrentProp.SkillID == MCGGameConfig.SkillConfig.Prop_Girl_Active.SkillID then
            return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Boss[1012].LamiaSlowDownTangsengOrAngel,true)
        end
    end

    return false
end

--- 更新德古拉在星宝附近变回原形次数
function helper:IncDraculaEndSkillNearPlayerCount(Character)
    local PlayerInfo = CheckSpecificBossType(Character,1013)
    if not PlayerInfo then
        return false
    end
    local PlayerUID = Character:GetUID()

    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Boss[1013].DraculaEndSkillNearPlayer)
    if count == nil then
        return false
    end

    --检查周围有没有星宝
    local Radius = 500
    local ObjectTypes = UE4.TArray(UE4.ECollisionChannel)
    ObjectTypes:Add(UE4.ECollisionChannel.ECC_Pawn)

    local OwnerLocation = Character:K2_GetActorLocation()
    local DebugTrace = UE4.EDrawDebugTrace.None

    local HitResults = UE4.TArray(UE4.FHitResult)
    UE4.UKismetSystemLibrary.SphereTraceMultiForObjects(
            Character,
            OwnerLocation,
            OwnerLocation,
            Radius,
            ObjectTypes,
            false,
            nil,
            DebugTrace,
            HitResults,
            true,
            UE4.FLinearColor(1,1,1,1),
            UE4.FLinearColor(1,0,0,1),
            3
    )

    local HasPlayerInside = false
    local len = HitResults:Length()
    if len > 0 then
        for i = 1, len do
            local HitData = HitResults:Get(i)
            if HitData and HitData.Actor then
                local HitChar = HitData.Actor:Cast(UE4.AMoeGameCharacter)
                if HitChar then
                    if not HitChar:IsBoss() then
                        HasPlayerInside = true
                        break
                    end
                end
            end
        end
    end
    if HasPlayerInside then
        count = count + 1
        return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Boss[1013].DraculaEndSkillNearPlayer, count)
    end
    return false
end


--- 更新玩家进入记录区域的次数
function helper:IncPlayerEnterRecorderAreaCount(Character,RecordKeys)
    if not Character or not RecordKeys then
        return false
    end
    local PlayerUID = Character:GetUID()
    if not PlayerUID then
        return false
    end
    local PlayerInfo = Character:GetBasePlayerInfo()
    if not PlayerInfo then
        return false
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return false
    end
    local Record = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.GetInRecorderArea)

    --RecordKeys:{Key1,Key2,Kye3...}
    for _,AreaKey in ipairs(RecordKeys) do
        Record[AreaKey] = Record[AreaKey] and Record[AreaKey]+1 or 1
    end

    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.GetInRecorderArea,Record)
end

return helper