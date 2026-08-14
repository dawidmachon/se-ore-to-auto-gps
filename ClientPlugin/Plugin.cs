using System.Reflection;
using ClientPlugin.Patches;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Layouts;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using VRage.Plugins;
using System.Runtime.CompilerServices;

#if !DEV_BUILD
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif

namespace ClientPlugin;

// ReSharper disable once UnusedType.Global
public class Plugin : IPlugin
{
    public const string Name = "OreToAutoGps";
    public static Plugin Instance { get; private set; }
    private SettingsGenerator settingsGenerator;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Init(object gameInstance)
    {
        Instance = this;
        Instance.settingsGenerator = new SettingsGenerator();

        var harmony = new Harmony(Name);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // Legit input: what the player's ore detector already detected. MyOreDepositGroup and
        // its OnDepositQueryComplete callback are internal, so resolve by name and patch manually.
        var depositGroupType = AccessTools.TypeByName("Sandbox.Game.Entities.Cube.MyOreDepositGroup");
        var onComplete = depositGroupType != null ? AccessTools.Method(depositGroupType, "OnDepositQueryComplete") : null;
        var postfix = new HarmonyMethod(typeof(OreDepositPatches), nameof(OreDepositPatches.OnDepositQueryCompletePostfix));
        if (onComplete != null)
            harmony.Patch(onComplete, postfix: postfix);

        AutoGpsService.Log("Initialized");
    }

    public void Dispose()
    {
        // IMPORTANT: Do NOT call harmony.UnpatchAll() here! It may break other plugins.
        AutoGpsService.Reset();
        Instance = null;
    }

    public void Update()
    {
        // Called every simulation frame on the main thread.
        AutoGpsService.HandleUpdate();
    }

    // ReSharper disable once UnusedMember.Global
    public void OpenConfigDialog()
    {
        Instance.settingsGenerator.SetLayout<Simple>();
        MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
    }
}
