using System;
using System.Windows.Forms;
using pkNX.Game;

namespace pkNX.WinForms;

public sealed partial class ShinyRate : Form
{
    private const int BaseShinyOdds = 4096;
    private const int MaxFixedRerolls = 4091;

    private readonly ShinyRateInfo Data;
    private readonly bool Loaded;
    private int SuggestedRerolls = 1;

    public ShinyRate(ShinyRateInfo info)
    {
        InitializeComponent();
        Data = info;

        ConfigureUi();
        LoadInitialState();

        Loaded = true;
        UpdateSelectionState();
        UpdateRerollLabels();
        UpdateTargetHelper();
    }

    public bool Modified { get; set; }

    private void ConfigureUi()
    {
        WinFormsTheme.Apply(this);

        NUD_Rerolls.Minimum = 1;
        NUD_Rerolls.Maximum = MaxFixedRerolls;

        NUD_Rate.Minimum = 0.01m;
        NUD_Rate.Maximum = decimal.Round((decimal)GetChance(MaxFixedRerolls) * 100, 2);

        RB_Always.Enabled = Data.AllowAlways;

        Tips.SetToolTip(RB_Default, "Restores the vanilla shiny reroll logic.");
        Tips.SetToolTip(RB_Fixed, "Patches the game to use a fixed PID roll count from 1 to 4091.");
        Tips.SetToolTip(RB_Always, "SWSH only. Patches the reroll loop so the routine resolves as shiny.");
        Tips.SetToolTip(NUD_Rerolls, "The number of PID rolls to try. Each roll is roughly 1:4096.");
        Tips.SetToolTip(NUD_Rate, "Choose a target overall chance and use it to calculate the closest fixed roll count.");
    }

    private void LoadInitialState()
    {
        if (Data.IsFixed)
        {
            RB_Fixed.Checked = true;
            SetFixedRerollCount(Data.GetFixedRate(), updateData: false);
        }
        else if (Data.IsAlways)
        {
            RB_Always.Checked = true;
        }
        else
        {
            RB_Default.Checked = true;
        }
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        var message =
            "Save shiny rate changes?\n\n" +
            "This will patch the ExeFS main executable for the selected game. Default restores the original shiny reroll logic, Fixed writes the selected PID roll count, and Always Shiny writes the always-shiny patch when supported.";
        if (!ThemedConfirmationDialog.Show(this, "Save Shiny Rate", message, "Save"))
            return;

        Modified = true;
        Close();
    }

    private void B_Cancel_Click(object sender, EventArgs e) => Close();

    private void ChangeSelection(object sender, EventArgs e)
    {
        if (sender is RadioButton { Checked: false })
            return;

        UpdateSelectionState();
        if (!Loaded)
            return;

        if (RB_Default.Checked)
            Data.SetDefault();
        else if (RB_Always.Checked)
            Data.SetAlwaysShiny();
        else
            Data.SetFixedRate((int)NUD_Rerolls.Value);
    }

    private void ChangeRerollCount(object sender, EventArgs e)
    {
        if (Loaded && RB_Fixed.Checked)
            Data.SetFixedRate((int)NUD_Rerolls.Value);

        UpdateRerollLabels();
    }

    private void ChangePercent(object sender, EventArgs e)
    {
        UpdateTargetHelper();
    }

    private void B_ApplyTarget_Click(object sender, EventArgs e)
    {
        RB_Fixed.Checked = true;
        SetFixedRerollCount(SuggestedRerolls, updateData: Loaded);
    }

    private void Preset_Click(object sender, EventArgs e)
    {
        if (sender is not Button { Tag: int odds })
            return;

        RB_Fixed.Checked = true;
        var target = 1d / odds;
        SetTargetPercent(target);
        SetFixedRerollCount(GetRerollsForChance(target), updateData: Loaded);
    }

    private void UpdateSelectionState()
    {
        var fixedMode = RB_Fixed.Checked;
        GB_Rerolls.Enabled = fixedMode;
        GB_RerollHelper.Enabled = fixedMode;
        GB_Presets.Enabled = fixedMode;

        L_ModeDescription.Text = RB_Default.Checked
            ? "Uses the game's original shiny logic."
            : RB_Always.Checked
                ? "Writes the SWSH always-shiny branch patch."
                : "Writes a fixed PID roll count.";
    }

    private void SetFixedRerollCount(int count, bool updateData)
    {
        count = Math.Clamp(count, 1, MaxFixedRerolls);
        NUD_Rerolls.Value = count;
        UpdateRerollLabels();

        if (updateData && RB_Fixed.Checked)
            Data.SetFixedRate(count);
    }

    private void UpdateRerollLabels()
    {
        var count = (int)NUD_Rerolls.Value;
        var chance = GetChance(count);
        L_Overall.Text = $"{chance:P2}";
        L_CurrentOdds.Text = $"Approx. odds: {FormatOdds(chance)}";
    }

    private void UpdateTargetHelper()
    {
        var target = (double)NUD_Rate.Value / 100;
        SuggestedRerolls = GetRerollsForChance(target);
        L_RerollCount.Text = $"Use {SuggestedRerolls} rolls for {FormatOdds(GetChance(SuggestedRerolls))}";
    }

    private void SetTargetPercent(double target)
    {
        var value = (decimal)(target * 100);
        if (value < NUD_Rate.Minimum)
            value = NUD_Rate.Minimum;
        if (value > NUD_Rate.Maximum)
            value = NUD_Rate.Maximum;

        NUD_Rate.Value = decimal.Round(value, 2);
        UpdateTargetHelper();
    }

    private static double GetChance(int count)
    {
        count = Math.Clamp(count, 1, MaxFixedRerolls);
        return 1 - Math.Pow((BaseShinyOdds - 1d) / BaseShinyOdds, count);
    }

    private static int GetRerollsForChance(double chance)
    {
        chance = Math.Clamp(chance, 0.0001d, GetChance(MaxFixedRerolls));
        var rolls = (int)Math.Ceiling(Math.Log(1 - chance) / Math.Log((BaseShinyOdds - 1d) / BaseShinyOdds));
        return Math.Clamp(rolls, 1, MaxFixedRerolls);
    }

    private static string FormatOdds(double chance)
    {
        if (chance <= 0)
            return "1:infinite";

        var odds = Math.Max(1, (int)Math.Round(1 / chance));
        return $"1:{odds:N0}";
    }
}
