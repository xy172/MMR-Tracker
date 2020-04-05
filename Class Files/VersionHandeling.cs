﻿using MMR_Tracker.Forms;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MMR_Tracker_V2
{
    class VersionHandeling
    {
        public static int Version = 0; // The current verion of logic being used
        public static int EntranceRandoVersion = 14; // The version of logic that entrance randomizer was implimented
        public static bool entranceRadnoEnabled = false; // Whether or not entrances should be seperated into their own colum
        public static bool OverRideAutoEntranceRandoEnable = false;
        public static bool CheckForUpdate = true;
        public static List<int> ValidVersions = new List<int> { 8, 13, 16 }; // Versions of logic used in main releases

        public static decimal trackerVersion = 1.7m;

        public static Dictionary<int, int> AreaClearDictionary()
        {
            //Rando Version 1.5 = Logic Version 3
            //Rando Version 1.6 = Logic Version 5
            //Rando Version 1.7 = Logic Version 6
            //Rando Version 1.8 = Logic Version 8
            //Rando Version 1.9 - 1.11 = Logic Version 13
            //Entrance Rando Dev Build 1.11.0.2 = Logic Version 16 (Used to test entrance rando features)
            var EntAreaDict = new Dictionary<int, int>();
            var AreaDicVersion = 0;

            if (!ValidVersions.Contains(Version) && !IsEntranceRando())
            { AreaDicVersion = ValidVersions.Aggregate((x, y) => Math.Abs(x - Version) < Math.Abs(y - Version) ? x : y); }
            else { AreaDicVersion = Version; }

            switch (AreaDicVersion)
            {
                case 3:
                    EntAreaDict.Add(100, 99); //Woodfall Clear, Woodfall Entrance
                    EntAreaDict.Add(103, 102); //Snowhead Clear, Snowhead Entrance
                    EntAreaDict.Add(108, 107); //GreatBay Clear, GreatBay Entrance
                    EntAreaDict.Add(113, 112); //Ikana Clear, StoneTower Entrance
                    break;
                case 5:
                case 6:
                    EntAreaDict.Add(101, 100); //Woodfall Clear, Woodfall Entrance
                    EntAreaDict.Add(104, 103); //Snowhead Clear, Snowhead Entrance
                    EntAreaDict.Add(109, 108); //GreatBay Clear, GreatBay Entrance
                    EntAreaDict.Add(114, 113); //Ikana Clear, StoneTower Entrance
                    break;
                case 8:
                case 13:
                    EntAreaDict.Add(105, 104); //Woodfall Clear, Woodfall Entrance
                    EntAreaDict.Add(108, 107); //Snowhead Clear, Snowhead Entrance
                    EntAreaDict.Add(113, 112); //GreatBay Clear, GreatBay Entrance
                    EntAreaDict.Add(118, 117); //Ikana Clear, StoneTower Entrance
                    break;
            }
            return EntAreaDict;
        }

        public static bool IsEntranceRando()
        {
            return (Version >= EntranceRandoVersion || OOT_Support.isOOT);
        }

        public static string[] SwitchDictionary(int Currentversion, bool OOT = false)
        {
            string[] files = Directory.GetFiles(@"Recources");
            Dictionary<int, string> dictionaries = new Dictionary<int, string>();//< Int (Version),String (Path to the that dictionary)>
            Dictionary<int, string> Pairs = new Dictionary<int, string>();//< Int (Version),String (Path to the that dictionary)>
            int smallestDicEntry = 0;
            int largestDicEntry = 0;
            int smallestPairEntry = 0;
            int largestPairEntry = 0;
            foreach (var i in files)
            {
                var dic = "MMRDICTIONARY";
                if (OOT) { dic = "OOTRDICTIONARY"; }
                if (i.Contains(dic))
                {
                    var entry = i.Replace("Recources\\" + dic + "V", "");
                    entry = entry.Replace(".csv", "");
                    int version = 0;
                    try { version = Int32.Parse(entry); }
                    catch { continue; }
                    dictionaries.Add(version, i);
                    if (version > largestDicEntry) { largestDicEntry = version; }
                    if (smallestDicEntry == 0) { smallestDicEntry = largestDicEntry; }
                    if (version < smallestDicEntry) { smallestDicEntry = version; }
                }
                if (i.Contains("ENTRANCEPAIRS"))
                {
                    var entry = i.Replace("Recources\\ENTRANCEPAIRSV", "");
                    entry = entry.Replace(".csv", "");
                    int version = 0;
                    try { version = Int32.Parse(entry); }
                    catch { continue; }
                    Pairs.Add(version, i);
                    if (version > largestPairEntry) { largestPairEntry = version; }
                    if (smallestPairEntry == 0) { smallestPairEntry = largestPairEntry; }
                    if (version < smallestPairEntry) { smallestPairEntry = version; }
                }

            }

            string[] currentdictionary = new string[2];
            var index = 0;

            if (dictionaries.ContainsKey(Currentversion)) { index = Currentversion; }
            else //If we are using a logic version that doesn't have a dictionary, use the dictioary with the closest version
            { index = dictionaries.Keys.Aggregate((x, y) => Math.Abs(x - Currentversion) < Math.Abs(y - Currentversion) ? x : y); }

            currentdictionary[0] = dictionaries[index];

            Console.WriteLine(currentdictionary[0]);

            if (Pairs.ContainsKey(Currentversion))
            { index = Currentversion; }
            else //If we are using a logic version that doesn't have a Pair List, use the pair List with the closest version
            { index = Pairs.Keys.Aggregate((x, y) => Math.Abs(x - Currentversion) < Math.Abs(y - Currentversion) ? x : y); }

            currentdictionary[1] = Pairs[index];

            Console.WriteLine(currentdictionary[1]);

            return currentdictionary;
        }

        public static bool GetLatestTrackerVersion()
        {
            var client = new GitHubClient(new ProductHeaderValue("MMR-Tracker"));
            var releases = client.Repository.Release.GetAll("Thedrummonger", "MMR-Tracker");
            Release Latest = new Release();
            decimal LatestVersion = 0;

            foreach(var i in releases.Result)
            {
                try 
                { 
                    var TryVersion = Convert.ToDecimal(i.TagName.Replace("V", ""));
                    LatestVersion = TryVersion;
                    Latest = i;
                    break;
                }
                catch 
                { 
                    Console.WriteLine($"Github Release Tag {i.TagName} was malformed"); 
                }
            }
            
            if (Latest == null || LatestVersion == 0) { return false; }

            if (trackerVersion < LatestVersion && CheckForUpdate)
            {
                if (Debugging.ISDebugging && (Control.ModifierKeys != Keys.Shift)) { Console.WriteLine($"Tracker Out of Date. Latest Version: { Latest.TagName } Current Version V{ trackerVersion }"); }
                else
                {
                    var Download = MessageBox.Show($"Your tracker version V{ trackerVersion } is out of Date! Would you like to download the latest version { Latest.TagName } ?", "Tracker Out of Date", MessageBoxButtons.YesNo);
                    if (Download == DialogResult.Yes) { { Process.Start(Latest.HtmlUrl); return true; } }
                }
            }
            return false;
        }
    }
}
