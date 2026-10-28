using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KCAccess.Patches
{
    /// <summary>
    /// Game bug with BepInEx installed: the game's mods (local and Steam Workshop) never load. ModCompiler compiles them
    /// with RoslynCSharp's ScriptDomain against the game's own Managed\0Harmony.dll by path and against "0Harmony.dll" by
    /// name, which now resolves to BepInEx's Harmony, so Roslyn refuses the second 0Harmony (and the game's loader then
    /// crashes logging that error, which has no source file). The bare name is dropped before compiling
    /// (Core.ModReferences); mods compile against the game's Harmony as they were written to.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ModCompileReferences
    {
        private static MethodBase Target()
        {
            var domain = AccessTools.TypeByName("RoslynCSharp.ScriptDomain");
            var mode = AccessTools.TypeByName("RoslynCSharp.ScriptSecurityMode");
            return domain != null && mode != null ? AccessTools.Method(domain, "CompileAndLoadFiles", new[] { typeof(string[]), mode }) : null;
        }

        private static bool Prepare() => Target() != null;

        private static MethodBase TargetMethod() => Target();

        private static void Prefix(object __instance)
        {
            try
            {
                var service = Traverse.Create(__instance).Property("RoslynCompilerService").GetValue();
                if (Traverse.Create(service).Property("References").GetValue() is IList<string> references)
                {
                    int n = Core.ModReferences.DropBareDuplicates(references);
                    if (n > 0) Plugin.Log.LogInfo("Mod compiler: dropped " + n + " duplicate reference(s) so the game's mods compile with BepInEx");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Mod compiler references: " + e.Message);
            }
        }
    }
}
