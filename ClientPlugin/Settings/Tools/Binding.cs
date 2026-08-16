using VRage.Input;
using ModApiModifiers = VRage.ModAPI.MyKeyboardModifiers;

namespace ClientPlugin.Settings.Tools;

public struct Binding
{
    public MyKeys Key;
    public bool Ctrl;
    public bool Alt;
    public bool Shift;

    public Binding(MyKeys key, bool ctrl = false, bool alt = false, bool shift = false)
    {
        Key = key;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }

    public override string ToString()
    {
        if (Key == MyKeys.None)
            return "None";

        var ctrl = Ctrl ? "Ctrl+" : "";
        var alt = Alt ? "Alt+" : "";
        var shift = Shift ? "Shift+" : "";
        return $"{ctrl}{alt}{shift}{Key}";
    }

    public bool IsPressed(IMyInput input) => Key != MyKeys.None && (IsModifierKey(Key) || AreModifiersMatch(input)) && input.IsKeyPress(Key);
    public bool HasPressed(IMyInput input) => Key != MyKeys.None && (IsModifierKey(Key) || AreModifiersMatch(input)) && input.IsNewKeyPressed(Key);

    private bool AreModifiersMatch(IMyInput input)
    {
        return input.IsAnyCtrlKeyPressed() == Ctrl &&
               input.IsAnyAltKeyPressed() == Alt &&
               input.IsAnyShiftKeyPressed() == Shift;
    }

    // A binding whose key IS a modifier (just "Alt", "LeftShift", ...) must not additionally
    // require modifier-state matching - same rule the game's MyControl.IsKeyPressed applies.
    private static bool IsModifierKey(MyKeys key)
    {
        return key == MyKeys.Control || key == MyKeys.LeftControl || key == MyKeys.RightControl
            || key == MyKeys.Shift || key == MyKeys.LeftShift || key == MyKeys.RightShift
            || key == MyKeys.Alt || key == MyKeys.LeftAlt || key == MyKeys.RightAlt;
    }

    // Conversion to/from the game's modifier flags. The assign-key dialog captures combos
    // (e.g. Alt+L) into MyControl; these move that state in and out of the saved config.
    public ModApiModifiers GetKeyboardModifiers()
    {
        var m = ModApiModifiers.None;
        if (Ctrl) m |= ModApiModifiers.Control;
        if (Alt) m |= ModApiModifiers.Alt;
        if (Shift) m |= ModApiModifiers.Shift;
        return m;
    }

    public void SetKeyboardModifiers(ModApiModifiers modifiers)
    {
        Ctrl = (modifiers & (ModApiModifiers.Control | ModApiModifiers.LeftControl | ModApiModifiers.RightControl)) != ModApiModifiers.None;
        Alt = (modifiers & (ModApiModifiers.Alt | ModApiModifiers.LeftAlt | ModApiModifiers.RightAlt)) != ModApiModifiers.None;
        Shift = (modifiers & (ModApiModifiers.Shift | ModApiModifiers.LeftShift | ModApiModifiers.RightShift)) != ModApiModifiers.None;
    }
}