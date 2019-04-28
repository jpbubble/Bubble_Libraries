// Lic:
// BubbleJCRClass.cs
// (c)  Jeroen Petrus Broks.
// 
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL was not
// distributed with this file, You can obtain one at
// http://mozilla.org/MPL/2.0/.
// Version: 19.04.28
// EndLic


/*
 * This class serves as the API to allow 
 * the Lua scripts to read JCR files.
 * 
 * This class is "mandadory", and all Bubble projects will therefore include it!
 */

using System.Text;
using System.Collections.Generic;
using NLua;
using UseJCR6;

namespace Bubble {

    class JCR_Bubble {

        #region My own shit! Should NOT be used by Lua
        private BubbleState Parent;
        private Lua bstate => Parent.state;


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
            ", "JCR Init chunk");
        }

        #endregion

        #region The actual API
        public string EntryList(int res=-1, bool asmap=false) {
            var sb = new StringBuilder("return {");
            var comma = false;
            var JCR = Parent.JCR;
            // if (res>=0) JCR=Resources[res];
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
        #endregion
    }
}


