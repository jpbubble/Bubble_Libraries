// Lic:
// BubbleSuperGlobal.cs
// Bubble
// version: 19.05.20
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

using System;
using System.Text;
using System.Collections.Generic;
using TrickyUnits;

namespace Bubble {
    class BubbleSuperGlobal {

        Dictionary<string, string> globs = new Dictionary<string, string>();
        Dictionary<string, string> types = new Dictionary<string, string>();
        public bool strict = false;

        public string GetGlob(string v) {
            if (qstr.Prefixed(v, "#")) throw new Exception("# not allowed in var calling!");
            if (!globs.ContainsKey(v)) return "nil";
            return globs[v];
        }

        public string GetType(string v) {
            if (!types.ContainsKey(v)) {
                if (strict) throw new Exception($"Undeclared variable in strict mode < {v}");
                if (!globs.ContainsKey(v)) return "nil";
                return "var";
            }
            return types[v];
        }

        public void DefGlob(string k, string v) {
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
            globs[k] = v;
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
        }

        static public void Init(string vm) => new BubbleSuperGlobal(vm);


    }
}
