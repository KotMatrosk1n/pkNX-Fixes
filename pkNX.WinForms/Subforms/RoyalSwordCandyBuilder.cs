using pkNX.Containers;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers;
using pkNX.Structures.FlatBuffers.SWSH;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwShShopInventory = pkNX.Structures.FlatBuffers.SWSH.ShopInventory;

namespace pkNX.WinForms;

public sealed class RoyalSwordCandyBuilderForm : Form
{
    private const int DefaultRoyalCandyItemId = 1128;
    private const int DefaultTemplateItemId = 50;
    private const int DefaultVirtualCount = 999;

    private readonly string RomFsPath;
    private readonly string ExeFsPath;
    private readonly NumericUpDown ItemIdBox = new();
    private readonly NumericUpDown TemplateIdBox = new();
    private readonly TextBox OutputPathBox = new();
    private readonly Button BrowseOutputButton = new();
    private readonly CheckBox RomFsCheck = new();
    private readonly CheckBox ExeFsCheck = new();
    private readonly CheckBox InfiniteUseCheck = new();
    private readonly CheckBox StoryLadderCheck = new();
    private readonly CheckBox VirtualCountCheck = new();
    private readonly CheckBox MaxCapCheck = new();
    private readonly NumericUpDown VirtualCountBox = new();
    private readonly NumericUpDown MaxCapBox = new();
    private readonly CheckBox OverwriteCheck = new();
    private readonly Button ValidateButton = new();
    private readonly Button BuildButton = new();
    private readonly Button OpenOutputButton = new();
    private readonly Button CopyLogButton = new();
    private readonly DataGridView ResultGrid = new();
    private readonly TextBox LogText = new();
    private readonly ToolTip ButtonToolTips = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 450,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    public RoyalSwordCandyBuilderForm(string romFsPath, string exefsPath)
    {
        RomFsPath = romFsPath;
        ExeFsPath = exefsPath;

        Text = "Royal Sword Candy Builder";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 690);
        Size = new Size(1280, 760);

        InitializeLayout();
        ApplyTheme();
        LoadDefaults();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ButtonToolTips.Dispose();

        base.Dispose(disposing);
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var settings = new TableLayoutPanel
        {
            ColumnCount = 8,
            Dock = DockStyle.Fill,
            RowCount = 3,
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        ConfigureNumeric(ItemIdBox, 1, 0xFFF, DefaultRoyalCandyItemId);
        ConfigureNumeric(TemplateIdBox, 1, 0xFFF, DefaultTemplateItemId);
        ConfigureNumeric(VirtualCountBox, 1, 0xFFFF, DefaultVirtualCount);
        ConfigureNumeric(MaxCapBox, 1, 100, 23);
        VirtualCountBox.Enabled = true;
        MaxCapBox.Enabled = false;

        ConfigureCheck(RomFsCheck, "RomFS", true, "Generate item table, item text, and shop inventory files.");
        ConfigureCheck(ExeFsCheck, "ExeFS", true, "Generate the Royal Candy main NSO patch.");
        ConfigureCheck(InfiniteUseCheck, "Infinite Use", true, "Keep Royal Candy from decrementing after use.");
        ConfigureCheck(StoryLadderCheck, "Story Ladder", true, "Use the Royal Sword story cap ladder instead of a fixed cap.");
        ConfigureCheck(VirtualCountCheck, "Virtual Count", true, "Report a virtual inventory count for Royal Candy in the bag UI.");
        ConfigureCheck(MaxCapCheck, "Max Cap", false, "Generate a diagnostic output capped at this milestone.");
        MaxCapCheck.CheckedChanged += (_, _) => MaxCapBox.Enabled = MaxCapCheck.Checked;

        OutputPathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        OutputPathBox.Margin = new Padding(0, 4, 8, 4);
        ConfigureActionButton(BrowseOutputButton, "Browse", "Choose the LayeredFS output folder.", BrowseOutput);

        settings.Controls.Add(CreateLabel("Item ID"), 0, 0);
        settings.Controls.Add(ItemIdBox, 1, 0);
        settings.Controls.Add(CreateLabel("Template"), 2, 0);
        settings.Controls.Add(TemplateIdBox, 3, 0);
        settings.Controls.Add(CreateLabel("Output"), 4, 0);
        settings.Controls.Add(OutputPathBox, 5, 0);
        settings.Controls.Add(BrowseOutputButton, 6, 0);
        settings.Controls.Add(CreateOutputModePanel(), 7, 0);

        settings.Controls.Add(RomFsCheck, 0, 1);
        settings.SetColumnSpan(RomFsCheck, 2);
        settings.Controls.Add(ExeFsCheck, 2, 1);
        settings.SetColumnSpan(ExeFsCheck, 2);
        settings.Controls.Add(InfiniteUseCheck, 4, 1);
        settings.SetColumnSpan(InfiniteUseCheck, 2);
        settings.Controls.Add(StoryLadderCheck, 6, 1);
        settings.SetColumnSpan(StoryLadderCheck, 2);

        settings.Controls.Add(VirtualCountCheck, 0, 2);
        settings.SetColumnSpan(VirtualCountCheck, 2);
        settings.Controls.Add(VirtualCountBox, 2, 2);
        settings.Controls.Add(MaxCapCheck, 3, 2);
        settings.Controls.Add(MaxCapBox, 4, 2);
        var outputNote = CreateLabel("RomFS and ExeFS are written into the selected LayeredFS output folder.");
        settings.Controls.Add(outputNote, 5, 2);
        settings.SetColumnSpan(outputNote, 3);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 14, 0, 0),
            WrapContents = false,
        };
        ConfigureActionButton(ValidateButton, "Validate", "Run the full builder into a temporary dry-run folder.", ValidateBuild);
        ConfigureActionButton(BuildButton, "Build", "Write the selected Royal Candy output folder.", BuildOutput);
        ConfigureActionButton(OpenOutputButton, "Open", "Open the selected output folder.", OpenOutput);
        ConfigureActionButton(CopyLogButton, "Copy Log", "Copy the current builder log.", CopyLog);
        actions.Controls.Add(ValidateButton);
        actions.Controls.Add(BuildButton);
        actions.Controls.Add(OpenOutputButton);
        actions.Controls.Add(CopyLogButton);

        ConfigureGrid(ResultGrid);
        ResultGrid.Columns.Add(CreateTextColumn("Status", 86));
        ResultGrid.Columns.Add(CreateTextColumn("Area", 100));
        ResultGrid.Columns.Add(CreateTextColumn("Output", 250));
        ResultGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Message",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 430,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        LogText.Dock = DockStyle.Fill;
        LogText.Multiline = true;
        LogText.ReadOnly = true;
        LogText.ScrollBars = ScrollBars.Vertical;
        LogText.WordWrap = false;

        root.Controls.Add(settings, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(ResultGrid, 0, 2);
        root.Controls.Add(LogText, 0, 3);
        Controls.Add(root);
    }

    private FlowLayoutPanel CreateOutputModePanel()
    {
        OverwriteCheck.Text = "Overwrite";
        OverwriteCheck.Checked = true;
        OverwriteCheck.AutoSize = true;
        OverwriteCheck.Margin = new Padding(0, 9, 0, 0);
        ButtonToolTips.SetToolTip(OverwriteCheck, "Allow generated files to overwrite existing files in the output folder.");

        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Controls = { OverwriteCheck },
        };
    }

    private void LoadDefaults()
    {
        OutputPathBox.Text = GetDefaultOutputPath();
        VirtualCountCheck.CheckedChanged += (_, _) => VirtualCountBox.Enabled = VirtualCountCheck.Checked;
        SetResults([
            new("Info", "Project", string.Empty, $"RomFS: {RomFsPath}"),
            new("Info", "Project", string.Empty, $"ExeFS: {ExeFsPath}"),
            new("Info", "Builder", string.Empty, "Ready to validate or build the Royal Candy LayeredFS output."),
        ]);
    }

    private string GetDefaultOutputPath()
    {
        var root = Directory.GetParent(RomFsPath)?.FullName ?? RomFsPath;
        return Path.Combine(root, "royal-candy-1128-royal-sword-full-ladder");
    }

    private void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose Royal Candy LayeredFS output folder",
            SelectedPath = Directory.Exists(OutputPathBox.Text) ? OutputPathBox.Text : Directory.GetParent(OutputPathBox.Text)?.FullName,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            OutputPathBox.Text = dialog.SelectedPath;
    }

    private void ValidateBuild() => RunBuild(validateOnly: true);
    private void BuildOutput() => RunBuild(validateOnly: false);

    private void RunBuild(bool validateOnly)
    {
        var options = CreateOptions(validateOnly);
        var outputLabel = validateOnly ? "Dry Run" : "Build";
        ToggleActions(false);
        try
        {
            var summary = RoyalCandyLayeredFsBuilder.Build(options);
            SetResults(summary.Results);
            LogText.Text = string.Join(Environment.NewLine, summary.Notes);
            if (validateOnly)
                TryDeleteDirectory(options.OutputPath);
            else
                MessageBox.Show(this, "Royal Candy output was generated.", "Royal Sword Candy Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            SetResults([
                new("Fail", outputLabel, string.Empty, ex.Message),
            ]);
            LogText.Text = ex.ToString();
        }
        finally
        {
            ToggleActions(true);
        }
    }

    private RoyalCandyBuildOptions CreateOptions(bool validateOnly)
    {
        var itemId = (int)ItemIdBox.Value;
        var templateId = (int)TemplateIdBox.Value;
        var output = validateOnly
            ? Path.Combine(Path.GetTempPath(), "RoyalSwordCandyBuilder", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture))
            : OutputPathBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Choose an output folder first.");
        if (!validateOnly && Directory.Exists(output) && !OverwriteCheck.Checked)
            throw new InvalidOperationException("The output folder already exists and overwrite is disabled.");

        return new(
            RomFsPath,
            ExeFsPath,
            output,
            itemId,
            templateId,
            RomFsCheck.Checked,
            ExeFsCheck.Checked,
            StoryLadderCheck.Checked,
            InfiniteUseCheck.Checked,
            VirtualCountCheck.Checked ? (int)VirtualCountBox.Value : null,
            MaxCapCheck.Checked ? (int)MaxCapBox.Value : null);
    }

    private void OpenOutput()
    {
        var path = OutputPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return;

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void CopyLog()
    {
        if (!string.IsNullOrWhiteSpace(LogText.Text))
            Clipboard.SetText(LogText.Text);
    }

    private void SetResults(IEnumerable<BuildResult> results)
    {
        ResultGrid.Rows.Clear();
        foreach (var result in results)
            ResultGrid.Rows.Add(result.Status, result.Area, result.Output, result.Message);
    }

    private void ToggleActions(bool enabled)
    {
        ValidateButton.Enabled = enabled;
        BuildButton.Enabled = enabled;
        BrowseOutputButton.Enabled = enabled;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Dry-run cleanup should not hide the validation result.
        }
    }

    private static void ConfigureNumeric(NumericUpDown box, int min, int max, int value)
    {
        box.Minimum = min;
        box.Maximum = max;
        box.Value = value;
        box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        box.Margin = new Padding(0, 4, 12, 4);
        box.ThousandsSeparator = false;
    }

    private void ConfigureCheck(CheckBox box, string text, bool value, string tooltip)
    {
        box.Text = text;
        box.Checked = value;
        box.AutoSize = true;
        box.Margin = new Padding(0, 10, 8, 0);
        ButtonToolTips.SetToolTip(box, tooltip);
    }

    private void ConfigureActionButton(Button button, string text, string tooltip, Action action)
    {
        button.Text = text;
        button.Width = 96;
        button.Height = 32;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Click += (_, _) => action();
        ButtonToolTips.SetToolTip(button, tooltip);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.Dock = DockStyle.Fill;
        grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    }

    private static Label CreateLabel(string text) => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, int width) => new()
    {
        HeaderText = header,
        Width = width,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    private void ApplyTheme()
    {
        BackColor = SystemColors.Control;
        ResultGrid.BackgroundColor = SystemColors.Window;
        LogText.BackColor = SystemColors.Window;
        LogText.ForeColor = SystemColors.WindowText;
    }
}

internal static class RoyalCandyLayeredFsBuilder
{
    private const int RareCandyItemId = 50;
    private const int RoyalCandyItemId = 1128;
    private const int RareCandyUiHookCodeCaveSearchStart = 0x007BC338;
    private const int KeyItemType = 9;
    private const byte KeyItemTypeByte = KeyItemType;
    private const ulong EarlyPokeMartHash = 0x1F3FF031A3A24490;
    private const ulong BadgePokeMartInventoryHash = 0x66CA73B2966BB871;
    private const ulong SceneMainMasterWorkHash = 0x00188D41BB7B57FB;
    private const ulong FirstHopFlagHash = 0x005A329212277F11;
    private const string ItemPath = "bin/pml/item/item.dat";
    private const string ShopPath = "bin/appli/shop/bin/shop_data.bin";
    private const string MessageRoot = "bin/message";
    private const string ItemInfoFile = "iteminfo.dat";
    private const string RoyalCandyName = "Royal Candy";
    private const string RoyalCandyPluralName = "Royal Candies";
    private const string RoyalCandyDescription = "Raises one Pokemon's level up to the\ncurrent Royal Candy cap.";

    public static RoyalCandyBuildSummary Build(RoyalCandyBuildOptions options)
    {
        var results = new List<BuildResult>();
        var notes = new List<string>
        {
            "Royal Sword Candy Builder",
            "=========================",
            "",
            $"Item id: {options.ItemId}",
            $"Template item id: {options.TemplateItemId}",
            $"Output: {options.OutputPath}",
            $"Max story cap: {(options.MaxStoryCap is { } cap ? cap.ToString(CultureInfo.InvariantCulture) : "full ladder")}",
            "",
        };

        Directory.CreateDirectory(options.OutputPath);

        if (options.BuildRomFs)
        {
            PatchItemData(options, results, notes);
            PatchItemText(options, results, notes);
            PatchShopData(options, results, notes);
        }

        if (options.BuildExeFs)
            PatchExeFsMain(options, results, notes);

        WriteReadme(options, notes);
        results.Add(new("Pass", "Output", "README.md", "Generated Royal Candy build notes."));
        return new(results, notes);
    }

    private static void PatchItemData(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        var sourcePath = GetRomFsPath(options.RomFsPath, ItemPath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Could not find Sword/Shield item table.", sourcePath);

        var itemData = File.ReadAllBytes(sourcePath);
        var items = Item8.GetArray(itemData);
        if ((uint)options.ItemId >= (uint)items.Length)
            throw new ArgumentOutOfRangeException(nameof(options.ItemId), $"Item id {options.ItemId} is outside the item table.");
        if ((uint)options.TemplateItemId >= (uint)items.Length)
            throw new ArgumentOutOfRangeException(nameof(options.TemplateItemId), $"Template item id {options.TemplateItemId} is outside the item table.");

        var template = items[options.TemplateItemId];
        var rareCandy = items[RareCandyItemId];
        var target = items[options.ItemId];
        var originalTarget = target.Data.ToArray();
        var sharedIds = GetIdsSharingEntry(itemData, options.ItemId).ToArray();

        template.Data.CopyTo(target.Data);
        target.Price = 1;
        target.PriceWatts = 0;
        target.PriceAlternate = template.PriceAlternate;
        target.Pouch = Item8.PouchID.Key;
        target.PouchFlags = rareCandy.PouchFlags;
        target.EffectField = FieldItemType.Medicine;
        target.CanUseOnPokemon = true;
        target.ItemType = KeyItemTypeByte;
        target.LevelUp = true;
        target.ItemSprite = rareCandy.ItemSprite;
        target.GroupType = Item8.GroupIndexType.None;
        target.GroupIndex = 0;
        target.SortIndex = template.SortIndex;

        var patched = Item8.SetArray(items, itemData);
        patched = AppendUniqueItemRow(patched, options.ItemId, target.Data);
        WriteOutputBytes(options.OutputPath, "romfs/" + ItemPath, patched);

        var notesPath = Path.Combine(options.OutputPath, "royal_candy_item_row_notes.txt");
        File.WriteAllText(notesPath, string.Join(Environment.NewLine, [
            "Royal Candy item row patch",
            "==========================",
            "",
            $"Template source id: {options.TemplateItemId}",
            $"Royal Candy item id: {options.ItemId}",
            "",
            "The selected item row is cloned from the template, adjusted into a Key Items pocket level-up item, and pointed at a new unique raw item row.",
            $"Vanilla candidate bytes: {Convert.ToHexString(originalTarget)}",
            $"Template bytes:          {Convert.ToHexString(template.Data.ToArray())}",
            $"Patched candidate bytes: {Convert.ToHexString(target.Data.ToArray())}",
            $"Original shared row ids: {FormatSharedIds(sharedIds, options.ItemId)}",
        ]));

        results.Add(new("Pass", "RomFS", "romfs/" + ItemPath, "Royal Candy item row generated."));
        notes.Add($"- Generated romfs/{ItemPath} from item {options.TemplateItemId} into item {options.ItemId}.");
    }

    private static void PatchItemText(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        var messageRoot = GetRomFsPath(options.RomFsPath, MessageRoot);
        if (!Directory.Exists(messageRoot))
            throw new DirectoryNotFoundException($"Could not find message root: {messageRoot}");

        var patchedFiles = 0;
        foreach (var languageDirectory in Directory.EnumerateDirectories(messageRoot))
        {
            var commonDirectory = Path.Combine(languageDirectory, "common");
            if (!Directory.Exists(commonDirectory))
                continue;

            foreach (var sourcePath in Directory.EnumerateFiles(commonDirectory, "itemname*.dat"))
            {
                var fileName = Path.GetFileName(sourcePath);
                var replacement = fileName.Contains("plural", StringComparison.OrdinalIgnoreCase)
                    ? RoyalCandyPluralName
                    : RoyalCandyName;
                if (PatchOneTextFile(commonDirectory, options, fileName, options.ItemId, replacement))
                    patchedFiles++;
            }

            if (PatchOneTextFile(commonDirectory, options, ItemInfoFile, options.ItemId, RoyalCandyDescription))
                patchedFiles++;
        }

        results.Add(new(patchedFiles == 0 ? "Warning" : "Pass", "RomFS", "romfs/bin/message", $"Patched {patchedFiles:N0} item text file(s)."));
        notes.Add($"- Patched {patchedFiles:N0} item name/description message files.");
    }

    private static bool PatchOneTextFile(string commonDirectory, RoyalCandyBuildOptions options, string fileName, int lineIndex, string replacement)
    {
        var sourcePath = Path.Combine(commonDirectory, fileName);
        if (!File.Exists(sourcePath))
            return false;

        var text = new TextFile(File.ReadAllBytes(sourcePath), new TextConfig(GameVersion.SW), remapChars: true)
        {
            SETEMPTYTEXT = false,
        };
        var lines = text.Lines;
        var flags = text.Flags;
        if ((uint)lineIndex >= (uint)lines.Length)
            throw new IndexOutOfRangeException($"{fileName} has {lines.Length} lines; cannot patch line {lineIndex}.");

        lines[lineIndex] = replacement;
        var patched = new TextFile(new TextConfig(GameVersion.SW), remapChars: true)
        {
            SETEMPTYTEXT = false,
            Lines = lines,
            Flags = flags,
        };

        var relativePath = Path.GetRelativePath(options.RomFsPath, sourcePath).Replace('\\', '/');
        WriteOutputBytes(options.OutputPath, "romfs/" + relativePath, patched.Data);
        return true;
    }

    private static void PatchShopData(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        var sourcePath = GetRomFsPath(options.RomFsPath, ShopPath);
        if (!File.Exists(sourcePath))
        {
            results.Add(new("Warning", "RomFS", "romfs/" + ShopPath, "shop_data.bin was not found; shop patch skipped."));
            notes.Add("- Shop patch skipped because shop_data.bin was not found.");
            return;
        }

        var shop = FlatBufferConverter.DeserializeFrom<SwShShopInventory>(File.ReadAllBytes(sourcePath));
        var shopNotes = new List<string>
        {
            "Royal Candy shop patch",
            "======================",
            "",
            $"Inserted item id: {options.ItemId}",
            "",
        };

        var changed = false;
        changed |= AddToSingleShop(shop, EarlyPokeMartHash, options.ItemId, shopNotes, "Poke Mart [0 Badges, Before Catching Tutorial]");
        changed |= AddToAllMultiShopInventories(shop, BadgePokeMartInventoryHash, options.ItemId, shopNotes, "Poke Mart Inventories");

        WriteOutputBytes(options.OutputPath, "romfs/" + ShopPath, changed ? shop.SerializeFrom() : File.ReadAllBytes(sourcePath));
        File.WriteAllText(Path.Combine(options.OutputPath, "royal_candy_shop_notes.txt"), string.Join(Environment.NewLine, shopNotes));

        results.Add(new(changed ? "Pass" : "Warning", "RomFS", "romfs/" + ShopPath, changed ? "Royal Candy added to Poke Mart inventories." : "No matching shop inventories were found."));
        notes.Add(changed ? "- Added Royal Candy to early Poke Mart inventories." : "- Shop data copied unchanged because no matching Poke Mart inventories were found.");
    }

    private static bool AddToSingleShop(SwShShopInventory shop, ulong hash, int itemId, List<string> notes, string label)
    {
        var target = shop.Single.FirstOrDefault(z => z.Hash == hash);
        if (target is null)
        {
            notes.Add($"- Missing single shop: {label} [{hash:X16}]");
            return false;
        }

        return AddToInventory(target.Inventories.Items, itemId, notes, label);
    }

    private static bool AddToAllMultiShopInventories(SwShShopInventory shop, ulong hash, int itemId, List<string> notes, string label)
    {
        var target = shop.Multi.FirstOrDefault(z => z.Hash == hash);
        if (target is null)
        {
            notes.Add($"- Missing multi shop: {label} [{hash:X16}]");
            return false;
        }

        var changed = false;
        for (var i = 0; i < target.Inventories.Count; i++)
            changed |= AddToInventory(target.Inventories[i].Items, itemId, notes, $"{label} [{i} Badge{(i == 1 ? "" : "s")}]");

        return changed;
    }

    private static bool AddToInventory(IList<int> items, int itemId, List<string> notes, string label)
    {
        if (items.Contains(itemId))
        {
            notes.Add($"- Already present: {label}");
            return false;
        }

        items.Add(itemId);
        notes.Add($"- Added to {label}. New inventory: {string.Join(", ", items)}");
        return true;
    }

    private static void PatchExeFsMain(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        if (options.ItemId != RoyalCandyItemId)
            throw new InvalidOperationException("The Royal Candy ExeFS patch currently targets item id 1128 only.");

        var mainPath = Path.Combine(options.ExeFsPath, "main");
        if (!File.Exists(mainPath))
            throw new FileNotFoundException("Could not find exefs/main for Royal Candy patching.", mainPath);

        var nso = new NSO(File.ReadAllBytes(mainPath));
        if (!nso.Header.Valid)
            throw new InvalidOperationException("The selected main file is not a valid NSO.");

        var patchNotes = new List<string>
        {
            "Royal Candy ExeFS patch",
            "=======================",
            "",
            $"Build ID: {Convert.ToHexString(nso.Header.DigestBuildID)}",
            $"Royal Candy item id: {options.ItemId}",
            "",
        };

        PatchExpCandyFixedAmountBypass(nso.DecompressedText, options.ItemId, patchNotes);
        if (options.InfiniteUse)
            PatchInfiniteCandidateItemUse(nso.DecompressedText, options.ItemId, patchNotes);
        if (options.StoryCapLadder)
            PatchStoryCapLadder(nso.DecompressedText, options.ItemId, FirstHopFlagHash, options.MaxStoryCap, patchNotes);
        if (options.VirtualCount is { } virtualCount)
            PatchCandidateVirtualInventoryCount(nso.DecompressedText, options.ItemId, virtualCount, patchNotes);
        PatchRoyalCandyUiRoute(nso.DecompressedText, options.ItemId, patchNotes);

        WriteOutputBytes(options.OutputPath, "exefs/main", nso.Write());
        var notesPath = Path.Combine(options.OutputPath, "exefs", "royal_candy_ui_hook_patch_notes.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(notesPath)!);
        File.WriteAllText(notesPath, string.Join(Environment.NewLine, patchNotes));

        results.Add(new("Pass", "ExeFS", "exefs/main", "Royal Candy ExeFS patch generated."));
        notes.AddRange(patchNotes.Where(z => z.StartsWith("- ", StringComparison.Ordinal)));
    }

    private static void PatchRoyalCandyUiRoute(byte[] text, int candidateId, List<string> notes)
    {
        var check = new RareCandyUiCheck(0x007BC1F8, 8, 0x007BC200, 0x007BC2B4);
        var caveOffset = FindZeroRun(text, 0xC, RareCandyUiHookCodeCaveSearchStart);
        if (caveOffset < 0)
            caveOffset = FindZeroRun(text, 0xC, 0);
        if (caveOffset < 0)
        {
            var largest = FindLargestZeroRun(text);
            throw new InvalidOperationException($"Could not find a 12-byte zero-filled code cave for the Royal Candy UI route. Largest run: text+0x{largest.Offset:X} length 0x{largest.Length:X}.");
        }

        var actualCompare = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(check.CompareOffset));
        var expectedCompare = EncodeCmpImmediate(check.ItemRegister, RareCandyItemId);
        if (actualCompare != expectedCompare)
            throw new InvalidOperationException($"Unexpected instruction at text+0x{check.CompareOffset:X}: {actualCompare:X8}; expected {expectedCompare:X8}.");

        var originalBranchOffset = check.CompareOffset + 4;
        var actualBranch = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(originalBranchOffset));
        var expectedBranch = EncodeConditionalBranch(originalBranchOffset, check.FailOffset, Arm64Condition.NE);
        if (actualBranch != expectedBranch)
            throw new InvalidOperationException($"Unexpected branch at text+0x{originalBranchOffset:X}: {actualBranch:X8}; expected {expectedBranch:X8}.");

        WriteInstruction(text, originalBranchOffset, EncodeConditionalBranch(originalBranchOffset, caveOffset, Arm64Condition.NE));
        WriteInstruction(text, caveOffset, EncodeCmpImmediate(check.ItemRegister, candidateId));
        WriteInstruction(text, caveOffset + 4, EncodeConditionalBranch(caveOffset + 4, check.PassOffset, Arm64Condition.EQ));
        WriteInstruction(text, caveOffset + 8, EncodeBranch(caveOffset + 8, check.FailOffset));

        notes.Add($"- text+0x{check.CompareOffset:X}: Royal Candy now enters the confirmed Rare Candy bag UI route through stub text+0x{caveOffset:X}.");
    }

    private static void PatchExpCandyFixedAmountBypass(byte[] text, int candidateId, List<string> notes)
    {
        const int firstRangeCompareOffset = 0x007BC1BC;
        const int secondRangeCompareOffset = 0x007BC1C4;
        const int expCandyIndexRegister = 9;

        foreach (var offset in new[] { firstRangeCompareOffset, secondRangeCompareOffset })
        {
            var actualCompare = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset));
            var expectedCompare = EncodeCmpImmediate(expCandyIndexRegister, 4);
            if (actualCompare != expectedCompare)
                throw new InvalidOperationException($"Unexpected Exp Candy range compare at text+0x{offset:X}: {actualCompare:X8}; expected {expectedCompare:X8}.");

            WriteInstruction(text, offset, EncodeCmpImmediate(expCandyIndexRegister, 3));
        }

        notes.Add("- text+0x7BC1BC/text+0x7BC1C4: item id 1128 no longer enters the fixed Exp Candy XL amount table.");
    }

    private static void PatchInfiniteCandidateItemUse(byte[] text, int candidateId, List<string> notes)
    {
        const int quantityMoveOffset = 0x007B1F20;
        const int resumeOffset = quantityMoveOffset + 4;
        const int itemRegister = 22;
        const uint expectedQuantityMove = 0x2A0003E2;

        var actualQuantityMove = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(quantityMoveOffset));
        if (actualQuantityMove != expectedQuantityMove)
            throw new InvalidOperationException($"Unexpected consume-quantity move at text+0x{quantityMoveOffset:X}: {actualQuantityMove:X8}; expected {expectedQuantityMove:X8}.");

        var caveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy infinite-use patch");
        WriteInstruction(text, quantityMoveOffset, EncodeBranch(quantityMoveOffset, caveOffset));
        WriteInstruction(text, caveOffset, EncodeCmpImmediate(itemRegister, candidateId));
        WriteInstruction(text, caveOffset + 4, EncodeConditionalSelect32(2, 31, 0, Arm64Condition.EQ));
        WriteInstruction(text, caveOffset + 8, EncodeBranch(caveOffset + 8, resumeOffset));

        notes.Add($"- text+0x{quantityMoveOffset:X}: Royal Candy passes zero to the bag decrement call while other items keep the selected quantity.");
    }

    private static void PatchStoryCapLadder(byte[] text, int candidateId, ulong firstHopFlagHash, int? maxStoryCap, List<string> notes)
    {
        var milestones = GetDefaultStoryCapLadder(firstHopFlagHash)
            .Where(milestone => maxStoryCap is null || milestone.Cap <= maxStoryCap)
            .ToArray();
        if (milestones.Length == 0)
            throw new InvalidOperationException("Royal Candy story-cap ladder has no milestones after applying the requested max cap.");

        var capHelperOffset = WriteStoryCapHelper(text, milestones);
        PatchCandidateUseGateDynamicCap(text, candidateId, capHelperOffset, notes);
        PatchCandidateQuantityMaxDynamicCap(text, candidateId, capHelperOffset, notes);
        PatchCandidateQuantityInventoryClampBypass(text, candidateId, notes);

        notes.Add($"- text+0x{capHelperOffset:X}: shared Royal Candy cap helper returns the highest unlocked Royal Sword story cap; default cap is level 10.");
        if (maxStoryCap is { } cap)
            notes.Add($"  - Diagnostic max story cap: only milestones at or below cap {cap} were included.");
        foreach (var milestone in milestones.OrderBy(milestone => milestone.Cap))
        {
            var source = milestone.Kind == RoyalSwordLevelCapMilestoneKind.WorkAtLeast
                ? $"work hash 0x{milestone.FlagHash:X16} >= {milestone.WorkMinimum}"
                : $"flag hash 0x{milestone.FlagHash:X16}";
            notes.Add($"  - Cap {milestone.Cap}: {milestone.Label} via {source}.");
        }
    }

    private static RoyalSwordLevelCapMilestone[] GetDefaultStoryCapLadder(ulong firstHopFlagHash) =>
    [
        new(90, SceneMainMasterWorkHash, "Leon 149/189/190 clear, post-Leon main_event_3000 progress", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 3000),
        new(85, SceneMainMasterWorkHash, "Rose 175 clear, story progress reaches post-Rose main_event_1910", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1910),
        new(80, SceneMainMasterWorkHash, "Raihan 213 finals clear, pre-Leon story progress", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1780),
        new(75, SceneMainMasterWorkHash, "Oleana 143 clear, Rose Tower resolved", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1660),
        new(70, SceneMainMasterWorkHash, "Hop 130/131/132 Semifinals clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1550),
        new(65, 0xE336BF34143E0946, "Raihan gym clear (FE_GC_DORAGON_CLEAR)"),
        new(60, 0xA52A7561C28A76F1, "Piers gym clear (FE_GC_AKU_CLEAR)"),
        new(55, SceneMainMasterWorkHash, "Marnie 138 Route 9/Spikemuth clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1330),
        new(54, SceneMainMasterWorkHash, "Hop 202/203/204 Hero's Bath clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1300),
        new(52, 0x7042D310DF3DB17F, "Gordie gym clear, Sword (FE_GC_IWAKO_CLEAR)"),
        new(50, SceneMainMasterWorkHash, "Hop 127/128/129 Route 7 clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1200),
        new(47, 0xDF7AC7105B946783, "Opal gym clear (FE_GC_FAIRY_CLEAR)"),
        new(44, SceneMainMasterWorkHash, "Bede 133 Stow-on-Side mural clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 1090),
        new(42, 0xC07B67FC3148B754, "Bea gym clear, Sword (FE_GC_KAKUGO_CLEAR)"),
        new(40, SceneMainMasterWorkHash, "Hop 124/125/126 Stow-on-Side clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 950),
        new(38, 0xABFC3E0B626D6B24, "Kabu gym clear (FE_GC_HONO_CLEAR)"),
        new(36, SceneMainMasterWorkHash, "Marnie 196 Budew Drop Inn clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 760),
        new(32, SceneMainMasterWorkHash, "Bede 240 Galar Mine No. 2 clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 720),
        new(30, 0x8B4F4365890D1CF9, "Nessa gym clear (FE_GC_MIZU_CLEAR)"),
        new(28, SceneMainMasterWorkHash, "Hop 121/122/123 Hulbury clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 640),
        new(25, 0xB02911749203329A, "Milo gym clear (FE_GC_KUSA_CLEAR)"),
        new(23, SceneMainMasterWorkHash, "Bede 195 Galar Mine clear", RoyalSwordLevelCapMilestoneKind.WorkAtLeast, 550),
        new(20, 0x005A329212277F11, "second Hop win candidate (FE_EV0280_WIN)"),
        new(16, firstHopFlagHash, "first Hop win, confirmed for Hop 007/008/009 (FE_EV0110_WIN by default)"),
    ];

    private static int WriteStoryCapHelper(byte[] text, RoyalSwordLevelCapMilestone[] milestones)
    {
        const int flagworkGlobalAddress = 0x02610798;
        const int flagworkObjectOffset = 0x1B8;
        const int flagGetOffset = 0x01410F00;
        const int workGetOffset = 0x014114C0;

        var ordered = milestones.OrderByDescending(milestone => milestone.Cap).ToArray();
        var checks = ordered.Select((milestone, index) => new
        {
            Milestone = milestone,
            Chunks = AllocateCapCheckChunks(text, index),
        }).ToArray();
        var defaultReturn = AllocateCodeCave(text, 0x8, "Royal Candy cap ladder default return");

        for (var i = 0; i < checks.Length; i++)
        {
            var current = checks[i];
            var nextOffset = i == checks.Length - 1 ? defaultReturn : checks[i + 1].Chunks.LoadGlobal;
            WriteLevelCapCheck(text, current.Chunks, current.Milestone, nextOffset, flagworkGlobalAddress, flagworkObjectOffset, flagGetOffset, workGetOffset);
        }

        WriteInstruction(text, defaultReturn, EncodeMovzImmediate32(0, 10));
        WriteInstruction(text, defaultReturn + 4, EncodeRet());
        return checks[0].Chunks.LoadGlobal;
    }

    private static RoyalSwordLevelCapCheckChunks AllocateCapCheckChunks(byte[] text, int index) => new(
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} load global"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} load table"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} hash low"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} hash high"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} call flag getter"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} restore link register"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} decision"),
        AllocateCodeCave(text, 0x8, $"Royal Candy cap ladder check {index} cap return"));

    private static int AllocateCodeCave(byte[] text, int requiredBytes, string label)
    {
        var offset = FindCodeCaveOrThrow(text, requiredBytes, label);
        ReserveCodeCave(text, offset, requiredBytes);
        return offset;
    }

    private static void WriteLevelCapCheck(byte[] text, RoyalSwordLevelCapCheckChunks chunks, RoyalSwordLevelCapMilestone milestone, int nextOffset, int flagworkGlobalAddress, int flagworkObjectOffset, int flagGetOffset, int workGetOffset)
    {
        WriteInstruction(text, chunks.LoadGlobal, EncodeAdrp(8, chunks.LoadGlobal, flagworkGlobalAddress));
        WriteInstruction(text, chunks.LoadGlobal + 4, EncodeLdrUnsigned64(8, 8, flagworkGlobalAddress & 0xFFF));
        WriteInstruction(text, chunks.LoadGlobal + 8, EncodeBranch(chunks.LoadGlobal + 8, chunks.LoadTable));

        WriteInstruction(text, chunks.LoadTable, EncodeLdrUnsigned64(8, 8, 0));
        WriteInstruction(text, chunks.LoadTable + 4, EncodeLdrUnsigned64(0, 8, flagworkObjectOffset));
        WriteInstruction(text, chunks.LoadTable + 8, EncodeBranch(chunks.LoadTable + 8, chunks.HashLow));

        WriteInstruction(text, chunks.HashLow, EncodeMovzImmediate64(1, (int)(milestone.FlagHash & 0xFFFF), 0));
        WriteInstruction(text, chunks.HashLow + 4, EncodeMovkImmediate64(1, (int)((milestone.FlagHash >> 16) & 0xFFFF), 16));
        WriteInstruction(text, chunks.HashLow + 8, EncodeBranch(chunks.HashLow + 8, chunks.HashHigh));

        WriteInstruction(text, chunks.HashHigh, EncodeMovkImmediate64(1, (int)((milestone.FlagHash >> 32) & 0xFFFF), 32));
        WriteInstruction(text, chunks.HashHigh + 4, EncodeMovkImmediate64(1, (int)((milestone.FlagHash >> 48) & 0xFFFF), 48));
        WriteInstruction(text, chunks.HashHigh + 8, EncodeBranch(chunks.HashHigh + 8, chunks.Call));

        var accessorOffset = milestone.Kind == RoyalSwordLevelCapMilestoneKind.WorkAtLeast ? workGetOffset : flagGetOffset;
        WriteInstruction(text, chunks.Call, 0xA9BF7BFD);
        WriteInstruction(text, chunks.Call + 4, EncodeBranchLink(chunks.Call + 4, accessorOffset));
        WriteInstruction(text, chunks.Call + 8, EncodeBranch(chunks.Call + 8, chunks.Restore));

        WriteInstruction(text, chunks.Restore, 0xA8C17BFD);
        WriteInstruction(text, chunks.Restore + 4, EncodeBranch(chunks.Restore + 4, chunks.Decision));
        WriteInstruction(text, chunks.Restore + 8, EncodeNop());

        if (milestone.Kind == RoyalSwordLevelCapMilestoneKind.WorkAtLeast)
        {
            WriteInstruction(text, chunks.Decision, EncodeCmpImmediate(0, milestone.WorkMinimum));
            WriteInstruction(text, chunks.Decision + 4, EncodeConditionalBranch(chunks.Decision + 4, chunks.ReturnCap, Arm64Condition.HS));
            WriteInstruction(text, chunks.Decision + 8, EncodeBranch(chunks.Decision + 8, nextOffset));
        }
        else
        {
            WriteInstruction(text, chunks.Decision, EncodeCompareAndBranchNonZero32(chunks.Decision, chunks.ReturnCap, 0));
            WriteInstruction(text, chunks.Decision + 4, EncodeBranch(chunks.Decision + 4, nextOffset));
            WriteInstruction(text, chunks.Decision + 8, EncodeNop());
        }

        WriteInstruction(text, chunks.ReturnCap, EncodeMovzImmediate32(0, milestone.Cap));
        WriteInstruction(text, chunks.ReturnCap + 4, EncodeRet());
    }

    private static void PatchCandidateUseGateDynamicCap(byte[] text, int candidateId, int capHelperOffset, List<string> notes)
    {
        const int rareCandyCompareOffset = 0x007BB204;
        const int rareCandyBranchOffset = 0x007BB208;
        const int nonRareCandyOffset = 0x007BB26C;
        const int epilogueOffset = 0x007BB2E0;
        const int getLevelOffset = 0x0077A5F0;
        const int itemRegister = 20;
        const uint expectedBranch = 0x54000321;
        const uint moveSelectedPokemonToX0 = 0xAA1303E0;

        ExpectInstruction(text, rareCandyCompareOffset, EncodeCmpImmediate(itemRegister, RareCandyItemId), "Rare Candy use-gate compare");
        ExpectInstruction(text, rareCandyBranchOffset, expectedBranch, "Rare Candy use-gate branch");

        var itemCheckCaveOffset = FindNearbyConditionalBranchZeroRun(text, 0xC, rareCandyBranchOffset);
        if (itemCheckCaveOffset < 0)
            throw NoCave("Royal Candy dynamic use-gate item check", text);

        WriteInstruction(text, rareCandyBranchOffset, EncodeConditionalBranch(rareCandyBranchOffset, itemCheckCaveOffset, Arm64Condition.NE));
        WriteInstruction(text, itemCheckCaveOffset, EncodeCmpImmediate(itemRegister, candidateId));
        WriteInstruction(text, itemCheckCaveOffset + 4, EncodeConditionalBranch(itemCheckCaveOffset + 4, nonRareCandyOffset, Arm64Condition.NE));

        var logicChunks = AllocateCodeCaves(text, 4, "Royal Candy dynamic use-gate logic");
        WriteInstruction(text, itemCheckCaveOffset + 8, EncodeBranch(itemCheckCaveOffset + 8, logicChunks[0]));
        WriteInstruction(text, logicChunks[0], moveSelectedPokemonToX0);
        WriteInstruction(text, logicChunks[0] + 4, EncodeBranchLink(logicChunks[0] + 4, getLevelOffset));
        WriteInstruction(text, logicChunks[0] + 8, EncodeBranch(logicChunks[0] + 8, logicChunks[1]));
        WriteInstruction(text, logicChunks[1], EncodeMovRegister32(21, 0));
        WriteInstruction(text, logicChunks[1] + 4, EncodeBranchLink(logicChunks[1] + 4, capHelperOffset));
        WriteInstruction(text, logicChunks[1] + 8, EncodeBranch(logicChunks[1] + 8, logicChunks[2]));
        WriteInstruction(text, logicChunks[2], EncodeCmpRegister32(21, 0));
        WriteInstruction(text, logicChunks[2] + 4, EncodeMovzImmediate32(8, 1));
        WriteInstruction(text, logicChunks[2] + 8, EncodeBranch(logicChunks[2] + 8, logicChunks[3]));
        WriteInstruction(text, logicChunks[3], EncodeConditionalSelect32(0, 8, 31, Arm64Condition.LT));
        WriteInstruction(text, logicChunks[3] + 4, EncodeBranch(logicChunks[3] + 4, epilogueOffset));

        notes.Add($"- text+0x{rareCandyBranchOffset:X}: Royal Candy use gate now checks runtime story cap helper text+0x{capHelperOffset:X}.");
    }

    private static void PatchCandidateQuantityMaxDynamicCap(byte[] text, int candidateId, int capHelperOffset, List<string> notes)
    {
        const int rareCandyCompareOffset = 0x007BB3C0;
        const int rareCandyBranchOffset = 0x007BB3C4;
        const int nonRareCandyOffset = 0x007BB3EC;
        const int epilogueOffset = 0x007BB458;
        const int getLevelOffset = 0x0077A5F0;
        const int itemRegister = 19;
        const uint expectedBranch = 0x54000141;
        const uint moveSelectedPokemonToX0 = 0xAA1403E0;

        ExpectInstruction(text, rareCandyCompareOffset, EncodeCmpImmediate(itemRegister, RareCandyItemId), "Rare Candy quantity-cap compare");
        ExpectInstruction(text, rareCandyBranchOffset, expectedBranch, "Rare Candy quantity-cap branch");

        var itemCheckCaveOffset = FindNearbyConditionalBranchZeroRun(text, 0xC, rareCandyBranchOffset);
        if (itemCheckCaveOffset < 0)
            throw NoCave("Royal Candy dynamic quantity item check", text);

        WriteInstruction(text, rareCandyBranchOffset, EncodeConditionalBranch(rareCandyBranchOffset, itemCheckCaveOffset, Arm64Condition.NE));
        WriteInstruction(text, itemCheckCaveOffset, EncodeCmpImmediate(itemRegister, candidateId));
        WriteInstruction(text, itemCheckCaveOffset + 4, EncodeConditionalBranch(itemCheckCaveOffset + 4, nonRareCandyOffset, Arm64Condition.NE));

        var logicChunks = AllocateCodeCaves(text, 4, "Royal Candy dynamic quantity logic");
        WriteInstruction(text, itemCheckCaveOffset + 8, EncodeBranch(itemCheckCaveOffset + 8, logicChunks[0]));
        WriteInstruction(text, logicChunks[0], moveSelectedPokemonToX0);
        WriteInstruction(text, logicChunks[0] + 4, EncodeBranchLink(logicChunks[0] + 4, getLevelOffset));
        WriteInstruction(text, logicChunks[0] + 8, EncodeBranch(logicChunks[0] + 8, logicChunks[1]));
        WriteInstruction(text, logicChunks[1], EncodeMovRegister32(21, 0));
        WriteInstruction(text, logicChunks[1] + 4, EncodeBranchLink(logicChunks[1] + 4, capHelperOffset));
        WriteInstruction(text, logicChunks[1] + 8, EncodeBranch(logicChunks[1] + 8, logicChunks[2]));
        WriteInstruction(text, logicChunks[2], EncodeSubRegister32(0, 0, 21));
        WriteInstruction(text, logicChunks[2] + 4, EncodeCmpImmediate(0, 0));
        WriteInstruction(text, logicChunks[2] + 8, EncodeBranch(logicChunks[2] + 8, logicChunks[3]));
        WriteInstruction(text, logicChunks[3], EncodeConditionalSelect32(0, 0, 31, Arm64Condition.GT));
        WriteInstruction(text, logicChunks[3] + 4, EncodeBranch(logicChunks[3] + 4, epilogueOffset));

        notes.Add($"- text+0x{rareCandyBranchOffset:X}: Royal Candy quantity max returns max(0, runtime story cap - selected Pokemon level).");
    }

    private static void PatchCandidateQuantityInventoryClampBypass(byte[] text, int candidateId, List<string> notes)
    {
        const int originalCompareOffset = 0x007BAF38;
        const int clampSelectOffset = 0x007BAF3C;
        const int resumeOffset = 0x007BAF40;
        const int getItemIdOffset = 0x007C8330;
        const uint expectedCompare = 0x6B36231F;
        const uint expectedClampSelect = 0x1A963316;
        const uint moveSelectedItemToX0 = 0xAA1703E0;

        ExpectInstruction(text, originalCompareOffset, expectedCompare, "quantity clamp compare");
        ExpectInstruction(text, clampSelectOffset, expectedClampSelect, "quantity clamp CSEL");

        var firstCaveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy inventory clamp first stub");
        WriteInstruction(text, firstCaveOffset, moveSelectedItemToX0);
        WriteInstruction(text, firstCaveOffset + 4, EncodeBranchLink(firstCaveOffset + 4, getItemIdOffset));

        var secondCaveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy inventory clamp item check");
        WriteInstruction(text, firstCaveOffset + 8, EncodeBranch(firstCaveOffset + 8, secondCaveOffset));
        WriteInstruction(text, secondCaveOffset, EncodeCmpImmediate(0, candidateId));
        WriteInstruction(text, secondCaveOffset + 4, EncodeConditionalBranch(secondCaveOffset + 4, resumeOffset, Arm64Condition.EQ));

        var thirdCaveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy inventory clamp vanilla replay");
        WriteInstruction(text, secondCaveOffset + 8, EncodeBranch(secondCaveOffset + 8, thirdCaveOffset));
        WriteInstruction(text, thirdCaveOffset, expectedCompare);
        WriteInstruction(text, thirdCaveOffset + 4, expectedClampSelect);
        WriteInstruction(text, thirdCaveOffset + 8, EncodeBranch(thirdCaveOffset + 8, resumeOffset));
        WriteInstruction(text, clampSelectOffset, EncodeBranch(clampSelectOffset, firstCaveOffset));

        notes.Add($"- text+0x{clampSelectOffset:X}: Royal Candy bypasses the vanilla min(inventory count, useful count) clamp.");
    }

    private static void PatchCandidateVirtualInventoryCount(byte[] text, int candidateId, int virtualCount, List<string> notes)
    {
        if (virtualCount is < 1 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(virtualCount), "Royal Candy virtual count must fit in a 16-bit MOV immediate.");

        const int itemCountFunctionOffset = 0x01421090;
        const int resumeOffset = itemCountFunctionOffset + 4;
        const uint expectedFirstInstruction = 0xA9BE4FF4;

        ExpectInstruction(text, itemCountFunctionOffset, expectedFirstInstruction, "item-count helper first instruction");

        var dispatchCaveOffset = AllocateCodeCave(text, 0xC, "Royal Candy virtual count dispatch");
        var returnCaveOffset = AllocateCodeCave(text, 0x8, "Royal Candy virtual count return");
        var vanillaCaveOffset = FindCodeCaveOrThrow(text, 0x8, "Royal Candy virtual count vanilla path");

        WriteInstruction(text, itemCountFunctionOffset, EncodeBranch(itemCountFunctionOffset, dispatchCaveOffset));
        WriteInstruction(text, dispatchCaveOffset, EncodeCmpImmediate(1, candidateId));
        WriteInstruction(text, dispatchCaveOffset + 4, EncodeConditionalBranch(dispatchCaveOffset + 4, returnCaveOffset, Arm64Condition.EQ));
        WriteInstruction(text, dispatchCaveOffset + 8, EncodeBranch(dispatchCaveOffset + 8, vanillaCaveOffset));
        WriteInstruction(text, returnCaveOffset, EncodeMovzImmediate32(0, virtualCount));
        WriteInstruction(text, returnCaveOffset + 4, EncodeRet());
        WriteInstruction(text, vanillaCaveOffset, expectedFirstInstruction);
        WriteInstruction(text, vanillaCaveOffset + 4, EncodeBranch(vanillaCaveOffset + 4, resumeOffset));

        notes.Add($"- text+0x{itemCountFunctionOffset:X}: item-count helper returns virtual count {virtualCount} for Royal Candy.");
    }

    private static int[] AllocateCodeCaves(byte[] text, int count, string label)
    {
        var offsets = new int[count];
        for (var i = 0; i < offsets.Length; i++)
            offsets[i] = AllocateCodeCave(text, 0xC, $"{label} chunk {i}");

        return offsets;
    }

    private static int FindCodeCaveOrThrow(byte[] text, int requiredBytes, string label)
    {
        var caveOffset = FindZeroRun(text, requiredBytes, RareCandyUiHookCodeCaveSearchStart);
        if (caveOffset < 0)
            caveOffset = FindZeroRun(text, requiredBytes, 0);
        if (caveOffset >= 0)
            return caveOffset;

        throw NoCave(label, text);
    }

    private static int FindNearbyConditionalBranchZeroRun(byte[] text, int requiredBytes, int anchorOffset)
    {
        const int ConditionalBranchReachBytes = (1 << 18) * 4;
        var minOffset = Math.Max(0, anchorOffset - ConditionalBranchReachBytes + requiredBytes);
        var maxOffset = Math.Min(text.Length - requiredBytes, anchorOffset + ConditionalBranchReachBytes - requiredBytes);
        if (minOffset > maxOffset)
            return -1;

        var afterAnchor = FindZeroRunWithin(text, requiredBytes, anchorOffset, maxOffset);
        if (afterAnchor >= 0)
            return afterAnchor;
        return FindZeroRunWithin(text, requiredBytes, minOffset, anchorOffset);
    }

    private static int FindZeroRunWithin(byte[] data, int requiredBytes, int startOffset, int endOffset)
    {
        var runStart = -1;
        var start = Math.Max(0, startOffset);
        var end = Math.Min(data.Length - 1, endOffset);
        for (var offset = start; offset <= end; offset++)
        {
            if (data[offset] == 0)
            {
                if (runStart < 0)
                    runStart = offset;
                var alignedStart = (runStart + 3) & ~3;
                if (offset - alignedStart + 1 >= requiredBytes)
                    return alignedStart;
                continue;
            }

            runStart = -1;
        }

        return -1;
    }

    private static int FindZeroRun(byte[] data, int requiredBytes, int startOffset)
    {
        var runStart = -1;
        for (var offset = Math.Max(0, startOffset); offset < data.Length; offset++)
        {
            if (data[offset] == 0)
            {
                if (runStart < 0)
                    runStart = offset;
                var alignedStart = (runStart + 3) & ~3;
                if (offset - alignedStart + 1 >= requiredBytes)
                    return alignedStart;
                continue;
            }

            runStart = -1;
        }

        return -1;
    }

    private static ZeroRun FindLargestZeroRun(byte[] data)
    {
        var best = new ZeroRun(-1, 0);
        var runStart = -1;
        for (var offset = 0; offset < data.Length; offset++)
        {
            if (data[offset] == 0)
            {
                if (runStart < 0)
                    runStart = offset;

                var length = offset - runStart + 1;
                if (length > best.Length)
                    best = new ZeroRun(runStart, length);
                continue;
            }

            runStart = -1;
        }

        return best;
    }

    private static InvalidOperationException NoCave(string label, byte[] text)
    {
        var largest = FindLargestZeroRun(text);
        return new InvalidOperationException($"Could not find a zero-filled code cave for {label}. Largest run: text+0x{largest.Offset:X} length 0x{largest.Length:X}.");
    }

    private static void ExpectInstruction(byte[] text, int offset, uint expected, string label)
    {
        var actual = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset));
        if (actual != expected)
            throw new InvalidOperationException($"Unexpected {label} at text+0x{offset:X}: {actual:X8}; expected {expected:X8}.");
    }

    private static void ReserveCodeCave(byte[] text, int offset, int length)
    {
        for (var current = offset; current < offset + length; current += 4)
            WriteInstruction(text, current, EncodeNop());
    }

    private static void WriteInstruction(byte[] text, int offset, uint instruction) =>
        BinaryPrimitives.WriteUInt32LittleEndian(text.AsSpan(offset, 4), instruction);

    private static uint EncodeCmpImmediate(int register, int immediate) =>
        (uint)(0x7100001F | ((immediate & 0xFFF) << 10) | ((register & 0x1F) << 5));

    private static uint EncodeConditionalBranch(int sourceOffset, int targetOffset, Arm64Condition condition)
    {
        var imm19 = GetBranchDelta(sourceOffset, targetOffset, 18, "Conditional branch");
        return (uint)(0x54000000 | ((imm19 & 0x7FFFF) << 5) | ((int)condition & 0xF));
    }

    private static uint EncodeCompareAndBranchNonZero32(int sourceOffset, int targetOffset, int register)
    {
        var imm19 = GetBranchDelta(sourceOffset, targetOffset, 18, "Compare-and-branch");
        return (uint)(0x35000000 | ((imm19 & 0x7FFFF) << 5) | (register & 0x1F));
    }

    private static uint EncodeBranch(int sourceOffset, int targetOffset)
    {
        var imm26 = GetBranchDelta(sourceOffset, targetOffset, 25, "Branch");
        return (uint)(0x14000000 | (imm26 & 0x03FFFFFF));
    }

    private static uint EncodeBranchLink(int sourceOffset, int targetOffset)
    {
        var imm26 = GetBranchDelta(sourceOffset, targetOffset, 25, "Branch-link");
        return (uint)(0x94000000 | (imm26 & 0x03FFFFFF));
    }

    private static int GetBranchDelta(int sourceOffset, int targetOffset, int signedBits, string label)
    {
        var delta = targetOffset - sourceOffset;
        if ((delta & 3) != 0)
            throw new ArgumentException($"{label} target must be 4-byte aligned.", nameof(targetOffset));

        var immediate = delta >> 2;
        var min = -(1 << signedBits);
        var max = 1 << signedBits;
        if (immediate < min || immediate >= max)
            throw new ArgumentOutOfRangeException(nameof(targetOffset), $"{label} target is outside ARM64 range.");
        return immediate;
    }

    private static uint EncodeNop() => 0xD503201F;
    private static uint EncodeRet() => 0xD65F03C0;

    private static uint EncodeMovzImmediate32(int register, int immediate)
    {
        if (immediate is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(immediate), "MOVZ immediate must fit in 16 bits.");
        return (uint)(0x52800000 | ((immediate & 0xFFFF) << 5) | (register & 0x1F));
    }

    private static uint EncodeMovzImmediate64(int register, int immediate, int shift)
    {
        if (immediate is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(immediate), "MOVZ immediate must fit in 16 bits.");
        if (shift is not (0 or 16 or 32 or 48))
            throw new ArgumentOutOfRangeException(nameof(shift), "MOVZ 64-bit shift must be 0, 16, 32, or 48.");
        return 0xD2800000u | (uint)((shift / 16) << 21) | (uint)((immediate & 0xFFFF) << 5) | (uint)(register & 0x1F);
    }

    private static uint EncodeMovkImmediate64(int register, int immediate, int shift)
    {
        if (immediate is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(immediate), "MOVK immediate must fit in 16 bits.");
        if (shift is not (0 or 16 or 32 or 48))
            throw new ArgumentOutOfRangeException(nameof(shift), "MOVK 64-bit shift must be 0, 16, 32, or 48.");
        return 0xF2800000u | (uint)((shift / 16) << 21) | (uint)((immediate & 0xFFFF) << 5) | (uint)(register & 0x1F);
    }

    private static uint EncodeMovRegister32(int destinationRegister, int sourceRegister) =>
        (uint)(0x2A0003E0 | ((sourceRegister & 0x1F) << 16) | (destinationRegister & 0x1F));

    private static uint EncodeCmpRegister32(int leftRegister, int rightRegister) =>
        (uint)(0x6B00001F | ((rightRegister & 0x1F) << 16) | ((leftRegister & 0x1F) << 5));

    private static uint EncodeSubRegister32(int destinationRegister, int leftRegister, int rightRegister) =>
        (uint)(0x4B000000 | ((rightRegister & 0x1F) << 16) | ((leftRegister & 0x1F) << 5) | (destinationRegister & 0x1F));

    private static uint EncodeLdrUnsigned64(int targetRegister, int baseRegister, int byteOffset)
    {
        if ((byteOffset & 7) != 0)
            throw new ArgumentException("64-bit LDR unsigned offset must be 8-byte aligned.", nameof(byteOffset));
        var scaled = byteOffset >> 3;
        if (scaled is < 0 or > 0xFFF)
            throw new ArgumentOutOfRangeException(nameof(byteOffset), "64-bit LDR unsigned offset must fit the ARM64 imm12 field.");
        return 0xF9400000u | (uint)(scaled << 10) | (uint)((baseRegister & 0x1F) << 5) | (uint)(targetRegister & 0x1F);
    }

    private static uint EncodeAdrp(int register, int sourceOffset, int targetAddress)
    {
        var sourcePage = sourceOffset & ~0xFFF;
        var targetPage = targetAddress & ~0xFFF;
        var pageDelta = (targetPage - sourcePage) >> 12;
        if (pageDelta < -(1 << 20) || pageDelta >= (1 << 20))
            throw new ArgumentOutOfRangeException(nameof(targetAddress), "ADRP target is outside ARM64 range.");

        var imm = pageDelta & 0x1FFFFF;
        var immlo = imm & 0x3;
        var immhi = (imm >> 2) & 0x7FFFF;
        return 0x90000000u | (uint)(immlo << 29) | (uint)(immhi << 5) | (uint)(register & 0x1F);
    }

    private static uint EncodeConditionalSelect32(int destinationRegister, int trueRegister, int falseRegister, Arm64Condition condition) =>
        (uint)(0x1A800000 | (((int)condition & 0xF) << 12) | ((falseRegister & 0x1F) << 16) | ((trueRegister & 0x1F) << 5) | (destinationRegister & 0x1F));

    private static IEnumerable<int> GetIdsSharingEntry(byte[] itemData, int itemId)
    {
        var targetEntry = GetItemEntryIndex(itemData, itemId);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(itemData);
        for (var i = 0; i < count; i++)
        {
            if (GetItemEntryIndex(itemData, i) == targetEntry)
                yield return i;
        }
    }

    private static byte[] AppendUniqueItemRow(byte[] itemData, int itemId, ReadOnlySpan<byte> row)
    {
        var maxEntryIndex = BinaryPrimitives.ReadUInt16LittleEndian(itemData.AsSpan(4));
        if (maxEntryIndex == ushort.MaxValue)
            throw new InvalidOperationException("Item raw-row table is already at the ushort limit.");

        var result = new byte[itemData.Length + row.Length];
        itemData.CopyTo(result.AsSpan());
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), (ushort)(maxEntryIndex + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x44 + (itemId * 2)), maxEntryIndex);
        row.CopyTo(result.AsSpan(itemData.Length));
        return result;
    }

    private static ushort GetItemEntryIndex(byte[] itemData, int itemId)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(itemData);
        if ((uint)itemId >= count)
            throw new IndexOutOfRangeException($"Item id {itemId} is outside the item index table.");

        return BinaryPrimitives.ReadUInt16LittleEndian(itemData.AsSpan(0x44 + (itemId * 2)));
    }

    private static string FormatSharedIds(IEnumerable<int> sharedIds, int itemId)
    {
        var otherIds = sharedIds.Where(z => z != itemId).ToArray();
        return otherIds.Length == 0
            ? "none"
            : string.Join(", ", otherIds.Take(20)) + (otherIds.Length > 20 ? $", ... ({otherIds.Length} total)" : "");
    }

    private static void WriteReadme(RoyalCandyBuildOptions options, List<string> notes)
    {
        var text = string.Join(Environment.NewLine, [
            "# Royal Candy Patch",
            "",
            "This folder is shaped like a Sword/Shield LayeredFS patch generated by pkNX Royal Sword Candy Builder.",
            "",
            $"Selected item id: `{options.ItemId}`",
            $"Template item id: `{options.TemplateItemId}`",
            $"Story cap mode: `{(options.StoryCapLadder ? "Royal Sword ladder" : "disabled")}`",
            $"Max story cap: `{(options.MaxStoryCap is { } cap ? cap.ToString(CultureInfo.InvariantCulture) : "full ladder")}`",
            "",
            "Generated pieces:",
            options.BuildRomFs ? "- `romfs/bin/pml/item/item.dat`: Royal Candy item metadata." : "- RomFS item output disabled.",
            options.BuildRomFs ? "- `romfs/bin/message/*/common/itemname*.dat` and `iteminfo.dat`: Royal Candy text." : "- RomFS text output disabled.",
            options.BuildRomFs ? "- `romfs/bin/appli/shop/bin/shop_data.bin`: Poke Mart test acquisition entries." : "- RomFS shop output disabled.",
            options.BuildExeFs ? "- `exefs/main`: Royal Candy route, non-consumption, virtual count, and cap helper patch." : "- ExeFS output disabled.",
            "",
            "Build log:",
            .. notes,
        ]);
        File.WriteAllText(Path.Combine(options.OutputPath, "README.md"), text);
    }

    private static string GetRomFsPath(string romFsPath, string relativePath) =>
        Path.Combine(romFsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static void WriteOutputBytes(string outputRoot, string relativePath, byte[] data)
    {
        var outputPath = Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, data);
    }

    private enum Arm64Condition
    {
        EQ = 0,
        NE = 1,
        HS = 2,
        HI = 8,
        LT = 11,
        GT = 12,
    }

    private enum RoyalSwordLevelCapMilestoneKind
    {
        Flag,
        WorkAtLeast,
    }

    private sealed record RareCandyUiCheck(int CompareOffset, int ItemRegister, int PassOffset, int FailOffset);
    private sealed record RoyalSwordLevelCapMilestone(int Cap, ulong FlagHash, string Label, RoyalSwordLevelCapMilestoneKind Kind = RoyalSwordLevelCapMilestoneKind.Flag, int WorkMinimum = 0);
    private sealed record RoyalSwordLevelCapCheckChunks(int LoadGlobal, int LoadTable, int HashLow, int HashHigh, int Call, int Restore, int Decision, int ReturnCap);
    private sealed record ZeroRun(int Offset, int Length);
}

internal sealed record RoyalCandyBuildOptions(
    string RomFsPath,
    string ExeFsPath,
    string OutputPath,
    int ItemId,
    int TemplateItemId,
    bool BuildRomFs,
    bool BuildExeFs,
    bool StoryCapLadder,
    bool InfiniteUse,
    int? VirtualCount,
    int? MaxStoryCap);

internal sealed record RoyalCandyBuildSummary(IReadOnlyList<BuildResult> Results, IReadOnlyList<string> Notes);
internal sealed record BuildResult(string Status, string Area, string Output, string Message);
