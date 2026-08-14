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
    [HarmonyPrefix]
    private static bool Prefix(object __instance, int idx, ref Vector2 __result)
    {
        try
        {
            var text = Traverse.Create(__instance).Field("m_text").GetValue<StringBuilder>();
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
