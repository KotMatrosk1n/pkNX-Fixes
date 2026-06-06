using pkNX.Containers;
using pkNX.Game;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers;
using pkNX.Structures.FlatBuffers.SWSH;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class RoyalSwordTrainerMap : Form
{
    private static readonly HashSet<int> RoyalLadderTrainerIDs = new()
    {
        7, 8, 9,
        32, 36, 37, 77, 107, 108,
        121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 135, 136, 138, 143, 144, 149, 175, 189, 190,
        191, 192, 193, 195, 196, 202, 203, 204, 213, 240,
    };

    private readonly TrainerEditor Trainers;
    private readonly IReadOnlyList<string> TrainerNames;
    private readonly IReadOnlyList<string> TrainerClasses;
    private readonly IReadOnlyList<string> SpeciesNames;
    private readonly string RomFsPath;
    private readonly byte[] PlacementData;

    private readonly List<TrainerMapEntry> Entries = [];
    private readonly List<TrainerMapEntry> VisibleEntries = [];
    private readonly List<string> LoadErrors = [];
    private readonly ComboBox ScopeFilter = new();
    private readonly TextBox SearchBox = new();
    private readonly DataGridView TrainerGrid = new();
    private readonly DataGridView PlacementGrid = new();
    private readonly TextBox DetailsText = new();
    private readonly Button CopyRowButton = new();
    private readonly Button CopyHashButton = new();
    private readonly Button CopyPlacementsButton = new();
    private readonly Button CopyVisibleButton = new();
    private readonly Label SummaryLabel = new();
    private readonly Timer FilterTimer = new() { Interval = 200 };
    private readonly ToolTip ButtonToolTips = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 450,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    private TrainerMapEntry? SelectedEntry;
    private PlacementHit? SelectedPlacement;

    public RoyalSwordTrainerMap(
        TrainerEditor trainers,
        IReadOnlyList<string> trainerNames,
        IReadOnlyList<string> trainerClasses,
        IReadOnlyList<string> speciesNames,
        string romFsPath,
        byte[] placementData)
    {
        Trainers = trainers;
        TrainerNames = trainerNames;
        TrainerClasses = trainerClasses;
        SpeciesNames = speciesNames;
        RomFsPath = romFsPath;
        PlacementData = placementData;

        Text = "Royal Sword Trainer Map";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 680);
        Size = new Size(1380, 780);

        InitializeLayout();
        ApplyTheme();
        BuildEntries();
        RefreshGrid();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FilterTimer.Dispose();
            ButtonToolTips.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var top = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 6),
            RowCount = 1,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        ScopeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        ScopeFilter.Items.AddRange(["All", "Royal Ladder", "Has Placement", "No Placement", "No Team"]);
        ScopeFilter.SelectedIndex = 0;
        ScopeFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        ScopeFilter.Margin = new Padding(0, 3, 14, 3);
        ScopeFilter.SelectedIndexChanged += (_, _) => QueueRefreshGrid();

        SearchBox.PlaceholderText = "Search trainer ID, name, class, hash, Pokemon, or placement...";
        SearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SearchBox.Margin = new Padding(0, 3, 14, 3);
        SearchBox.TextChanged += (_, _) => QueueRefreshGrid();

        SummaryLabel.AutoEllipsis = true;
        SummaryLabel.Dock = DockStyle.Fill;
        SummaryLabel.TextAlign = ContentAlignment.MiddleRight;

        FilterTimer.Tick += (_, _) =>
        {
            FilterTimer.Stop();
            RefreshGrid();
        };

        top.Controls.Add(CreateLabel("Scope"), 0, 0);
        top.Controls.Add(ScopeFilter, 1, 0);
        top.Controls.Add(CreateLabel("Search"), 2, 0);
        top.Controls.Add(SearchBox, 3, 0);
        top.Controls.Add(SummaryLabel, 4, 0);

        ConfigureGrid(TrainerGrid);
        TrainerGrid.VirtualMode = true;
        TrainerGrid.CellValueNeeded += TrainerGrid_CellValueNeeded;
        TrainerGrid.SelectionChanged += (_, _) => SelectCurrentTrainer();
        TrainerGrid.Columns.Add(CreateTextColumn("ID", 58));
        TrainerGrid.Columns.Add(CreateTextColumn("Royal", 62));
        TrainerGrid.Columns.Add(CreateTextColumn("Trainer", 164));
        TrainerGrid.Columns.Add(CreateTextColumn("Class", 170));
        TrainerGrid.Columns.Add(CreateTextColumn("Hash Name", 216));
        TrainerGrid.Columns.Add(CreateTextColumn("Pokemon", 74));
        TrainerGrid.Columns.Add(CreateTextColumn("Max Lv", 74));
        TrainerGrid.Columns.Add(CreateTextColumn("Placement", 84));
        TrainerGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Team Preview",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 320,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        var bottom = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            RowCount = 1,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));

        ConfigureGrid(PlacementGrid);
        PlacementGrid.VirtualMode = true;
        PlacementGrid.CellValueNeeded += PlacementGrid_CellValueNeeded;
        PlacementGrid.SelectionChanged += (_, _) => SelectCurrentPlacement();
        PlacementGrid.Columns.Add(CreateTextColumn("Area", 150));
        PlacementGrid.Columns.Add(CreateTextColumn("Zone", 220));
        PlacementGrid.Columns.Add(CreateTextColumn("Location", 112));
        PlacementGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Model / Path",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 180,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        var detailPane = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 0, 0),
            RowCount = 2,
        };
        detailPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPane.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        DetailsText.Dock = DockStyle.Fill;
        DetailsText.Multiline = true;
        DetailsText.ReadOnly = true;
        DetailsText.ScrollBars = ScrollBars.Vertical;
        DetailsText.WordWrap = false;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        ConfigureActionButton(CopyVisibleButton, "Copy Visible", "Copy all visible trainer rows as TSV.", CopyVisibleRows);
        ConfigureActionButton(CopyPlacementsButton, "Copy Placement", "Copy placement rows for the selected trainer as TSV.", CopySelectedPlacements);
        ConfigureActionButton(CopyHashButton, "Copy Hash", "Copy the selected trainer hash.", CopySelectedHash);
        ConfigureActionButton(CopyRowButton, "Copy Row", "Copy the selected trainer row as TSV.", CopySelectedRow);
        actions.Controls.Add(CopyVisibleButton);
        actions.Controls.Add(CopyPlacementsButton);
        actions.Controls.Add(CopyHashButton);
        actions.Controls.Add(CopyRowButton);

        detailPane.Controls.Add(DetailsText, 0, 0);
        detailPane.Controls.Add(actions, 0, 1);

        bottom.Controls.Add(PlacementGrid, 0, 0);
        bottom.Controls.Add(detailPane, 1, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(TrainerGrid, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);
    }

    private void BuildEntries()
    {
        var hashEntries = LoadTrainerHashEntries();
        var placementHits = LoadPlacementHits();
        var count = Math.Max(Trainers.Length, hashEntries.Count);

        for (int i = 0; i < count; i++)
        {
            var hashEntry = i < hashEntries.Count ? hashEntries[i] : TrainerHashEntry.Empty;
            var trainer = TryLoadTrainer(i);
            var team = trainer?.Team.Select(ToTeamMember).ToArray() ?? [];
            var classID = trainer?.Self.Class ?? -1;
            var className = classID >= 0 ? GetIndexedName(TrainerClasses, classID, $"Class {classID}") : string.Empty;
            var trainerName = GetIndexedName(TrainerNames, i, $"Trainer {i}");
            var hits = hashEntry.Hash == 0 || !placementHits.TryGetValue(hashEntry.Hash, out var found) ? [] : found;

            Entries.Add(new(
                i,
                RoyalLadderTrainerIDs.Contains(i),
                trainerName,
                classID,
                className,
                hashEntry.Name,
                hashEntry.Hash,
                trainer?.Self.Mode.ToString() ?? string.Empty,
                trainer?.Self.AI ?? 0,
                trainer?.Self.Money ?? 0,
                trainer?.Self.NumPokemon ?? 0,
                team,
                hits,
                GetClassDetails(classID),
                trainer == null ? "Trainer data missing or unreadable." : string.Empty));
        }
    }

    private List<TrainerHashEntry> LoadTrainerHashEntries()
    {
        var path = Path.Combine(RomFsPath, "bin", "trainer", "trainer_id_hash_table.tbl");
        try
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 8 || !AHTB.IsAHTB(data))
            {
                LoadErrors.Add("bin/trainer/trainer_id_hash_table.tbl is not an AHTB table.");
                return [];
            }

            var table = new AHTB(data);
            return table.Entries.Select(z => new TrainerHashEntry(z.Name, z.Hash)).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            LoadErrors.Add($"bin/trainer/trainer_id_hash_table.tbl: {ex.Message}");
            return [];
        }
    }

    private Dictionary<ulong, PlacementHit[]> LoadPlacementHits()
    {
        try
        {
            var placement = new GFPack(PlacementData);
            var areaNames = ReadPlacementTable(placement, "AreaNameHashTable.tbl");
            var zoneNames = ReadPlacementTable(placement, "ZoneNameHashTable.tbl");
            var hits = new Dictionary<ulong, List<PlacementHit>>();

            foreach (var area in areaNames.Values.Order(StringComparer.OrdinalIgnoreCase))
            {
                var fileName = $"{area}.bin";
                if (placement.GetIndexFileName(fileName) < 0)
                    continue;

                var archive = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(placement.GetDataFileName(fileName));
                foreach (var zone in archive.Table)
                {
                    var zoneName = GetZoneName(zone.Meta.ZoneID, zoneNames);
                    foreach (var holder in zone.Trainers)
                    {
                        var trainerHash = holder.TrainerID;
                        if (trainerHash == 0)
                            continue;

                        if (!hits.TryGetValue(trainerHash, out var list))
                            hits[trainerHash] = list = [];

                        list.Add(new(
                            area,
                            zoneName,
                            GetTrainerLocation(holder),
                            FormatHash(holder.Field00.Field00.HashModel),
                            FormatHash(holder.MovementPath)));
                    }
                }
            }

            return hits.ToDictionary(z => z.Key, z => z.Value.ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            LoadErrors.Add($"Placement scan failed: {ex.Message}");
            return [];
        }
    }

    private static Dictionary<ulong, string> ReadPlacementTable(GFPack placement, string fileName)
    {
        if (placement.GetIndexFileName(fileName) < 0)
            return [];

        var data = placement.GetDataFileName(fileName);
        return data.Length >= 8 && AHTB.IsAHTB(data)
            ? new AHTB(data).ToDictionary()
            : [];
    }

    private static string GetZoneName(ulong zoneHash, IReadOnlyDictionary<ulong, string> zoneNames)
    {
        var name = zoneNames.TryGetValue(zoneHash, out var value) ? value : FormatHash(zoneHash);
        if (SWSHInfo.Zones.TryGetValue(zoneHash, out var description) && !description.Equals(name, StringComparison.OrdinalIgnoreCase))
            return $"{description} [{name}]";

        return name;
    }

    private VsTrainer? TryLoadTrainer(int index)
    {
        if ((uint)index >= (uint)Trainers.Length)
            return null;

        try
        {
            return Trainers[index];
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            LoadErrors.Add($"Trainer {index:000}: {ex.Message}");
            return null;
        }
    }

    private string GetClassDetails(int classID)
    {
        if ((uint)classID >= (uint)Trainers.TrainerClass.Count)
            return string.Empty;

        try
        {
            var trainerClass = Trainers.GetClass(classID);
            var parts = new List<string>
            {
                $"Class Group: {trainerClass.Group}",
                $"Ball ID: {trainerClass.BallID}",
            };

            if (trainerClass is TrainerClass8 swsh)
            {
                parts.Add($"Class S1: {swsh.S1}");
                parts.Add($"Class S2: {swsh.S2}");
            }

            return string.Join(Environment.NewLine, parts);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            return $"Class data unavailable: {ex.Message}";
        }
    }

    private TeamMember ToTeamMember(TrainerPoke poke)
    {
        var species = GetIndexedName(SpeciesNames, poke.Species, $"Species {poke.Species}");
        var form = poke.Form == 0 ? string.Empty : $"-{poke.Form}";
        return new(species, form, poke.Level, poke.HeldItem, poke.Moves.Where(z => z != 0).ToArray(), poke.CanDynamax, poke.Shiny);
    }

    private void QueueRefreshGrid()
    {
        FilterTimer.Stop();
        FilterTimer.Start();
    }

    private void RefreshGrid()
    {
        var scope = ScopeFilter.SelectedItem as string ?? "All";
        var query = SearchBox.Text.Trim();
        var selected = SelectedEntry;
        var filtered = Entries.Where(z => MatchesFilter(z, scope, query)).ToArray();

        TrainerGrid.SuspendLayout();
        TrainerGrid.RowCount = 0;
        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        TrainerGrid.RowCount = VisibleEntries.Count;
        TrainerGrid.ResumeLayout();

        SummaryLabel.Text = $"{VisibleEntries.Count:N0} / {Entries.Count:N0}";

        if (VisibleEntries.Count == 0)
        {
            SelectTrainer(null);
            return;
        }

        var index = selected == null ? 0 : VisibleEntries.FindIndex(z => z.ID == selected.ID);
        if (index < 0)
            index = 0;

        TrainerGrid.CurrentCell = TrainerGrid.Rows[index].Cells[0];
        TrainerGrid.Rows[index].Selected = true;
        SelectTrainer(VisibleEntries[index]);
    }

    private void TrainerGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= VisibleEntries.Count)
            return;

        var entry = VisibleEntries[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => entry.ID.ToString("000"),
            1 => entry.IsRoyalMilestone ? "Yes" : string.Empty,
            2 => entry.TrainerName,
            3 => entry.ClassName,
            4 => entry.HashName,
            5 => $"{entry.Team.Count}/{entry.DeclaredPokemonCount}",
            6 => entry.MaxLevel == 0 ? string.Empty : entry.MaxLevel.ToString(),
            7 => entry.PlacementHits.Count.ToString(),
            8 => entry.TeamPreview,
            _ => string.Empty,
        };
    }

    private void PlacementGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (SelectedEntry is not { } entry || e.RowIndex < 0 || e.RowIndex >= entry.PlacementHits.Count)
            return;

        var hit = entry.PlacementHits[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => hit.Area,
            1 => hit.Zone,
            2 => hit.Location,
            3 => hit.Model == EmptyHashText ? hit.MovementPath : $"{hit.Model} / {hit.MovementPath}",
            _ => string.Empty,
        };
    }

    private static bool MatchesFilter(TrainerMapEntry entry, string scope, string query)
    {
        if (scope == "Royal Ladder" && !entry.IsRoyalMilestone)
            return false;
        if (scope == "Has Placement" && entry.PlacementHits.Count == 0)
            return false;
        if (scope == "No Placement" && entry.PlacementHits.Count != 0)
            return false;
        if (scope == "No Team" && entry.Team.Count != 0)
            return false;

        if (query.Length == 0)
            return true;

        var hash = FormatHash(entry.Hash);
        return entry.ID.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.ID.ToString("000").Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.TrainerName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.HashName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || hash.Contains(query, StringComparison.OrdinalIgnoreCase)
            || hash[2..].Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Team.Any(z => z.Species.Contains(query, StringComparison.OrdinalIgnoreCase))
            || entry.PlacementHits.Any(z => z.Area.Contains(query, StringComparison.OrdinalIgnoreCase)
                || z.Zone.Contains(query, StringComparison.OrdinalIgnoreCase)
                || z.Location.Contains(query, StringComparison.OrdinalIgnoreCase)
                || z.Model.Contains(query, StringComparison.OrdinalIgnoreCase)
                || z.MovementPath.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectCurrentTrainer()
    {
        if (TrainerGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= VisibleEntries.Count)
        {
            SelectTrainer(null);
            return;
        }

        SelectTrainer(VisibleEntries[row]);
    }

    private void SelectTrainer(TrainerMapEntry? entry)
    {
        SelectedEntry = entry;
        SelectedPlacement = null;

        PlacementGrid.SuspendLayout();
        PlacementGrid.RowCount = 0;
        PlacementGrid.RowCount = entry?.PlacementHits.Count ?? 0;
        PlacementGrid.ResumeLayout();

        if (entry == null)
        {
            DetailsText.Text = LoadErrors.Count == 0
                ? "No trainer selected."
                : string.Join(Environment.NewLine, LoadErrors);
            SetActionButtonsEnabled(false);
            return;
        }

        if (entry.PlacementHits.Count != 0)
        {
            PlacementGrid.CurrentCell = PlacementGrid.Rows[0].Cells[0];
            PlacementGrid.Rows[0].Selected = true;
            SelectedPlacement = entry.PlacementHits[0];
        }

        DetailsText.Text = BuildDetails(entry, SelectedPlacement);
        SetActionButtonsEnabled(true);
    }

    private void SelectCurrentPlacement()
    {
        if (SelectedEntry is not { } entry || PlacementGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= entry.PlacementHits.Count)
        {
            SelectedPlacement = null;
            DetailsText.Text = SelectedEntry == null ? "No trainer selected." : BuildDetails(SelectedEntry, null);
            SetActionButtonsEnabled(SelectedEntry is not null);
            return;
        }

        SelectedPlacement = entry.PlacementHits[row];
        DetailsText.Text = BuildDetails(entry, SelectedPlacement);
        SetActionButtonsEnabled(true);
    }

    private static string BuildDetails(TrainerMapEntry entry, PlacementHit? placement)
    {
        var values = new List<string>
        {
            $"Trainer ID: {entry.ID:000}",
            $"Royal Ladder: {(entry.IsRoyalMilestone ? "Yes" : "No")}",
            $"Trainer: {entry.TrainerName}",
            $"Class: {entry.ClassName} ({entry.ClassID})",
            $"Hash Name: {entry.HashName}",
            $"Hash64: {FormatHash(entry.Hash)}",
            $"Mode: {entry.Mode}",
            $"AI: 0x{entry.AI:X8}",
            $"Money Rate: {entry.MoneyRate}",
            $"Estimated Payout: {entry.EstimatedPayout:N0}",
            $"Pokemon Count: {entry.Team.Count} actual / {entry.DeclaredPokemonCount} declared",
            $"Max Level: {entry.MaxLevel}",
            $"Placement Hits: {entry.PlacementHits.Count}",
        };

        if (!string.IsNullOrWhiteSpace(entry.ClassDetails))
        {
            values.Add(string.Empty);
            values.Add(entry.ClassDetails);
        }

        if (!string.IsNullOrWhiteSpace(entry.LoadStatus))
        {
            values.Add(string.Empty);
            values.Add(entry.LoadStatus);
        }

        values.Add(string.Empty);
        values.Add("Team:");
        values.AddRange(entry.Team.Count == 0 ? ["None"] : entry.Team.Select(FormatTeamMember));

        values.Add(string.Empty);
        values.Add("Placement:");
        values.AddRange(entry.PlacementHits.Count == 0 ? ["None"] : entry.PlacementHits.Select(FormatPlacement));

        if (placement is not null)
        {
            values.Add(string.Empty);
            values.Add($"Selected Placement: {FormatPlacement(placement)}");
            values.Add($"Placement TSV: {ToPlacementTsv(entry, placement)}");
        }

        values.Add(string.Empty);
        values.Add("Trainer TSV:");
        values.Add(ToTrainerTsv(entry));

        return string.Join(Environment.NewLine, values);
    }

    private void CopySelectedRow()
    {
        if (SelectedEntry is not { } entry)
            return;

        Clipboard.SetText(TrainerTsvHeader() + Environment.NewLine + ToTrainerTsv(entry));
    }

    private void CopySelectedHash()
    {
        if (SelectedEntry is not { } entry)
            return;

        Clipboard.SetText(FormatHash(entry.Hash));
    }

    private void CopySelectedPlacements()
    {
        if (SelectedEntry is not { } entry)
            return;

        var rows = entry.PlacementHits.Select(z => ToPlacementTsv(entry, z));
        Clipboard.SetText(PlacementTsvHeader() + Environment.NewLine + string.Join(Environment.NewLine, rows));
    }

    private void CopyVisibleRows()
    {
        var rows = VisibleEntries.Select(ToTrainerTsv);
        Clipboard.SetText(TrainerTsvHeader() + Environment.NewLine + string.Join(Environment.NewLine, rows));
    }

    private void SetActionButtonsEnabled(bool hasTrainer)
    {
        CopyRowButton.Enabled = hasTrainer;
        CopyHashButton.Enabled = hasTrainer;
        CopyPlacementsButton.Enabled = hasTrainer && SelectedEntry?.PlacementHits.Count != 0;
        CopyVisibleButton.Enabled = VisibleEntries.Count != 0;
    }

    private static string GetTrainerLocation(PlacementZoneTrainerHolder holder)
    {
        try
        {
            return holder.Field00.Field00.Field00.Location3f;
        }
        catch (NullReferenceException)
        {
            return string.Empty;
        }
    }

    private static string FormatTeamMember(TeamMember member)
    {
        var flags = new List<string>();
        if (member.Form.Length != 0)
            flags.Add($"Form {member.Form[1..]}");
        if (member.CanDynamax)
            flags.Add("Dynamax");
        if (member.Shiny)
            flags.Add("Shiny");

        var suffix = flags.Count == 0 ? string.Empty : $" [{string.Join(", ", flags)}]";
        return $"Lv. {member.Level} {member.Species}{member.Form}{suffix}";
    }

    private static string FormatPlacement(PlacementHit hit) => $"{hit.Area} / {hit.Zone} @ {hit.Location}";

    private static string GetIndexedName(IReadOnlyList<string> values, int index, string fallback)
    {
        if ((uint)index >= (uint)values.Count)
            return fallback;

        var value = values[index];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private const string EmptyHashText = "0xCBF29CE484222645";

    private static string FormatHash(ulong hash) => $"0x{hash:X16}";
    private static string TrainerTsvHeader() => "ID\tRoyalLadder\tTrainer\tClassID\tClass\tHashName\tHash64\tMode\tAI\tMoneyRate\tPokemon\tMaxLevel\tPlacementHits\tTeamPreview";
    private static string PlacementTsvHeader() => "ID\tTrainer\tHashName\tHash64\tArea\tZone\tLocation\tModel\tMovementPath";

    private static string ToTrainerTsv(TrainerMapEntry entry) =>
        $"{entry.ID:000}\t{entry.IsRoyalMilestone}\t{CleanTsv(entry.TrainerName)}\t{entry.ClassID}\t{CleanTsv(entry.ClassName)}\t{CleanTsv(entry.HashName)}\t{FormatHash(entry.Hash)}\t{entry.Mode}\t0x{entry.AI:X8}\t{entry.MoneyRate}\t{entry.Team.Count}/{entry.DeclaredPokemonCount}\t{entry.MaxLevel}\t{entry.PlacementHits.Count}\t{CleanTsv(entry.TeamPreview)}";

    private static string ToPlacementTsv(TrainerMapEntry entry, PlacementHit hit) =>
        $"{entry.ID:000}\t{CleanTsv(entry.TrainerName)}\t{CleanTsv(entry.HashName)}\t{FormatHash(entry.Hash)}\t{CleanTsv(hit.Area)}\t{CleanTsv(hit.Zone)}\t{CleanTsv(hit.Location)}\t{hit.Model}\t{hit.MovementPath}";

    private static string CleanTsv(string value) => value.Replace('\t', ' ').Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');

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

    private void ConfigureActionButton(Button button, string text, string tooltip, Action action)
    {
        button.Text = text;
        button.Width = 116;
        button.Height = 32;
        button.Margin = new Padding(6, 0, 0, 4);
        button.Click += (_, _) => action();
        ButtonToolTips.SetToolTip(button, tooltip);
    }

    private void ApplyTheme()
    {
        BackColor = SystemColors.Control;
        TrainerGrid.BackgroundColor = SystemColors.Window;
        PlacementGrid.BackgroundColor = SystemColors.Window;
        DetailsText.BackColor = SystemColors.Window;
        DetailsText.ForeColor = SystemColors.WindowText;
    }

    private sealed record TrainerHashEntry(string Name, ulong Hash)
    {
        public static readonly TrainerHashEntry Empty = new(string.Empty, 0);
    }

    private sealed record TrainerMapEntry(
        int ID,
        bool IsRoyalMilestone,
        string TrainerName,
        int ClassID,
        string ClassName,
        string HashName,
        ulong Hash,
        string Mode,
        uint AI,
        int MoneyRate,
        int DeclaredPokemonCount,
        IReadOnlyList<TeamMember> Team,
        IReadOnlyList<PlacementHit> PlacementHits,
        string ClassDetails,
        string LoadStatus)
    {
        public int MaxLevel => Team.Select(z => z.Level).DefaultIfEmpty(0).Max();
        public int EstimatedPayout => MaxLevel * MoneyRate * 4;
        public string TeamPreview => Team.Count == 0 ? "None" : string.Join(", ", Team.Select(FormatTeamMember).Take(6));
    }

    private sealed record TeamMember(string Species, string Form, int Level, int HeldItem, IReadOnlyList<int> Moves, bool CanDynamax, bool Shiny);
    private sealed record PlacementHit(string Area, string Zone, string Location, string Model, string MovementPath);
}
