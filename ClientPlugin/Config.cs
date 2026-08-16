using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Elements;
using ClientPlugin.Settings.Tools;
using Sandbox.Graphics.GUI;
using VRageMath;

namespace ClientPlugin;

public class Config : INotifyPropertyChanged
{
    #region Options

    // Unassigned by default: every letter is bound in vanilla, so the player picks a free key.
    // Nothing is ever marked while this is unbound (player-interaction gate).
    private Binding markKey = new Binding();

    private int dedupRadiusMeters = 100;
    private bool showQuantity = true;
    private bool showOnHud = true;
    private bool includeCoordsInName = false;

    private int minorThreshold = 1000;
    private int fieldRadius = 500;

    private bool iron = true;
    private bool nickel = true;
    private bool cobalt = true;
    private bool magnesium = true;
    private bool silicon = true;
    private bool silver = true;
    private bool gold = true;
    private bool platinum = true;
    private bool uranium = true;
    private bool ice = true;
    private bool stone = false;

    private bool trackModdedOres = true;

    #endregion

    #region User interface

    public readonly string Title = "Ore to Auto Gps";

    [Separator("Player interaction")]
    [Keybind(label: "Mark detected ore", description: "Press this key in-game to create GPS markers for everything your ore detector has detected so far. Nothing is ever marked without this keypress.")]
    public Binding MarkKey
    {
        get => markKey;
        set => SetField(ref markKey, value);
    }

    [Separator("Markers")]
    [Slider(20f, 1000f, 10f, SliderAttribute.SliderType.Integer, description: "Link distance (m) that joins ore cells into one deposit and recognises the same deposit across scans.")]
    public int DedupRadiusMeters
    {
        get => dedupRadiusMeters;
        set => SetField(ref dedupRadiusMeters, value);
    }

    [Checkbox(description: "Show approximate deposit size in the GPS name/description")]
    public bool ShowQuantity
    {
        get => showQuantity;
        set => SetField(ref showQuantity, value);
    }

    [Checkbox(description: "Show created GPS on the HUD")]
    public bool ShowOnHud
    {
        get => showOnHud;
        set => SetField(ref showOnHud, value);
    }

    [Checkbox(description: "Include coordinates in the GPS name")]
    public bool IncludeCoordsInName
    {
        get => includeCoordsInName;
        set => SetField(ref includeCoordsInName, value);
    }

    [Separator("Small deposits (clutter control)")]
    [Slider(0f, 10000f, 100f, SliderAttribute.SliderType.Integer, description: "Deposits smaller than this (m^3) are grouped into shared markers instead of their own GPS. 0 = off (mark every deposit individually).")]
    public int MinorThreshold
    {
        get => minorThreshold;
        set => SetField(ref minorThreshold, value);
    }

    [Slider(100f, 2000f, 50f, SliderAttribute.SliderType.Integer, description: "Small deposits of the same ore within this distance (m) share one marker. No limit on how many merge as long as they stay this close.")]
    public int FieldRadius
    {
        get => fieldRadius;
        set => SetField(ref fieldRadius, value);
    }

    [Separator("Ores to mark")]
    [Checkbox(description: "Iron")]      public bool Iron      { get => iron; set => SetField(ref iron, value); }
    [Checkbox(description: "Nickel")]    public bool Nickel    { get => nickel; set => SetField(ref nickel, value); }
    [Checkbox(description: "Cobalt")]    public bool Cobalt    { get => cobalt; set => SetField(ref cobalt, value); }
    [Checkbox(description: "Magnesium")] public bool Magnesium { get => magnesium; set => SetField(ref magnesium, value); }
    [Checkbox(description: "Silicon")]   public bool Silicon   { get => silicon; set => SetField(ref silicon, value); }
    [Checkbox(description: "Silver")]    public bool Silver    { get => silver; set => SetField(ref silver, value); }
    [Checkbox(description: "Gold")]      public bool Gold      { get => gold; set => SetField(ref gold, value); }
    [Checkbox(description: "Platinum")]  public bool Platinum  { get => platinum; set => SetField(ref platinum, value); }
    [Checkbox(description: "Uranium")]   public bool Uranium   { get => uranium; set => SetField(ref uranium, value); }
    [Checkbox(description: "Ice")]       public bool Ice       { get => ice; set => SetField(ref ice, value); }
    [Checkbox(description: "Stone")]     public bool Stone     { get => stone; set => SetField(ref stone, value); }

    [Checkbox(description: "Also mark modded / unknown ore types not listed above (custom ores from server mods)")]
    public bool TrackModdedOres { get => trackModdedOres; set => SetField(ref trackModdedOres, value); }

    [Separator("Maintenance")]
    [Button(description: "Remove every GPS created by this plugin during the current session")]
    public void ClearAll()
    {
        int removed = AutoGpsService.ClearAll();
        MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
            MyMessageBoxStyleEnum.Info,
            buttonType: MyMessageBoxButtonsType.OK,
            messageText: new StringBuilder("Removed " + removed + " GPS marker(s) created this session."),
            messageCaption: new StringBuilder("Ore to Auto Gps"),
            size: new Vector2(0.5f, 0.4f)));
    }

    #endregion

    #region Property change notification boilerplate

    public static readonly Config Default = new Config();
    public static readonly Config Current = ConfigStorage.Load();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
