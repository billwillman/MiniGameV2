local PlayerInfo = _MOE.class("DBPlayerInfo")

function PlayerInfo:Ctor(dbData)
    self.dbData = dbData
end

return PlayerInfo