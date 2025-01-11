local moon = require("moon")
local _MOE = _MOE or {}
moon.exports._MOE = _MOE
_MOE.class = require("_Common.BaseClass")
_MOE.TableUtils = require("_Common.TableUtils")

_MOE.ErrorHandler = function (err)
    print(err)
	print(debug.traceback())
end