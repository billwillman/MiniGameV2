local PlayerMeetingTaskTrigger = UE4.Class()

function PlayerMeetingTaskTrigger:OnCustomBeginOverlap(OtherActor)
    if self.SideId == nil or not OtherActor or not OtherActor:IsA(UE4.AMoeGameCharacter) then
        return
    end
    --- 必须是DS上
    if not _MOE.Utils.WorldUtils:IsServer() then
        return
    end
    -- _MOE.Logger.Log("=======>>>PlayerMeetingTaskTrigger")
    local OtherPlayerInfo = OtherActor:GetBasePlayerInfo()
    if not OtherPlayerInfo then
        return
    end
    if self.SideId ~= OtherPlayerInfo:GetSideId() then
        return
    end
    local ParentComponent = self:K2_GetRootComponent():GetAttachParent()
    if not ParentComponent then
        return
    end
    local MyCharacter = ParentComponent:GetOwner()
    if not MyCharacter or MyCharacter == OtherActor then
        return
    end
    local PlayerUID = MyCharacter:GetUID()
    if not PlayerUID then
        return
    end
    local DataBoard = _MCG.PlayerDataBoardEntityManager:GetPlayerDataBoard(PlayerUID)
    if not DataBoard then
        return
    end
    local count = DataBoard:GetDataValue(_MCG.PlayerDataBoardDefine.DataName.SideMeetingTaskCount) or 0
    count = count + 1
    DataBoard:SetDataFromServer(_MCG.PlayerDataBoardDefine.DataName.SideMeetingTaskCount, count) -- 阵营触碰次数
    _MOE.Logger.Log("[PlayerMeetingTaskTrigger] SideId:", self.SideId, "count:", count)
end

return PlayerMeetingTaskTrigger