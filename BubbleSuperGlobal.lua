--[[
BubbleSuperGlobal.lua
Bubble
version: 19.05.20
Copyright (C)  Jeroen P. Broks
This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.
Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:
1. The origin of this software must not be misrepresented; you must not
claim that you wrote the original software. If you use this software
in a product, an acknowledgment in the product documentation would be
appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
]]


SuperGlobal = {}
local META = {}

local function SERIALIZE()
	return Bubble_Serialize:Serialize()
end

function META.__index(t,k) 
	if k:upper()=="SERIALIZE" then return SERIALIZE end
	local r = Bubble_SuperGlobal:GetGlob(k)
	local t = Bubble_SuperGlobal:GetType(k)
	local ret
	if t=="boolean" then
		ret = r:lower() == "true"
	elseif t=="number" then
		ret = tonumber(r) or 0
	else
		ret = r
	end
	return ret
end

function META.__newindex(t,k,v)
	Bubble_SuperGlobal:DefGlob(k,v)
end


setmetatable(SuperGlobal,META)


