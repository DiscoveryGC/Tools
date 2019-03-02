using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MadMilkman.Ini;

namespace BaseToSolarCntlConverter
{
    using Key = KeyValuePair<string, string>;

    // Can I quickly point out that I'd never normally store everything in strings!
    // Only doing this because I need to store them and both the input and output are strings
    // So converting them back and forth would be a waste of time. *looks at Raisu with devil eyes*
    public struct MarketItem
    {
        public string Good;
        public string Quantity;
        public string Price;
        public string MinStock;
        public string MaxStock;
        public string BuySellState;
    }

    public struct Password
    {
        public string Pass;
        public string Admin;
        public string Viewshop;
        public string Manageshop;
        public string Managefactory;
    }

    class Program
    {
        private static List<Password> lstPasswords = new List<Password>();
        private static List<MarketItem> lstItems = new List<MarketItem>();
        private static uint[] createIDTable = null;

        // Credit to CreateID function goes to Cannon
        public static uint CreateID(string nickName)
        {
            const uint FLHASH_POLYNOMIAL = 0xA001;
            const int LOGICAL_BITS = 30;
            const int PHYSICAL_BITS = 32;

            // Build the crc lookup table if it hasn't been created
            if (createIDTable == null)
            {
                createIDTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint x = i;
                    for (uint bit = 0; bit < 8; bit++)
                        x = ((x & 1) == 1) ? (x >> 1) ^ (FLHASH_POLYNOMIAL << (LOGICAL_BITS - 16)) : x >> 1;
                    createIDTable[i] = x;
                }
                if (2926433351 != CreateID("st01_to_st03_hole")) throw new Exception("Create ID hash algoritm is broken!");
                if (2460445762 != CreateID("st02_to_st01_hole")) throw new Exception("Create ID hash algoritm is broken!");
                if (2263303234 != CreateID("st03_to_st01_hole")) throw new Exception("Create ID hash algoritm is broken!");
                if (2284213505 != CreateID("li05_to_li01")) throw new Exception("Create ID hash algoritm is broken!");
                if (2293678337 != CreateID("li01_to_li05")) throw new Exception("Create ID hash algoritm is broken!");
            }

            byte[] tNickName = Encoding.ASCII.GetBytes(nickName.ToLowerInvariant());

            // Calculate the hash.
            uint hash = 0;
            for (int i = 0; i < tNickName.Length; i++)
                hash = (hash >> 8) ^ createIDTable[(byte)hash ^ tNickName[i]];
            // b0rken because byte swapping is not the same as bit reversing, but 
            // that's just the way it is; two hash bits are shifted out and lost
            hash = (hash >> 24) | ((hash >> 8) & 0x0000FF00) | ((hash << 8) & 0x00FF0000) | (hash << 24);
            hash = (hash >> (PHYSICAL_BITS - LOGICAL_BITS)) | 0x80000000;

            return hash;
        }

        public static string ParseHexToString(string hex)
        {
            if (hex.Length % 4 != 0)
                throw new ArgumentException("Length of hex string must be a multiple of 4", nameof(hex));

            StringBuilder result = new StringBuilder(hex.Length / 4);
            for (int i = 0; i < hex.Length; i += 4)
            {
                string codeUnitHex = hex.Substring(i, 4);
                if (!ushort.TryParse(codeUnitHex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codeUnit))
                    throw new ArgumentException("Hex string contains non-hex character(s)", nameof(hex));

                result.Append((char)codeUnit);
            }

            return result.ToString();
        }

        public static void Main(string[] args)
        {
            
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\Freelancer\Accts\Multiplayer";
            List<string> lstFiles = Directory.GetFiles(dir + @"\player_bases", "base_*.ini").ToList();
            var IniOptions = new IniOptions()
            {
                SectionDuplicate = IniDuplication.Allowed,
                KeyDuplicate = IniDuplication.Allowed,
                KeySpaceAroundDelimiter = true,
            };
            
            IniFile thingForXal = new IniFile(IniOptions);
            thingForXal.Load(@"D:\Games\Dev Build\DATA\initialworld.ini");
            Dictionary<string, uint> stuff = new Dictionary<string, uint>();

            foreach (var i in thingForXal.Sections)
            {
                if (i.Name == "Group")
                {
                    var aa = "";
                    uint bb = 0;

                    foreach (var ii in i.Keys)
                    {
                        if (ii.Name == "nickname")
                        {
                            aa = ii.Value;
                            bb = CreateID(ii.Value);
                        }
                    }

                    if (bb != 0)
                        stuff[aa] = bb;
                }
            }

            thingForXal = new IniFile(IniOptions);
            foreach (var fuck in stuff)
            {
                thingForXal.Sections.Add("faction or something");
                thingForXal.Sections[thingForXal.Sections.Count - 1].Keys.Add("nickname", fuck.Key);
                thingForXal.Sections[thingForXal.Sections.Count - 1].Keys.Add("hash", fuck.Value.ToString());
            }

            thingForXal.Save(@"FactionThingForXal.ini");

            foreach (var a in lstFiles)
            {
                // Please note this will still include the underscore. The format is *double* underscore
                // pob_std__*
                string fileName = "pob_std_" + Path.GetFileName(a).Substring(4);
                try
                {
                    IniFile newIniFile = new IniFile(IniOptions);
                    IniFile baseData = new IniFile(IniOptions);
                    baseData.Load(a);

                    Console.WriteLine("Building INI File: {0}", a);
                    
                    int iUpgradeLevel = 0;
                    string pos = "0, 0, 0";
                    int iSection = 0;
                    
                    foreach (IniSection section in baseData.Sections)
                    {
                        if (section.Name == "Base")
                        {
                            newIniFile.Sections.Add("Standard Playerbase");
                            iSection = newIniFile.Sections.Count - 1;
                            string baseName = "0";
                            foreach (IniKey key in section.Keys)
                            {
                                switch (key.Name)
                                {
                                    case "nickname":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("nickname", "std" + key.Value.Substring(2)));
                                        newIniFile.Sections[iSection].Keys.Add(new Key("nickname_readable", key.Value.Substring(3)));
                                        Console.WriteLine("Base Name: {0}", key.Value.Substring(3));
                                        Console.WriteLine("Please enter the system nickname where this base is present.");
                                        Console.WriteLine("(Please note, if this is wrong it will render the base unusable)");
                                        baseName = Console.ReadLine();
                                        break;
                                    // We skip base solar and base loadout because of the way it's handled
                                    case "basetype":
                                        if (key.Value == "legacy")
                                            newIniFile.Sections[iSection].Keys.Add(new Key("class", "Legacy"));
                                        break;
                                    case "upgrade":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("base_level", key.Value));
                                        newIniFile.Sections[iSection].Keys.Add(new Key("can_upgrade_beyond_max_core_level", "false"));
                                        iUpgradeLevel = Convert.ToInt32(key.Value);
                                        break;
                                    case "affilation":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("affiliation", key.Value));
                                        break;
                                    case "system":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("system", key.Value));
                                        break;
                                    case "invulnerable":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("invulnerable", key.Value));
                                        break;
                                    case "money":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("money", key.Value));
                                        break;
                                    case "pos":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("pos", key.Value));
                                        pos = key.Value;
                                        break;
                                    case "rot":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("rot", key.Value));
                                        break;
                                    case "infoname":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("infoname", key.Value));
                                        break;
                                    case "infocardpara":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("infocardpara", key.Value));
                                        break;
                                    case "health":
                                        newIniFile.Sections[iSection].Keys.Insert(6, new Key("currenthealth", key.Value));
                                        newIniFile.Sections[iSection].Keys.Insert(7, new Key("maximumhealth", key.Value)); // We assume that they are at full health
                                        break;
                                    case "defensemode":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("defensemode", key.Value));
                                        break;
                                    case "ally_tag":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("ally", ParseHexToString(key.Value)));
                                        break;
                                    case "hostile_tag":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("hostile", ParseHexToString(key.Value)));
                                        break;
                                    case "passwd":
                                        Password password = new Password();
                                        string passString = ParseHexToString(key.Value);
                                        if (passString.Contains(" "))
                                        {
                                            password.Pass = passString.Substring(0, passString.IndexOf(" ", StringComparison.Ordinal));
                                            password.Admin = "0";
                                            password.Viewshop = "1";
                                            password.Managefactory = "0";
                                            password.Manageshop = "0";
                                        }
                                        else
                                        {
                                            password.Pass = passString;
                                            password.Admin = "1";
                                            password.Viewshop = "0";
                                            password.Managefactory = "0";
                                            password.Manageshop = "0";
                                        }

                                        lstPasswords.Add(password);
                                        break;
                                    case "commodity":
                                        string val = key.Value;
                                        Regex.Replace(val, @"\s+", "");
                                        var arr = val.Split(',');

                                        MarketItem mi = new MarketItem();
                                        mi.Good = arr[0];
                                        mi.Quantity = arr[1];
                                        mi.Price = arr[2];
                                        mi.MinStock = arr[3];
                                        mi.MaxStock = arr[4];
                                        mi.BuySellState = "3";

                                        lstItems.Add(mi);
                                        break;
                                }
                            }

                            newIniFile.Sections[iSection].Keys.Add(new Key("proxybase", Convert.ToString(CreateID(baseName + "_proxy_base"))));
                        }

                        else if (section.Name == "ShieldModule")
                        {
                            newIniFile.Sections.Add("Standard Shield");
                            iSection = newIniFile.Sections.Count - 1;
                            newIniFile.Sections[iSection].Keys.Add(new Key("time_until_deactivation", "120"));
                        }

                        else if (section.Name == "StorageModule")
                        {
                            newIniFile.Sections.Add("Storage Module");
                            iSection = newIniFile.Sections.Count - 1;
                            newIniFile.Sections[iSection].Keys.Add(new Key("name", "Standard Storage Module"));
                            newIniFile.Sections[iSection].Keys.Add(new Key("pos", pos));
                            newIniFile.Sections[iSection].Keys.Add(new Key("rot", "0, 0, 0"));
                        }

                        else if (section.Name == "DefenseModule")
                        {
                            newIniFile.Sections.Add("DefenceModule");
                            iSection = newIniFile.Sections.Count - 1;
                            foreach (var key in section.Keys)
                            {
                                switch (key.Name)
                                {
                                    case "type":
                                        switch (key.Value)
                                        {
                                            case "1":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("name", "Standard Station Defence Array"));
                                                break;
                                            case "2":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("name", "Heavy Station Defence Array"));
                                                break;
                                            case "3":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("name", "Light Station Defence Array"));
                                                break;
                                        }
                                        break;
                                    case "pos":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("pos", key.Value));
                                        break;
                                    case "rot":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("rot", key.Value));
                                        break;
                                }
                            }
                        }

                        else if (section.Name == "BuildModule")
                        {
                            newIniFile.Sections.Add("Building Module");
                            iSection = newIniFile.Sections.Count - 1;
                            foreach (var key in section.Keys)
                            {
                                switch (key.Name)
                                {
                                    case "build_type":
                                        switch (key.Value)
                                        {
                                            case "1":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Base Core Upgrade"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "2"));
                                                break;
                                            case "2":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Standard Shield Module"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "5"));
                                                break;
                                            case "3":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Standard Storage Module"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "1"));
                                                break;
                                            case "4":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Standard Station Defence Array"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "3"));
                                                break;
                                            case "5":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Docking Module Production Chamber"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "6"));
                                                break;
                                            case "6":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Jump Drive Assembly Line"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "6"));
                                                break;
                                            case "7":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Hyperspace Scanner Fabrication Plant"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "6"));
                                                break;
                                            case "8":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Cloaking Device Production Modulee"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "6"));
                                                break;
                                            case "9":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Heavy Station Defence Array"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "3"));
                                                break;
                                            case "10":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Light Station Defence Array"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "3"));
                                                break;
                                            case "11":
                                                newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Cloak Disrupter Manufactory"));
                                                newIniFile.Sections[iSection].Keys.Add(new Key("target_type", "6"));
                                                break;
                                        }
                                        break;
                                    case "consumed":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("required_material", key.Value));
                                        break;
                                }

                            }
                        }

                        else if (section.Name == "FactoryModule")
                        {
                            newIniFile.Sections.Add("Factory Module");
                            iSection = newIniFile.Sections.Count - 1;
                            foreach (var key in section.Keys)
                            {
                                if (key.Name != "type") continue;
                                switch (key.Value)
                                {
                                    case "5":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("alias","Docking Module Production Chamber"));
                                        break;
                                    case "6":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Jump Drive Assembly Line"));
                                        break;
                                    case "7":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Hyperspace Scanner Fabrication Plant"));
                                        break;
                                    case "8":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Cloaking Device Production Module"));
                                        break;
                                    case "11":
                                        newIniFile.Sections[iSection].Keys.Add(new Key("alias", "Cloak Disrupter Manufactory"));
                                        break;
                                }
                                break;
                            }
                        }
                    }

                    // This is why we stored the value earlier. 
                    switch (Convert.ToString(iUpgradeLevel))
                    {
                        case "1":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_01"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_01"));
                            break;
                        case "2":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_02"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_02"));
                            break;
                        case "3":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_03"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_03"));
                            break;
                        case "4":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_03"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_04"));
                            break;
                        case "5":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_05"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_05"));
                            break;
                        case "6":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_06"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_06"));
                            break;
                        case "7":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_07"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_07"));
                            break;
                        case "8":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_08"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_08"));
                            break;
                        case "9":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_09"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_09"));
                            break;
                        case "10":
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(2, new Key("objsolar", "dsy_playerbase_10"));
                            newIniFile.Sections["Standard Playerbase"].Keys.Insert(3, new Key("objloadout", "dsy_playerbase_10"));
                            break;
                    }

                    // Loop over all the different items we stored and add them (did it this way so we didn't mess up the previous section)
                    foreach (var i in lstItems)
                    {
                        newIniFile.Sections.Add("Market Item");
                        iSection = newIniFile.Sections.Count - 1;
                        newIniFile.Sections[iSection].Keys.Add(new Key("good", i.Good));
                        newIniFile.Sections[iSection].Keys.Add(new Key("quantity", i.Quantity));
                        newIniFile.Sections[iSection].Keys.Add(new Key("price", i.Price));
                        newIniFile.Sections[iSection].Keys.Add(new Key("min_stock", i.MinStock));
                        newIniFile.Sections[iSection].Keys.Add(new Key("max_stock", i.MaxStock));
                        newIniFile.Sections[iSection].Keys.Add(new Key("buysellstate", i.BuySellState));
                    }

                    // Loop over all the passwords and add them in (in the new flashy more features format) :3
                    foreach (var i in lstPasswords)
                    {
                        newIniFile.Sections.Add("Password");
                        iSection = newIniFile.Sections.Count - 1;
                        newIniFile.Sections[iSection].Keys.Add(new Key("password", i.Pass));
                        newIniFile.Sections[iSection].Keys.Add(new Key("admin", i.Admin));
                        newIniFile.Sections[iSection].Keys.Add(new Key("viewshop", i.Viewshop));
                        newIniFile.Sections[iSection].Keys.Add(new Key("manageshop", i.Manageshop));
                        newIniFile.Sections[iSection].Keys.Add(new Key("managefactory", i.Managefactory));
                    }

                    // Confirm we did the bulk of the stuff
                    Console.WriteLine("Outputting Conversion");
                    newIniFile.Save(fileName); // Save the file in the application directory
                    Console.WriteLine("File successfully converted and saved."); // Inform no save issues

                    string targetFile = dir + @"\spawned_solars\playerbase\" + fileName;
                    if (File.Exists(targetFile))
                    {
                        // We want to delete files that already exist as they may be out of date
                        Console.WriteLine("File already exists, attempting to delete . . .");
                        File.Delete(targetFile);
                    }

                    File.Move(Directory.GetCurrentDirectory() + @"\" + fileName, targetFile); // Move the file to where it needs to go
                    Console.WriteLine("File successfully moved to correct directory."); // Inform success
                    Console.WriteLine(); // Blank line to seperate each item in the list
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured while trying to convert file '{0}'", a);
                    Console.WriteLine("Exception: {0}", ex.Message);
                    continue;
                }
            }
            Console.WriteLine("Utility has finished converting old PoB files.");
            Console.ReadLine();
        }
    }
}
