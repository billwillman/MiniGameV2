local _M = UE4.Class()

local Meta = require("Feature.CHASE.Script.Modplay.Core.GameMode.Games.MCG.Task.PlayerInfoDataBoardMeta")

function _M:_GetMetaInfo(name)
    local SideId = self:GetSideId()
    local metaInfo = Meta[SideId]
    if not metaInfo then
        return
    end
    local ret = metaInfo[name]
    if ret then
        --- 访问权限控制
        local access = ret.Access and ret.Access or 0
        if access == Meta.AccessType.Both then
            return ret
        end
        if access == Meta.AccessType.Server and _MOE.Utils.WorldUtils:IsServer() then
            return ret
        end
        return nil
    end
    return ret
end


function _M:SetInternalValue(name, value)
    self.Data = self.Data or {}
    self.Data[name] = value
end

function _M:GetInternalValue(name)
    if self.Data then
        local ret = self.Data[name]
        return ret
    end
end

function _M:ReceiveBeginPlay()
    _MCG.PlayerDataBoardEntityManager:AddEntity(self)
end

function _M:ReceiveEndPlay()
    _MCG.PlayerDataBoardEntityManager:RemoveEntity(self)
    self.Data = nil
    self._Owner = nil
end

-------------------------------------------- 外部可调用 ------------------------------------------
function _M:GetDataValue(name)
    local meta = self:_GetMetaInfo(name)
    if not meta then
        return
    end
    if meta.GetFunc then
        self.Data = self.Data or {}
        return meta.GetFunc(self:GetOwnerLua(), name, self.Data)
    end
    return self:GetInternalValue(name)
end

function _M:SetDataFromServer(name, value)
    local meta = self:_GetMetaInfo(name)
    if not meta then
        return false
    end
    --- 只允许Server设置数据
    if not _MOE.Utils.WorldUtils:IsServer() then
        return false
    end
    if meta.ReadOnly then -- 只读属性
        return false
    end
    if meta.SetFunc then
        self.Data = self.Data or {}
        meta.SetFunc(self:GetOwnerLua(), name, value, self.Data)
    else
        self:SetInternalValue(name, value)
    end
    self:UpdateEventFromServer(name)
    return true
end

--- 阵营关系
function _M:GetSideId()
    if self.SideId == nil then
        local PlayerInfo = self:GetOwnerLua()
        if PlayerInfo then
            self.SideId = PlayerInfo:GetSideId()
        end
    end
    return self.SideId
end

function _M:GetPlayerUID()
    if self.PlayerUID == nil then
        local PlayerInfo = self:GetOwnerLua()
        if PlayerInfo then
            self.PlayerUID = PlayerInfo:GetPlayerUID()
            if self.PlayerUID == 0 then -- PlayerUID不会为0
                self.PlayerUID = nil
            end
        end
    end
    return self.PlayerUID
end

--- 是真人玩家
function _M:IsRealPlayer()
    if self._IsRealPlayer == nil then
        local PlayerInfo = self:GetOwnerLua()
        if PlayerInfo then
            self._IsRealPlayer = PlayerInfo:IsRealPlayer()
        end
    end
    return self._IsRealPlayer
end

function _M:UpdateEventFromServer(Key)
    ---- 数据有更新
    local meta = self:_GetMetaInfo(Key)
    if meta == nil then
        return false
    end
    if meta.DSUpdateEvent then
        if type(meta.DSUpdateEvent) == "table" then
            local sourcePlayerInfo = self:GetOwnerLua()
            for _, evetName in pairs(meta.DSUpdateEvent) do
                _MOE.EventManager:DispatchEvent(evetName, sourcePlayerInfo)
            end
        elseif string.len(meta.DSUpdateEvent) > 0 then
            _MOE.EventManager:DispatchEvent(meta.DSUpdateEvent, self:GetOwnerLua())
        end
        return true
    end
    return false
end

function _M:GetOwnerLua()
    if self._Owner == nil then
        self._Owner = self:GetOwner()
    end
    return self._Owner
end

return _M