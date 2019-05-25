// Lic:
// BubbleJCRClass.cs
// (c)  Jeroen Petrus Broks.
// 
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL was not
// distributed with this file, You can obtain one at
// http://mozilla.org/MPL/2.0/.
// Version: 19.05.09
// EndLic



/*
 * This class serves as the API to allow 
 * the Lua scripts to read JCR files.
 * 
 * This class is "mandadory", and all Bubble projects will therefore include it!
 */

using System;
using System.Text;
using System.Collections.Generic;
using NLua;
using UseJCR6;

namespace Bubble {

    class JCR_Bubble {

        #region My own shit! Should NOT be used by Lua
        private BubbleState Parent;
        private Lua bstate => Parent.state;
        private Dictionary<int, TJCRDIR> JCRDict = new Dictionary<int, TJCRDIR>();        
        



        public JCR_Bubble(BubbleState fromparent) {
            Parent = fromparent;            
            bstate.DoString(@"
                function JCR_EntryList(resource,asmap)
                    local ret
                    if resource==nil or type(resource)=='boolean' then
                       ret = Bubble_JCR:EntryList(-1,resource==true)
                    else
                       assert(JCR_HasResource(resource),'No JCR resource on id #'..resource)
                       ret = Bubble_JCR:EntryList(resource,asmap)
                    end
                    local lk,retf = xpcall(function() return load(ret,'EntryGenerator') end,BubbleCrash); if not lk then return nil end
                    local ok,rettab = xpcall(retf,BubbleCrash)
                    if ok then return rettab else return nil end
                end

                function JCR_Dir(file) return Bubble_JCR:Dir(file) end
                function JCR_Free(id) Bubble_JCR:Free(id) end
                function JCR_Has(id) return Bubble_JCR:Has(id) end
                function JCR_GetError() return Bubble_JCR.GetError end
                
                function JCR_GetString(r,e)
                       if type(r)=='string' and e==nill then
                          return Bubble_JCR:GetString(-1,r)
                       elseif type(r)=='number' and type(e)=='string' then
                          return Bubble_JCR:GetString(r,e)
                       else
                          error('Invalid parameters for GetString')
                       end
                end

                function JCR_EntryExists(i,n)
                    if (n) then 
                       return Bubble_JCR:EntryExists(n,i)
                    else
                       return Bubble_JCR:EntryExists(i,-1)
                    end
                end

                function JCR_InitNIL()
                   NIL.Load([[
                          class UseBubbleNIL
                               get string NAME
                                   return 'Bubble'
                               end
                               bool Exists(string file)
                                 // print('check: '..file)
                                 return Bubble_JCR:EntryExists(file,-1)
                               end
                               string Load(string file)
                                 return JCR_GetString(file)
                               end
                               //void CONSTRUCTOR()
                                  //print('Init NIL')
                               //end
                          end
                          NIL.UseStuff=UseBubbleNIL.NEW()
                   ]],'UseBubbleNIL') ()              
                end


            ", "JCR Init chunk");
        }

        #endregion

        #region The actual API
        public string GetError { get; private set; } = "";
        public int Dir(string file) {
            GetError = "";
            var J = JCR6.Dir(file);
            if (J == null) {
                GetError = JCR6.JERROR;
                return -1;
            }
            var i = 0;
            while (JCRDict.ContainsKey(i)) i++;
            JCRDict[i] = J;
            return i;
        }

        public bool Has(int ret) => JCRDict.ContainsKey(ret);

        public void Free(int res) {
            GetError = "";
            if (!JCRDict.ContainsKey(res)) { GetError = $"JCRDir #{res} doesn't exist, so it can't be freed!"; }
            JCRDict.Remove(res);
        }

        public string GetString(int res, string entry) {
            try {
                var JCR = Parent.JCR;
                if (res >= 0) {
                    if (!JCRDict.ContainsKey(res)) { GetError = "JCRDir #{res} doesn't exist so cannot be loaded!"; return ""; }
                    JCR = JCRDict[res];
                }
                return JCR.LoadString(entry);
            } catch (Exception e){
                SBubble.MyError($"JCR_GetString({res},\"{entry}\")", "Fetching the string failed!", $".NET reported: {e.Message}\nJCR6 reported: {JCR6.JERROR}\n\n");
                return "ERROR!";
            }
        }

        public string EntryList(int res=-1, bool asmap=false) {
            GetError = "";
            var sb = new StringBuilder("return {");
            var comma = false;
            var JCR = Parent.JCR;
            if (res >= 0) {
                if (!JCRDict.ContainsKey(res)) { GetError = $"JCRDir #{res} doesn't exist so cannot be listed out!"; return ""; }
                JCR = JCRDict[res];
            }
            foreach (TJCREntry e in JCR.Entries.Values) {
                if (comma) sb.Append(", "); comma = true;
                if (asmap) sb.Append($"[\"{e.Entry.ToUpper()}\"] = ");
                sb.Append("{ Entry = \"" + e.Entry + "\",");
                if (e.Author != "") sb.Append($"Author = \"{e.Author}\", ");
                if (e.Notes != "") sb.Append($"Notes =\"{e.Notes}\", ");
                sb.Append($"CompressedSize = {e.CompressedSize}, Size = {e.Size}, Storage = '{e.Storage}', Ratio = '{e.Ratio}', Offset = {e.Offset} {'}'}");
            }
            sb.Append("}");
            //System.Console.WriteLine(sb.ToString()); //debug
            return sb.ToString();
        }

        public bool EntryExists(string fn,int res = -1) {
            GetError = "";
            //System.Console.WriteLine($"Checking for {fn} in resource {res}");
            var JCR = Parent.JCR;
            if (res >= 0) {
                if (!JCRDict.ContainsKey(res)) { GetError = $"JCRExists #{res} doesn't exist so cannot be listed out!"; return false; }
                JCR = JCRDict[res];
            }
            return JCR.Entries.ContainsKey(fn.ToUpper());

        }
        #endregion
    }
}



