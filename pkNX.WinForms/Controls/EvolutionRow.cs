using System;
using System.Linq;
using System.Windows.Forms;
using pkNX.Structures;
using PKHeX.Drawing.PokeSprite;

namespace pkNX.WinForms;

public partial class EvolutionRow : UserControl
{
    public EvolutionRow()
    {
        InitializeComponent();

        CB_Method.Items.AddRange(EvoMethods);
        CB_Species.Items.AddRange(species);

        CB_Species.SelectedIndexChanged += (_, __) => ChangeSpecies(CB_Species.SelectedIndex, (int)NUD_Form.Value);
        NUD_Form.ValueChanged += (_, __) => ChangeSpecies(CB_Species.SelectedIndex, (int)NUD_Form.Value);

        CB_Arg.Items.AddRange(None);
        CB_Method.SelectedIndexChanged += (s, e) =>
        {
            if (Loading || CB_Method.SelectedIndex < 0)
                return;

            ConfigureArgument((EvolutionType)CB_Method.SelectedIndex, 0);
        };
    }

    private void ChangeSpecies(int spec, int form) => PB_Preview.Image = SpriteUtil.GetSprite((ushort)spec, (byte)form, 0, 0, 0, false, PKHeX.Core.Shiny.Never);

    private EvolutionMethod? current;
    private bool Loading;

    public void LoadEvolution(EvolutionMethod s)
    {
        var evo = current = s;
        Loading = true;
        SetSelectedIndex(CB_Species, evo.Species);
        NUD_Form.Value = Clamp(evo.Form, NUD_Form.Minimum, NUD_Form.Maximum);
        NUD_Level.Value = Clamp(evo.Level, NUD_Level.Minimum, NUD_Level.Maximum);
        SetSelectedIndex(CB_Method, (int)evo.Method);
        ConfigureArgument(evo.Method, evo.Argument);
        Loading = false;
        ChangeSpecies(CB_Species.SelectedIndex, (int)NUD_Form.Value);
    }

    public void SaveEvolution()
    {
        var evo = current;
        if (evo == null)
            return;
        var method = CB_Method.SelectedIndex < 0 ? EvolutionType.None : (EvolutionType)CB_Method.SelectedIndex;
        var argumentType = method.GetArgType();

        evo.Species = (ushort)Math.Max(0, CB_Species.SelectedIndex);
        evo.Form = (byte)NUD_Form.Value;
        evo.Level = (byte)NUD_Level.Value;
        evo.Method = method;
        evo.Argument = argumentType >= EvolutionTypeArgumentType.Items && CB_Arg.SelectedIndex >= 0
            ? (ushort)CB_Arg.SelectedIndex
            : (ushort)0;
    }

    private void ConfigureArgument(EvolutionType method, int argument)
    {
        var argumentType = method.GetArgType();
        var hasEvolution = (int)method > 0;
        var hasVisibleArgument = argumentType >= EvolutionTypeArgumentType.Items;

        L_Method.Visible = L_Species.Visible = L_Form.Visible = L_Level.Visible = hasEvolution;
        L_Arg.Visible = CB_Arg.Visible = hasEvolution && hasVisibleArgument;

        var values = hasVisibleArgument ? GetArgs(argumentType) : None;
        CB_Arg.BeginUpdate();
        CB_Arg.Items.Clear();
        CB_Arg.Items.AddRange(values);
        CB_Arg.EndUpdate();

        SetSelectedIndex(CB_Arg, hasVisibleArgument ? argument : 0);
    }

    private static void SetSelectedIndex(ComboBox comboBox, int index)
    {
        if (comboBox.Items.Count == 0)
            return;

        comboBox.SelectedIndex = Math.Max(0, Math.Min(index, comboBox.Items.Count - 1));
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
    {
        if (value < minimum)
            return minimum;
        if (value > maximum)
            return maximum;
        return value;
    }

    public static string[] items = [];
    public static string[] movelist = [];
    public static string[] species = [];
    public static string[] types = [];

    private static readonly string[] EvoMethods = Enum.GetNames<EvolutionType>();
    private static readonly string[] Levels = Enumerable.Range(0, 100 + 1).Select(z => z.ToString()).ToArray();
    private static readonly string[] Stats = Enumerable.Range(0, 255 + 1).Select(z => z.ToString()).ToArray();
    private static readonly string[] None = [""];

    private static string[] GetArgs(EvolutionTypeArgumentType type)
    {
        return type switch
        {
            EvolutionTypeArgumentType.NoArg => None,
            EvolutionTypeArgumentType.Level => Levels,
            EvolutionTypeArgumentType.Items => items,
            EvolutionTypeArgumentType.Moves => movelist,
            EvolutionTypeArgumentType.Species => species,
            EvolutionTypeArgumentType.Stat => Stats,
            EvolutionTypeArgumentType.Type => types,
            EvolutionTypeArgumentType.Version => Stats,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
