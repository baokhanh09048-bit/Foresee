using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace foresee.Scripts;

[ModInitializer("Init")]
public static class Entry
{
    public static void Init()
    {
        ForeseeSettings.ResetForSession();

        var harmony = new Harmony("sts2.llxs.foresee");
        harmony.PatchAll();

        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
        Log.Info("Foresee initialized v0.1.3");
    }
}
