using System;
using System.Diagnostics;
using TrickyUnits;
using NLua;
using UseJCR6;

namespace Bubble{

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

        public BubbleMainAPI(BubbleState fromparent) {
            Parent = fromparent;
            try {
                bstate.DoString(@"-- Init me ;)

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
                        if (!f) then A_Bubble:Beep() return end
                        A_Bubble:XBeep(f,d or 250)
                    end
            ");
            } catch (Exception E) {
                CrashHandler("Main Script Error", E.Message, "");
            }
        }

	}

    class BubbleState {
        internal Lua state = new Lua();
        TJCRDIR JCR;
        public BubbleError MyError = delegate (string ct, string m, string trace) {
            Debug.WriteLine($"LUA ERROR!\n{m}");
            if (trace != "") Debug.WriteLine($"\n{trace}");
        };


        public string PreprocessLua(string script) {
            // temp code, as the true thing comes later!
            var r = script;
            return script;
        }

        public LuaFunction Use(string fuse) {
            if (JCR.Exists($"{fuse}.lua")) {
                try {
                    var script = JCR.LoadString($"{fuse}.lua");
                    script = PreprocessLua(script);
                    return (LuaFunction)state.DoString(script,$"{fuse}.lua")[0];
                } catch (Exception e) {
                    MyError("Use Compile", e.Message, "");
                    return null;
                }
            }
            MyError("Use Error","I was unable to locate '{fuse}' in Use request","");
            return null;
        }

        public BubbleState(TJCRDIR J,BubbleError e=null) {
            state["A_Bubble"] = new BubbleMainAPI(this);
            JCR = J;
            if (e != null) MyError = e;
        }
    }
	
}

