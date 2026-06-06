using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using pkNX.Game;
using pkNX.Randomization;
using pkNX.Structures;
using PKHeX.Drawing.PokeSprite;

namespace pkNX.WinForms;

public partial class BTTE : Form
{
    private const int TrainerHeaderHeight = 42;
    private static readonly string[] DefaultFormNames = Enumerable.Range(0, 32).Select(i => i.ToString()).ToArray();
    private static readonly object[] TrainerBallNames = GetTrainerBallNames();

    private LearnsetRandomizer? learn;
    private readonly string[][] AltForms;
    private readonly PictureBox[] pba;

    private int entry = -1;
    private TrainerPoke pkm = new TrainerPoke7b();
    private bool loadingPKM;

    private readonly IPersonalTable Personal;
    private readonly GameManager Game;
    private readonly GameData Data;
    private readonly TrainerEditor Trainers;

    private string[] abilitylist = [];
    private string[] movelist = [];
    private readonly string[] itemlist;
    private string[] specieslist = [];
    private string[] types = [];
    private string[] natures = [];
    private readonly string[] trName;
    private readonly string[] trClass;

    private readonly CheckBox[] AIBits;
    private readonly List<SearchableComboBoxBehavior> SearchableCombos = [];
    private readonly HashSet<ComboBox> SearchableRegistered = [];
    private readonly Dictionary<TeamSpriteKey, Image> TeamSpriteCache = [];
    private const sbyte TrainerClassOwnershipUnknown = -1;
    private const sbyte TrainerClassOwnershipShared = 0;
    private const sbyte TrainerClassOwnershipSingleOwner = 1;
    private sbyte[] TrainerClassOwnership = [];
    private readonly Label L_ClassBall = new();
    private readonly ComboBox CB_ClassBall = new();
    private readonly ToolTip TrainerToolTip = new()
    {
        AutoPopDelay = 12000,
        InitialDelay = 500,
        ReshowDelay = 100,
        ShowAlways = true,
        BackColor = WinFormsTheme.PanelBackground,
        ForeColor = WinFormsTheme.Text,
    };
    private bool CloseConfirmed;
    private bool UpdatingMoneyItems;
    private bool RandomizerSettingsLoaded;
    private bool PokemonEditorListsLoaded;
    private bool MoveListLoaded;
    private bool StatsInitialized;
    private bool TrainerItemListsLoaded;

    public BTTE(GameData data, TrainerEditor editor, GameManager game)
    {
        Game = game;
        Data = data;
        Trainers = editor;
        InitializeComponent();
        pba = [PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6];

        Stats.Personal = Personal = data.PersonalData;

        AltForms = new string[Personal.Table.Length][];
        Array.Fill(AltForms, DefaultFormNames);

        itemlist = Game.GetStrings(TextName.ItemNames);
        trName = Game.GetStrings(TextName.TrainerNames);
        trClass = Game.GetStrings(TextName.TrainerClasses);
        itemlist[0] = "(None)";

        AIBits = Game.Info.SWSH
            ? [CHK_AI_Basic, CHK_AI_Strong, CHK_AI_Expert, CHK_AI_Double, CHK_AI_Raid, CHK_AI_Allowance, CHK_AI_PokeChange, CHK_AI_FireGym1, CHK_AI_FireGym2, CHK_AI_Unused1, CHK_AI_Item, CHK_AI_FireGym3, CHK_AI_Unused2]
            : [CHK_AI_Basic, CHK_AI_Strong, CHK_AI_Expert, CHK_AI_Double, CHK_AI_Allowance, CHK_AI_Item, CHK_AI_PokeChange,                                   CHK_AI_Unused1];

        InitializeTrainerClassOwnership();
        SetupTrainerClassBallControl();
        Setup();
        foreach (var pb in pba)
            pb.Click += (_, e) => ClickSlot(pb, e);

        if (CB_TrainerID.SelectedIndex < 0)
            CB_TrainerID.SelectedIndex = 0;

        L_Gift.Visible = CB_Gift.Visible = NUD_GiftCount.Visible = Game.Info.GG;
        GB_Additional_AI.Visible = Game.Info.SWSH;

        ApplyTrainerEditorTheme();
        ConfigureSearchableDropdowns();
        RegisterTrainerItemLazyLoads();
        CB_Money.SelectedIndexChanged += (_, _) =>
        {
            if (!UpdatingMoneyItems)
                UpdateMoneyToolTip();
        };
        CB_Trainer_Class.SelectedIndexChanged += (_, _) => LoadTrainerClassBall();
        RegisterPokemonEditorLazyLoads();
        TC_trdata.SelectedIndexChanged += (_, _) =>
        {
            if (TC_trdata.SelectedTab == Tab_Rand)
                EnsureRandomizerSettingsLoaded();
        };
        Shown += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            ConfigureTrainerToolTips();
            CB_TrainerID.Focus();
            CB_TrainerID.SelectAll();
        }));
    }

    public bool Modified { get; set; }

    private void ApplyTrainerEditorTheme()
    {
        WinFormsTheme.Apply(this);
        WinFormsTheme.Apply(mnuVSD);
        AddTrainerEditorHeader();
        PolishTrainerEditorColors();
    }

    private void AddTrainerEditorHeader()
    {
        var dataTabBounds = TC_trdata.Bounds;
        var pokeTabBounds = TC_trpoke.Bounds;
        var dataNativeTabHeight = Math.Max(1, TC_trdata.DisplayRectangle.Top);
        var pokeNativeTabHeight = Math.Max(1, TC_trpoke.DisplayRectangle.Top);
        var dataTop = TrainerHeaderHeight - dataNativeTabHeight;
        var pokeTop = TrainerHeaderHeight - pokeNativeTabHeight;

        TC_trdata.Bounds = new Rectangle(dataTabBounds.Left, dataTop, dataTabBounds.Width, Math.Max(1, dataTabBounds.Bottom - dataTop));
        TC_trpoke.Bounds = new Rectangle(pokeTabBounds.Left, pokeTop, pokeTabBounds.Width, Math.Max(1, pokeTabBounds.Bottom - pokeTop));

        var header = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = WinFormsTheme.WindowBackground,
            Bounds = new Rectangle(0, 0, ClientSize.Width, TrainerHeaderHeight),
        };
        header.Paint += (_, e) =>
        {
            using var border = new Pen(WinFormsTheme.Border);
            e.Graphics.DrawLine(border, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        Controls.Remove(CB_TrainerID);
        Controls.Remove(B_Save);
        header.Controls.Add(CB_TrainerID);
        header.Controls.Add(B_Save);

        var trainerTabs = WinFormsTheme.AddThemedTabButtons(header, TC_trdata, 6, 8, 28, 60, 18);
        var pokemonTabs = WinFormsTheme.AddThemedTabButtons(header, TC_trpoke, 0, 8, 28, 58, 18);
        LayoutTrainerEditorHeader(header, trainerTabs, pokemonTabs);
        header.Resize += (_, _) => LayoutTrainerEditorHeader(header, trainerTabs, pokemonTabs);

        Controls.Add(header);
        header.BringToFront();
    }

    private void LayoutTrainerEditorHeader(Control header, IReadOnlyList<Button> trainerTabs, IReadOnlyList<Button> pokemonTabs)
    {
        const int padding = 6;
        const int gap = 8;
        const int controlTop = 8;
        const int controlHeight = 28;

        B_Save.Size = new Size(92, controlHeight);
        B_Save.Location = new Point(header.Width - B_Save.Width - padding, controlTop);

        var pokemonTabWidth = pokemonTabs.Count == 0 ? 0 : pokemonTabs[^1].Right - pokemonTabs[0].Left;
        var pokemonLeft = Math.Max(0, B_Save.Left - gap - pokemonTabWidth);
        for (var i = 0; i < pokemonTabs.Count; i++)
        {
            var button = pokemonTabs[i];
            button.Location = i == 0
                ? new Point(pokemonLeft, controlTop)
                : new Point(pokemonTabs[i - 1].Right + 2, controlTop);
        }

        var trainerRight = trainerTabs.Count == 0 ? padding : trainerTabs[^1].Right;
        var selectorLeft = trainerRight + gap;
        var selectorRight = pokemonTabs.Count == 0 ? B_Save.Left - gap : pokemonTabs[0].Left - gap;
        CB_TrainerID.Location = new Point(selectorLeft, controlTop + 1);
        CB_TrainerID.Size = new Size(Math.Max(120, selectorRight - selectorLeft), 23);
    }

    private void PolishTrainerEditorColors()
    {
        foreach (var page in new[] { Tab_Trainer, Tab_Rand, Tab_Main, Tab_Stats, Tab_Moves, Tab_RTrainer, Tab_RSpecies, Tab_RMoves, Tab_Mods })
            page.BackColor = WinFormsTheme.PanelBackground;

        foreach (var group in new[] { GB_Items, GB_AI, GB_Additional_AI, GB_Moves })
        {
            group.BackColor = WinFormsTheme.PanelBackground;
            group.ForeColor = WinFormsTheme.Text;
        }

        foreach (var panel in new[] { flowLayoutPanel2, FLP_Species, FLP_Form, FLP_LevelShiny, FLP_Ability, FLP_HeldItem, FLP_Nature, FLP_Gender, FLP_Friendship, FLP_Mega, FLP_CanDynamax })
        {
            panel.BackColor = WinFormsTheme.PanelBackground;
            panel.ForeColor = WinFormsTheme.Text;
        }

        Stats.BackColor = WinFormsTheme.PanelBackground;
        Stats.ForeColor = WinFormsTheme.Text;

        foreach (var pb in pba)
        {
            pb.BackColor = WinFormsTheme.InputBackground;
            pb.BorderStyle = BorderStyle.FixedSingle;
        }

        if (Game.Info.SWSH)
            LayoutTrainerMetadataControls();
    }

    private void SetupTrainerClassBallControl()
    {
        if (!Game.Info.SWSH)
            return;

        L_ClassBall.Name = nameof(L_ClassBall);
        L_ClassBall.Text = "Class Ball:";

        CB_ClassBall.DropDownStyle = ComboBoxStyle.DropDownList;
        CB_ClassBall.FormattingEnabled = true;
        CB_ClassBall.Name = nameof(CB_ClassBall);
        CB_ClassBall.Items.AddRange(TrainerBallNames);
        CB_ClassBall.SelectedIndexChanged += (_, _) =>
        {
            if (!loading)
                SaveTrainerClassBall(CB_Trainer_Class.SelectedIndex);
            UpdateClassBallToolTip();
        };

        Tab_Trainer.Controls.Add(L_ClassBall);
        Tab_Trainer.Controls.Add(CB_ClassBall);
        LayoutTrainerMetadataControls();
        SetTrainerClassBallVisible(false);
    }

    private void LayoutTrainerMetadataControls()
    {
        Tab_Trainer.AutoScroll = true;
        Tab_Trainer.AutoScrollMinSize = new Size(0, 390);

        const int labelX = 8;
        const int labelWidth = 96;
        const int inputX = 112;
        const int inputHeight = 23;
        const int fullInputWidth = 240;
        const int row1 = 12;
        const int row2 = 42;
        const int row3 = 72;

        PlaceMetadataLabel(L_Trainer_Class, labelX, row1, labelWidth, inputHeight);
        CB_Trainer_Class.Location = new Point(inputX, row1);
        CB_Trainer_Class.Size = new Size(fullInputWidth, inputHeight);

        PlaceMetadataLabel(L_Money, labelX, row2, labelWidth, inputHeight);
        CB_Money.Location = new Point(inputX, row2);
        CB_Money.Size = new Size(94, inputHeight);
        CB_Money.DropDownWidth = CB_Money.Width;

        PlaceMetadataLabel(L_Mode, 214, row2, 48, inputHeight);
        CB_Mode.Location = new Point(268, row2);
        CB_Mode.Size = new Size(84, inputHeight);

        PlaceMetadataLabel(L_ClassBall, labelX, row3, labelWidth, inputHeight);
        CB_ClassBall.Location = new Point(inputX, row3);
        CB_ClassBall.Size = new Size(fullInputWidth, inputHeight);

        GB_Items.Top = 132;
        GB_AI.Top = GB_Items.Top;
        GB_Additional_AI.Left = GB_Items.Left;
        GB_Additional_AI.Width = GB_Items.Width;
        GB_Additional_AI.Top = GB_Items.Bottom + 34;
    }

    private static void PlaceMetadataLabel(Label label, int x, int y, int width, int height)
    {
        label.AutoSize = false;
        label.Location = new Point(x, y);
        label.Size = new Size(width, height);
        label.TextAlign = ContentAlignment.MiddleRight;
        label.BackColor = Color.Transparent;
    }

    private void SetTrainerClassBallVisible(bool visible)
    {
        L_ClassBall.Visible = visible;
        CB_ClassBall.Visible = visible;
        CB_ClassBall.Enabled = visible;
    }

    private static object[] GetTrainerBallNames() => Enumerable.Range(0, (int)Ball.Beast + 1)
        .Select(z => FormatTrainerBallName((Ball)z))
        .Cast<object>()
        .ToArray();

    private static string FormatTrainerBallName(Ball ball)
    {
        if (ball == Ball.None)
            return "None (0)";

        var name = ball switch
        {
            Ball.Poke => "Poke Ball",
            Ball.LAPoke => "LA Poke Ball",
            Ball.LAGreat => "LA Great Ball",
            Ball.LAUltra => "LA Ultra Ball",
            Ball.LAFeather => "LA Feather Ball",
            Ball.LAWing => "LA Wing Ball",
            Ball.LAJet => "LA Jet Ball",
            Ball.LAHeavy => "LA Heavy Ball",
            Ball.LALeaden => "LA Leaden Ball",
            Ball.LAGigaton => "LA Gigaton Ball",
            Ball.LAOrigin => "LA Origin Ball",
            _ => $"{ball} Ball",
        };
        return $"{name} ({(int)ball})";
    }

    private void InitializeTrainerClassOwnership()
    {
        if (!Game.Info.SWSH)
            return;

        var count = Math.Max(Trainers.TrainerClass.Count, trClass.Length);
        TrainerClassOwnership = Enumerable.Repeat(TrainerClassOwnershipUnknown, count).ToArray();
    }

    private string GetTrainerClassOwnerName(int trainerIndex)
    {
        var name = (uint)trainerIndex < (uint)trName.Length ? trName[trainerIndex].Trim() : string.Empty;
        return string.IsNullOrWhiteSpace(name) ? $"#{trainerIndex}" : name;
    }

    private bool CanEditTrainerClassBall(int classIndex) =>
        Game.Info.SWSH
        && (uint)classIndex < (uint)Trainers.TrainerClass.Count
        && IsTrainerClassSingleOwner(classIndex);

    private bool IsTrainerClassSingleOwner(int classIndex)
    {
        if ((uint)classIndex >= (uint)TrainerClassOwnership.Length)
            return false;

        ref var state = ref TrainerClassOwnership[classIndex];
        if (state == TrainerClassOwnershipUnknown)
            state = ComputeTrainerClassHasSingleOwner(classIndex)
                ? TrainerClassOwnershipSingleOwner
                : TrainerClassOwnershipShared;

        return state == TrainerClassOwnershipSingleOwner;
    }

    private bool ComputeTrainerClassHasSingleOwner(int classIndex)
    {
        string? firstOwnerName = null;
        for (int i = 0; i < Trainers.TrainerData.Count; i++)
        {
            if (ReadTrainerClass(Trainers.TrainerData[i]) != classIndex)
                continue;

            var ownerName = GetTrainerClassOwnerName(i);
            if (firstOwnerName is null)
            {
                firstOwnerName = ownerName;
            }
            else if (!string.Equals(firstOwnerName, ownerName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return firstOwnerName is not null;
    }

    private static int ReadTrainerClass(byte[] data) =>
        data.Length < 2 ? -1 : System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));

    private void ConfigureSearchableDropdowns()
    {
        RegisterSearch(CB_TrainerID);
        RegisterSearch(CB_Trainer_Class);
        RegisterSearch(CB_Money);
        if (Game.Info.SWSH)
            RegisterSearch(CB_ClassBall);
    }

    private void RegisterSearch(ComboBox comboBox)
    {
        if (!SearchableRegistered.Add(comboBox))
            return;

        SearchableCombos.Add(new SearchableComboBoxBehavior(this, comboBox));
    }

    private void ConfigureTrainerToolTips()
    {
        TrainerToolTip.SetToolTip(CB_TrainerID, "Type to search trainers by name or index.");
        TrainerToolTip.SetToolTip(CB_Trainer_Class, "Trainer class shown before the trainer name in battle.");
        UpdateMoneyToolTip();
        UpdateClassBallToolTip();
        TrainerToolTip.SetToolTip(CB_Mode, "Battle format for this trainer encounter.");
        TrainerToolTip.SetToolTip(B_Save, "Save trainer edits to the loaded project and close the editor.");
        TrainerToolTip.SetToolTip(B_Randomize, "Randomize trainers using the selected randomizer options.");
        TrainerToolTip.SetToolTip(B_Boost, "Apply the configured level boost to every trainer Pokemon.");
        TrainerToolTip.SetToolTip(B_MaxAI, "Enable the strongest known AI flags for every trainer.");
        TrainerToolTip.SetToolTip(B_Dump, "Export a text summary of trainer teams.");
        TrainerToolTip.SetToolTip(B_CurrentAttack, "Fill this Pokemon's moves with its current level-up moves.");
        TrainerToolTip.SetToolTip(B_HighAttack, "Fill this Pokemon's moves with high-power level-up moves.");
        TrainerToolTip.SetToolTip(B_Clear, "Clear all four moves for this Pokemon.");
        TrainerToolTip.SetToolTip(Stats.CHK_Gigantamax, "Lets this trainer Pokemon use its Gigantamax form when Dynamax is allowed.");

        foreach (var pb in pba)
            TrainerToolTip.SetToolTip(pb, "Right-click for View, Set, and Delete. Ctrl-click views, Shift-click sets, Alt-click deletes.");

        mnuView.ToolTipText = "Load this team slot into the editor fields.";
        mnuSet.ToolTipText = "Write the current editor fields into this team slot.";
        mnuDelete.ToolTipText = "Remove this team slot and shift later Pokemon left.";

        SetAIToolTip(CHK_AI_Basic, "Basic battle decisions.");
        SetAIToolTip(CHK_AI_Strong, "Uses stronger move-selection logic.");
        SetAIToolTip(CHK_AI_Expert, "Uses advanced battle decision logic.");
        SetAIToolTip(CHK_AI_Double, "Enables double-battle aware decisions.");
        SetAIToolTip(CHK_AI_Raid, "Raid-style AI behavior used by some Sword/Shield encounters.");
        SetAIToolTip(CHK_AI_Allowance, "Known trainer AI bit 4. Existing pkNX names it Allowance; exact battle behavior is not documented, so preserve it unless you are testing AI changes.");
        SetAIToolTip(CHK_AI_Item, "Allows the trainer AI to use battle items.");
        SetAIToolTip(CHK_AI_PokeChange, "Allows the trainer AI to switch Pokemon.");
        SetAIToolTip(CHK_AI_FireGym1, "Sword/Shield gym-specific AI flag used by Fire Gym trainers.");
        SetAIToolTip(CHK_AI_FireGym2, "Sword/Shield gym-specific AI flag used by Fire Gym trainers.");
        SetAIToolTip(CHK_AI_FireGym3, "Sword/Shield gym-specific AI flag used by Fire Gym trainers.");
        SetAIToolTip(CHK_AI_Unused1, "Unknown or currently unused AI flag.");
        SetAIToolTip(CHK_AI_Unused2, "Unknown or currently unused AI flag.");
    }

    private void SetAIToolTip(CheckBox checkBox, string text) => TrainerToolTip.SetToolTip(checkBox, text);

    private void RegisterPokemonEditorLazyLoads()
    {
        foreach (var control in new Control[] { CB_Species, CB_Form, CB_Ability, CB_Item, CB_Nature, CB_Gender, NUD_Level, CHK_Shiny, NUD_Friendship, CHK_CanMega, NUD_MegaForm, CHK_CanDynamax })
            control.Enter += (_, _) => EnsurePokemonEditorListsLoaded();

        foreach (var control in new Control[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4, B_CurrentAttack, B_HighAttack, B_Clear })
            control.Enter += (_, _) => EnsureMoveListLoaded();

        TC_trpoke.SelectedIndexChanged += (_, _) =>
        {
            if (TC_trpoke.SelectedTab == Tab_Stats)
            {
                EnsurePokemonEditorListsLoaded();
                EnsureStatsInitialized();
            }
            else if (TC_trpoke.SelectedTab == Tab_Moves)
            {
                EnsureMoveListLoaded();
            }
        };
    }

    private void RegisterTrainerItemLazyLoads()
    {
        foreach (var combo in GetTrainerItemCombos())
        {
            combo.Enter += (_, _) => EnsureTrainerItemListsLoaded();
            combo.DropDown += (_, _) => EnsureTrainerItemListsLoaded();
        }
    }

    private ComboBox[] GetTrainerItemCombos() => [CB_Item_1, CB_Item_2, CB_Item_3, CB_Item_4, CB_Gift];

    private void EnsureTrainerItemListsLoaded()
    {
        if (TrainerItemListsLoaded)
            return;

        var combos = GetTrainerItemCombos();
        var values = combos.Select(GetTrainerItemComboValue).ToArray();
        TrainerItemListsLoaded = true;

        for (var i = 0; i < combos.Length; i++)
        {
            SetComboItems(combos[i], itemlist);
            RegisterSearch(combos[i]);
            SetTrainerItemComboValue(combos[i], values[i]);
        }
    }

    private void SetTrainerItemComboValue(ComboBox combo, int value)
    {
        value = Math.Clamp(value, 0, itemlist.Length - 1);
        combo.Tag = value;

        if (TrainerItemListsLoaded)
        {
            combo.SelectedIndex = combo.Items.Count == 0 ? -1 : Math.Clamp(value, 0, combo.Items.Count - 1);
            return;
        }

        combo.SelectedIndex = -1;
        combo.Text = GetTrainerItemName(value);
    }

    private int GetTrainerItemComboValue(ComboBox combo)
    {
        if (combo.Items.Count != 0)
            return Math.Clamp(combo.SelectedIndex, 0, itemlist.Length - 1);

        return combo.Tag is int value
            ? Math.Clamp(value, 0, itemlist.Length - 1)
            : 0;
    }

    private string GetTrainerItemName(int value) => (uint)value < (uint)itemlist.Length ? itemlist[value] : $"Item {value}";

    private void EnsurePokemonEditorListsLoaded()
    {
        if (PokemonEditorListsLoaded)
            return;

        abilitylist = Game.GetStrings(TextName.AbilityNames);
        specieslist = Game.GetStrings(TextName.SpeciesNames);
        natures = Game.GetStrings(TextName.Natures);

        specieslist[0] = "---";
        abilitylist[0] = "(None)";

        SetComboItems(CB_Species, specieslist);
        SetComboItems(CB_Item, itemlist);
        SetComboItems(CB_Nature, natures.Take(25).Cast<object>().ToArray());
        RegisterSearch(CB_Species);
        RegisterSearch(CB_Item);
        RegisterSearch(CB_Nature);
        CB_Gender.Items.Add("- / Genderless/Random");
        CB_Gender.Items.Add("♂ / Male");
        CB_Gender.Items.Add("♀ / Female");
        CB_Form.Items.Add("");
        CB_Species.SelectedIndex = 0;

        PokemonEditorListsLoaded = true;
    }

    private void EnsureMoveListLoaded()
    {
        if (MoveListLoaded)
            return;

        movelist = EditorUtil.SanitizeMoveList(Game.GetStrings(TextName.MoveNames));
        movelist[0] = "(None)";

        SetComboItems(CB_Move1, movelist);
        SetComboItems(CB_Move2, movelist);
        SetComboItems(CB_Move3, movelist);
        SetComboItems(CB_Move4, movelist);
        RegisterSearch(CB_Move1);
        RegisterSearch(CB_Move2);
        RegisterSearch(CB_Move3);
        RegisterSearch(CB_Move4);
        MoveListLoaded = true;
    }

    private void EnsureStatsInitialized()
    {
        if (StatsInitialized)
            return;

        types = Game.GetStrings(TextName.TypeNames);
        Stats.Initialize(types);
        StatsInitialized = true;
    }

    private void EnsureRandomizerSettingsLoaded()
    {
        if (RandomizerSettingsLoaded)
            return;

        PG_Moves.SelectedObject = EditUtil.Settings.Move;
        PG_RTrainer.SelectedObject = EditUtil.Settings.Trainer;
        PG_Species.SelectedObject = EditUtil.Settings.Species;
        RandomizerSettingsLoaded = true;
    }

    private void SetMoneyItemsForLevel(int highestLevel)
    {
        highestLevel = Math.Max(0, highestLevel);
        if (CB_Money.Items.Count == 256 && CB_Money.Tag is int currentLevel && currentLevel == highestLevel)
            return;

        var selected = Math.Clamp(CB_Money.SelectedIndex, 0, 255);

        UpdatingMoneyItems = true;
        CB_Money.BeginUpdate();
        CB_Money.Items.Clear();
        CB_Money.Items.AddRange(Enumerable.Range(0, 256).Select(z => $"${GetTrainerPayout(highestLevel, z):N0}").Cast<object>().ToArray());
        CB_Money.EndUpdate();
        CB_Money.SelectedIndex = selected;
        CB_Money.Tag = highestLevel;
        UpdatingMoneyItems = false;
    }

    private void UpdateMoneyItemsForTrainer(int trainerIndex)
    {
        var highestLevel = GetHighestTeamLevel(trainerIndex);
        SetMoneyItemsForLevel(highestLevel);
    }

    private void UpdateMoneyItemsForTeam(IList<TrainerPoke> team)
    {
        var highestLevel = GetHighestTeamLevel(team);
        SetMoneyItemsForLevel(highestLevel);
    }

    private void UpdateMoneyToolTip()
    {
        var rate = Math.Max(0, CB_Money.SelectedIndex);
        var highestLevel = CB_Money.Tag is int level ? level : GetHighestTeamLevel(entry);
        var payout = GetTrainerPayout(highestLevel, rate);
        TrainerToolTip.SetToolTip(
            CB_Money,
            $"Prize payout rate stored per trainer.\n\nEstimated Sword/Shield payout: rate {rate} x highest team level {highestLevel} x 4 = ${payout:N0}.\nChange this Money value or the trainer team's levels to change the payout.");
    }

    private void UpdateClassBallToolTip()
    {
        if (!Game.Info.SWSH)
            return;

        var classIndex = CB_Trainer_Class.SelectedIndex;
        var ballIndex = CB_ClassBall.SelectedIndex;
        var className = (uint)classIndex < (uint)CB_Trainer_Class.Items.Count
            ? CB_Trainer_Class.GetItemText(CB_Trainer_Class.Items[classIndex])
            : "this class";
        var ballName = (uint)ballIndex < (uint)CB_ClassBall.Items.Count
            ? CB_ClassBall.GetItemText(CB_ClassBall.Items[ballIndex])
            : "Unknown";

        TrainerToolTip.SetToolTip(
            CB_ClassBall,
            $"Ball assigned by trainer class.\n\nCurrent class: {className}\nCurrent ball: {ballName}\nThis is stored on the trainer class record, so trainers that share the same class can share this ball setting.");
    }

    private int GetHighestTeamLevel(int trainerIndex)
    {
        if ((uint)trainerIndex >= (uint)Trainers.Length)
            return 0;

        return GetHighestTeamLevel(Trainers[trainerIndex].Team);
    }

    private static int GetHighestTeamLevel(IList<TrainerPoke> team) =>
        team.Count == 0 ? 0 : team.Max(z => z.Level);

    private static int GetTrainerPayout(int highestLevel, int rate) => highestLevel * rate * 4;

    private int GetSlot(object sender)
    {
        var send = ((sender as ToolStripItem)?.Owner as ContextMenuStrip)?.SourceControl ?? sender as PictureBox;
        return Array.IndexOf(pba, send);
    }

    private void ClickSlot(object sender, EventArgs e)
    {
        switch (ModifierKeys)
        {
            case Keys.Control: ClickView(sender, e); break;
            case Keys.Shift: ClickSet(sender, e); break;
            case Keys.Alt: ClickDelete(sender, e); break;
        }
    }

    private void ClickView(object sender, EventArgs e)
    {
        int slot = GetSlot(sender);
        if (pba[slot].Image == null)
        {
            System.Media.SystemSounds.Exclamation.Play();
            return;
        }

        // Load the PKM
        var pk = Trainers[entry].Team[slot];
        if (pk.Species != 0)
        {
            PopulateFields(pk);
            // Visual to display what slot is currently loaded.
            GetSlotColor(slot, PKHeX.Drawing.PokeSprite.Properties.Resources.slotView68);
        }
        else
        {
            System.Media.SystemSounds.Exclamation.Play();
        }
    }

    private void ClickSet(object sender, EventArgs e)
    {
        int slot = GetSlot(sender);
        EnsurePokemonEditorListsLoaded();
        if (CB_Species.SelectedIndex <= 0)
        { WinFormsUtil.Alert("Can't set empty slot."); return; }

        var pk = PreparePKM();
        var tr = Trainers[entry];
        if (slot < tr.Team.Count)
        {
            tr.Team[slot] = pk;
        }
        else
        {
            tr.Team.Add(pk);
            slot = tr.Team.Count - 1;
        }

        GetQuickFiller(pba[slot], pk);
        GetSlotColor(slot, PKHeX.Drawing.PokeSprite.Properties.Resources.slotSet68);
        RefreshMoneyDisplayForCurrentTrainer();
    }

    private void ClickDelete(object sender, EventArgs e)
    {
        int slot = GetSlot(sender);

        if (slot < Trainers[entry].Team.Count)
            Trainers[entry].Team.RemoveAt(slot);

        PopulateTeam(Trainers[entry].Team);
        GetSlotColor(slot, PKHeX.Drawing.PokeSprite.Properties.Resources.slotDel68);
        RefreshMoneyDisplayForCurrentTrainer();
    }

    private void PopulateTeam(IList<TrainerPoke> team)
    {
        for (int i = 0; i < team.Count; i++)
            GetQuickFiller(pba[i], team[i]);
        for (int i = team.Count; i < 6; i++)
            pba[i].Image = null;
    }

    private void GetSlotColor(int slot, Image color)
    {
        foreach (PictureBox t in pba)
            t.BackgroundImage = null;

        pba[slot].BackgroundImage = color;
    }

    private void RefreshMoneyDisplayForCurrentTrainer()
    {
        UpdateMoneyItemsForTeam(Trainers[entry].Team);
        UpdateMoneyToolTip();
    }

    private void GetQuickFiller(PictureBox pb, TrainerPoke pk)
    {
        var shiny = pk.Shiny ? PKHeX.Core.Shiny.Always : PKHeX.Core.Shiny.Never;
        var key = new TeamSpriteKey((ushort)pk.Species, (byte)pk.Form, (byte)(pk.Gender - 1), pk.HeldItem, pk.Shiny);
        if (!TeamSpriteCache.TryGetValue(key, out var image))
        {
            image = SpriteUtil.GetSprite(key.Species, key.Form, key.Gender, 0, key.HeldItem, false, shiny);
            TeamSpriteCache[key] = image;
        }

        pb.Image = image;
    }

    // Top Level Functions
    private void RefreshFormAbility(object sender, EventArgs e)
    {
        if (entry < 0)
            return;
        RefreshPKMSlotAbility();
        if (loadingPKM)
            return;
        pkm.Form = CB_Form.SelectedIndex;

        if (StatsInitialized && !Stats.UpdatingFields)
            Stats.UpdateStats();
    }

    private void RefreshSpeciesAbility(object sender, EventArgs e)
    {
        if (entry < 0)
            return;
        FormUtil.SetForms(CB_Species.SelectedIndex, CB_Form, AltForms);
        if (loadingPKM)
            return;
        pkm.Species = (ushort)CB_Species.SelectedIndex;
        RefreshPKMSlotAbility();

        if (StatsInitialized && !Stats.UpdatingFields)
            Stats.UpdateStats();
    }

    private void RefreshPKMSlotAbility()
    {
        int previousAbilityIndex = CB_Ability.SelectedIndex;

        ushort species = (ushort)CB_Species.SelectedIndex;
        byte formnum = (byte)CB_Form.SelectedIndex;
        int index = Personal[species].FormIndex(species, formnum);

        var pi = Personal[index];
        CB_Ability.Items.Clear();
        CB_Ability.Items.Add("Any (1 or 2)");
        CB_Ability.Items.Add(abilitylist[pi.Ability1] + " (1)");
        CB_Ability.Items.Add(abilitylist[pi.Ability2] + " (2)");
        CB_Ability.Items.Add(abilitylist[pi.AbilityH] + " (H)");

        CB_Ability.SelectedIndex = Math.Clamp(previousAbilityIndex, -1, CB_Ability.Items.Count - 1);
    }

    private static string GetEntryTitle(string str, int i) => $"{str} - {i:000}";

    private void Setup()
    {
        SetEntryTitleItems(CB_TrainerID, trName, Trainers.Length);
        SetEntryTitleItems(CB_Trainer_Class, trClass, trClass.Length);

        CHK_CanMega.CheckedChanged += (s, e) => NUD_MegaForm.Visible = CHK_CanMega.Checked;
        NUD_MegaForm.Visible = false;

        CB_TrainerID.SelectedIndex = 0;
        entry = 0;
    }

    private static void SetEntryTitleItems(ComboBox comboBox, IReadOnlyList<string> names, int count)
    {
        comboBox.BeginUpdate();
        try
        {
            comboBox.Items.Clear();
            var items = new object[count];
            for (int i = 0; i < items.Length; i++)
            {
                var name = (uint)i < (uint)names.Count ? names[i] : string.Empty;
                items[i] = GetEntryTitle(name, i);
            }
            comboBox.Items.AddRange(items);
        }
        finally
        {
            comboBox.EndUpdate();
        }
    }

    private static void SetComboItems(ComboBox comboBox, object[] items)
    {
        comboBox.BeginUpdate();
        try
        {
            comboBox.Items.Clear();
            comboBox.Items.AddRange(items);
        }
        finally
        {
            comboBox.EndUpdate();
        }
    }

    private void ChangeTrainerIndex(object sender, EventArgs e)
    {
        SaveEntry();
        LoadEntry();
        if (TC_trdata.SelectedIndex == TC_trdata.TabCount - 1) // last
            TC_trdata.SelectedIndex = 0;
    }

    private void SaveEntry()
    {
        if (entry < 0)
            return;
        var tr = Trainers[entry];
        PrepareTrainer(tr.Self);
    }

    private bool loading;

    private void LoadEntry()
    {
        entry = CB_TrainerID.SelectedIndex;
        var tr = Trainers[entry];

        loading = true;
        SuspendTrainerLayout();
        try
        {
            PopulateFieldsTrainer(tr.Self);
            UpdateMoneyItemsForTeam(tr.Team);
            PopulateTeam(tr.Team);
            UpdateMoneyToolTip();
        }
        finally
        {
            ResumeTrainerLayout();
            loading = false;
        }
    }

    private void SuspendTrainerLayout()
    {
        SuspendLayout();
        TC_trdata.SuspendLayout();
        TC_trpoke.SuspendLayout();
        Tab_Trainer.SuspendLayout();
        Tab_Main.SuspendLayout();
        Tab_Stats.SuspendLayout();
        Tab_Moves.SuspendLayout();
    }

    private void ResumeTrainerLayout()
    {
        Tab_Moves.ResumeLayout(false);
        Tab_Stats.ResumeLayout(false);
        Tab_Main.ResumeLayout(false);
        Tab_Trainer.ResumeLayout(false);
        Tab_Trainer.PerformLayout();
        TC_trpoke.ResumeLayout(false);
        TC_trdata.ResumeLayout(false);
        ResumeLayout(false);
    }

    private void UpdateTrainerName(object sender, EventArgs e)
    {
        if (loading)
            return;
        string str = TB_TrainerName.Text;
        CB_TrainerID.Items[entry] = GetEntryTitle(str, entry);
    }

    private void PopulateFields(TrainerPoke pk)
    {
        EnsurePokemonEditorListsLoaded();
        EnsureMoveListLoaded();
        EnsureStatsInitialized();

        pkm = pk.Clone();

        Stats.UpdatingFields = loadingPKM = true;

        CB_Species.SelectedIndex = pkm.Species;
        CB_Form.SelectedIndex = pkm.Form;
        CB_Ability.SelectedIndex = pkm.Ability;
        CB_Nature.SelectedIndex = pkm.Nature;
        NUD_Level.Value = Math.Min(NUD_Level.Maximum, pkm.Level);
        CB_Item.SelectedIndex = pkm.HeldItem;
        CHK_Shiny.Checked = pkm.Shiny;
        CB_Gender.SelectedIndex = pkm.Gender;

        CB_Move1.SelectedIndex = pkm.Move1;
        CB_Move2.SelectedIndex = pkm.Move2;
        CB_Move3.SelectedIndex = pkm.Move3;
        CB_Move4.SelectedIndex = pkm.Move4;

        if (pkm is TrainerPoke7b b)
        {
            CHK_CanMega.Checked = b.CanMegaEvolve;
            NUD_MegaForm.Value = b.MegaFormChoice;
            NUD_Friendship.Value = b.Friendship;
            FLP_Friendship.Visible = FLP_Mega.Visible = true;
            FLP_HeldItem.Visible = FLP_Ability.Visible = FLP_CanDynamax.Visible = false;
        }
        else if (pkm is TrainerPoke8 c)
        {
            CHK_CanDynamax.Checked = c.CanDynamax;
            Stats.CB_DynamaxLevel.SelectedIndex = c.DynamaxLevel;
            Stats.CHK_Gigantamax.Checked = c.CanGigantamax;
            FLP_Friendship.Visible = FLP_Mega.Visible = false;
            FLP_HeldItem.Visible = FLP_Ability.Visible = FLP_CanDynamax.Visible = true;
        }

        Stats.LoadStats(pkm);
        loadingPKM = false;
    }

    private TrainerPoke PreparePKM()
    {
        EnsurePokemonEditorListsLoaded();
        EnsureMoveListLoaded();
        EnsureStatsInitialized();

        var pk = pkm.Clone();
        pk.Species = CB_Species.SelectedIndex;
        pk.Form = CB_Form.SelectedIndex;
        pk.Level = (int)NUD_Level.Value;
        pk.Ability = CB_Ability.SelectedIndex;
        pk.HeldItem = CB_Item.SelectedIndex;
        pk.Shiny = CHK_Shiny.Checked;
        pk.Nature = CB_Nature.SelectedIndex;
        pk.Gender = CB_Gender.SelectedIndex;

        pk.Move1 = CB_Move1.SelectedIndex;
        pk.Move2 = CB_Move2.SelectedIndex;
        pk.Move3 = CB_Move3.SelectedIndex;
        pk.Move4 = CB_Move4.SelectedIndex;

        switch (pk)
        {
            case TrainerPoke7b b:
                b.CanMegaEvolve = CHK_CanMega.Checked;
                b.MegaFormChoice = (int)NUD_MegaForm.Value;
                b.Friendship = (int)NUD_Friendship.Value;
                break;
            case TrainerPoke8 c:
                c.CanDynamax = CHK_CanDynamax.Checked;
                c.DynamaxLevel = (byte)Stats.CB_DynamaxLevel.SelectedIndex;
                c.CanGigantamax = Stats.CHK_Gigantamax.Checked;
                break;
        }

        return pk;
    }

    private void PopulateFieldsTrainer(TrainerData tr)
    {
        // some trainers have trclasses without corresponding trnames in the text, so add them
        if (Game.Info.SWSH)
        {
            var classes = CB_Trainer_Class.Items;
            for (int i = classes.Count; i <= 253; i++)
                classes.Add($"{trClass[1]} - {i} *");
        }

        // Load Trainer Data
        CB_Trainer_Class.SelectedIndex = tr.Class;
        LoadTrainerClassBall();
        SetTrainerItemComboValue(CB_Item_1, tr.Item1);
        SetTrainerItemComboValue(CB_Item_2, tr.Item2);
        SetTrainerItemComboValue(CB_Item_3, tr.Item3);
        SetTrainerItemComboValue(CB_Item_4, tr.Item4);
        CB_Money.SelectedIndex = tr.Money;
        CB_Mode.SelectedIndex = (int)tr.Mode;
        LoadAIBits(tr.AI);
        if (tr is TrainerData7b b)
        {
            SetTrainerItemComboValue(CB_Gift, b.Gift);
            NUD_GiftCount.Value = b.GiftQuantity;
        }
    }

    private void LoadTrainerClassBall()
    {
        if (!Game.Info.SWSH)
            return;

        var classIndex = CB_Trainer_Class.SelectedIndex;
        if (CB_ClassBall.Items.Count == 0 || !CanEditTrainerClassBall(classIndex))
        {
            SetTrainerClassBallVisible(false);
            if (CB_ClassBall.Items.Count != 0)
                CB_ClassBall.SelectedIndex = 0;
            UpdateClassBallToolTip();
            return;
        }

        var trainerClass = Trainers.GetClass(classIndex);
        var ball = Math.Clamp(trainerClass.BallID, 0, CB_ClassBall.Items.Count - 1);
        SetTrainerClassBallVisible(true);
        CB_ClassBall.SelectedIndex = ball;
        UpdateClassBallToolTip();
    }

    private void SaveTrainerClassBall(int classIndex)
    {
        if (!CB_ClassBall.Visible || !CanEditTrainerClassBall(classIndex))
            return;

        Trainers.GetClass(classIndex).BallID = CB_ClassBall.SelectedIndex;
    }

    private void PrepareTrainer(TrainerData tr)
    {
        tr.Class = CB_Trainer_Class.SelectedIndex;
        SaveTrainerClassBall(tr.Class);
        tr.Item1 = GetTrainerItemComboValue(CB_Item_1);
        tr.Item2 = GetTrainerItemComboValue(CB_Item_2);
        tr.Item3 = GetTrainerItemComboValue(CB_Item_3);
        tr.Item4 = GetTrainerItemComboValue(CB_Item_4);
        tr.Money = CB_Money.SelectedIndex;
        tr.Mode = (BattleMode)CB_Mode.SelectedIndex;
        tr.AI = SaveAIBits(tr.AI);
        if (tr is TrainerData7b b)
        {
            b.Gift = GetTrainerItemComboValue(CB_Gift);
            b.GiftQuantity = (int)NUD_GiftCount.Value;
        }
    }

    private void LoadAIBits(uint val)
    {
        for (int i = 0; i < AIBits.Length; i++)
            AIBits[i].Checked = ((val >> i) & 1) == 1;
    }

    private uint SaveAIBits(uint oldval)
    {
        uint val = oldval;
        for (int i = 0; i < AIBits.Length; i++)
        {
            if (AIBits[i].Checked)
                val |= 1u << i;
            else
                val &= ~(1u << i);
        }
        return val;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!CloseConfirmed && e.CloseReason == CloseReason.UserClosing && !ConfirmCloseWithoutSaving())
        {
            e.Cancel = true;
            return;
        }

        SaveEntry();
        base.OnFormClosing(e);
    }

    private void DumpTxt(object sender, EventArgs e)
    {
        if (!ConfirmDump())
            return;

        EnsurePokemonEditorListsLoaded();
        EnsureMoveListLoaded();

        using var sfd = new SaveFileDialog { FileName = "Trainers.txt" };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        var sb = new StringBuilder();
        for (int i = 0; i < Trainers.Length; i++)
        {
            var tr = Trainers[i];
            tr.Name = trName[i];
            sb.Append(GetTrainerString(tr));
        }
        File.WriteAllText(sfd.FileName, sb.ToString());
    }

    private string GetTrainerString(VsTrainer Trainer)
    {
        var file = Trainer.ID;
        var tr = Trainer.Self;
        var name = Trainer.Name;
        var team = Trainer.Team;
        var sb = new StringBuilder();
        if (tr.Class > trClass.Length) // Klara and Avery out of bounds trclass edge case
            tr.Class = 1;

        sb.AppendLine("======");
        sb.Append(file).Append(" - ").Append(trClass[tr.Class]).Append(' ').AppendLine(name);
        sb.AppendLine("======");
        sb.Append("Pokémon: ").Append(tr.NumPokemon).AppendLine();
        sb.Append("Money Rate: ").Append(tr.Money).Append(" (Estimated Payout: ").Append(GetTrainerPayout(team, tr.Money)).Append(')').AppendLine();
        for (int i = 0; i < tr.NumPokemon; i++)
        {
            var pk = team[i];
            if (pk.Shiny)
                sb.Append("Shiny ");
            sb.Append(specieslist[pk.Species]);
            if (pk.Form > 0)
                sb.Append('-').Append(pk.Form);
            sb.Append(" (Lv. ").Append(pk.Level).Append(") ");
            if (pk.HeldItem > 0)
                sb.Append("@ ").Append(itemlist[pk.HeldItem]);

            if (pk.Nature != 0)
                sb.Append(" (Nature: ").Append(natures[pk.Nature]).Append(')');

            sb.Append(" (Moves: ").AppendJoin("/", pk.Moves.Select(m => m == 0 ? "(None)" : movelist[m])).Append(')');

            var ivs = pk.IVs;
            sb.Append(" IVs: ").AppendJoin("/", ivs);
            var evs = pk.EVs;
            if (evs.Any(z => z != 0))
                sb.Append(" EVs: ").AppendJoin("/", pk.EVs);

            if (pk is IAwakened a)
            {
                var avs = a.AVs();
                if (avs.Any(z => z != 0))
                    sb.Append(" AVs: ").AppendJoin("/", avs);
            }
            sb.AppendLine();
        }
        return sb.ToString();

        static int GetTrainerPayout(IList<TrainerPoke> team, int rate)
        {
            if (rate == 0 || team.Count == 0)
                return 0;
            return team.Max(z => z.Level) * rate * 4;
        }
    }

    private void UpdateStats(object sender, EventArgs e)
    {
        if (Stats.UpdatingFields)
            return;
        EnsureStatsInitialized();
        if (sender == CB_Nature)
            pkm.Nature = WinFormsUtil.GetIndex(CB_Nature);
        else if (sender == NUD_Level)
            pkm.Level = (int)NUD_Level.Value;
        else if (sender == NUD_Friendship)
            pkm.Friendship = (int)NUD_Friendship.Value;

        Stats.UpdateStats();
    }

    private void B_HighAttack_Click(object sender, EventArgs e)
    {
        EnsurePokemonEditorListsLoaded();
        EnsureMoveListLoaded();
        pkm.Species = CB_Species.SelectedIndex;
        pkm.Level = (int)NUD_Level.Value;
        pkm.Form = CB_Form.SelectedIndex;
        var learnset = GetLearnsetRandomizer();
        var movedata = Data.MoveData.LoadAll();
        var moves = learnset.GetHighPoweredMoves(movedata, (ushort)pkm.Species, (byte)pkm.Form, 4);
        SetMoves(moves);
    }

    private void B_CurrentAttack_Click(object sender, EventArgs e)
    {
        EnsurePokemonEditorListsLoaded();
        EnsureMoveListLoaded();
        pkm.Species = CB_Species.SelectedIndex;
        pkm.Level = (int)NUD_Level.Value;
        pkm.Form = CB_Form.SelectedIndex;
        var moves = GetLearnsetRandomizer().GetCurrentMoves((ushort)pkm.Species, (byte)pkm.Form, pkm.Level, 4);
        SetMoves(moves);
    }

    private void B_Clear_Click(object sender, EventArgs e)
    {
        EnsureMoveListLoaded();
        SetMoves(new int[4]);
    }

    private void SetMoves(IList<int> moves)
    {
        EnsureMoveListLoaded();
        var mcb = new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 };
        for (int i = 0; i < mcb.Length; i++)
            mcb[i].SelectedIndex = moves[i];
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (!ConfirmSave())
            return;

        SaveEntry();
        Modified = true;
        CloseConfirmed = true;
        Close();
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Randomize Trainers", "Randomize trainer data using the current randomizer settings?"))
            return;

        SaveEntry();
        var trand = GetRandomizer();
        trand.Execute();
        LoadEntry();
        System.Media.SystemSounds.Asterisk.Play();
    }

    private TrainerRandomizer GetRandomizer()
    {
        EnsureRandomizerSettingsLoaded();

        var moves = Data.MoveData.LoadAll();
        var rmove = new MoveRandomizer(Game.Info, moves, Personal);
        int[] banned = Legal.GetBannedMoves(Game.Info.Game, moves.Length);
        rmove.Initialize((MovesetRandSettings)PG_Moves.SelectedObject!, banned);
        int[] ban = [];

        if (Game.Info.SWSH)
        {
            var pt = Data.PersonalData;
            ban = pt.Table.Take(Game.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalMisc_SWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();
        }

        var rspec = new SpeciesRandomizer(Game.Info, Personal);
        var rform = new FormRandomizer(Personal);
        rspec.Initialize((SpeciesSettings)PG_Species.SelectedObject!, ban);
        var learnset = GetLearnsetRandomizer();
        learnset.Moves = moves;
        var evos = Data.EvolutionData;
        var trand = new TrainerRandomizer(Game.Info, Personal, Trainers.LoadAll(), evos.LoadAll())
        {
            ClassCount = CB_Trainer_Class.Items.Count,
            Learn = learnset,
            RandMove = rmove,
            RandSpec = rspec,
            RandForm = rform,
            GetBlank = () => Game.Info.SWSH ? new TrainerPoke8() : new TrainerPoke7b(), // this should probably be less specific
        };
        trand.Initialize((TrainerRandSettings)PG_RTrainer.SelectedObject!, (SpeciesSettings)PG_Species.SelectedObject!);
        return trand;
    }

    private LearnsetRandomizer GetLearnsetRandomizer() =>
        learn ??= new LearnsetRandomizer(Game.Info, Data.LevelUpData.LoadAll(), Personal);

    private void B_Boost_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Boost Trainer Levels", "Apply the configured level boost to every trainer Pokemon?"))
            return;

        SaveEntry();
        var trand = GetRandomizer();
        var settings = (TrainerRandSettings)PG_RTrainer.SelectedObject!;
        trand.ModifyAllPokemon(pk => TrainerRandomizer.BoostLevel(pk, settings.LevelBoostRatio));
        LoadEntry();
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_MaxAI_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Max Trainer AI", "Enable the strongest known AI flags for every trainer?"))
            return;

        SaveEntry();
        var trand = GetRandomizer();
        trand.ModifyAllTrainers(TrainerRandomizer.MaximizeAIFlags);
        LoadEntry();
        System.Media.SystemSounds.Asterisk.Play();
    }

    private bool ConfirmSave()
        => ThemedConfirmationDialog.Show(
            this,
            "Save Trainers",
            "Save the current trainer editor changes?\n\nThis applies trainer parties, trainer data, and randomizer changes to the loaded project. Closing without saving will discard this editor session.",
            "Save");

    private bool ConfirmDump()
        => ThemedConfirmationDialog.Show(
            this,
            "Dump Trainers",
            "Export trainer data to a text file?\n\nThis does not modify the loaded project.",
            "Dump");

    private bool ConfirmBulkAction(string title, string action)
        => ThemedConfirmationDialog.Show(
            this,
            title,
            action + "\n\nThis can modify many trainer entries. Review the result and press Save to write it to the loaded project.",
            "Continue");

    private bool ConfirmCloseWithoutSaving()
        => ThemedConfirmationDialog.Show(
            this,
            "Close Trainer Editor",
            "Close the trainer editor without saving?\n\nAny edits made in this editor session will be discarded and the loaded project data will not be updated.",
            "Close");

    private readonly record struct TeamSpriteKey(ushort Species, byte Form, byte Gender, int HeldItem, bool Shiny);
}

public static class FormUtil
{
    internal static void SetForms(int species, ComboBox cb, string[][] AltForms)
    {
        cb.Items.Clear();
        string[] forms = AltForms[species];
        if (forms.Length < 2)
        {
            cb.Items.Add("");
            cb.Enabled = false;
        }
        else
        {
            cb.Items.AddRange(forms);
            cb.Enabled = true;
        }
        cb.SelectedIndex = 0;
    }
}
