// Lic:
// BubbleSave.cs
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
using System.IO;
using System.Collections.Generic;
using TrickyUnits;
using UseJCR6;

namespace Bubble {
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
        private string _wdir = "";
        private string stateID = "";
        public string WorkDir {
            get {
                if (_wdir == "") {
                    _wdir = Dirry.C($"$Home$/.BubbleHome/{SBubble.ID}");
                    if (SBubble.BGC("BubbleHome") != "")
                        _wdir = Dirry.AD(SBubble.BGC("BubbleHome"));
                    else if (SBubble.IDDat("Home") != "")
                        _wdir = Dirry.AD(SBubble.IDDat("Home")).Replace("\\", "/");                    
                }
                return _wdir.Replace('\\','/');
            }
            set {
                _wdir = Dirry.AD(value).Replace("\\", "/");
            }
        }

        public bool SaveString(string str, string afile, bool dontcrash) {
            try {
                LastError = "";
                var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
                var d = qstr.ExtractDir(file);
                var s = file.Split('/'); foreach (string dps in s) if (dps == "..") throw new Exception("I don't accept file names with .. references in the path!");
                Directory.CreateDirectory(d);
                QuickStream.SaveString(afile, str);
                return true;
            } catch (Exception error) {
                var s = str;
                if (s.Length > 10) s = $"{qstr.Left(s, 4)}..{qstr.Right(s, 4)}";
                LastError = $"BubSave.SaveString(\"{str}\",\"{afile}\"): {error.Message}";
                if (!dontcrash) SBubble.MyError("Runtime error", LastError, SBubble.TraceLua(stateID));
                return false;
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
                if (!dontcrash) SBubble.MyError("Runtime error", LastError, SBubble.TraceLua(stateID));
                return "";
            }
        }

        #region Save
        SortedDictionary<string, string> JCRSaved = null;
        public void JCRStartSave() {
            JCRSaved = new SortedDictionary<string, string>();
        }

        public void JCRSave(string key,string value) {
            if (JCRSaved == null)
                SBubble.MyError("Sigh!", "Hack attempt on the JCR Save system?\nShame on you!", "");
            else if (JCRSaved.ContainsKey(key.ToUpper()))
                SBubble.MyError($"JCRSave(\"{key}\",<value>):", "Duplicate definition", "");
            else
                JCRSaved[key.ToUpper()] = value;
        }

        public void JCREndSave(string afile, bool hashed) {
            var file = WorkDir; if (qstr.Right(file, 1) != "/") file += "/"; file += afile;
            var d = qstr.ExtractDir(file);
            var s = file.Split('/'); foreach (string dps in s) if (dps == "..") throw new Exception("I don't accept file names with .. references in the path!");
            var hashtable = new Dictionary<string, string>();
            var storage = "lzma";
            Directory.CreateDirectory(qstr.ExtractDir(file));
            if (SBubble.IDDat("CStorage") != "") storage = SBubble.IDDat("CStorage");
            if (JCRSaved == null)
                SBubble.MyError("Sigh!", "Hack attempt on the JCR Save system?\nShame on you!", "");
            try {
                var j = new TJCRCreate(file, storage);
                j.AddString($"[rem]\nJust some header stuff to verify this is a saved data file for a Bubble project\n\n[var]\nENGINE=BUBBLE\nBUBBLE={SBubble.IDDat("BubbleEngine")}\nID{SBubble.ID}\n", "BUBBLEID", storage);
                foreach (string ename in JCRSaved.Keys) {
                    var str = JCRSaved[ename];
                    if (hashed) hashtable[ename] = qstr.md5(str);
                    j.AddString(str, ename);
                }
                if (hashed) j.NewStringMap(hashtable, "HASHES", storage);
                j.Close();
            } catch (Exception e) {
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    SBubble.MyError("JCR6 Error during saving", JCR6.JERROR, $"Resulted to .NET Crash:\n\n{e.Message}");
                } else {
                    SBubble.MyError(".NET Error during saving", e.Message, "");
                }
            } finally {
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
                var l = new List<string>();
                foreach (string en in JCRLoaded.Entries.Keys) l.Add(en);
                JCRLoadedList = l.ToArray();
                JCRLoadedIndex = 0;
                if (JCRLoadedWantHash)
                    JCRLoadHashes = JCRLoaded.LoadStringMap("Hashes");
            } catch (Exception e) {
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    SBubble.MyError("JCR6 Error during loading", JCR6.JERROR, $"Resulted to .NET Crash:\n\n{e.Message}");
                } else {
                    SBubble.MyError(".NET Error during loading", e.Message, "");
                }
                JCRLoaded = null;
            }
        }
        public string JCRLoadNext() {
            if (JCRLoaded==null) { SBubble.MyError("Huh?", "Nothing been set up for loading JCR based save files!",""); }
            string ret = "";
            do {
                if (JCRLoadedIndex >= JCRLoadedList.Length) return ""; // end of list reached
                ret = JCRLoadedList[JCRLoadedIndex];
                JCRLoadedIndex++;
            } while (ret == "HASHES"); // reserved names must be skipped
            return ret;
        }
        public string JCRLoadGet(string entry) {
            if (JCRLoaded == null) { SBubble.MyError("Huh?", "Nothing been set up for loading JCR based save files!", ""); }
            try {
                var ret = JCRLoaded.LoadString(entry);
                if (JCRLoadedWantHash) {
                    if (qstr.md5(ret) != JCRLoadHashes[entry.ToUpper()]) {
                        throw new Exception("Hash mismatch? Currupted data?");
                    }
                }
                return ret;
            } catch (Exception e) {
                if (JCR6.JERROR != "" && JCR6.JERROR.ToUpper() != "OK") {
                    SBubble.MyError("JCR6 Error during loading", JCR6.JERROR, $"Resulted to .NET Crash:\n\n{e.Message}");
                } else {
                    SBubble.MyError(".NET Error during loading", e.Message, "");
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


