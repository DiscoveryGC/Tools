using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MadMilkman.Ini;

namespace NoDock
{
    class Program
    {
        private struct BaseStruct
        {
            public string BaseNick;
            public string BaseSystem;
            public string BaseInfocard;
            public string BaseRep;
        }

        private struct FactionFormat
        {
            public string FactionNickname;
            public List<string> OfficialFactionIDs;

            public FactionFormat(string FactionNickname, List<string> OfficialFactions)
            {
                this.FactionNickname = FactionNickname;
                this.OfficialFactionIDs = OfficialFactions;
            }
        }

        private static Dictionary<string, FactionFormat> _factionMap = new Dictionary<string, FactionFormat>();
        private static List<string> _ignoredFactions = new List<string>();

        static void Main(string[] args)
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!File.Exists(dir + @"\EXE\Freelancer.exe"))
            {
                Console.WriteLine("ERR: Could not find Freelancer data. Aborting.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            List<BaseStruct> lstBaseStructs = new List<BaseStruct>();
            List<string> lstFactions = new List<string>();

            var IniOptions = new IniOptions() { SectionDuplicate = IniDuplication.Allowed, KeyDuplicate = IniDuplication.Allowed };
            IniFile factionFile = new IniFile(IniOptions);
            factionFile.Load(@"FactionsMap.ini");
            foreach (var i in factionFile.Sections["Factions"].Keys)
            {
                if (i.Name == "ignore")
                {
                    if (i.Value.Contains(","))
                        _ignoredFactions = i.Value.Split(',').ToList();
                    else
                        _ignoredFactions.Add(i.Value);
                    continue;
                }

                else
                {
                    if (i.TrailingComment == null) continue;
                    if (i.Value.Contains(','))
                    {
                        List<string> split = i.Value.Split(',').ToList();
                        _factionMap[i.LeadingComment.Text.Trim()] = new FactionFormat(i.Name, split);
                    }

                    else
                    {
                        _factionMap[i.LeadingComment.Text.Trim()] = new FactionFormat(i.Name, new List<string>() { i.Value });
                    }
                }
            }

            IniFile universeData = new IniFile(IniOptions);
            universeData.Load(dir + @"\DATA\UNIVERSE\Universe.ini");

            foreach (IniSection section in universeData.Sections)
            {
                if (section.Name.ToLowerInvariant() != "base") continue;
                BaseStruct baseStruct = new BaseStruct() { BaseNick = null, BaseSystem = null};
                foreach (IniKey key in section.Keys)
                {
                    if (key.Name.ToLowerInvariant() == "nickname" && (key.Value.Contains("proxy") || key.Value.Contains("miner"))) break;
                    
                    switch (key.Name.ToLowerInvariant())
                    {
                        case "nickname":
                            baseStruct.BaseNick = key.Value;
                            continue;
                        case "system":
                            baseStruct.BaseSystem = key.Value;
                            continue;
                    }
                }

                if (baseStruct.BaseNick == null || baseStruct.BaseSystem == null)
                    continue;
                lstBaseStructs.Add(baseStruct);
            }

            IniFile MBasesData = new IniFile(IniOptions);
            MBasesData.Load(dir + @"\DATA\MISSIONS\mbases.ini");

            for (var index = 0; index < lstBaseStructs.Count; index++)
            {
                BaseStruct BS = lstBaseStructs[index];
                try
                {
                    IniSection SectionData = MBasesData.Sections.First(sd => sd.Name == "MBase" && sd.Keys.Select
                            (key => key.Name == "nickname" && key.Value == BS.BaseNick).FirstOrDefault());

                    if (SectionData.TrailingComment.Text != null && !SectionData.TrailingComment.Text.Contains("-----------------------"))
                        BS.BaseInfocard = SectionData.TrailingComment.Text;

                    foreach (IniKey key in SectionData.Keys)
                    {
                        if (key.Name == "local_faction")
                        {
                            BS.BaseRep = key.Value;
                            break;
                        }
                    }

                    lstBaseStructs[index] = BS;
                }

                catch (Exception ex)
                {
                    Console.WriteLine("> Possible Null Reference Exception - MBases.ini");
                    Console.WriteLine("> Unable to load in {0}", BS.BaseNick);
                    Console.WriteLine("Exception: {0}", ex.Message);
                    Console.WriteLine();
                    lstBaseStructs.RemoveAt(index); // If we fail, this BaseStruct will be invalid so we should remove it
                    index--; // Make sure we don't skip the next base
                }
            }

            IniFile newConfigFile = new IniFile(IniOptions);
            newConfigFile.Sections.Add("General");
            newConfigFile.Sections["General"].Keys.Add("enabled", "yes");
            newConfigFile.Sections["General"].Keys.Add("debug", "0");
            newConfigFile.Sections["General"].Keys.Add("duration", "120");

            newConfigFile.Sections.Add("Ignored Ships");
            newConfigFile.Sections.Add("ID List");
            foreach (var i in _factionMap)
            {
                if (_ignoredFactions.Contains(i.Value.FactionNickname)) continue;
                newConfigFile.Sections["ID List"].Keys.Add(i.Key);
                newConfigFile.Sections.Add(i.Key);
                newConfigFile.Sections[i.Key].Keys.Add("ID", i.Value.FactionNickname);

                List<BaseStruct> lstBS = lstBaseStructs.Where(x => x.BaseRep == i.Value.FactionNickname.Replace("dsy_license_", "")).ToList();
                foreach (var ii in lstBS)
                {
                    BaseStruct BS = ii;
                    if (BS.BaseRep == null) continue;
                    if (BS.BaseRep.EndsWith("guardian"))
                        BS.BaseRep = BS.BaseRep.Replace("guardian", "grp");

                    WriteToFile(ref newConfigFile, BS, i.Key);
                }

                if (i.Value.OfficialFactionIDs.Count > 0)
                {
                    foreach (var iter in i.Value.OfficialFactionIDs)
                    {
                        string faction = iter.Trim();
                        newConfigFile.Sections.Add(faction);
                        newConfigFile.Sections[faction].Keys.Add("ID", faction).TrailingComment.Text = i.Key;
                        newConfigFile.Sections["ID List"].Keys.Add(faction);
                        foreach (var ii in lstBS)
                        {
                            BaseStruct BS = ii;
                            if (BS.BaseRep == null) continue;
                            if (BS.BaseRep.EndsWith("guardian"))
                                BS.BaseRep = BS.BaseRep.Replace("guardian", "grp");

                            WriteToFile(ref newConfigFile, BS, faction);
                        }
                    }
                }
            }

            /*IniFile SortedConfig = new IniFile(IniOptions);
            foreach (IniSection i in newConfigFile.Sections.OrderBy(s => s.Name))
            {
                SortedConfig.Sections.Add(i.Name);
                foreach (IniKey k in i.Keys)
                    SortedConfig.Sections[i.Name].Keys.Add(k.Name, k.Value);
            }*/

            newConfigFile.Save("laz_nodock.cfg");
        }

        static void WriteToFile(ref IniFile ini, BaseStruct BS, string faction)
        {
            if (BS.BaseInfocard != null)
                ini.Sections[faction].Keys
                    .Add("nodock", BS.BaseNick + " ;" + BS.BaseInfocard);
            else
                ini.Sections[faction].Keys
                    .Add("nodock", BS.BaseNick + " ; " + BS.BaseSystem);
        }
    }
}
