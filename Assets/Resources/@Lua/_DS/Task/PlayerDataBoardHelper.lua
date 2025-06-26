----------------- 数据面板帮助函数 ---------------------------
local helper = {}

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

--- 星宝法宝变身次数
function helper:IncPlayerChangeMagicCount(Character)
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
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.Player.ChangeMagicCount)
    if count == nil then
        return false
    end
    count = count + 1
    return DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.Player.ChangeMagicCount, count)
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

return helper