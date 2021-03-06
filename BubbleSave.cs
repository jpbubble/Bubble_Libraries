// Lic:
// BubbleSave.cs
// Bubble
// version: 20.07.19
// Copyright (C) 2019 Jeroen P. Broks
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

#define NoCrash

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TrickyUnits;
using UseJCR6;


namespace Bubble {

    delegate void BubSaveXtra(TJCRCreate JCR, string xDat);
    delegate bool BubLoadXtra(TJCRDIR JCR, string xDat);

    class Bubble_Save {
        #region Init
        public static void Init(string state) => new Bubble_Save(state);
        private Bubble_Save(string mystate) {
            var BS = SBubble.State(mystate);
            var LS = BS.state;
            LS["Bubble_Save"] = this;
            SBubble.DoNIL(mystate, QuickStream.StringFromEmbed("BubbleSave.nil"),"BubbleSave NIL");
            stateID = mystate;
        }
        #endregion

        public string LastError { get; private set; } = "";
        private static string _wdir = "";
        private string stateID = "";

        static public readonly TMap<string, BubSaveXtra> SaveXtra = new TMap<string, BubSaveXtra>();
        static public readonly TMap<string, BubLoadXtra> LoadXtra = new TMap<string, BubLoadXtra>();
        public bool SaveXtraLoaded(string chk) => SaveXtra[chk.ToUpper()] != null;
        public bool LoadXtraLoaded(string chk) => LoadXtra[chk.ToUpper()] != null;
        
        static public string SWorkDir {
            get {
                if (_wdir == "") {
                    _wdir = Dirry.C($"$Home$/.BubbleHome/{SBubble.ID}");
                    if (SBubble.BGC("BubbleHome") != "")
                        _wdir = Dirry.AD($"{SBubble.BGC("BubbleHome")}/{SBubble.ID}");
                    else if (SBubble.IDDat("Home") != "")
                        _wdir = Dirry.AD($"{SBubble.IDDat("Home")}").Replace("\\", "/");                    
                }
                return _wdir.Replace('\\','/');
            }
            set {
                _wdir = Dirry.AD(value).Replace("\\", "/");
            }
        }
        public string WorkDir { get => SWorkDir; set { SWorkDir = value; } }// makes it available in NIL and Lua!

        public bool Exists(string afile) {
            try {
                var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
                var d = qstr.ExtractDir(file);
                var s = file.Split('/'); foreach (string dps in s) if (dps == "..") { SBubble.MyError("File check error", "I don't accept file names with .. references in the path!", d); return false; }
                return File.Exists(file);
            } catch (Exception duh) {
                BubConsole.WriteLine($"Something went wrong while looking if {afile} exists!\n\n  {duh.Message}\n\nReturning false, as result!");
                return false;
            }
        }

        public bool SaveString(string str, string afile, bool dontcrash) {
            try {
                LastError = "";
                var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
                var d = qstr.ExtractDir(file);
                var s = file.Split('/'); foreach (string dps in s) if (dps == "..") throw new Exception("I don't accept file names with .. references in the path!");
                Directory.CreateDirectory(d);
                QuickStream.SaveString(file, str);
                return true;
            } catch (Exception error) {
                var s = str;
                if (s.Length > 10) s = $"{qstr.Left(s, 4)}..{qstr.Right(s, 4)}";
                LastError = $"BubSave.SaveString(\"{str}\",\"{afile}\"): {error.Message}";
                if (!dontcrash) SBubble.MyError("Runtime error", LastError, SBubble.TraceLua(stateID)); else BubConsole.CError(LastError);
                return false;
            }
        }

        public string LoadLines(string afile, bool dontcrash) {
            try {
                var lines = LoadString(afile, dontcrash).Split('\n');
                var ret = new System.Text.StringBuilder("return {");
                for (int i = 0; i < lines.Length; i++) {
                    if (i != 0) ret.Append(", ");
                    ret.Append($"\"{qstr.SafeString(lines[i])}\"");
                }
                ret.Append("}");
                return ret.ToString();
            } catch (Exception Klote) {
                LastError += $"BubSave.LoadSLines(\"{afile}\"): {Klote.Message}";
                if (!dontcrash) SBubble.MyError("Runtime error", LastError, SBubble.TraceLua(stateID)); else BubConsole.CError(LastError);
                return "return nil;";
            }
        }

        public string LoadString(string afile, bool dontcrash) {
            try {
                LastError = "";
                var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
                var d = qstr.ExtractDir(file);
                var s = file.Split('/'); foreach (string dps in s) if (dps == "..") throw new Exception("I don't accept file names with .. references in the path!");
                return QuickStream.LoadString(file);
            } catch (Exception er) {
                LastError = $"BubSave.LoadString(\"{afile}\"): {er.Message}";
                if (!dontcrash) SBubble.MyError("Runtime error", LastError, SBubble.TraceLua(stateID)); else BubConsole.CError(LastError); 
                return "";
            }
        }

        #region Save
        SortedDictionary<string, string> JCRSaved = null;
        SortedDictionary<string, string> JCRSavedXtra = null;
        static int SaveStarted = 0;
        static int SaveEnded = 0;
        public void JCRStartSave() {
            JCRSaved = new SortedDictionary<string, string>();
            JCRSavedXtra = new SortedDictionary<string, string>();
            SaveStarted++;
            BubConsole.WriteLine($"Save Started. Started:{SaveStarted} Ended:{SaveEnded}");
        }

        public void JCRSave(string key,string value) {
            if (JCRSaved == null)
                SBubble.MyError("Sigh!", "Hack attempt on the JCR Save system?\nShame on you!", "D");
            else if (JCRSaved.ContainsKey(key.ToUpper()))
                SBubble.MyError($"JCRSave(\"{key}\",<value>):", "Duplicate definition", "");
            else
                JCRSaved[key.ToUpper()] = value;
            BubConsole.WriteLine($"Gonna Save: {key}");
        }

        public void JCRSaveXtra(string key, string value) {
            if (JCRSavedXtra == null)
                SBubble.MyError("Sigh!", "Hack attempt on the JCR Save system?\nShame on you!", "X");
            else if (JCRSavedXtra.ContainsKey(key.ToUpper()))
                SBubble.MyError($"JCRSave(\"{key}\",<value>):", "Duplicate definition", "");
            else if (SaveXtra[key.ToUpper()]==null)
                SBubble.MyError($"JCRSave(\"{key}\",<value>):", $"The engine you used has no SaveXtra module called '{key}'", "");
            else
                JCRSavedXtra[key.ToUpper()] = value;
            BubConsole.WriteLine($"Gonna Save Xtra: {key}");
        }


        public void JCREndSave(string afile, bool hashed) {
            var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
            var d = qstr.ExtractDir(file);
            var s = file.Split('/'); foreach (string dps in s) if (dps == "..") throw new Exception("I don't accept file names with .. references in the path!");
            var hashtable = new Dictionary<string, string>();
            var storage = "lzma";
            TJCRCreate j=null;
            Directory.CreateDirectory(qstr.ExtractDir(file));
            if (SBubble.IDDat("CStorage") != "") storage = SBubble.IDDat("CStorage");
            if (JCRSaved == null)
                SBubble.MyError("Sigh!", "Hack attempt on the JCR Save system?\nShame on you!", "");
            BubConsole.WriteLine("Ending save");
            try {
                 j = new TJCRCreate(file, storage);
                j.AddString($"[rem]\nJust some header stuff to verify this is a saved data file for a Bubble project\n\n[var]\nENGINE=BUBBLE\nBUBBLE={SBubble.IDDat("BubbleEngine")}\nID={SBubble.ID}\n", "BUBBLEID", storage);
                foreach (string ename in JCRSaved.Keys) {
                    var str = JCRSaved[ename];
                    if (hashed) hashtable[ename] = qstr.md5(str);
                    j.AddString(str, ename,storage);
                }
                foreach(string xname in JCRSavedXtra.Keys) {
                    SaveXtra[xname](j,JCRSavedXtra[xname]);
                }
                if (hashed) j.NewStringMap(hashtable, "HASHES", storage);
                j.Close();
                SaveEnded++;
                BubConsole.WriteLine($"Saved Ended -- Started:{SaveStarted}; Ended:{SaveEnded}");
            } catch (Exception e) {
#if NoCrash
                var err = new StringBuilder("Writing the savegame file failed!\n\n");
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    err.Append($"JCR6 Error during saving: {JCR6.JERROR}\nResulted to .NET Crash:\n\n{e.Message}");
                } else {
                    System.Diagnostics.Debug.WriteLine($"ERROR: {e.Message}\n\nStack Trace:\n{e.StackTrace}");
                    err.Append($".NET error occured: {e.Message}\n");
#if DEBUG
                    err.Append($".NET StackTrace\n{e.StackTrace}\n");
#endif
                    err.Append("\nPlease note that due to this issue it's possible the savegame file has been damaged!");
                    Confirm.Annoy($"{err}");
#else
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    SBubble.MyError("JCR6 Error during saving", JCR6.JERROR, $"Resulted to .NET Crash:\n\n{e.Message}");
                } else {
                    System.Diagnostics.Debug.WriteLine($"ERROR: {e.Message}\n\nStack Trace:\n{e.StackTrace}");
#if DEBUG
                    SBubble.MyError(".NET Error during saving", e.Message, e.StackTrace);
#else
                    SBubble.MyError(".NET Error during saving", e.Message, "");
#endif
#endif
                }
            } finally {
#if NoCrash
                if (j != null) j.Close();
#endif
                JCRSaved = null;
            }
        }
#endregion

#region Load
        TJCRDIR JCRLoaded = null;
        string[] JCRLoadedList = null;
        int JCRLoadedIndex = -1;
        bool JCRLoadedWantHash = false;
        Dictionary<string, string> JCRLoadHashes = null;
        public void JCRStartLoad(string afile,bool wanthash) {
            try {
                var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
                var d = qstr.ExtractDir(file);
                var s = file.Split('/'); foreach (string dps in s) if (dps == "..") throw new Exception("I don't accept file names with .. references in the path!");
                JCRLoaded = JCR6.Dir(file);
                JCRLoadedWantHash = wanthash || JCRLoaded.Exists("Hashes");
                if (!JCRLoaded.Exists("BUBBLEID")) throw new Exception("There is no BUBBLEID in the savegame file!");
                var l = new List<string>();
                foreach (string en in JCRLoaded.Entries.Keys) l.Add(en);
                JCRLoadedList = l.ToArray();
                JCRLoadedIndex = 0;
                if (JCRLoadedWantHash)
                    JCRLoadHashes = JCRLoaded.LoadStringMap("Hashes");
            } catch (Exception e) {
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    SBubble.MyError("JCR6 Error during loading (start)", JCR6.JERROR, $"Resulted to .NET Crash:\n\n{e.Message}");
                } else {
                    SBubble.MyError(".NET Error during loading (start)", e.Message, "");
                }
                JCRLoaded = null;
            }
        }
        public string JCRGetPure() => JCRLoaded.LoadString("PURE");

        public bool JCRXLoad(string module,string xtra) {
            if (LoadXtra[module]==null) {
                SBubble.MyError("YIKES!", $"Call to non-existent XLoad module {module}", "");
                return false;
            }
            try {
                return LoadXtra[module](JCRLoaded, xtra);                
            } catch (Exception Failure) {
                SBubble.MyError("XLoad module error", Failure.Message, "");
                return false;
            }
        }

        public string JCRLoadNext() {
            if (JCRLoaded==null) { SBubble.MyError("Huh?", "Nothing been set up for loading JCR based save files!",""); }
            string ret = "";
            do {
                if (JCRLoadedIndex >= JCRLoadedList.Length) return ""; // end of list reached
                ret = JCRLoadedList[JCRLoadedIndex];
                JCRLoadedIndex++;
            } while (ret == "HASHES" || ret == "BUBBLEID" || qstr.Prefixed(ret.ToUpper(),"XTRA/") || ret=="PURE"); // reserved names must be skipped
            return ret;
        }
        public string JCRLoadGet(string entry) {
            if (JCRLoaded == null) { SBubble.MyError("Huh?", "Nothing been set up for loading JCR based save files!", ""); }
            try {
                var ret = JCRLoaded.LoadString(entry);
                if (JCRLoadedWantHash) {
                    if (!JCRLoadHashes.ContainsKey(entry.ToUpper())){
                        throw new Exception($"No hash found for {entry}! One is required! Corrupted data?");
                    }
                    if (qstr.md5(ret) != JCRLoadHashes[entry.ToUpper()]) {
                        throw new Exception("Hash mismatch? Currupted data?");
                    }
                }
                return ret;
            } catch (Exception e) {
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    SBubble.MyError("JCR6 Error during loading (get)", JCR6.JERROR, $"Resulted to .NET Crash:\n\n{e.Message}");
                } else {
                    SBubble.MyError(".NET Error during loading (get)", e.Message, "");
                }
                JCRLoaded = null;
                return "";
            }            
        }
        public void JCRLoadClose() {
            JCRLoaded = null;
            JCRLoadedList = null;
            JCRLoadedIndex = -1;
        }
    }
#endregion
}