using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace pkNX.WinForms;

internal static class TextSyntaxHelper
{
    private static readonly Regex VariableRegex = new(@"(?<!\\)\[VAR ([^\]\(]+)(?:\(([^\]]*)\))?\]", RegexOptions.Compiled);
    private static readonly Regex WaitRegex = new(@"(?<!\\)\[WAIT ([0-9]+)\]", RegexOptions.Compiled);
    private static readonly Regex NullTextRegex = new(@"(?<!\\)\[~ ([0-9]+)\]", RegexOptions.Compiled);
    private static readonly Regex RubyRegex = new(@"(?<!\\)\{([^|{}]+)\|([^|{}]+)(?:\|([^|{}]+))?\}", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> VariableDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0100"] = "trainer/player name",
        ["TRNAME"] = "trainer/player name",
        ["0101"] = "Pokemon species name",
        ["PKNAME"] = "Pokemon species name",
        ["0102"] = "Pokemon nickname",
        ["PKNICK"] = "Pokemon nickname",
        ["0103"] = "type name",
        ["TYPE"] = "type name",
        ["0105"] = "location name",
        ["LOCATION"] = "location name",
        ["0106"] = "ability name",
        ["ABILITY"] = "ability name",
        ["0107"] = "move name",
        ["MOVE"] = "move name",
        ["0108"] = "item name",
        ["ITEM1"] = "item name",
        ["0109"] = "item name",
        ["ITEM2"] = "item name",
        ["010A"] = "bag item name",
        ["sTRBAG"] = "bag item name",
        ["010B"] = "box name",
        ["BOX"] = "box name",
        ["010C"] = "Pokemon name",
        ["010D"] = "EV stat name",
        ["EVSTAT"] = "EV stat name",
        ["010E"] = "trainer class",
        ["0114"] = "speaker/message tag",
        ["0189"] = "trainer nickname",
        ["TRNICK"] = "trainer nickname",
        ["01D3"] = "met location phrase",
        ["01D6"] = "met date/place phrase",
        ["01D7"] = "memory text",
        ["01D8"] = "memory/feeling text",
        ["01AF"] = "online/event value",
        ["01BF"] = "online/event text",
        ["01C1"] = "DLC/event text",
        ["01C2"] = "DLC/event value",
        ["0200"] = "number slot 1",
        ["NUM1"] = "number slot 1",
        ["0201"] = "number slot 2",
        ["NUM2"] = "number slot 2",
        ["0202"] = "number slot 3",
        ["NUM3"] = "number slot 3",
        ["0203"] = "number slot 4",
        ["NUM4"] = "number slot 4",
        ["0204"] = "number slot 5",
        ["NUM5"] = "number slot 5",
        ["0205"] = "number slot 6",
        ["NUM6"] = "number slot 6",
        ["0206"] = "formatted number",
        ["0207"] = "formatted number/currency",
        ["0208"] = "number slot 9",
        ["NUM9"] = "number slot 9",
        ["1000"] = "singular/plural item prefix",
        ["ITEMPLUR0"] = "singular/plural item prefix",
        ["1001"] = "plural item prefix",
        ["ITEMPLUR1"] = "plural item prefix",
        ["1003"] = "name/article prefix",
        ["1100"] = "gendered word selector",
        ["GENDBR"] = "gendered word selector",
        ["1101"] = "plural suffix selector",
        ["NUMBRNCH"] = "plural suffix selector",
        ["1300"] = "text style start",
        ["1301"] = "text style start",
        ["1302"] = "text highlight/color",
        ["iCOLOR2"] = "text highlight/color",
        ["1303"] = "text highlight/color",
        ["iCOLOR3"] = "text highlight/color",
        ["BD06"] = "battle text prefix",
        ["BE05"] = "message timing/sound cue",
        ["FF00"] = "text color/style",
        ["COLOR"] = "text color/style",
    };

    private static readonly TextVariableDefinition[] Variables =
    [
        new(TextVariableGroup.Pokemon, "Pokemon Species", "0101", "0000", "Inserts a Pokemon species name from the script's runtime value slot."),
        new(TextVariableGroup.Pokemon, "Pokemon Nickname", "0102", "0000", "Inserts a Pokemon nickname from the script's runtime value slot."),
        new(TextVariableGroup.Pokemon, "Pokemon Name", "010C", "0000", "Inserts a Pokemon name value. Some contexts use this instead of the species-only variable."),
        new(TextVariableGroup.Pokemon, "Player / Trainer Name", "0100", "0000", "Inserts the player or trainer name currently provided by the script."),
        new(TextVariableGroup.Pokemon, "Trainer Nickname", "0189", "0000", "Inserts a trainer nickname or related trainer-name text value."),

        new(TextVariableGroup.Item, "Item Name", "0109", "0000", "Inserts an item name from the script's runtime value slot."),
        new(TextVariableGroup.Item, "Item Name Alt", "0108", "0000", "Alternate item-name variable used by some scripts and UI text."),
        new(TextVariableGroup.Item, "Bag Item Name", "010A", "0000", "Inserts an item name using the bag-item text formatter."),
        new(TextVariableGroup.Item, "Item Plural Prefix", "1000", string.Empty, "Adds an item article/pluralization prefix when the script expects item-count grammar."),
        new(TextVariableGroup.Item, "Plural Item Prefix", "1001", string.Empty, "Adds a plural item prefix/suffix selector used by some item quantity messages."),

        new(TextVariableGroup.Move, "Move Name", "0107", "0000", "Inserts a move name from the script's runtime value slot."),

        new(TextVariableGroup.Number, "Number Slot 1", "0200", "0000", "Inserts the first numeric runtime value supplied by the script."),
        new(TextVariableGroup.Number, "Number Slot 2", "0201", "0000", "Inserts the second numeric runtime value supplied by the script."),
        new(TextVariableGroup.Number, "Number Slot 3", "0202", "0000", "Inserts the third numeric runtime value supplied by the script."),
        new(TextVariableGroup.Number, "Number Slot 4", "0203", "0000", "Inserts the fourth numeric runtime value supplied by the script."),
        new(TextVariableGroup.Number, "Number Slot 5", "0204", "0000", "Inserts the fifth numeric runtime value supplied by the script."),
        new(TextVariableGroup.Number, "Number Slot 6", "0205", "0000", "Inserts the sixth numeric runtime value supplied by the script."),
        new(TextVariableGroup.Number, "Formatted Number", "0206", "0000", "Inserts a formatted numeric runtime value."),
        new(TextVariableGroup.Number, "Formatted Number / Currency", "0207", "0000", "Inserts a formatted number, often used for money or currency-like values."),
        new(TextVariableGroup.Number, "Number Slot 9", "0208", "0000", "Inserts the ninth numeric runtime value supplied by the script."),
    ];

    public static IReadOnlyList<TextVariableDefinition> GetVariables(TextVariableGroup group)
        => Variables.Where(z => z.Group == group).ToArray();

    public static string BuildVariableToken(string code, string args)
    {
        var cleanCode = code.Trim();
        var cleanArgs = args.Trim();
        return cleanArgs.Length == 0 ? $"[VAR {cleanCode}]" : $"[VAR {cleanCode}({cleanArgs})]";
    }

    public static string GetReadableTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var readable = text;
        readable = WaitRegex.Replace(readable, m => $"[pause {m.Groups[1].Value} frames]");
        readable = NullTextRegex.Replace(readable, m => $"[empty/link line {m.Groups[1].Value}]");
        readable = RubyRegex.Replace(readable, m => $"{m.Groups[1].Value} (ruby: {m.Groups[2].Value})");
        readable = VariableRegex.Replace(readable, GetReadableVariablePreview);

        return readable
            .Replace(@"\\", "[literal backslash]")
            .Replace(@"\[", "[")
            .Replace(@"\{", "{")
            .Replace(@"\n", "[line break]")
            .Replace(@"\r", "[wait + scroll]")
            .Replace(@"\c", "[wait + clear]");
    }

    public static string GetReadableTextToolTip(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "Empty line. Empty saved lines are encoded by the text writer as blank text placeholders.";

        var tips = new List<string>();
        if (text.Contains(@"\n"))
            tips.Add(@"\n = line break inside the same text box.");
        if (text.Contains(@"\r"))
            tips.Add(@"\r = wait for input, then scroll to the next text page.");
        if (text.Contains(@"\c"))
            tips.Add(@"\c = wait for input, then clear/end the text box.");
        if (WaitRegex.IsMatch(text))
            tips.Add("[WAIT n] = pause for n frames.");
        if (NullTextRegex.IsMatch(text))
            tips.Add("[~ n] = blank placeholder that points at line n.");
        if (VariableRegex.IsMatch(text))
            tips.Add("[VAR code(args)] = runtime value inserted by the game. Args are usually value slots, indexes, style IDs, or formatting parameters.");
        if (RubyRegex.IsMatch(text))
            tips.Add("{base|ruby} = ruby/furigana annotation used by Japanese text.");

        return tips.Count == 0
            ? "Plain text line."
            : "Detected text codes:" + Environment.NewLine + string.Join(Environment.NewLine, tips);
    }

    public static string RawToFriendly(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        return raw
            .Replace(@"\n", Environment.NewLine)
            .Replace(@"\r", "[WAIT_SCROLL]")
            .Replace(@"\c", "[WAIT_CLEAR]");
    }

    public static string FriendlyToRaw(string friendly)
    {
        if (string.IsNullOrEmpty(friendly))
            return string.Empty;

        var text = friendly
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("[WAIT_SCROLL]", @"\r", StringComparison.OrdinalIgnoreCase)
            .Replace("[WAIT_CLEAR]", @"\c", StringComparison.OrdinalIgnoreCase);

        return text.Replace("\n", @"\n");
    }

    private static string GetReadableVariablePreview(Match match)
    {
        var code = match.Groups[1].Value;
        var args = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
        var label = VariableDescriptions.TryGetValue(code, out var description)
            ? description
            : $"variable {code}";

        return args.Length == 0
            ? $"[{label}]"
            : $"[{label}: {args}]";
    }
}

internal enum TextVariableGroup
{
    Pokemon,
    Item,
    Move,
    Number,
}

internal sealed record TextVariableDefinition(
    TextVariableGroup Group,
    string Name,
    string Code,
    string DefaultArgs,
    string Description)
{
    public string Token => TextSyntaxHelper.BuildVariableToken(Code, DefaultArgs);
}
