using System.Reflection;
using System.Text;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using VRageMath;

namespace ClientPlugin.Patches;

// Guards a vanilla crash in MyGuiControlMultilineEditableText.GetCarriageOffset(int idx): it
// calls m_text.AppendSubstring(m_text, num, idx - num) and indexes m_text out of bounds when
// the caret index (idx) exceeds the current text length. That happens when the GPS shown in the
// panel is deleted or replaced - its description gets shorter/empty while the caret position is
// left pointing past the new end. This prefix skips the original and returns Zero in that case.
// Normal editing (idx within [0, m_text.Length]) is unaffected.
[HarmonyPatch(typeof(MyGuiControlMultilineEditableText), "GetCarriageOffset")]
internal static class MultilineTextCrashFix
{
    // Cached once: this runs on every caret render of any multiline text control, so the
    // reflection lookup must not allocate (Traverse does). Null on renamed fields - the
    // catch below then falls through to the original method, same as before.
    private static readonly FieldInfo TextField = AccessTools.Field(typeof(MyGuiControlMultilineEditableText), "m_text");

    [HarmonyPrefix]
    private static bool Prefix(object __instance, int idx, ref Vector2 __result)
    {
        try
        {
            var text = (StringBuilder)TextField?.GetValue(__instance);
            if (text != null && (idx < 0 || idx > text.Length))
            {
                __result = Vector2.Zero;
                return false;
            }
        }
        catch { }
        return true;
    }
}
