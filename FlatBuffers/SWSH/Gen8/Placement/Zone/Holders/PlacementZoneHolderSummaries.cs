using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace pkNX.Structures.FlatBuffers.SWSH;

public static class PlacementZoneLabelProvider
{
    private const ulong EmptyHash = 0xCBF29CE484222645;

    private static IReadOnlyDictionary<ulong, string> ZoneNames { get; set; } = new Dictionary<ulong, string>();
    private static IReadOnlyDictionary<ulong, string> ObjectNames { get; set; } = new Dictionary<ulong, string>();
    private static IReadOnlyDictionary<ulong, string> ItemNames { get; set; } = new Dictionary<ulong, string>();
    private static IReadOnlyDictionary<int, ulong> ItemHashesByID { get; set; } = new Dictionary<int, ulong>();
    private static IReadOnlyDictionary<ulong, string> StaticSpawnNames { get; set; } = new Dictionary<ulong, string>();
    private static IReadOnlyDictionary<ulong, string> TrainerNames { get; set; } = new Dictionary<ulong, string>();
    private static IReadOnlyDictionary<ulong, string> HashNames { get; set; } = new Dictionary<ulong, string>();

    public static void Configure(
        IReadOnlyDictionary<ulong, string> zoneNames,
        IReadOnlyDictionary<ulong, string> objectNames,
        IReadOnlyDictionary<ulong, string> itemNames,
        IReadOnlyDictionary<ulong, string> staticSpawnNames,
        IReadOnlyDictionary<ulong, string> trainerNames,
        IReadOnlyDictionary<ulong, string> hashNames)
    {
        ZoneNames = zoneNames;
        ObjectNames = objectNames;
        ItemNames = itemNames;
        ItemHashesByID = BuildItemHashLookup(itemNames);
        StaticSpawnNames = staticSpawnNames;
        TrainerNames = trainerNames;
        HashNames = hashNames;
    }

    public static string Zone(ulong hash) => ZoneNames.TryGetValue(hash, out var name) ? name : Hash(hash);

    public static string Object(ulong hash) => ObjectNames.TryGetValue(hash, out var name) ? CleanPath(name) : Hash(hash);

    public static string Item(ulong hash) => ItemNames.TryGetValue(hash, out var name) ? name : Hash(hash);

    public static string Model(ulong hash)
    {
        if (PlacementZoneOtherNPCHolder.Models.TryGetValue(hash, out var model))
            return model;

        return Hash(hash, "Unresolved model");
    }

    public static string StaticSpawn(ulong hash) => StaticSpawnNames.TryGetValue(hash, out var name) ? name : Hash(hash, "Unresolved static encounter");

    public static string Trainer(ulong hash) => TrainerNames.TryGetValue(hash, out var name) ? CleanHashName(name) : Hash(hash, "Unresolved trainer");

    public static IReadOnlyCollection<ulong> ItemHashes => ItemNames.Keys.ToArray();

    public static bool TryGetItemHash(string text, out ulong hash)
    {
        var token = text.Trim();
        if (token.Length == 0)
        {
            hash = 0;
            return false;
        }

        foreach (var (itemHash, name) in ItemNames)
        {
            if (string.Equals(token, name, System.StringComparison.OrdinalIgnoreCase))
            {
                hash = itemHash;
                return true;
            }
        }

        if (TryGetItemID(token, out var itemID) && ItemHashesByID.TryGetValue(itemID, out hash))
            return true;

        if (TryParseHex(token, out hash))
            return true;

        hash = 0;
        return false;
    }

    public static string Hash(ulong hash)
    {
        if (hash is 0 or EmptyHash)
            return "None";

        if (HashNames.TryGetValue(hash, out var name))
            return CleanHashName(name);

        return hash.ToString("X16");
    }

    public static string Hash(ulong hash, string unresolvedLabel)
    {
        var value = Hash(hash);
        return value.Length == 16 && value.All(System.Uri.IsHexDigit) ? $"{unresolvedLabel} ({value})" : value;
    }

    public static string Sign(ulong hash) => Hash(hash, "Unresolved sign");

    public static string Trigger(ulong hash) => Hash(hash, "Unresolved trigger");

    public static string Path(ulong hash) => Hash(hash, "Unresolved path");

    public static string Flag(ulong hash) => Hash(hash, "Unresolved flag");

    public static string CleanPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Replace('\\', '/');
        var slash = text.LastIndexOf('/');
        if (slash >= 0 && slash + 1 < text.Length)
            text = text[(slash + 1)..];

        var dot = text.LastIndexOf('.');
        if (dot > 0)
            text = text[..dot];

        return text;
    }

    public static string CleanEvent(string? value)
    {
        var text = CleanPath(value);
        if (text.StartsWith("Play_", System.StringComparison.OrdinalIgnoreCase))
            text = text[5..];
        if (text.StartsWith("Stop_", System.StringComparison.OrdinalIgnoreCase))
            text = text[5..];

        return text.Replace('_', ' ');
    }

    public static string CleanHashName(string value)
    {
        var text = CleanPath(value);
        return string.IsNullOrWhiteSpace(text) ? value : text;
    }

    public static string Location(float x, float y, float z) => $"({x:0.#}, {y:0.#}, {z:0.#})";

    private static IReadOnlyDictionary<int, ulong> BuildItemHashLookup(IReadOnlyDictionary<ulong, string> itemNames)
    {
        var result = new Dictionary<int, ulong>();
        foreach (var (hash, name) in itemNames)
        {
            if (TryGetItemID(name, out var id))
                result[id] = hash;
        }

        return result;
    }

    private static bool TryGetItemID(string text, out int itemID)
    {
        var close = text.LastIndexOf(')');
        var open = close > 0 ? text.LastIndexOf('(', close - 1) : -1;
        if (open >= 0 && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemID))
            return true;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemID);
    }

    private static bool TryParseHex(string text, out ulong hash)
    {
        var token = text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        hash = 0;
        return token.Length is >= 8 and <= 16 &&
            token.All(System.Uri.IsHexDigit) &&
            ulong.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);
    }
}

internal static class PlacementZoneSummaryUtil
{
    public static string Hash(ulong hash) => PlacementZoneLabelProvider.Hash(hash);

    public static string Hashes(IEnumerable<ulong> hashes, int max = 3)
    {
        var values = hashes.Where(z => PlacementZoneLabelProvider.Hash(z) != "None").Take(max + 1).ToArray();
        if (values.Length == 0)
            return "None";

        var text = string.Join(", ", values.Take(max).Select(PlacementZoneLabelProvider.Item));
        return values.Length > max ? $"{text}, ..." : text;
    }

    public static string Numbers(IEnumerable<uint> values, int max = 3)
    {
        var list = values.Take(max + 1).ToArray();
        if (list.Length == 0)
            return "None";

        var text = string.Join(", ", list.Take(max));
        return list.Length > max ? $"{text}, ..." : text;
    }

    public static string Label(string label, ulong hash) => $"{label}: {Hash(hash)}";

    public static string At(PlacementZoneMetaTripleXYZ? placement)
        => placement == null ? "@ unknown position" : $"@ {placement.Location3f}";

    public static string CountSuffix(int count) => count == 1 ? string.Empty : $" ({count} total)";
}

public partial class PlacementZoneFieldItemHolder
{
    public override string ToString()
    {
        var item = Field00;
        var contents = item.Flags.Count != 0
            ? PlacementZoneSummaryUtil.Hashes(item.Flags)
            : PlacementZoneSummaryUtil.Numbers(item.Items);
        var quantity = item.Quantity == 0 ? string.Empty : $" x{item.Quantity}";
        var model = PlacementZoneLabelProvider.CleanPath(item.Field02);
        var source = string.IsNullOrWhiteSpace(model) ? string.Empty : $" [{model}]";
        return $"Field item {contents}{quantity}{source} {PlacementZoneSummaryUtil.At(item.Field00)}";
    }
}

public partial class PlacementZoneHiddenItemHolder
{
    public override string ToString()
    {
        var item = Field00;
        var contents = item.Field02.Count == 0
            ? "None"
            : string.Join(", ", item.Field02.Take(3).Select(z => z.ToString()));
        if (item.Field02.Count > 3)
            contents += ", ...";

        return $"Hidden item {contents} {PlacementZoneSummaryUtil.At(item.Field00)}";
    }
}

public partial class PlacementZoneHiddenItemChance
{
    public override string ToString() => $"{PlacementZoneLabelProvider.Item(Hash)} x{Quantity} ({Chance}%)";
}

public partial class PlacementZoneFlightAnchorHolder
{
    public override string ToString()
    {
        var anchor = FlightAnchor;
        return $"Fly point Unlock: {PlacementZoneLabelProvider.Flag(anchor.UnlockFlagHash)} {PlacementZoneSummaryUtil.At(anchor.Placement)}";
    }
}

public partial class PlacementZoneNPCHolder
{
    public override string ToString()
    {
        var npc = Field00;
        return $"NPC {PlacementZoneLabelProvider.Object(npc.Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(npc.Field00)}";
    }
}

public partial class PlacementZoneSymbolSpawnHolder
{
    public override string ToString()
    {
        var symbol = Object;
        return $"Symbol {PlacementZoneLabelProvider.Object(symbol.Identifier.HashObjectName)} {PlacementZoneSummaryUtil.At(symbol.Identifier)}";
    }
}

public partial class PlacementZoneTrainerTipHolder
{
    public override string ToString()
    {
        var tip = Field00;
        return $"Trainer tip {PlacementZoneLabelProvider.Object(tip.Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(tip.Field00)}";
    }
}

public partial class PlacementZoneAdvancedTipHolder
{
    public override string ToString()
    {
        var tip = Field00.Field00;
        var model = string.IsNullOrWhiteSpace(tip?.NameModel) ? "Advanced tip" : PlacementZoneLabelProvider.CleanPath(tip.NameModel);
        return $"{model} Sign: {PlacementZoneLabelProvider.Sign(SignHash)} {PlacementZoneSummaryUtil.At(tip?.Field00)}";
    }
}

public partial class PlacementZoneAdvancedTip
{
    public override string ToString() => Field00?.ToString() ?? "Advanced tip";
}

public partial class PlacementZone_F14
{
    public override string ToString()
    {
        var model = PlacementZoneLabelProvider.CleanPath(NameModel);
        return $"{(string.IsNullOrWhiteSpace(model) ? "Advanced tip" : model)} {PlacementZoneSummaryUtil.At(Field00)}";
    }
}

public partial class PlacementZone_F14_B
{
    public override string ToString() => $"{Field00}: {Field03:0.#}, {Field06:0.#}, {Field08:0.#}, {Field09:0.#}, {Field10:0.#}";
}

public partial class PlacementZone_F14_Union
{
    public override string ToString() => $"{(Field00 ? "Enabled" : "Disabled")} {Field01}";
}

public partial class PlacementZone_F14_Sub
{
    public override string ToString() => $"{Field00:0.#}, {Field01:0.#}";
}

public partial class PlacementZoneTriggerHolder
{
    public override string ToString()
    {
        var trigger = Object;
        return $"Trigger {PlacementZoneLabelProvider.Trigger(trigger.TriggerName)} @ {trigger.Field00.Location3f}";
    }
}

public partial class PlacementZoneFieldItem
{
    public override string ToString()
    {
        var contents = Flags.Count != 0 ? PlacementZoneSummaryUtil.Hashes(Flags) : PlacementZoneSummaryUtil.Numbers(Items);
        return $"{contents} x{Quantity} {PlacementZoneSummaryUtil.At(Field00)}";
    }
}

public partial class PlacementZoneFieldItem_A
{
    public override string ToString() => Field00 ? "Enabled" : "Disabled";
}

public partial class PlacementZoneHiddenItem
{
    public override string ToString()
    {
        var contents = Field02.Count == 0 ? "None" : string.Join(", ", Field02.Take(2));
        return $"{contents}{(Field02.Count > 2 ? $", ... ({Field02.Count} total)" : string.Empty)} {PlacementZoneSummaryUtil.At(Field00)}";
    }
}

public partial class PlacementZoneHiddenItemValue
{
    public override string ToString() => $"{Field00}: {Field04:0.#}";
}

public partial class PlacementZoneFlightAnchor
{
    public override string ToString() => $"Unlock: {PlacementZoneLabelProvider.Flag(UnlockFlagHash)} {PlacementZoneSummaryUtil.At(Placement)}";
}

public partial class PlacementZoneNPC
{
    public override string ToString() => $"{PlacementZoneLabelProvider.Object(Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZoneSymbolSpawn
{
    public override string ToString() => $"{PlacementZoneLabelProvider.Object(Identifier.HashObjectName)} {PlacementZoneSummaryUtil.At(Identifier)}";
}

public partial class PlacementZone_F20_Sub
{
    public override string ToString() => $"{Field00}: {Field04:0.#}, {Field06:0.#}, {Field08:0.#}, {Field09:0.#}, {Field10:0.#}";
}

public partial class PlacementZoneTrainerTip
{
    public override string ToString() => $"{PlacementZoneLabelProvider.Object(Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZone_F09
{
    public override string ToString() => $"{Field00}: {Field04:0.#}, {Field06:0.#}, {Field08:0.#}, {Field09:0.#}, {Field10:0.#}";
}

public partial class PlacementZone_F09_Union
{
    public override string ToString() => $"{Field00}: {Field01}";
}

public partial class PlacementZone_F09_Sub
{
    public override string ToString() => $"{Field00:0.#}, {Field01:0.#}";
}

public partial class PlacementZoneTrigger
{
    public override string ToString() => $"{PlacementZoneLabelProvider.Trigger(TriggerName)} @ {Field00.Location3f}";
}

public partial class PlacementZone_F10
{
    public override string ToString()
    {
        var name = string.IsNullOrWhiteSpace(PlayName) ? "Environment" : PlacementZoneLabelProvider.CleanEvent(PlayName);
        return $"{name} {PlacementZoneSummaryUtil.At(Field00)}";
    }
}

public partial class PlacementZoneStepJump
{
    public override string ToString() => PlacementZoneSummaryUtil.At(Field00);
}

public partial class PlacementZoneLadder
{
    public override string ToString() => PlacementZoneSummaryUtil.At(Field00);
}

public partial class PlacementZone_F23_Sub
{
    public override string ToString() => $"{Field00}: {Field04:0.#}";
}

public partial class PlacementZonePopupHolder
{
    public override string ToString()
    {
        var popup = Field00;
        return $"Popup {PlacementZoneSummaryUtil.Hash(popup.Hash06)} {PlacementZoneSummaryUtil.At(popup.Field00)}";
    }
}

public partial class PlacementZone_F24
{
    public override string ToString() => $"Popup {PlacementZoneSummaryUtil.Hash(Hash06)} {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZone_F24_IntFloat
{
    public override string ToString() => $"{Field00}: {Field04:0.#}";
}

public partial class PlacementZone_F24_Table
{
    public override string ToString() => $"{PlacementZoneSummaryUtil.Hash(Hash00)} -> {PlacementZoneSummaryUtil.Hash(Hash01)} ({Field02})";
}

public partial class PlacementZoneEnvironmentHolder
{
    public override string ToString()
    {
        var environment = Field00;
        var name = string.IsNullOrWhiteSpace(environment.PlayName) ? "Environment" : PlacementZoneLabelProvider.CleanEvent(environment.PlayName);
        return $"{name} {PlacementZoneSummaryUtil.At(environment.Field00)}";
    }
}

public partial class PlacementZoneStepJumpHolder
{
    public override string ToString()
    {
        var jump = Field00;
        return $"Step jump {PlacementZoneSummaryUtil.At(jump.Field00)}";
    }
}

public partial class PlacementZoneLadderHolder
{
    public override string ToString()
    {
        var ladder = Field00;
        return $"Ladder {PlacementZoneSummaryUtil.At(ladder.Field00)}";
    }
}

public partial class PlacementZoneNestHoleHolder
{
    public override string ToString()
    {
        var nest = Field00.Field00;
        return $"Nest common {PlacementZoneSummaryUtil.Hash(Common)}, rare {PlacementZoneSummaryUtil.Hash(Rare)} {PlacementZoneSummaryUtil.At(nest.Field00)}";
    }
}

public partial class PlacementZone_F21_A
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F21_B
{
    public override string ToString() => PlacementZoneSummaryUtil.At(Field00);
}

public partial class PlacementZone_F21_IntFloat
{
    public override string ToString() => $"{Field00}: {Field04:0.#}";
}

public partial class PlacementZone_F21_BoolObject14
{
    public override string ToString() => $"{Type}: {Object}";
}

public partial class PlacementZone_F21_Inner
{
    public override string ToString() => $"{Field00:0.#}, {Field01:0.#}";
}

public partial class PlacementZoneBerryTreeHolder
{
    public override string ToString()
    {
        var tree = Field00.Field00;
        return $"Berry tree ({Field01.Count} drops) {PlacementZoneSummaryUtil.At(tree.Field00)}";
    }
}

public partial class PlacementZone_F22_0
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F22_0_0
{
    public override string ToString() => PlacementZoneSummaryUtil.At(Field00);
}

public partial class PlacementZone_F22_Sub
{
    public override string ToString() => $"{Field00}: {Field04:0.#}";
}

public partial class PlacementZone_F22_BoolObject14
{
    public override string ToString() => $"{Type}: {Object}";
}

public partial class PlacementZone_F22_Inner
{
    public override string ToString() => $"{Field00:0.#}, {Field01:0.#}";
}

public partial class PlacementZoneBerryTreeRandom
{
    public override string ToString() => $"{PlacementZoneSummaryUtil.Hash(Hash)} x{Quantity} ({Rate})";
}

public partial class PlacementZonePokeCenterSpawnAnchorHolder
{
    public override string ToString() => $"Pokemon Center anchor {PlacementZoneSummaryUtil.At(Field00.Field00)}";
}

public partial class PlacementZone_F12
{
    public override string ToString() => PlacementZoneSummaryUtil.At(Field00);
}

public partial class PlacementZoneQuadrantHolder
{
    public override string ToString()
    {
        var quadrant = Field00;
        return $"Quadrant {PlacementZoneLabelProvider.Object(quadrant.Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(quadrant.Field00)}";
    }
}

public partial class PlacementZone_F17
{
    public override string ToString() => $"{PlacementZoneLabelProvider.Object(Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZone_F17_Sub
{
    public override string ToString() => $"{Field00}: {Field01:0.#}, {Field03:0.#}, {Field04:0.#}, {Field06:0.#}, {Field08:0.#}, {Field09:0.#}, {Field10:0.#}";
}

public partial class PlacementZoneIKStepHolder
{
    public override string ToString()
    {
        var step = Field00;
        return $"IK step {PlacementZoneSummaryUtil.Hash(step.Field01)} {PlacementZoneSummaryUtil.At(step.Field00)}";
    }
}

public partial class PlacementZone_F25
{
    public override string ToString() => $"{PlacementZoneSummaryUtil.Hash(Field01)} {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZone_F25_X
{
    public override string ToString() => $"{Field00}: {Field06:0.#}, {Field08:0.#}, {Field09:0.#}, {Field10:0.#}";
}

public partial class PlacementZoneMovementPathHolder
{
    public override string ToString() => $"Path {PlacementZoneLabelProvider.Path(PathName)} ({Field05.Count} points) {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZoneStaticObjectsHolder
{
    public override string ToString()
    {
        var obj = Object;
        return $"Static object {PlacementZoneLabelProvider.Object(obj.Identifier.HashObjectName)} ({obj.Spawns.Count} spawns) {PlacementZoneSummaryUtil.At(obj.Identifier)}";
    }
}

public partial class PlacementZoneStaticObject
{
    public override string ToString()
        => $"{PlacementZoneLabelProvider.Object(Identifier.HashObjectName)} ({Spawns.Count} spawns) {PlacementZoneSummaryUtil.At(Identifier)}";
}

public partial class PlacementZoneStaticObjectSpawn
{
    public override string ToString()
    {
        var behavior = string.IsNullOrWhiteSpace(Behavior)
            ? string.Empty
            : $" [{PlacementZoneLabelProvider.CleanHashName(Behavior)}]";
        return $"{PlacementZoneLabelProvider.StaticSpawn(SpawnID)}{behavior}";
    }
}

public partial class PlacementZoneRotomRallyEntry
{
    public override string ToString() => $"Rotom Rally {PlacementZoneSummaryUtil.At(Field00)}";
}

public partial class PlacementZoneUnitObject
{
    public override string ToString()
    {
        var model = PlacementZoneLabelProvider.CleanPath(NameModel);
        return $"{(string.IsNullOrWhiteSpace(model) ? "Unit object" : model)} {PlacementZoneSummaryUtil.At(Field00)}";
    }
}

public partial class PlacementZoneUnitObjectDetails
{
    public override string ToString() => $"{Field00}: {Field04:0.#}, {Field06:0.#}, {Field08:0.#}, {Field09:0.#}, {Field10:0.#}";
}

public partial class PlacementZoneUnitObjectToggle
{
    public override string ToString() => $"{(Field00 ? "Enabled" : "Disabled")} {Field01}";
}

public partial class PlacementZoneUnitObjectInner
{
    public override string ToString() => $"{Field00:0.#}, {Field01:0.#}";
}

public partial class PlacementZone_F16
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F16_A
{
    public override string ToString()
    {
        var hashModel = PlacementZoneSummaryUtil.Hash(HashModel);
        if (PlacementZoneOtherNPCHolder.Models.TryGetValue(HashModel, out var model))
            hashModel = model;

        return $"{hashModel}: {PlacementZoneLabelProvider.Object(Identifier.HashObjectName)} {PlacementZoneSummaryUtil.At(Identifier)}";
    }
}

public partial class PlacementZone_F16_ArrayEntry
{
    public override string ToString() => $"{Field00}, {Field01}, {Field02}, {Field03:0.#}, {Field04}, {Field05:0.#}";
}

public partial class PlacementZone_F16_IntFloat
{
    public override string ToString() => $"{Field00}: {Field04:0.#}";
}

public partial class PlacementZone_F08
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F08_A
{
    public override string ToString()
    {
        var model = PlacementZoneLabelProvider.Model(HashModel);
        return $"{model}: {PlacementZoneLabelProvider.Object(Field00.HashObjectName)} {PlacementZoneSummaryUtil.At(Field00)}";
    }
}

public partial class PlacementZone_F08_ArrayEntry
{
    public override string ToString() => $"Type {Field00}, {Field03:0.#}, {Field04}, {PlacementZoneSummaryUtil.Hash(Field05)}";
}

public partial class PlacementZone_F08_Nine
{
    public override string ToString() => $"Flags: {Field00}, {Field01}, {Field02}, {Field05}; Hashes: {PlacementZoneSummaryUtil.Hash(Hash04)}, {PlacementZoneSummaryUtil.Hash(Hash07)}";
}

public partial class PlacementZone_F08_IntFloat
{
    public override string ToString() => $"Type {Field00}: {Field04:0.#}";
}

public partial class PlacementZone_F02_Nine
{
    public override string ToString() => $"{Field00}, {Field01}, {Field02}, {PlacementZoneSummaryUtil.Hash(Hash04)}, {Field05}, {PlacementZoneSummaryUtil.Hash(Hash07)}";
}

public partial class PlacementZone_F02
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F02_Field1
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F02_Inner
{
    public override string ToString() => Field00.ToString();
}

public partial class PlacementZone_F02_IntFloat
{
    public override string ToString() => $"{Field00}: {Field01:0.#}, {Field02:0.#}, {Field03:0.#}, {Field04:0.#}";
}

public partial class FlatDummyObject
{
    public override string ToString() => Field00 == 0 ? "Empty" : Field00.ToString();
}

public partial class FlatDummyEntry
{
    public override string ToString() => "Empty";
}

public partial class PlacementZoneDeepX
{
    public string Location3f => PlacementZoneLabelProvider.Location(Field00, Field01, Field02);

    public override string ToString() => $"{Location3f} {PlacementZoneSummaryUtil.Hash(Field09)}";
}

public partial class PlacementZoneDeepY
{
    public override string ToString() => $"{Field00}: {Field01}, {Field04}, {Field06}, {Field08}";
}
