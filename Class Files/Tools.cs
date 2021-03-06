﻿using MMR_Tracker_V2;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MMR_Tracker.Class_Files
{
    class Tools
    {
        public static event EventHandler NetDataProcessed = delegate { };

        //Used to pass Logic items between forms
        public static LogicObjects.LogicEntry CurrentSelectedItem = new LogicObjects.LogicEntry();
        public static List<LogicObjects.LogicEntry> CurrentselectedItems = new List<LogicObjects.LogicEntry>();

        public static LogicObjects.ItemUnlockData FindRequirements(LogicObjects.LogicEntry Item, List<LogicObjects.LogicEntry> logic)
        {
            List<int> ImportantItems = new List<int>();
            List<LogicObjects.PlaythroughItem> playthrough = new List<LogicObjects.PlaythroughItem>();
            var LogicCopy = Utility.CloneLogicList(logic);
            foreach (var i in LogicCopy)
            {
                if (i.Unrandomized()) { i.IsFake = true; }
            }
            var ItemCopy = LogicCopy[Item.ID];
            LogicEditing.ForceFreshCalculation(LogicCopy);
            foreach (var i in LogicCopy)
            {
                ImportantItems.Add(i.ID);
                if (i.IsFake) { i.SpoilerRandom = i.ID; }
            }
            PlaythroughGenerator.UnlockAllFake(LogicCopy, ImportantItems, 0, playthrough);
            List<int> UsedItems = new List<int>();
            bool isAvailable = (LogicEditing.RequirementsMet(ItemCopy.Required, LogicCopy, UsedItems) && LogicEditing.CondtionalsMet(ItemCopy.Conditionals, LogicCopy, UsedItems));
            if (!isAvailable) { return new LogicObjects.ItemUnlockData(); }
            List<int> NeededItems = Tools.ResolveFakeToRealItems(new LogicObjects.PlaythroughItem { SphereNumber = 0, Check = ItemCopy, ItemsUsed = UsedItems }, playthrough, LogicCopy);
            List<int> FakeItems = Tools.FindAllFakeItems(new LogicObjects.PlaythroughItem { SphereNumber = 0, Check = ItemCopy, ItemsUsed = UsedItems }, playthrough, LogicCopy);
            NeededItems = NeededItems.Distinct().ToList();
            return new LogicObjects.ItemUnlockData {playthrough = playthrough, FakeItems = FakeItems, ResolvedRealItems = NeededItems, UsedItems =UsedItems };
        }
        public static void CreateDictionary()
        {
            string file = Utility.FileSelect("Select A Logic File", "Logic File (*.txt)|*.txt");
            if (file == "") { return; }

            LogicObjects.TrackerInstance CDLogic = new LogicObjects.TrackerInstance();
            int LogicVersion = VersionHandeling.GetVersionFromLogicFile(File.ReadAllLines(file)).Version;
            LogicEditing.PopulateTrackerInstance(CDLogic);

            List<LogicObjects.SpoilerData> SpoilerLog = Tools.ReadHTMLSpoilerLog("", CDLogic);
            if (SpoilerLog.Count == 0) { return; }

            //For each entry in your logic list, check each entry in your spoiler log to find the rest of the data
            foreach (LogicObjects.LogicEntry entry in CDLogic.Logic)
            {
                foreach (LogicObjects.SpoilerData spoiler in SpoilerLog)
                {
                    if (spoiler.LocationID == entry.ID)
                    {
                        entry.IsFake = false;
                        entry.LocationName = spoiler.LocationName;
                        entry.SpoilerLocation = spoiler.LocationName;
                        entry.LocationArea = spoiler.LocationArea;
                        entry.ItemSubType = "Item";
                        if (entry.DictionaryName.Contains("Bottle:")) { entry.ItemSubType = "Bottle"; }
                        if (entry.DictionaryName.StartsWith("Entrance")) { entry.ItemSubType = "Entrance"; }

                        if (!CDLogic.EntranceRando)
                        {
                            if (entry.DictionaryName == "Woodfall Temple access")
                            { entry.LocationArea = "Dungeon Entrance"; entry.ItemSubType = "Dungeon Entrance"; }
                            if (entry.DictionaryName == "Snowhead Temple access")
                            { entry.LocationArea = "Dungeon Entrance"; entry.ItemSubType = "Dungeon Entrance"; }
                            if (entry.DictionaryName == "Great Bay Temple access")
                            { entry.LocationArea = "Dungeon Entrance"; entry.ItemSubType = "Dungeon Entrance"; }
                            if (entry.DictionaryName == "Inverted Stone Tower Temple access")
                            { entry.LocationArea = "Dungeon Entrance"; entry.ItemSubType = "Dungeon Entrance"; }
                        } //Dungeon Entrance Rando is dumb
                    }
                    if (spoiler.ItemID == entry.ID)
                    {
                        entry.IsFake = false; //Not necessary, might cause problem but also might fix them ¯\_(ツ)_/¯
                        entry.ItemName = spoiler.ItemName;
                        entry.SpoilerItem = spoiler.ItemName;
                    }
                }
            }

            List<string> csv = new List<string> { "DictionaryName,LocationName,ItemName,LocationArea,ItemSubType,SpoilerLocation,SpoilerItem" };
            //Write this data to list of strings formated as lines of csv and write that to a text file
            foreach (LogicObjects.LogicEntry entry in CDLogic.Logic)
            {
                if (!entry.IsFake)
                {
                    csv.Add(string.Format("{0},{1},{2},{3},{4},{5},{6}",
                         entry.DictionaryName, entry.LocationName, entry.ItemName, entry.LocationArea,
                         entry.ItemSubType, entry.SpoilerLocation, entry.SpoilerItem));
                }
            }

            SaveFileDialog saveDic = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                Title = "Save Dictionary File",
                FileName = "MMRDICTIONARYV" + LogicVersion + ".csv"
            };
            saveDic.ShowDialog();
            File.WriteAllLines(saveDic.FileName, csv);
        }
        public static List<LogicObjects.SpoilerData> ReadTextSpoilerlog(string Path, LogicObjects.TrackerInstance instance)
        {
            List<LogicObjects.SpoilerData> SpoilerData = new List<LogicObjects.SpoilerData>();

            if (Path == "")
            {
                OpenFileDialog SpoilerFile = new OpenFileDialog
                {
                    Title = "Select A Logic File",
                    Filter = "Text Spoiler Log (*.txt)|*.txt",
                    FilterIndex = 1,
                    Multiselect = false
                };
                if (SpoilerFile.ShowDialog() != DialogResult.OK) { return SpoilerData; }
                Path = SpoilerFile.FileName;
            }
            List<int> usedId = new List<int>();
            foreach (string line in File.ReadLines(Path))
            {
                LogicObjects.SpoilerData entry = new LogicObjects.SpoilerData();
                if (line.Contains("->"))
                {
                    var linedata = line.Split(new string[] { "->" }, StringSplitOptions.None);
                    linedata[0] = linedata[0].Replace("*", "");//Not sure if this is neccassary but I'm to
                    linedata[1] = linedata[1].Replace("*", "");//lazy to check and it's not hurting anything
                    entry.LocationName = linedata[0].Trim();
                    entry.ItemName = linedata[1].Trim();
                    entry.LocationID = -2;
                    entry.ItemID = -2;
                    bool itemfound = false;
                    foreach (LogicObjects.LogicEntry X in instance.Logic)
                    {
                        if (X.SpoilerLocation == entry.LocationName) { entry.LocationID = X.ID; }
                        if (X.SpoilerItem == entry.ItemName && !usedId.Contains(X.ID) && !itemfound)
                        { entry.ItemID = X.ID; usedId.Add(X.ID); itemfound = true; }
                    }
                    SpoilerData.Add(entry);
                }
            }
            return SpoilerData;
        }
        public static List<LogicObjects.SpoilerData> ReadHTMLSpoilerLog(string Path, LogicObjects.TrackerInstance Instance)
        {
            List<LogicObjects.SpoilerData> SpoilerData = new List<LogicObjects.SpoilerData>();

            if (Path == "")
            {
                OpenFileDialog SpoilerFile = new OpenFileDialog
                {
                    Title = "Select an HTML Spoiler Log",
                    Filter = "HTML Spoiler Log (*.html)|*.html",
                    FilterIndex = 1,
                    Multiselect = false
                };
                if (SpoilerFile.ShowDialog() != DialogResult.OK) { return SpoilerData; }
                Path = SpoilerFile.FileName;
            }

            string Region = "";
            LogicObjects.SpoilerData entry = new LogicObjects.SpoilerData();
            foreach (string line in File.ReadAllLines(Path))
            {
                if (line.Contains("<tr class=\"region\">"))
                {
                    Region = line.Trim();
                    Region = Region.Replace("<tr class=\"region\"><td colspan=\"3\">", "");
                    Region = Region.Replace("</td></tr>", "");
                }
                if (line.Contains("data-newlocationid="))
                {
                    var X = line.Trim().Split('"');
                    entry.LocationID = Int32.Parse(X[3]);
                    entry.ItemID = Int32.Parse(X[1]);
                }
                if (line.Contains("<td class=\"newlocation\">"))
                {
                    var X = line.Trim();
                    X = X.Replace("<td class=\"newlocation\">", "");
                    X = X.Replace("</td>", "");
                    entry.LocationName = X;
                }
                if (line.Contains("<td class=\"spoiler itemname\"><span data-content=\"") || line.Contains("<td class=\"spoiler itemname\"> <span data-content=\""))
                {
                    var X = line.Trim();
                    X = X.Replace("<td class=\"spoiler itemname\"> <span data-content=\"", "");
                    X = X.Replace("<td class=\"spoiler itemname\"><span data-content=\"", "");
                    X = X.Replace("\"></span></td>", "");
                    entry.ItemName = X;
                    entry.LocationArea = Region;
                    SpoilerData.Add(entry);
                    entry = new LogicObjects.SpoilerData();
                }
                if (line.Contains("<h2>Item Locations</h2>")) { break; }
            }

            if (Instance.EntranceRando) { return SpoilerData; }

            //Fix Dungeon Entrances
            Dictionary<string, int> EntIDMatch = new Dictionary<string, int>();
            var entranceIDs = Instance.EntranceAreaDic;
            foreach (LogicObjects.SpoilerData Thing in SpoilerData)
            {
                if (entranceIDs.ContainsValue(Thing.ItemID)) { EntIDMatch.Add(Thing.ItemName, Thing.ItemID); }
            }
            foreach (LogicObjects.SpoilerData Thing in SpoilerData)
            {
                if (EntIDMatch.ContainsKey(Thing.LocationName)) { Thing.LocationID = EntIDMatch[Thing.LocationName]; }
            }

            return SpoilerData;
        }
        public static bool SaveInstance(LogicObjects.TrackerInstance Instance)
        {
            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "MMR Tracker Save (*.MMRTSAV)|*.MMRTSAV", FilterIndex = 1 };
            if (saveDialog.ShowDialog() != DialogResult.OK) { return false; }
            Instance.UnsavedChanges = false;
            //Clear the undo and redo list because otherwise the save file is massive
            var SaveInstance = Utility.CloneTrackerInstance(Instance);
            SaveInstance.UndoList.Clear();
            SaveInstance.RedoList.Clear();
            File.WriteAllText(saveDialog.FileName, JsonConvert.SerializeObject(SaveInstance));
            return true;
        }
        public static void LoadInstance()
        {
            if (!Tools.PromptSave(LogicObjects.MainTrackerInstance)) { return; }
            string file = Utility.FileSelect("Select A Save File", "MMR Tracker Save (*.MMRTSAV)|*.MMRTSAV");
            if (file == "") { return; }
            var backup = Utility.CloneTrackerInstance(LogicObjects.MainTrackerInstance);
            LogicObjects.MainTrackerInstance = new LogicObjects.TrackerInstance();
            //Try to load the save file with the new system. If that fails try wth the old system. If that fails restore the current instance and show an error.
            try { LogicObjects.MainTrackerInstance = JsonConvert.DeserializeObject<LogicObjects.TrackerInstance>(File.ReadAllText(file)); }
            catch 
            {
                try
                {
                    string[] options = File.ReadAllLines(file);
                    LogicObjects.MainTrackerInstance.Logic = JsonConvert.DeserializeObject<List<LogicObjects.LogicEntry>>(options[0]);
                    if (options.Length > 1) { LogicObjects.MainTrackerInstance.LogicVersion = Int32.Parse(options[1].Replace("version:", "")); }
                    else
                    {
                        MessageBox.Show("Save File Invalid!");
                        LogicObjects.MainTrackerInstance = backup;
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("Save File Invalid!");
                    LogicObjects.MainTrackerInstance = backup;
                    return;
                }
            }
            //Extra saftey checks for older save files
            LogicObjects.MainTrackerInstance.EntranceRando = LogicObjects.MainTrackerInstance.IsEntranceRando();
            if (LogicObjects.MainTrackerInstance.LogicVersion == 0)
            {
                LogicObjects.MainTrackerInstance.LogicVersion = VersionHandeling.GetVersionFromLogicFile(LogicObjects.MainTrackerInstance.RawLogicFile).Version;
            }
            return;
        }
        public static void SaveState(LogicObjects.TrackerInstance Instance, List<LogicObjects.LogicEntry> Logic = null )
        {
            if (Logic == null) { Logic = Instance.Logic; }
            Instance.UndoList.Add(Utility.CloneLogicList(Logic));
            if (Instance.UndoList.Count() > 50) { Instance.UndoList.RemoveAt(0); }
            Instance.RedoList = new List<List<LogicObjects.LogicEntry>>();
            //(Application.OpenForms["FRMTracker"] as FRMTracker).EnableUndoRedo(true, false);
        }
        public static void Undo(LogicObjects.TrackerInstance Instance)
        {
            if (Instance.UndoList.Any())
            {
                Instance.UnsavedChanges = true;
                var lastItem = Instance.UndoList.Count - 1;
                Instance.RedoList.Add(Utility.CloneLogicList(Instance.Logic));
                Instance.Logic = Utility.CloneLogicList(Instance.UndoList[lastItem]);
                Instance.UndoList.RemoveAt(lastItem);
            }
        }
        public static void Redo(LogicObjects.TrackerInstance Instance)
        {
            if (Instance.RedoList.Any())
            {
                Instance.UnsavedChanges = true;
                var lastItem = Instance.RedoList.Count - 1;
                Instance.UndoList.Add(Utility.CloneLogicList(Instance.Logic));
                Instance.Logic = Utility.CloneLogicList(Instance.RedoList[lastItem]);
                Instance.RedoList.RemoveAt(lastItem);
            }
        }
        public static bool PromptSave(LogicObjects.TrackerInstance Instance, bool OnlyIfUnsaved = true)
        {
            if (Instance.UnsavedChanges || !OnlyIfUnsaved)
            {
                DialogResult result = MessageBox.Show("Would you like to save?", "You have unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel) { return false; }
                if (result == DialogResult.Yes)
                {
                    if (!SaveInstance(Instance)) { return false; }
                }
            }
            return true;
        }
        public static void UpdateNames(LogicObjects.TrackerInstance Instance)
        {
            string[] VersionData = VersionHandeling.GetDictionaryPath(Instance);
            if (VersionData.Count() > 0)
            {
                Instance.LogicDictionary = JsonConvert.DeserializeObject<List<LogicObjects.LogicDictionaryEntry>>(Utility.ConvertCsvFileToJsonObject(VersionData[0]));
            }
            foreach (var entry in Instance.Logic)
            {
                foreach (var dicent in Instance.LogicDictionary)
                {
                    if (entry.DictionaryName == dicent.DictionaryName)
                    {
                        entry.LocationName = dicent.LocationName;
                        entry.ItemName = dicent.ItemName;
                        entry.LocationArea = dicent.LocationArea;
                        entry.ItemSubType = dicent.ItemSubType;
                        entry.SpoilerItem = dicent.SpoilerItem;
                        entry.SpoilerLocation = dicent.SpoilerLocation;
                        break;
                    }
                }
            }
        }
        public static Dictionary<int, int> CreateRandItemDic(List<LogicObjects.LogicEntry> logic, bool Spoiler = false)
        {
            var spoilerDic = new Dictionary<int, int>();
            foreach (var i in logic)
            {
                var value = (Spoiler) ? i.SpoilerRandom : i.RandomizedItem;
                if (value > -2 && !spoilerDic.ContainsKey(value))
                {
                    spoilerDic.Add(value, i.ID);
                }
            }
            return spoilerDic;
        }
        public static void CreateOptionsFile()
        {
            if (!File.Exists("options.txt"))
            {
                List<string> options = new List<string>(); 
                var file = File.Create("options.txt");
                file.Close();

                if (!Debugging.ISDebugging || (Control.ModifierKeys == Keys.Shift))
                {
                    var firsttime = MessageBox.Show("Welcome to the Majoras Mask Randomizer Tracker by thedrummonger! It looks like this is your first time running the tracker. If so select Yes. Otherwise, select No.", "First Time Setup", MessageBoxButtons.YesNo);
                    if (firsttime == DialogResult.Yes)
                    {
                        MessageBox.Show("Please Take this opportunity to familliarize yourself with how to use this tracker. There are many features that are not obvious or explained anywhere outside of the about page. This information can be accessed at any time by selecting 'Info' -> 'About'. Click OK to show the About Page. Once you have read through the information, close the window to return to setup.", "How to Use", MessageBoxButtons.OK);
                        InformationDisplay DebugScreen = new InformationDisplay();
                        DebugScreen.DebugFunction = 2;
                        DebugScreen.ShowDialog();
                    }

                    var DefaultSetting = MessageBox.Show("If you would like to change the default options, press Yes. Otherwise, press No. Selecting Cancel at an option prompt will leave it default. These can be changed later in the Options text document that will be created in your tracker folder. The can also be changed per instance in the options tab", "Default Setting", MessageBoxButtons.YesNo);
                    if (DefaultSetting == DialogResult.Yes) 
                    {
                        var ShowToolTips = MessageBox.Show("Would you like to see tooltips that display the full name of an item when you mouse over it?", "Show Tool Tips", MessageBoxButtons.YesNoCancel);
                        if (ShowToolTips == DialogResult.No) { options.Add("ToolTips:0"); }
                        else { options.Add("ToolTips:1"); }

                        var SeperateMArkedItems = MessageBox.Show("Would you like Marked locations to be moved to the bottom of the list?", "Show Tool Tips", MessageBoxButtons.YesNoCancel);
                        if (SeperateMArkedItems == DialogResult.Yes) { options.Add("SeperateMarked:1"); }
                        else { options.Add("SeperateMarked:0"); }

                        var MiddleClickFunction = MessageBox.Show("Would you like Middle Click to star a location instead of Setting it? Staring a location will allow you to mark it as important so you can easily refrence it later. Setting a location will add what item or entrance is placed at that location. (Yes: Star, No: Set)", "Star or Set", MessageBoxButtons.YesNoCancel);
                        if (MiddleClickFunction == DialogResult.Yes) { options.Add("MiddleClickFunction:1"); }
                        else { options.Add("MiddleClickFunction:0"); }

                        var DisableEntrances = MessageBox.Show("Would you like the tracker to automatically mark entrances as unrandomized when creating an instance? This is usefull if you don't plan to use entrance randomizer often.", "Start with Entrance Rando", MessageBoxButtons.YesNoCancel);
                        if (DisableEntrances == DialogResult.No) { options.Add("DisableEntrancesOnStartup:0"); }
                        else { options.Add("DisableEntrancesOnStartup:1"); }

                        var UpdateCheck = MessageBox.Show("Would you like the tracker to notify you when a newer version has been released?", "Check For Updates", MessageBoxButtons.YesNoCancel);
                        if (UpdateCheck == DialogResult.No) { options.Add("CheckForUpdates:0"); }
                        else { options.Add("CheckForUpdates:1"); }
                    }
                }
                else
                {
                    options.Add("ToolTips:1");
                    options.Add("SeperateMarked:0");
                    options.Add("DisableEntrancesOnStartup:0");
                    options.Add("CheckForUpdates:0");
                }
                File.WriteAllLines("options.txt", options);
            }
        }
        public static List<int> ResolveFakeToRealItems(LogicObjects.PlaythroughItem item, List<LogicObjects.PlaythroughItem> Playthrough, List<LogicObjects.LogicEntry> logic)
        {
            var RealItems = new List<int>();
            var New = new LogicObjects.PlaythroughItem();
            foreach (var j in item.ItemsUsed)
            {
                if (!logic[j].IsFake) { RealItems.Add(j); }
                else
                {
                    var NewItem = Playthrough.Find(i => i.Check.ID == j);
                    foreach (var k in ResolveFakeToRealItems(NewItem, Playthrough, logic)) { RealItems.Add(k); }
                }
            }
            return RealItems;
        }
        public static List<int> FindAllFakeItems(LogicObjects.PlaythroughItem item, List<LogicObjects.PlaythroughItem> Playthrough, List<LogicObjects.LogicEntry> logic)
        {
            var FakeItems = new List<int>();
            var New = new LogicObjects.PlaythroughItem();
            foreach (var j in item.ItemsUsed)
            {
                if (logic[j].IsFake)
                {
                    FakeItems.Add(j);
                    var NewItem = Playthrough.Find(i => i.Check.ID == j);
                    foreach (var k in FindAllFakeItems(NewItem, Playthrough, logic)) { FakeItems.Add(k); }
                }
            }
            return FakeItems;
        }
        public static bool SameItemMultipleChecks(int item, LogicObjects.TrackerInstance Instance)
        {
            if (item < 0 || !Instance.Options.StrictLogicHandeling) { return false; }
            int count = 0;
            foreach (var entry in Instance.Logic)
            {
                if (entry.RandomizedItem == item && entry.Checked) { count += 1; }
            }
            return count > 1;
        }
        public static void CreateTrackerInstance(LogicObjects.TrackerInstance Instance, string[] RawLogic)
        {
            Instance.RawLogicFile = RawLogic;
            LogicEditing.PopulateTrackerInstance(Instance);
            LogicEditing.CalculateItems(Instance);


            if (!Instance.IsMM())
            {
                DialogResult dialogResult = MessageBox.Show("This logic file was NOT created for the Majoras Mask Randomizer. While this tracker can support other games, support is very Limited. Many features will be disabled and core features might not work as intended. Do you wish to continue?", "Other Randomizer", MessageBoxButtons.YesNo);
                if (dialogResult != DialogResult.Yes) { Instance = new LogicObjects.TrackerInstance(); return; }
            }
            else if (!VersionHandeling.ValidVersions.Contains(Instance.LogicVersion))
            {
                DialogResult dialogResult = MessageBox.Show("This version of logic is not supported. Only official releases of versions 1.8 and up are supported. This may result in the tracker not funtioning Correctly. If you are using an official release and are seeing this message, Please update your tracker. Do you wish to continue?", "Unsupported Version", MessageBoxButtons.YesNo);
                if (dialogResult != DialogResult.Yes) { Instance = new LogicObjects.TrackerInstance(); return; }
            }
            if (File.Exists("options.txt"))
            {
                foreach (var i in File.ReadAllLines("options.txt"))
                {
                    if (i.Contains("ToolTips:0")) { Instance.Options.ShowEntryNameTooltip = false; }
                    if (i.Contains("SeperateMarked:1")) { Instance.Options.MoveMarkedToBottom = true; }
                    if (i.Contains("DisableEntrancesOnStartup:0")) { Instance.Options.UnradnomizeEntranesOnStartup = false; }
                    if (i.Contains("CheckForUpdates:0")) { Instance.Options.CheckForUpdate = false; }
                    if (i.Contains("MiddleClickFunction:1")) { Instance.Options.MiddleClickStarNotMark = true; }
                }
            }
        }
        public static void WhatUnlockedThis()
        {
            var Requirements = Tools.FindRequirements(Tools.CurrentSelectedItem, LogicObjects.MainTrackerInstance.Logic).ResolvedRealItems;
            var FakeItems = Tools.FindRequirements(Tools.CurrentSelectedItem, LogicObjects.MainTrackerInstance.Logic).FakeItems;
            var Playthrough = Tools.FindRequirements(Tools.CurrentSelectedItem, LogicObjects.MainTrackerInstance.Logic).playthrough;
            var ItemsUsed = Tools.FindRequirements(Tools.CurrentSelectedItem, LogicObjects.MainTrackerInstance.Logic).UsedItems;
            string message = "Logic Entries used:\n";
            if (Requirements.Count == 0)
            {
                MessageBox.Show("Nothing is needed to check this location.", Tools.CurrentSelectedItem.LocationName + " Was Unlocked with:");
                return;
            }
            foreach (var i in ItemsUsed) { message = message + (LogicObjects.MainTrackerInstance.Logic[i].ItemName ?? LogicObjects.MainTrackerInstance.Logic[i].DictionaryName) + "\n"; }
            message = message + "\nReal items used:\n";
            foreach (var i in Requirements) { message = message + LogicObjects.MainTrackerInstance.Logic[i].ItemName + "\n"; }
            message = message + "\nFake Items Breakdown:\n";
            foreach (var i in FakeItems.OrderBy(x => LogicObjects.MainTrackerInstance.Logic[x].DictionaryName)) 
            {
                var ItemInPlaythrough = Playthrough.Find(x => x.Check.ID == i) ?? new LogicObjects.PlaythroughItem { ItemsUsed = new List<int>() };
                message = message + LogicObjects.MainTrackerInstance.Logic[i].DictionaryName + "\n";
                message = message + WhatUnlockedThisDetailed(i, "", Playthrough, ItemInPlaythrough.ItemsUsed);
            }
            InformationDisplay Display = new InformationDisplay();
            Display.Text = Tools.CurrentSelectedItem.LocationName + " Was Unlocked with:";
            InformationDisplay.Playthrough = message.Split( new[] { "\n" }, StringSplitOptions.None).ToList();
            Display.DebugFunction = 4;
            Display.Show();
            Tools.CurrentSelectedItem = new LogicObjects.LogicEntry();
        }
        public static string WhatUnlockedThisDetailed(int i, String In, List<LogicObjects.PlaythroughItem> Playthorugh, List<int> TopLevel)
        {
            string Message = "";
            string Indent = In + "      ";
            var check = Playthorugh.Find(x => x.Check.ID == i);
            foreach(var j in check.ItemsUsed.OrderBy(x => LogicObjects.MainTrackerInstance.Logic[x].DictionaryName))
            {
                if (LogicObjects.MainTrackerInstance.Logic[j].IsFake)
                {
                    Message = Message + Indent + LogicObjects.MainTrackerInstance.Logic[j].DictionaryName + "\n";
                    if (!TopLevel.Contains(j))
                    {
                        Message = Message + WhatUnlockedThisDetailed(j, Indent, Playthorugh, TopLevel);
                    }
                }
                else
                {
                    Message = Message + Indent + LogicObjects.MainTrackerInstance.Logic[j].ItemName + "\n";
                }
            }
            return Message;
        }
        public static List<int> ParseSettingString(string c)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(c))
            {
                return result;
            }
            try
            {
                result.Clear();
                string[] Sections = c.Split('-');
                int[] NewSections = new int[13];
                if (Sections.Length != NewSections.Length) { return null; }
                for (int i = 0; i < 13; i++)
                {
                    if (Sections[12 - i] != "") { NewSections[i] = Convert.ToInt32(Sections[12 - i], 16); }
                }
                for (int i = 0; i < 32 * 13; i++)
                {
                    int j = i / 32;
                    int k = i % 32;
                    if (((NewSections[j] >> k) & 1) > 0) { result.Add(i); }
                }
            }
            catch
            {
                return null;
            }
            return result;
        }
        public static void ManageNetData(List<LogicObjects.NetData> Data)
        {
            foreach(var i in Data)
            {
                if (LogicObjects.MainTrackerInstance.Logic.ElementAt(i.ID) != null && !LogicObjects.MainTrackerInstance.Logic[i.ID].Checked)
                {
                    if (!LogicObjects.MainTrackerInstance.Logic[i.ID].HasRandomItem(true) && LogicObjects.MainTrackerInstance.Logic[i.ID].SpoilerRandom < 0)
                    {
                        LogicObjects.MainTrackerInstance.Logic[i.ID].RandomizedItem = i.RandomizedItem;
                    }
                    LogicEditing.CheckObject(LogicObjects.MainTrackerInstance.Logic[i.ID], LogicObjects.MainTrackerInstance);
                }
            }
            LogicEditing.CalculateItems(LogicObjects.MainTrackerInstance);
            NetDataProcessed(null, null);
        }
    }
}
