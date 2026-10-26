using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace BagOHeim
{
    /// <summary>
    /// Applies each patch class on its own. With <c>PatchAll</c>, one patch that can't be applied (the game changed a method it
    /// targets) stops every patch after it; this logs the one that failed and carries on with the rest.
    /// </summary>
    internal static class SafePatch
    {
        public static void All(Harmony harmony, Assembly assembly, ManualLogSource log)
        {
            foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try
                {
                    harmony.PatchAll(type);
                }
                catch (Exception e)
                {
                    log.LogError($"Couldn't apply the patch {type.Name}; the rest are fine. {e.Message}");
                }
            }
        }
    }
}
