SuperGlobal = {}
local META = {}



function META.__index(t,k)    
	local r = Bubble_SuperGlobal:GetGlob(k)
	local t = Bubble_SuperGlobal:GetType(k)
	local ret
	if t=="boolean" then
		ret = r:lower() == "true"
	elseif t=="number" then
		ret = tonumber(r)
	else
		ret = r
	end
	return ret
end

function META.__newindex(t,k,v)
	Bubble_SuperGlobal:DefGlob(k,v)
end


setmetatable(SuperGlobal,META)
