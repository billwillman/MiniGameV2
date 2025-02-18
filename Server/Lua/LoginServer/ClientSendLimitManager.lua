-------------- 发包限制管理(通过redis记录，这里不缓存全靠redis来判断，这里只是用来封装访问接口) ---------------------
local ClientSendLimitManager = _MOE.class("ClientSendLimitManager")

return ClientSendLimitManager