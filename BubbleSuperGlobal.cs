// Lic:
// BubbleSuperGlobal.cs
// Bubble
// version: 19.08.05
// Copyright (C)  Jeroen P. Broks
// This software is provided 'as-is', without any express or implied
// warranty.  In no event will the authors be held liable for any damages
// arising from the use of this software.
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
// 1. The origin of this software must not be misrepresented; you must not
// claim that you wrote the original software. If you use this software
// in a product, an acknowledgment in the product documentation would be
// appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be
// misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.
// EndLic




#undef superglobcalldebug

using System;
using System.Text;
using System.Collections.Generic;
using TrickyUnits;

namespace Bubble {
    class BubbleSuperGlobal {

        static Dictionary<string, string> globs = new Dictionary<string, string>();
        static Dictionary<string, string> types = new Dictionary<string, string>();
        public bool strict = false;
        readonly string statename;

        string LuaTraceBack => SBubble.TraceLua(statename);

        public string GetGlob(string v) {
            try {
                if (qstr.Prefixed(v, "#")) throw new Exception("# not allowed in var calling!");
                if (!globs.ContainsKey(v)) {
#if superglobcalldebug
                BubConsole.CSay($"Asked: {v}!");
                foreach(string key in globs.Keys) {
                    BubConsole.WriteLine($"{key}\t = {globs[key]}",255,180,0);
                }
#endif
                    return "nil";
                }
                return globs[v];
            } catch (Exception e) {
                SBubble.MyError(".NET API error", e.Message, $"Retrieving SuperGlobal.{v}");
                return "**ERROR**";
            }
        }

        public string GetType(string v) {
            try {
                if (!types.ContainsKey(v)) {
                    if (strict) throw new Exception($"Undeclared variable in strict mode < {v}");
                    if (!globs.ContainsKey(v)) return "nil";
                    return "var";
                }
                return types[v];
            } catch (Exception er) {
                SBubble.MyError(".NET API error", er.Message, $"Type catching SuperGlobal.{v}");
                return "var";
            }
        }

        public void DefGlob(string k, string v) {
            try {
                if (qstr.Prefixed(k, "#")) {
                    switch (k) {
                        case "#strict": strict = true; break;
                        case "#int":
                        case "#number":
                            types[v] = "number";
                            break;
                        case "#var":
                            types[v] = "var";
                            break;
                        case "#string":
                            types[v] = "string";
                            break;
                        case "#boolean":
                            types[v] = "boolean";
                            break;
                        case "#clearall":
                            if (v == "DontSayMattressToMrLambert") CLEARALL();
                            break;
                        default:
                            throw new Exception("False \"#\" instruction to SuperGlobal");
                    }
                    return;
                }
                if (strict && !types.ContainsKey(k)) throw new Exception($"Undeclared variable in strict mode > {k}");
                switch (GetType(k)) {
                    case "string":
                    case "var":
                        break;
                    case "number":
                        try {
                            Int64.Parse(v);
                        } catch {
                            throw new Exception("Apparently the number variable value was not a number (please note, only integers are accepted!)");
                        }
                        break;
                }
                // BubConsole.CSay($"Defined SuperGlobal: {k} = {v}");
                globs[k] = v;
            } catch (Exception e) {
                SBubble.MyError(".NET API error", e.Message, $"SuperGlobal.{k} = {v}");
            }
        }
            

        public void CLEARALL() {
            types.Clear();
            globs.Clear();
        }

        private string safe(string k) {
            var r = new StringBuilder();
            for(int i = 0; i < k.Length; i++) {
                var ch = k[i];
                var bt = (byte)ch;
                if ((bt >= 65 && bt <= 90) || (bt >= 48 && bt <= 57) || (bt >= 97 && bt <= 122) || ch == '_' || ch == ' ')
                    r.Append(ch);
                else
                    r.Append($"\\{qstr.Right($"000{bt}", 3)}");
            }
            return r.ToString();
        }

        public string Serialize() {
            var ret = new StringBuilder("SuperGlobal[\"#clearall\"]=\"DontSayMattressToMrLambert\"\n");
            foreach(string k in types.Keys) { ret.Append($"SuperGlobal['#{types[k]}'] = '{safe(k)}'\n"); }
            foreach(string k in globs.Keys) { ret.Append($"SuperGlobal['{safe(k)}'] = '{safe(globs[k])}'\n"); }
            return ret.ToString();
        }

    

        private BubbleSuperGlobal(string vm) {
            var s = SBubble.State(vm).state;
            s["Bubble_SuperGlobal"] = this;
            s.DoString(QuickStream.StringFromEmbed("SuperGlobal.lua"), "SuperGlobal Header");
            statename = vm;
        }

        static public void Init(string vm) => new BubbleSuperGlobal(vm);


    }
}




