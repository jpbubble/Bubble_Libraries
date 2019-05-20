// Lic:
// BubbleMainClass.cs
// (c)  Jeroen Petrus Broks.
// 
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL was not
// distributed with this file, You can obtain one at
// http://mozilla.org/MPL/2.0/.
// Version: 19.05.20
// EndLic





#define BubbleDEBUG

#define NewStateDEBUG

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using TrickyUnits;
using NLua;
using UseJCR6;

namespace Bubble {

    /// <summary>
    /// Callback used to generate errors. 
    /// </summary>
    /// <param name="ct">Will either contain 'compile', 'pre-process' or 'run-time' </param>
    /// <param name="message">Error message itself</param>
    /// <param name="trace">Tranceback (if applicable)</param>
    delegate void BubbleError(string ct, string message, string trace);
	
	// This class is only meant to provide the links between Lua and 
	// BUBBLE, and it should only be used that way!
	internal class BubbleMainAPI{
		
		public string Version => MKL.Newest;
        public void ForeColor(byte b) => Console.ForegroundColor = (ConsoleColor)b;
        public void BackColor(byte b) => Console.BackgroundColor = (ConsoleColor)b;
        public void CrashHandler(string ct, string message, string trace) => Parent.MyError(ct, message, trace);
        public void Beep() => Console.Beep();
        public void XBeep(int f, int d) => Console.Beep(f, d);
        private Lua bstate => Parent.state;
        private BubbleState Parent;

        public string NILScript => SBubble.NILScript;



public BubbleMainAPI(BubbleState fromparent) {
            Parent = fromparent;
            try {                
                bstate.DoString(@"-- Init me ;)

                    function StartNIL()                       
                       NIL = (loadstring or load)(A_Bubble.NILScript,'NIL')();
                       if (not NIL) then BubbleCrash('NIL failed to load') end
                    end

                    function QErTrace(Err)
                        return Err ..'\n\nTracebak:\n'..debug.traceback..'\n'
                    end

                    sprintf = string.format
            
                    function BubbleVersion() return A_Bubble.Version end
                    function Color(fore,back)
                        back=back or 0
                        assert(type(fore)=='number' and fore>=0 and fore<=15,'Invalid foreground color!')
                        assert(type(back)=='number' and back>=0 and fore<=15,'Invalid background color!')
                        A_Bubble:ForeColor(fore)
                        A_Bubble:BackColor(back)
                    end

                    function BubbleCrash(x)
                        if type(x)=='string' then
                        A_Bubble:CrashHandler('Run-Time',x,debug.traceback())
                        end
                        -- TODO: Getting to work out .NET exceptions, as they are just thrown in the .NET type.
                    end

                    function Beep(f,d)
                        if (not f) then A_Bubble:Beep() return end
                        A_Bubble:XBeep(f,d or 250)
                    end

            ", "BubbleMainAPICoreScript");
            } catch (Exception E) {
#if BubbleDEBUG
                CrashHandler("Main Script Error", E.Message, E.StackTrace);
#else
                CrashHandler("Main Script Error", E.Message, "");
#endif
            }
        }

	}

    class BubbleState {
        internal Lua state = new Lua();
        readonly public TJCRDIR JCR;
        public BubbleError MyError = delegate (string ct, string m, string trace) {
            Debug.WriteLine($"LUA ERROR!\n{m}");
            if (trace != "") Debug.WriteLine($"\n{trace}");
        };


        public string PreprocessLua(string script) {
            // temp code, as the true thing comes later!
            var r = script;
            return script;
        }

        public object Use(string fuse) {
            if (JCR.Exists(fuse)) {
                try {                    
                    switch (qstr.ExtractExt(fuse).ToLower()) {
                        case "lua": {
                                var script = JCR.LoadString(fuse);
                                script = PreprocessLua(script);
                                var res = state.DoString(script, fuse);
                                return res;
                            }
                        case "nil": {
                                var l = $"local ok, ret = pcall( NIL.Use,'{fuse}')\nif not(ok) then BubbleCrash(ret) else return ret end";
                                Debug.Print(l);
                                var ret = state.DoString(l); //--state.DoString($"return NIL.LoadString([[{script}]])()");
                                Debug.Print("Done");
                                return ret;
                            }
                        default:
                            MyError("Bubble", $"I don't know how script {fuse} works", "");
                            return null; // Will never be called, but C# can't tell!
                    }
                    
                } catch (Exception e) {
#if BubbleDEBUG
                    MyError("Use Compile", e.Message, e.StackTrace);
#else
                    MyError("Use Compile", e.Message, "");
#endif
                    return null;
                }
            }
            MyError("Use Error",$"I was unable to locate '{fuse}' in Use request","");
            return null;
        }

        static int DSI = 0;
        public object[] DoString(string command,string chunk="?") {
            var h = qstr.md5($"BubbleID:{DateTime.Now.ToString()}:{DSI}");
            DSI++;
            try {
                if (chunk == "?") chunk = $"BUBCALL:{h}";
                return state.DoString(command, chunk);
            } catch (Exception e) {
                MyError("DoString error", e.Message, "");
                return null;
            }
        }

        public BubbleState(TJCRDIR J,BubbleError e=null) {
            state["A_Bubble"] = new BubbleMainAPI(this); 
            state["Bubble_JCR"] = new JCR_Bubble(this); 
            state["Bubble_Bank"] = new BubbleBank(this);
            state.DoString(@"-- print('Start NIL')
                             StartNIL()  -- print('NIL')
                             JCR_InitNIL() -- print('JCR')
                             ", "StartNIL");            
            JCR = J;
            if (e != null) MyError = e;            
        }
        
        
    }

    delegate void StateInit(string VM);
    /*
    struct StateInitStruct {
        public StateInit func;
        public string param;
        public StateInitStruct(StateInit f,string p) { func = f;param = p; }
    }
    */

    static class SBubble {
        static Dictionary<string, BubbleState> States = new Dictionary<string, BubbleState>();
        static public TJCRDIR JCR { get; private set; }
        static string JCRFile = qstr.Left(MKL.MyExe, MKL.MyExe.Length - 4) + ".Bubble.jcr";
        static TGINI Identify;
        static public BubbleError MyError = null;
        static public string NILScript { get; private set; } = "error(\"'NIL.lua' was not properly loaded! Was it properly embedded in the VS project?\")\n";
        static public string Title => Identify.C("Title");
        static public string ID => Identify.C("BubbleID");
        static public string IDDat(string tag) => Identify.C(tag);
        static internal List<StateInit> AlwaysInit = new List<StateInit>();
        static public void AddInit(StateInit s) => AlwaysInit.Add(s);
        static public string RunMode => Identify.C("RunMode").ToUpper();
        static TGINI BubbleGlobalConfig = new TGINI();
        static public string BGC(string tag) => BubbleGlobalConfig.C(tag);
        static public string[] BGCList(string tag) => BubbleGlobalConfig.List(tag).ToArray();

        static void ICrash(string E) {
            Console.WriteLine(E);
            Debug.WriteLine("ERROR!");
            Debug.WriteLine(E);
            //if (Debugger.IsAttached) Console.ReadKey(true);
            Environment.Exit(1);
        }

        static public void SetUpNIL() {
            foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames()) {
                if (name.EndsWith("NIL.lua", StringComparison.InvariantCultureIgnoreCase)) {
                    //Console.WriteLine($"Emb>{name}");
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
                        var QS = new QuickStream(stream);
                        var b = QS.ReadBytes((int)QS.Size);
                        NILScript = Encoding.Default.GetString(b);
                        QS.Close();
                        //Console.WriteLine(NILScript);
                    }
                    return;
                }
            }
            MyError("Internal", "NIL.lua not found!", "N/A");
        }

        static public void Init(string reqengine,BubbleError ErrorHandler=null) {
            var globconfigfile = Dirry.C("$Home$/.Tricky__ApplicationSupport/Bubble_GlobalConfig.GINI").Replace('\\','/');
            if (File.Exists(globconfigfile)) {
                BubbleGlobalConfig = GINI.ReadFromFile(globconfigfile);
            }
            if (!File.Exists(JCRFile)) 
                ICrash($"Cannot find required resource file: {JCRFile}");
            try {
                JCR = JCR6.Dir(JCRFile);
                Identify = GINI.ReadFromLines(JCR.ReadLines("ID/BUBBLEID"));
                if (JCR6.JERROR!="")
					ICrash($"Error reading identification!\nJCR reported: {JCR6.JERROR}");
                if (Identify.C("BubbleEngine") == "")
                    ICrash("The resource JCR does not appear to be a valid BUBBLE application");
                if (Identify.C("BubbleEngine") != reqengine)
                    ICrash($"Wrong engine! The resource JCR appears to be for the BUBBLE {Identify.C("BubbleEngine")} engine, and this is the BUBBLE {reqengine} engine");
            } catch (Exception E) {
                ICrash($".NET says {E.Message}\nJCR6 says {JCR6.JERROR}\nAnyway, something's not right here!");
            }
            MyError = ErrorHandler;
            SetUpNIL();
        }

        static public string[] ResFiles {
            get {
                var Cnt = JCR.Entries.Count;
                var ret = new string[Cnt];
                var i = 0;
                foreach(TJCREntry Ent in JCR.Entries.Values) {
                    if (i>=Cnt) {
                        Console.WriteLine("INTERAL ERROR! Entry overflow! Please report!");
                        Environment.Exit(0);
                    }
                    ret[i] = Ent.Entry;
                    i++;
                }
                if (i < Cnt) {
                    Console.WriteLine("INTERNAL ERROR! Entry underrun! Please report!");
                }                
                return ret;
            }
        }

        static public void NewState(string stateID,string scriptfile) {
            var ns = new BubbleState(JCR, MyError);
#if NewStateDEBUG
            Debug.WriteLine($"Create {stateID} -- file {scriptfile} ");
#endif
            States[stateID.ToUpper()] = ns;
            foreach (StateInit si in AlwaysInit) {
#if NewStateDEBUG
                Debug.WriteLine("Classimp");
#endif
                si(stateID);
            }
#if NewStateDEBUG
            Debug.WriteLine("Script itself");
#endif
            ns.Use(scriptfile);
        }

        static public BubbleState State(string stateId) {
            var s = stateId.ToUpper();
            if (!States.ContainsKey(s.ToUpper())) {
                MyError("Bubble Error", $"State \"{stateId}\" does not exist", "");
                //throw new Exception($"State \"{stateId}\" does not exist");
                return null;
            }
            return States[s.ToUpper()];
        }

        public static bool HaveState(string stateID) {
            if (States==null) BubConsole.CError("Hey, I cannot check the existsance of a state when the states are nil!");
            BubConsole.CSay($"Bubb: Checking state {stateID}!");
            var ret = States.ContainsKey(stateID.ToUpper());
            BubConsole.CSay($"Outcome {ret}");
            return ret;
        }
        static public void KillState(string stateID) => States.Remove(stateID.ToUpper());
        

        static public void DoNIL(string stateID, string NILScript, string chunk = "NILSCRIPT") {
            var bs = State(stateID);
            try {
                var Safe = new StringBuilder(1);
                for (int i = 0; i < NILScript.Length; i++) {
                    var c = NILScript[i];
                    var b = (int)c;
                    if (c == '[' || c == ']')
                        //Safe.Append($"\\{b.ToString("D3")}");
                        Safe.Append($"]]..'{c}'..[[");
                    else
                        Safe.Append(c);
                }
                //var s = $"local s,e = xpcall( NIL.LoadString,BubbleCrash,[[{NILScript}]],\"Translation: {chunk}\")\nif s then e() end";
                /*
                var s = $"local s,e = xpcall( NIL.LoadString,BubbleCrash,[[{ Safe.ToString() }]],\"Translation: {chunk}\")\nif s and (type(e)=='function') then e() end\n";
                s += $"if type(e)=='string' then CSay('[NIL TRANSLATION RESULT]\\n'..e..'\\n[/NIL TRANSLATION RESULT]') else CSay('NIL Result is:'..type(e)) end\n";
                s += $"for k,v in pairs(_G) do print(type(v),k,v) end"; // debug!
                //*/
                var s = $"local f=assert(NIL.LoadString([[{ Safe.ToString() }]],'Trans: {chunk}') ) f()";
                //var s = $"print('<trans chunk={chunk}>'..NIL.Translate([[{Safe.ToString() }]])..'</trans>')\n"; // {s}"; // debug
                Debug.WriteLine($"[DONIL chunk='{chunk}']\n{s}\n[/DONIL]");
                bs.state.DoString(s, $"DoNIL(\"{stateID}\",<script>,\"{chunk}\"");
                Debug.WriteLine("===");
            } catch (Exception E) {
                MyError("NIL quickrun error", E.Message, "");
            }
        }

        static public string TraceLua(string stateID) {
            if (!States.ContainsKey(stateID.ToUpper())) return $"Non-existent state: {stateID}";
            var bs = State(stateID).state.DoString("return debug.traceback()","Traceback call")[0];
            return (string)bs;
        }

        static public string StringArray2Lua(string[] a) {
            var r = new StringBuilder("{");
            var comma = false;
            foreach(string s in a) {
                if (comma) r.Append(", "); comma = true;
                r.Append("\"");
                for(int i = 0; i < s.Length; i++) {
                    var c = (byte)(s[i]);
                    if (c >= 32 && c < 126 && c != '\\' && c!='"')
                        r.Append(s[i]);
                    else
                        r.Append('\\' + $"{qstr.Right($"00{c}", 3)}");
                }
                r.Append("\"");
            }
            r.Append("}");
            return r.ToString();
        }

        
    }
	
}





