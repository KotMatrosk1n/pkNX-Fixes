using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using PKHeX.Core;
using pkNX.Containers;
using pkNX.Game;
using pkNX.Randomization;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers;
using pkNX.Structures.FlatBuffers.SWSH;
using Ball = pkNX.Structures.Ball;
using FixedGender = pkNX.Structures.FixedGender;
using GameData = pkNX.Game.GameData;
using GameVersion = pkNX.Structures.GameVersion;
using Legal = pkNX.Structures.Legal;
using Nature = pkNX.Structures.Nature;
using Shiny = pkNX.Structures.Shiny;
using Species = pkNX.Structures.Species;

namespace pkNX.WinForms.Controls;

internal class EditorSWSH : EditorBase
{
    protected override GameManagerSWSH ROM { get; }
    private GameData Data => ROM.Data;
    protected internal EditorSWSH(GameManagerSWSH rom) => ROM = rom;

    public void EditCommon()
    {
        var text = ROM.GetFilteredFolder(GameFile.GameText, z => Path.GetExtension(z) == ".dat");
        var config = new TextConfig(ROM.Game);
        var tc = new TextContainer(text, config);
        using var form = new TextEditor(tc, TextEditor.TextEditorMode.Common);
        form.ShowDialog();
        if (!form.Modified)
            text.CancelEdits();
    }

    public void EditScript()
    {
        var text = ROM.GetFilteredFolder(GameFile.StoryText, z => Path.GetExtension(z) == ".dat");
        var config = new TextConfig(ROM.Game);
        var tc = new TextContainer(text, config);
        using var form = new TextEditor(tc, TextEditor.TextEditorMode.Script);
        form.ShowDialog();
        if (!form.Modified)
            text.CancelEdits();
    }

    public void EditTrainers()
    {
        var editor = new TrainerEditor
        {
            ReadClass = data => new TrainerClass8(data),
            ReadPoke = data => new TrainerPoke8(data),
            ReadTrainer = data => new TrainerData8(data),
            ReadTeam = TrainerPoke8.ReadTeam,
            WriteTeam = TrainerPoke8.WriteTeam,
            TrainerData = ROM.GetFilteredFolder(GameFile.TrainerSpecData),
            TrainerPoke = ROM.GetFilteredFolder(GameFile.TrainerSpecPoke),
            TrainerClass = ROM.GetFilteredFolder(GameFile.TrainerSpecClass),
        };
        editor.Initialize();
        using var form = new BTTE(Data, editor, ROM);
        form.ShowDialog();
        if (!form.Modified)
            editor.CancelEdits();
        else
            editor.Save();
    }

    public void EditPokémon()
    {
        var editor = new PokeEditor
        {
            Evolve = Data.EvolutionData,
            Learn = Data.LevelUpData,
            Mega = Data.MegaEvolutionData,
            Personal = Data.PersonalData,
            TMHM = Legal.TMHM_SWSH,
            TR = Legal.TR_SWSH,
        };
        using var form = new PokeDataUI(editor, ROM, Data);
        form.ShowDialog();
        if (!form.Modified)
            editor.CancelEdits();
        else
            editor.Save();
    }

    public void NotWorking_EditItems()
    {
        var obj = ROM.GetFilteredFolder(GameFile.ItemStats, z => new FileInfo(z).Length == 36);
        var cache = new DataCache<Item>(obj)
        {
            Create = Item.FromBytes,
            Write = item => item.Write(),
        };
        using var form = new GenericEditor<Item>(cache, ROM.GetStrings(TextName.ItemNames), "Item Editor");
        form.ShowDialog();
        if (!form.Modified)
            cache.CancelEdits();
        else
            cache.Save();
    }

    public void EditShinyRate()
    {
        if (ROM.PathExeFS == null)
        {
            WinFormsUtil.Alert("ExeFS not detected.");
            return;
        }

        var path = Path.Combine(ROM.PathExeFS, "main");
        if (!File.Exists(path))
        {
            WinFormsUtil.Alert("Not able to find `main` file in ExeFS.");
            return;
        }

        var data = FileMitm.ReadAllBytes(path);
        var nso = new NSO(data);

        var shiny = new ShinyRateSWSH(nso.DecompressedText);
        if (!shiny.IsEditable)
        {
            WinFormsUtil.Alert("Not able to find shiny rate logic in ExeFS.");
            return;
        }

        using var editor = new ShinyRate(shiny);
        editor.ShowDialog();
        if (!editor.Modified)
            return;

        nso.DecompressedText = shiny.Data;
        FileMitm.WriteAllBytes(path, nso.Write());
    }

    public void NotWorking_EditTM()
    {
        var path = Path.Combine(ROM.PathExeFS, "main");
        var data = FileMitm.ReadAllBytes(path);
        var list = new TMEditorGG(data);
        if (!list.Valid)
        {
            WinFormsUtil.Alert("Not able to find tm data in ExeFS.");
            return;
        }

        var moves = list.GetMoves();
        var allowed = Legal.GetAllowedMoves(ROM.Game, Data.MoveData.Length);
        var names = ROM.GetStrings(TextName.MoveNames);
        using var editor = new TMList(moves, allowed, names);
        editor.ShowDialog();
        if (!editor.Modified)
            return;

        list.SetMoves(editor.FinalMoves);
        data = list.Write();
        FileMitm.WriteAllBytes(path, data);
    }

    public void NotWorking_EditTypeChart()
    {
        var path = Path.Combine(ROM.PathExeFS, "main");
        var data = FileMitm.ReadAllBytes(path);
        var nso = new NSO(data);

        byte[] pattern = // N2nn3pia9transport18UnreliableProtocolE
        [
            0x4E, 0x32, 0x6E, 0x6E, 0x33, 0x70, 0x69, 0x61, 0x39, 0x74, 0x72, 0x61, 0x6E, 0x73, 0x70, 0x6F, 0x72,
            0x74, 0x31, 0x38, 0x55, 0x6E, 0x72, 0x65, 0x6C, 0x69, 0x61, 0x62, 0x6C, 0x65, 0x50, 0x72, 0x6F, 0x74,
            0x6F, 0x63, 0x6F, 0x6C, 0x45, 0x00,
        ];
        int ofs = CodePattern.IndexOfBytes(nso.DecompressedRO, pattern);
        if (ofs < 0)
        {
            WinFormsUtil.Alert("Not able to find type chart data in ExeFS.");
            return;
        }
        ofs += pattern.Length + 0x24; // 0x5B4C0C in lgpe 1.0 RO

        var cdata = new byte[18 * 18];
        var types = ROM.GetStrings(TextName.TypeNames);
        Array.Copy(nso.DecompressedRO, ofs, cdata, 0, cdata.Length);
        var chart = new TypeChartEditor(cdata);
        using var editor = new TypeChart(chart, types);
        editor.ShowDialog();
        if (!editor.Modified)
            return;

        chart.Data.CopyTo(nso.DecompressedRO.AsSpan(ofs));
        data = nso.Write();
        FileMitm.WriteAllBytes(path, data);
    }

    public void EditWild()
    {
        if (ROM.Game == GameVersion.SWSH)
        {
            var dr = WinFormsUtil.Prompt(MessageBoxButton.YesNoCancel, "No ExeFS data found. Please choose which game's encounter tables you wish to edit.", "Yes for Sword, No for Shield.");
            if (dr == MessageBoxResult.Cancel)
                return;
            PopWildEdit(dr == MessageBoxResult.Yes ? "k" : "t");
        }
        else
        {
            PopWildEdit(ROM.Game == GameVersion.SW ? "k" : "t");
        }
    }

    private void PopWildEdit(string file)
    {
        IFileContainer fp = ROM.GetFile(GameFile.NestData);
        var data_table = new GFPack(fp[0]);
        var sdo = data_table.GetDataFileName($"encount_symbol_{file}.bin");
        var hdo = data_table.GetDataFileName($"encount_{file}.bin");
        var s = FlatBufferConverter.DeserializeFrom<EncounterArchive>(sdo);
        var h = FlatBufferConverter.DeserializeFrom<EncounterArchive>(hdo);
        while (s.EncounterTables[0].SubTables.Count != 9)
        {
            s = FlatBufferConverter.DeserializeFrom<EncounterArchive>(sdo);
            h = FlatBufferConverter.DeserializeFrom<EncounterArchive>(hdo);
        }

        using var form = new SSWE(ROM, s, h);
        form.ShowDialog();
        if (!form.Modified)
            return;

        var sd = s.SerializeFrom();
        var hd = h.SerializeFrom();
        data_table.SetDataFileName($"encount_symbol_{file}.bin", sd);
        data_table.SetDataFileName($"encount_{file}.bin", hd);

        fp[0] = data_table.Write();
    }

    public void EditRaids()
    {
        IFileContainer fp = ROM.GetFile(GameFile.NestData);
        var data_table = new GFPack(fp[0]);
        const string nest = "nest_hole_encount.bin";
        var nest_encounts = FlatBufferConverter.DeserializeFrom<EncounterNestArchive>(data_table.GetDataFileName(nest));

        var arr = nest_encounts.Table;
        var raidTables = arr.ToArray();
        var cache = new DataCache<EncounterNestTable>(arr!);
        RaidPropertyGridUtil.Configure(
            raidTables,
            [],
            GetRewardTableIDs(data_table, "nest_hole_drop_rewards.bin"),
            GetRewardTableIDs(data_table, "nest_hole_bonus_rewards.bin"),
            GetRaidTableUsageLabels());
        var names = raidTables.Select(RaidPropertyGridUtil.GetRaidTableName).ToArray();

        void Randomize()
        {
            var pt = Data.PersonalData;
            int[] ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();

            var spec = EditUtil.Settings.Species;
            var srand = new SpeciesRandomizer(ROM.Info, Data.PersonalData);
            var frand = new FormRandomizer(Data.PersonalData);
            srand.Initialize(spec, ban);
            foreach (var t in arr)
            {
                foreach (var p in t.Entries)
                {
                    p.Species = srand.GetRandomSpecies(p.Species);
                    p.Form = frand.GetRandomForm(p.Species, false, spec.AllowRandomFusions, ROM.Info.Generation, Data.PersonalData.Table);
                    p.Ability = 4; // "A4" -- 1, 2, or H
                    p.Gender = 0; // random
                    p.IsGigantamax = false; // don't allow gmax flag on non-gmax species
                }
            }
        }

        static ulong[] GetRewardTableIDs(GFPack dataTable, string fileName)
        {
            try
            {
                var rewards = FlatBufferConverter.DeserializeFrom<NestHoleRewardArchive>(dataTable.GetDataFileName(fileName));
                return rewards.Table.Select(z => z.TableID).ToArray();
            }
            catch
            {
                return [];
            }
        }

        IReadOnlyDictionary<ulong, string> GetRaidTableUsageLabels()
        {
            const ulong EmptyHash = 0xCBF29CE484222645;

            try
            {
                var placement = new GFPack(ROM.GetFile(GameFile.Placement)[0]);
                var areaNames = new AHTB(placement.GetDataFileName("AreaNameHashTable.tbl")).ToDictionary();
                var zoneNames = new AHTB(placement.GetDataFileName("ZoneNameHashTable.tbl")).ToDictionary();
                var zoneDisplayNames = zoneNames.ToDictionary(
                    zone => zone.Key,
                    zone => SWSHInfo.Zones.TryGetValue(zone.Key, out var desc) ? $"{desc} [{zone.Value}]" : zone.Value);

                var references = new Dictionary<ulong, List<(string Kind, string Zone, string Location)>>();
                foreach (var area in areaNames.Values.OrderBy(z => z))
                {
                    var fileName = $"{area}.bin";
                    if (placement.GetIndexFileName(fileName) < 0)
                        continue;

                    var archive = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(placement.GetDataFileName(fileName));
                    foreach (var zone in archive.Table)
                    {
                        var zoneName = zoneDisplayNames.TryGetValue(zone.Meta.ZoneID, out var displayName)
                            ? displayName
                            : zone.Meta.ZoneID.ToString("X16");

                        foreach (var nest in zone.Nests)
                        {
                            var location = nest.Field00.Field00.Field00.Location3f;
                            AddReference(nest.Common, "Common", zoneName, location);
                            AddReference(nest.Rare, "Rare", zoneName, location);
                        }
                    }
                }

                return references.ToDictionary(z => z.Key, z => SummarizeRaidTableUsage(z.Value));

                void AddReference(ulong tableID, string kind, string zone, string location)
                {
                    if (tableID is 0 or EmptyHash)
                        return;

                    if (!references.TryGetValue(tableID, out var list))
                        references[tableID] = list = [];

                    list.Add((kind, zone, location));
                }
            }
            catch
            {
                return new Dictionary<ulong, string>();
            }
        }

        static string SummarizeRaidTableUsage(IReadOnlyList<(string Kind, string Zone, string Location)> references)
        {
            var parts = references
                .GroupBy(z => z.Kind)
                .OrderBy(z => z.Key == "Common" ? 0 : 1)
                .Select(z => $"{z.Key}: {SummarizeZones(z)}");
            return string.Join("; ", parts);
        }

        static string SummarizeZones(IEnumerable<(string Kind, string Zone, string Location)> references)
        {
            var list = references.ToArray();
            var zones = list.Select(z => z.Zone).Distinct().ToArray();
            var summary = string.Join(", ", zones.Take(2));
            if (zones.Length > 2)
                summary += $", +{zones.Length - 2} zones";
            if (list.Length > zones.Length)
                summary += $" ({list.Length} dens)";
            return summary;
        }

        using var form = new GenericEditor<EncounterNestTable>(cache, names, "Max Raid Battles Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            return;
        var data = nest_encounts.SerializeFrom();
        data_table.SetDataFileName(nest, data);
        fp[0] = data_table.Write();
    }

    public void EditRaidRewards()
    {
        IFileContainer fp = ROM.GetFile(GameFile.NestData);
        var data_table = new GFPack(fp[0]);
        const string nest = "nest_hole_drop_rewards.bin";
        byte[] originalData = data_table.GetDataFileName(nest);
        var nest_drops = FlatBufferConverter.DeserializeFrom<NestHoleRewardArchive>(originalData);

        var arr = nest_drops.Table;
        var cache = new DataCache<NestHoleRewardTable>(arr!);
        var names = ConfigureRaidRewardEditor(data_table, arr, usesQuantities: false);

        void Randomize()
        {
            int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Info.Game);
            foreach (var t in arr)
            {
                foreach (var i in t.Entries)
                    i.ItemID = (uint)PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
            }
        }

        using var form = new GenericEditor<NestHoleRewardTable>(cache, names, "Raid Rewards Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            return;
        var data = nest_drops.SerializeFrom();
        data_table.SetDataFileName(nest, data);
        fp[0] = data_table.Write();
    }

    public void EditRBonusRewards()
    {
        IFileContainer fp = ROM.GetFile(GameFile.NestData);
        var data_table = new GFPack(fp[0]);
        const string nest = "nest_hole_bonus_rewards.bin";
        var nest_bonus = FlatBufferConverter.DeserializeFrom<NestHoleRewardArchive>(data_table.GetDataFileName(nest));

        var arr = nest_bonus.Table;
        var cache = new DataCache<NestHoleRewardTable>(arr!);
        var names = ConfigureRaidRewardEditor(data_table, arr, usesQuantities: true);

        void Randomize()
        {
            int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Info.Game);
            foreach (var t in arr)
            {
                foreach (var i in t.Entries)
                    i.ItemID = (uint)PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
            }
        }

        using var form = new GenericEditor<NestHoleRewardTable>(cache, names, "Raid Bonus Rewards Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            return;
        var data = nest_bonus.SerializeFrom();
        data_table.SetDataFileName(nest, data);
        fp[0] = data_table.Write();
    }

    private string[] ConfigureRaidRewardEditor(GFPack dataTable, IList<NestHoleRewardTable> rewardTables, bool usesQuantities)
    {
        pkNX.Structures.ItemConverter.ItemNames = ROM.GetStrings(TextName.ItemNames);
        ShopItemNameFormatter.MoveNames = ROM.GetStrings(TextName.MoveNames);
        ShopItemNameFormatter.MachineTable = Item8MachineTable.FromItemData(ROM[GameFile.ItemStats][0]);
        RaidPropertyGridUtil.ConfigureRewardEditor(usesQuantities, GetRaidRewardTableUsageLabels(dataTable, usesQuantities));
        return rewardTables.Select(RaidPropertyGridUtil.GetRewardTableName).ToArray();
    }

    private IReadOnlyDictionary<ulong, string> GetRaidRewardTableUsageLabels(GFPack dataTable, bool bonusRewards)
    {
        try
        {
            const string nest = "nest_hole_encount.bin";
            var encounters = FlatBufferConverter.DeserializeFrom<EncounterNestArchive>(dataTable.GetDataFileName(nest));
            var speciesNames = ROM.GetStrings(TextName.SpeciesNames);
            var references = new Dictionary<ulong, List<(string Version, int DenTable, int Slot, int MinStar, int MaxStar, string Species)>>();

            foreach (var (table, tableIndex) in encounters.Table.Select((table, index) => (table, index)))
            {
                var version = GetShortGameVersion(table.GameVersion);
                var denTable = tableIndex / 2;
                foreach (var entry in table.Entries)
                {
                    var tableID = bonusRewards ? entry.BonusTableID : entry.DropTableID;
                    if (tableID == 0)
                        continue;

                    if (!references.TryGetValue(tableID, out var list))
                        references[tableID] = list = [];

                    list.Add((version, denTable, entry.EntryIndex, GetRaidMinStar(entry), GetRaidMaxStar(entry), GetSpeciesName(entry, speciesNames)));
                }
            }

            return references.ToDictionary(z => z.Key, z => SummarizeRewardTableUsage(z.Value));
        }
        catch
        {
            return new Dictionary<ulong, string>();
        }
    }

    private static string SummarizeRewardTableUsage(IReadOnlyList<(string Version, int DenTable, int Slot, int MinStar, int MaxStar, string Species)> references)
    {
        var distinct = references.Distinct().ToArray();
        if (distinct.Length <= 2)
            return string.Join("; ", distinct.Select(FormatRewardTableUse));

        var versions = distinct.Select(z => z.Version).Distinct().OrderBy(z => z).ToArray();
        var versionText = versions is ["SH", "SW"] ? "SW/SH" : string.Join("/", versions);
        var denCount = distinct.Select(z => (z.Version, z.DenTable)).Distinct().Count();
        var speciesCount = distinct.Select(z => z.Species).Distinct().Count();
        var starRefs = distinct.Where(z => z.MinStar > 0 && z.MaxStar > 0).ToArray();
        var stars = starRefs.Length == 0 ? "No-star" : FormatRaidStarRange(starRefs.Min(z => z.MinStar), starRefs.Max(z => z.MaxStar));
        var dens = denCount == 1 ? "1 den" : $"{denCount} dens";
        var species = speciesCount == 1 ? "1 species" : $"{speciesCount} species";

        return $"{versionText}, {distinct.Length} slots, {dens}, {stars}, {species}";
    }

    private static string FormatRewardTableUse((string Version, int DenTable, int Slot, int MinStar, int MaxStar, string Species) reference)
    {
        return $"{reference.Version} Den {reference.DenTable} Slot {reference.Slot:00}, {FormatRaidStarRange(reference.MinStar, reference.MaxStar)} {reference.Species}";
    }

    private static int GetRaidMinStar(EncounterNest entry) => entry.MinRank < 0 ? 0 : entry.MinRank + 1;

    private static int GetRaidMaxStar(EncounterNest entry) => entry.MaxRank < 0 ? 0 : entry.MaxRank + 1;

    private static string FormatRaidStarRange(int minStar, int maxStar)
    {
        if (minStar <= 0 || maxStar <= 0)
            return "No-star";

        return minStar == maxStar
            ? $"{minStar}★"
            : $"{minStar}-{maxStar}★";
    }

    private static string GetShortGameVersion(int version)
    {
        return version switch
        {
            1 => "SW",
            2 => "SH",
            _ => $"V{version}",
        };
    }

    private static string GetSpeciesName(EncounterNest entry, IReadOnlyList<string> speciesNames)
    {
        var species = (uint)entry.Species < (uint)speciesNames.Count && !string.IsNullOrWhiteSpace(speciesNames[entry.Species])
            ? speciesNames[entry.Species]
            : entry.Species.ToString();
        return entry.Form == 0 ? species : $"{species}-{entry.Form}";
    }

    public void EditStatic()
    {
        var arc = ROM.GetFile(GameFile.EncounterTableStatic);
        var data = arc[0];
        var objs = FlatBufferConverter.DeserializeFrom<EncounterStaticArchive>(data);

        var encounters = objs.Table;
        pkNX.Structures.ItemConverter.ItemNames = ROM.GetStrings(TextName.ItemNames);
        ShopItemNameFormatter.MoveNames = ROM.GetStrings(TextName.MoveNames);
        ShopItemNameFormatter.MachineTable = Item8MachineTable.FromItemData(ROM[GameFile.ItemStats][0]);
        StaticEncounterPropertyGridUtil.Configure(ROM.GetStrings(TextName.SpeciesNames), ROM.GetStrings(TextName.MoveNames));
        var names = encounters.Select(StaticEncounterPropertyGridUtil.GetStaticEncounterName).ToArray();
        var cache = new DirectCache<Structures.FlatBuffers.SWSH.EncounterStatic>(encounters);

        void Randomize()
        {
            int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Game);
            var pt = Data.PersonalData;
            int[] ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();

            var spec = EditUtil.Settings.Species;
            var srand = new SpeciesRandomizer(ROM.Info, Data.PersonalData);
            var frand = new FormRandomizer(Data.PersonalData);
            srand.Initialize(spec, ban);
            foreach (var t in encounters)
            {
                if (t.Species is >= (int)Species.Zacian and <= (int)Species.Eternatus) // Eternatus crashes when changed, keep Zacian and Zamazenta to make final boss battle fair
                    continue;
                t.Species = srand.GetRandomSpecies(t.Species);
                t.Form = (byte)frand.GetRandomForm(t.Species, false, spec.AllowRandomFusions, ROM.Info.Generation, Data.PersonalData.Table);
                t.Ability = Randomization.Util.Random.Next(1, 4); // 1, 2, or H
                t.HeldItem = PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
                t.Nature = (int)Nature.Random25;
                t.Gender = (int)FixedGender.Random;
                t.ShinyLock = (int)Shiny.Random;
                t.Moves = [0, 0, 0, 0];
                if (t.IVHP != -4 && t.IVs.Any(z => z != 31))
                    t.IVs = [-1, -1, -1, -1, -1, -1];
            }
        }

        using var form = new GenericEditor<Structures.FlatBuffers.SWSH.EncounterStatic>(cache, names, "Static Encounter Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            arc.CancelEdits();
        else
            arc[0] = objs.SerializeFrom();
    }

    public void EditShop1() => EditShop(false);

    public void EditShop2() => EditShop(true);

    private void EditShop(bool shop2)
    {
        var arc = ROM.GetFile(GameFile.Shops);
        var data = arc[0];
        pkNX.Structures.ItemConverter.ItemNames = ROM.GetStrings(TextName.ItemNames);
        ShopItemNameFormatter.MoveNames = ROM.GetStrings(TextName.MoveNames);
        ShopItemNameFormatter.MachineTable = Item8MachineTable.FromItemData(ROM[GameFile.ItemStats][0]);
        int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Game);
        var shop = FlatBufferConverter.DeserializeFrom<ShopInventory>(data);
        if (!shop2)
        {
            var table = shop.Single;
            var names = table.Select((z, i) => GetShopName(z, i)).ToArray();
            var cache = new DirectCache<SingleShop>(table!);
            using var form = new GenericEditor<SingleShop>(cache, names, $"{nameof(SingleShop)} Editor", Randomize);
            form.ShowDialog();
            if (!form.Modified)
            {
                arc.CancelEdits();
                return;
            }

            void Randomize()
            {
                foreach (var shopDefinition in table)
                {
                    var items = shopDefinition.Inventories.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (Legal.Pouch_TMHM_SWSH.Contains((ushort)items[i])) // skip TMs/TRs
                            continue;
                        items[i] = PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
                    }
                }
            }
        }
        else
        {
            var table = shop.Multi;
            var names = table.Select((z, i) => GetShopName(z, i)).ToArray();
            var cache = new DirectCache<MultiShop>(table!);
            using var form = new GenericEditor<MultiShop>(cache, names, $"{nameof(MultiShop)} Editor", Randomize);
            form.ShowDialog();
            if (!form.Modified)
            {
                arc.CancelEdits();
                return;
            }

            void Randomize()
            {
                foreach (var shopDefinition in table)
                {
                    foreach (var inv in shopDefinition.Inventories)
                    {
                        var items = inv.Items;
                        for (int i = 0; i < items.Count; i++)
                            items[i] = PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
                    }
                }
            }
        }
        arc[0] = shop.SerializeFrom();
    }

    private static string GetShopName(SingleShop shop, int index)
    {
        if (SingleShop.SWSH.TryGetValue(shop.Hash, out var name))
            return name;

        return GetFallbackShopName("Single Shop", shop.Hash, index, [shop.Inventories]);
    }

    private static string GetShopName(MultiShop shop, int index)
    {
        if (MultiShop.SWSH.TryGetValue(shop.Hash, out var name))
            return name;

        return GetFallbackShopName("Multi Shop", shop.Hash, index, shop.Inventories);
    }

    private static string GetFallbackShopName(string label, ulong hash, int index, IEnumerable<Inventory> inventories)
    {
        var summary = string.Join(" / ", inventories.Select(GetInventorySummary).Where(z => z.Length != 0).Take(2));
        if (summary.Length == 0)
            return $"{label} {index + 1} [{hash:X16}]";

        return $"{label} {index + 1} [{summary}]";
    }

    private static string GetInventorySummary(Inventory inventory)
    {
        const int MaxItems = 4;
        var items = inventory.Items;
        var summary = string.Join(", ", items.Take(MaxItems).Select(GetItemName));
        return items.Count > MaxItems ? $"{summary}, ..." : summary;
    }

    private static string GetItemName(int item)
    {
        return ShopItemNameFormatter.GetDisplayName(item);
    }

    public void EditMoves()
    {
        var obj = ROM[GameFile.MoveStats]; // mini
        var cache = new DataCache<Waza>(obj)
        {
            Create = FlatBufferConverter.DeserializeFrom<Waza>,
            Write = FlatBufferConverter.SerializeFrom,
        };
        MovePropertyGridUtil.Configure(ROM.GetStrings(TextName.MoveNames));
        using var form = new GenericEditor<Waza>(cache, ROM.GetStrings(TextName.MoveNames), "Move Editor");
        form.ShowDialog();
        if (!form.Modified)
        {
            cache.CancelEdits();
            return;
        }

        cache.Save();
        Data.MoveData.ClearAll(); // force reload if used again
    }

    public void EditRental()
    {
        var obj = ROM[GameFile.Rentals];
        var data = obj[0];
        var rentals = FlatBufferConverter.DeserializeFrom<RentalArchive>(data);
        var cache = new DataCache<Rental>(rentals.Table!);
        pkNX.Structures.ItemConverter.ItemNames = ROM.GetStrings(TextName.ItemNames);
        ShopItemNameFormatter.MoveNames = ROM.GetStrings(TextName.MoveNames);
        ShopItemNameFormatter.MachineTable = Item8MachineTable.FromItemData(ROM[GameFile.ItemStats][0]);
        RentalPropertyGridUtil.Configure(ROM.GetStrings(TextName.SpeciesNames), ROM.GetStrings(TextName.MoveNames));
        var names = rentals.Table.Select(RentalPropertyGridUtil.GetRentalName).ToArray();
        using var form = new GenericEditor<Rental>(cache, names, "Rental Editor");
        form.ShowDialog();
        if (!form.Modified)
        {
            cache.CancelEdits();
            return;
        }
        obj[0] = rentals.SerializeFrom();
    }

    public void EditItems()
    {
        var obj = ROM[GameFile.ItemStats]; // mini
        var data = obj[0];
        var allowedMachineMoves = Legal.GetAllowedMoves(ROM.Game, Data.MoveData.Length).Select(z => (ushort)z).ToArray();
        var items = Item8.GetArray(data, allowedMachineMoves);
        var cache = new DataCache<Item8>(items);
        using var form = new GenericEditor<Item8>(cache, ROM.GetStrings(TextName.ItemNames), "Item Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
        {
            cache.CancelEdits();
            return;
        }

        void Randomize()
        {
            var tradeEvos = new[] { 221, 226, 227, 233, 235, 252, 321, 322, 323, 324, 325, 573, 646, 647 };
            foreach (var item in items)
            {
                if (item.ItemSprite == -1 || !tradeEvos.Contains(item.ItemID))
                    continue;

                item.EvoStone = true;
                item.EffectField = FieldItemType.Evolution;
                item.CanUseOnPokemon = true;
            }
        }

        var editedData = Item8.SetArray(items, data);
        obj[0] = editedData;
        ShopItemNameFormatter.MachineTable = Item8MachineTable.FromItemData(editedData, allowedMachineMoves);
    }

    public void EditGift()
    {
        var arc = ROM.GetFile(GameFile.EncounterTableGift);
        var data = arc[0];
        var objs = FlatBufferConverter.DeserializeFrom<EncounterGiftArchive>(data);

        var gifts = objs.Table;
        var names = Enumerable.Range(0, gifts.Count).Select(z => $"{z:000}").ToArray();
        var cache = new DirectCache<Structures.FlatBuffers.SWSH.EncounterGift>(gifts!);

        void Randomize()
        {
            int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Game);
            var pt = Data.PersonalData;
            int[] ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();

            var spec = EditUtil.Settings.Species;
            var srand = new SpeciesRandomizer(ROM.Info, Data.PersonalData);
            var frand = new FormRandomizer(Data.PersonalData);
            srand.Initialize(spec, ban);
            foreach (var t in gifts)
            {
                // swap gmax gifts and kubfu for other gmax capable species
                if (t.CanGigantamax || t.Species == (int)Species.Kubfu)
                {
                    t.Species = Legal.GigantamaxForms[Randomization.Util.Random.Next(Legal.GigantamaxForms.Length)];
                    t.Form = (byte)(t.Species is (int)Species.Pikachu or (int)Species.Meowth ? 0 : frand.GetRandomForm(t.Species, false, false, ROM.Info.Generation, Data.PersonalData.Table)); // Pikachu & Meowth altforms can't gmax
                }
                else
                {
                    t.Species = srand.GetRandomSpecies(t.Species);
                    t.Form = (byte)frand.GetRandomForm(t.Species, false, spec.AllowRandomFusions, ROM.Info.Generation, Data.PersonalData.Table);
                }

                t.Ability = Randomization.Util.Random.Next(1, 4); // 1, 2, or H
                t.Ball = (Ball)Randomization.Util.Random.Next(1, Structures.FlatBuffers.SWSH.EncounterGift.BallToItem.Length);
                t.HeldItem = PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
                t.Nature = (int)Nature.Random25;
                t.Gender = (byte)FixedGender.Random;
                t.ShinyLock = (int)Shiny.Random;
                if (t.IVHP != -4 && t.IVs.Any(z => z != 31))
                    t.IVs = [-1, -1, -1, -1, -1, -1];
            }
        }

        void UpdateStarters()
        {
            var container = ROM.GetFile(GameFile.Placement);
            var placement = new GFPack(container[0]);

            // a_r0501_i0101.bin for Toxel
            // a_bt0101.bin for Type: Null
            // a_wr0201_i0101.bin for Bulbasaur, Squirtle, Porygon, and Kubfu
            // a_wr0301_i0401.bin for Cosmog
            // a_d0901.bin for Poipole
            const string file = "a_0101.bin";
            var table = placement.GetDataFileName(file);
            var obj = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(table);
            var critters = obj.Table[0].Critters;

            // Grookey
            critters[3].Species = (uint)gifts[0].Species;
            critters[3].Form = gifts[0].Form;

            // Scorbunny
            critters[1].Species = (uint)gifts[3].Species;
            critters[1].Form = gifts[3].Form;

            // Sobble
            critters[2].Species = (uint)gifts[4].Species;
            critters[2].Form = gifts[4].Form;

            var bin = obj.SerializeFrom();
            placement.SetDataFileName(file, bin);
            container[0] = placement.Write();
        }

        using var form = new GenericEditor<Structures.FlatBuffers.SWSH.EncounterGift>(cache, names, "Gift Pokémon Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
        {
            arc.CancelEdits();
        }
        else
        {
            UpdateStarters(); // update placement critter data to match new randomized species
            arc[0] = objs.SerializeFrom();
        }
    }

    private static ReadOnlySpan<int> tradingLines =>
    [
        127, 128, 129, 130, 131, 132, 133, 134, 135, 135, 136, 137,
        000, 008, 016, 024, 032, 040, 048, 056, 064, 072, 080,
    ];

    public void EditTrade()
    {
        var arc = ROM.GetFile(GameFile.EncounterTableTrade);
        var data = arc[0];
        var objs = FlatBufferConverter.DeserializeFrom<EncounterTradeArchive>(data);

        var trades = objs.Table;
        var names = Enumerable.Range(0, trades.Count).Select(z => $"{z:000}").ToArray();
        var cache = new DirectCache<Structures.FlatBuffers.SWSH.EncounterTrade>(trades);

        // Get dialogues
        var text = ROM.GetFilteredFolder(GameFile.StoryText, z => Path.GetExtension(z) == ".dat");
        var text_config = new TextConfig(ROM.Game);
        var tc = new TextContainer(text, text_config);

        string[] field_trade = ["null"];
        for (int i = 0; i < tc.Length; i++)
        {
            if (tc.GetFileName(i) != "field_trade")
                continue;
            field_trade = tc[i];
            break;
        }

        void Randomize()
        {
            int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Game);
            var pt = Data.PersonalData;
            int[] ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();

            var spec = EditUtil.Settings.Species;
            var srand = new SpeciesRandomizer(ROM.Info, Data.PersonalData);
            var frand = new FormRandomizer(Data.PersonalData);
            srand.Initialize(spec, ban);

            foreach (var t in trades)
            {
                // what you receive
                t.Species = srand.GetRandomSpecies(t.Species);
                t.Form = (byte)frand.GetRandomForm(t.Species, false, spec.AllowRandomFusions, ROM.Info.Generation, Data.PersonalData.Table);
                t.AbilityNumber = (byte)Randomization.Util.Random.Next(1, 4); // 1, 2, or H
                t.Ball = (Ball)Randomization.Util.Random.Next(1, Structures.FlatBuffers.SWSH.EncounterTrade.BallToItem.Length);
                t.HeldItem = PossibleHeldItems[Randomization.Util.Random.Next(PossibleHeldItems.Length)];
                t.Nature = (int)Nature.Random25;
                t.Gender = (int)FixedGender.Random;
                t.ShinyLock = (int)Shiny.Random;
                t.Relearn1 = 0;
                if (t.IVHP != -4 && t.IVs.Any(z => z != 31))
                    t.IVs = [-1, -1, -1, -1, -1, -1];

                // what you trade
                t.RequiredSpecies = srand.GetRandomSpecies(t.RequiredSpecies);
                t.RequiredForm = (byte)frand.GetRandomForm(t.RequiredSpecies, false, false, ROM.Info.Generation, Data.PersonalData.Table);
                t.RequiredNature = (int)Nature.Random25; // any
            }

            // Update Trade Dialogues
            var strings = PKHeX.Core.GameInfo.Strings;
            for (int i = 0; i < trades.Count; i++)
            {
                var t = trades[i];
                // Update trade dialog
                static string GetFormPrefix(int species, int form, GameStrings strings)
                {
                    if (form == 0)
                        return "";
                    var list = FormConverter.GetFormList((ushort)species, strings.types, strings.forms, ["♂", "♀", "-"], EntityContext.Gen8);
                    if (form >= list.Length)
                        return "";
                    return $"{list[form]} ";
                }

                var reqSpecies = t.RequiredSpecies;
                var reqForm = GetFormPrefix(reqSpecies, t.RequiredForm, strings);
                var resSpecies = t.Species;
                var resForm = GetFormPrefix(resSpecies, t.Form, strings);

                int ind = tradingLines[i];
                var line1 = $"Do you happen to have a {reqForm}{strings.Species[reqSpecies]}?";
                var line2 = $"I'd like to trade my {resForm}{strings.Species[resSpecies]} for it.";
                field_trade[ind] = $"{line1}\n{line2}";
            }
            tc.Save();
        }

        using var form = new GenericEditor<Structures.FlatBuffers.SWSH.EncounterTrade>(cache, names, "In-Game Trades Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            arc.CancelEdits();
        else
            arc[0] = objs.SerializeFrom();
    }

    public void EditDynamaxAdv()
    {
        var arc = ROM.GetFile(GameFile.DynamaxDens);
        var data = arc[0];
        var objs = FlatBufferConverter.DeserializeFrom<EncounterUndergroundArchive>(data);

        var table = objs.Table;
        var speciesNames = ROM.GetStrings(TextName.SpeciesNames);
        RaidPropertyGridUtil.ConfigureDynamaxAdventure(speciesNames, ROM.GetStrings(TextName.MoveNames));
        var names = table.Select(RaidPropertyGridUtil.GetDynamaxAdventureName).ToArray();
        var cache = new DirectCache<EncounterUnderground>(table!);

        void Randomize()
        {
            var pt = Data.PersonalData;
            int[] ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();

            var spec = EditUtil.Settings.Species;
            var srand = new SpeciesRandomizer(ROM.Info, Data.PersonalData);
            var frand = new FormRandomizer(Data.PersonalData);
            srand.Initialize(spec, ban);
            RaidAbilityRoll[] abilityRolls =
            [
                RaidAbilityRoll.Ability1,
                RaidAbilityRoll.Ability2,
                RaidAbilityRoll.HiddenAbility,
            ];
            foreach (var t in table)
            {
                // what you receive
                t.Species = srand.GetRandomSpecies(t.Species);
                t.Form = (byte)frand.GetRandomForm(t.Species, false, spec.AllowRandomFusions, ROM.Info.Generation, Data.PersonalData.Table);
                t.Ability = (uint)abilityRolls[Randomization.Util.Random.Next(abilityRolls.Length)];
                t.Move0 = t.Move1 = t.Move2 = t.Move3 = 0;
            }
        }

        using var form = new GenericEditor<EncounterUnderground>(cache, names, "Dynamax Adventures Encounter Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            arc.CancelEdits();
        else
            arc[0] = objs.SerializeFrom();
    }

    public void EditSymbolBehave()
    {
        bool altRand = Control.ModifierKeys == Keys.Alt;
        var obj = ROM.GetFile(GameFile.SymbolBehave);
        var data = obj[0];
        var root = FlatBufferConverter.DeserializeFrom<SymbolBehaveRoot>(data);
        var cache = new DataCache<SymbolBehave>(root.Table!);
        var speciesNames = ROM.GetStrings(TextName.SpeciesNames);
        SymbolBehaviorPropertyGridUtil.Configure(speciesNames, root.Table.Select(z => z.Behavior).ToArray());
        var names = root.Table.Select(SymbolBehaviorPropertyGridUtil.GetSymbolBehaviorName).ToArray();
        using var form = new GenericEditor<SymbolBehave>(cache, names, "Symbol Behavior Editor", Randomize);
        form.ShowDialog();
        if (!form.Modified)
            return;
        obj[0] = root.SerializeFrom();

        void Randomize()
        {
            var mode = altRand
                ? "WaterDash" // Sharpedo dash homing -- good luck running!
                : "Anawohoru"; // Diglett - Disappear when approached, pop out elsewhere
            foreach (var t in root.Table)
                t.Behavior = mode;
        }
    }

    public void EditMasterDump()
    {
        using var md = new DumperSWSH(ROM);
        md.ShowDialog();
    }

    public void EditPlacement()
    {
        var arc = ROM.GetFile(GameFile.Placement);
        var placement = new GFPack(arc[0]);
        var area_names = new AHTB(placement.GetDataFileName("AreaNameHashTable.tbl")).ToDictionary();
        var zone_names = new AHTB(placement.GetDataFileName("ZoneNameHashTable.tbl")).ToDictionary();
        var object_names = new AHTB(placement.GetDataFileName("ObjectNameHashTable.tbl")).ToDictionary();
        var vanish_flags = TryGetPlacementHashTable(placement, "VanishFlagAutoTable.tbl");
        var zoneDisplayNames = zone_names.ToDictionary(
            z => z.Key,
            z => SWSHInfo.Zones.TryGetValue(z.Key, out var description) ? $"{description} [{z.Value}]" : z.Value);
        foreach (var zone in SWSHInfo.Zones)
            zoneDisplayNames.TryAdd(zone.Key, zone.Value);
        var itemDisplayNames = GetPlacementItemDisplayNames();
        var staticSpawnDisplayNames = GetPlacementStaticSpawnDisplayNames();
        var hashDisplayNames = GetPlacementHashDisplayNames(area_names, zoneDisplayNames, object_names, itemDisplayNames, staticSpawnDisplayNames, vanish_flags);
        PlacementZoneLabelProvider.Configure(zoneDisplayNames, object_names, itemDisplayNames, staticSpawnDisplayNames, hashDisplayNames);

        List<PlacementZoneArchive> areas = [];
        List<string> names = [];

        int[] PossibleHeldItems = Legal.GetRandomItemList(ROM.Game);
        ushort[] PossibleTMHM = Legal.Pouch_TMHM_SWSH;

        foreach (var area in area_names)
        {
            var areaName = area.Value;
            var fileName = $"{areaName}.bin";
            if (placement.GetIndexFileName(fileName) < 0)
                continue;

            var bin = placement.GetDataFileName(fileName);
            var data = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(bin);

            names.Add(fileName);
            areas.Add(data);
        }

        var arr = areas.ToArray();
        var nameArr = names.ToArray();
        var cache = new DataCache<PlacementZoneArchive>(arr);
        var form = new GenericEditor<PlacementZoneArchive>(cache, nameArr, "Placement", Randomize, canSave: true);
        form.ConfigurePlacementZoneNames(zoneDisplayNames);
        form.ShowDialog();
        if (!form.Modified)
            return;

        // Stuff files back into the gfpak and save
        for (int i = 0; i < arr.Length; i++)
        {
            var obj = arr[i];
            var bin = obj.SerializeFrom();
            placement.SetDataFileName(nameArr[i], bin);
        }
        arc[0] = placement.Write();

        void Randomize()
        {
            var rnd = Randomization.Util.Random;
            var itemHashTableData = ROM.GetFile(GameFile.ItemHash)[0];
            var hashes = ItemHash8.GetItemHashTable(itemHashTableData);
            foreach (var area in areas)
            {
                foreach (var p in area.Table)
                {
                    // Randomize FieldItems
                    foreach (var item in p.FieldItems)
                    {
                        switch (item.Field00.Field02)
                        {
                            // Change red items to random items
                            case "bin/field/model/unit_obj/unit_obj_itemred01/unit_obj_itemred01":
                            {
                                var id = PossibleHeldItems[rnd.Next(PossibleHeldItems.Length)];
                                item.Field00.Flags[0] = hashes[id];
                                item.Field00.Items[0] = 1; // Set Amount to 1
                                break;
                            }
                            // Change yellow items to random TMs
                            case "bin/field/model/unit_obj/unit_obj_itemyel01/unit_obj_itemyel01":
                            {
                                var id = PossibleTMHM[rnd.Next(PossibleTMHM.Length)];
                                item.Field00.Flags[0] = hashes[id];
                                break;
                            }
                        }
                    }
                    // Randomize HiddenItems
                    foreach (var item in p.HiddenItems)
                    {
                        var id = PossibleHeldItems[rnd.Next(PossibleHeldItems.Length)];
                        item.Field00.Field02[0].Hash = hashes[id];
                        item.Field00.Field02[0].Quantity = 1; // Set Amount to 1
                        item.Field00.Field02[0].Chance = 100; // Set Chance to 100
                    }
                }
            }

            // Save changes
            for (int i = 0; i < arr.Length; i++)
            {
                var obj = arr[i];
                var bin = obj.SerializeFrom();
                placement.SetDataFileName(nameArr[i], bin);
            }
            arc[0] = placement.Write();
        }

        IReadOnlyDictionary<ulong, string> GetPlacementItemDisplayNames()
        {
            var itemNames = ROM.GetStrings(TextName.ItemNames);
            var itemHashTableData = ROM.GetFile(GameFile.ItemHash)[0];
            var hashes = ItemHash8.GetItemHashTable(itemHashTableData);
            return hashes
                .Where(z => (uint)z.Key < (uint)itemNames.Length)
                .GroupBy(z => z.Value)
                .ToDictionary(
                    z => z.Key,
                    z =>
                    {
                        var itemID = z.First().Key;
                        var name = itemNames[itemID];
                        return string.IsNullOrWhiteSpace(name) ? $"Item {itemID}" : $"{name} ({itemID})";
                    });
        }

        IReadOnlyDictionary<ulong, string> GetPlacementStaticSpawnDisplayNames()
        {
            var speciesNames = ROM.GetStrings(TextName.SpeciesNames);
            var data = ROM.GetFile(GameFile.EncounterTableStatic)[0];
            var statics = FlatBufferConverter.DeserializeFrom<EncounterStaticArchive>(data).Table;
            return statics
                .GroupBy(z => z.EncounterID)
                .ToDictionary(z => z.Key, z =>
                {
                    var encounter = z.First();
                    var species = (uint)encounter.Species < (uint)speciesNames.Length
                        ? speciesNames[encounter.Species]
                        : ((Species)encounter.Species).ToString();
                    var form = encounter.Form == 0 ? string.Empty : $"-{encounter.Form}";
                    return $"{species}{form} Lv. {encounter.Level} ({encounter.EncounterID:X16})";
                });
        }

        static IReadOnlyDictionary<ulong, string> TryGetPlacementHashTable(GFPack placement, string fileName)
        {
            if (placement.GetIndexFileName(fileName) < 0)
                return new Dictionary<ulong, string>();

            return new AHTB(placement.GetDataFileName(fileName)).ToDictionary();
        }

        static IReadOnlyDictionary<ulong, string> GetPlacementHashDisplayNames(params IReadOnlyDictionary<ulong, string>[] sources)
        {
            var result = new Dictionary<ulong, string>();
            foreach (var source in sources)
            {
                foreach (var (hash, name) in source)
                    result[hash] = name;
            }

            return result;
        }
    }
}
