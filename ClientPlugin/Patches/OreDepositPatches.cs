using System.Collections.Generic;
using Sandbox.Game.Entities.Cube;
using VRageMath;

namespace ClientPlugin.Patches;

// Manual Harmony target, registered from Plugin.Init. The patched method is
// MyOreDepositGroup.OnDepositQueryComplete (internal, resolved by name). This is the legit gate:
// it captures ONLY what the player's ore detector already detected.
internal static class OreDepositPatches
{
    // ReSharper disable once UnusedParameter.Global
    public static void OnDepositQueryCompletePostfix(List<MyEntityOreDeposit> deposits, List<Vector3I> emptyCells)
    {
        if (deposits == null || deposits.Count == 0) return;
        foreach (var deposit in deposits)
            AutoGpsService.CaptureDeposit(deposit);
    }
}
