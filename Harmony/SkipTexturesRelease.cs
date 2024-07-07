using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.AddressableAssets;

// ####################################################################
// We create several texture on the fly that can't be released
// via common asset loader. This wouldn't be much of an issue
// beside a warning that is being printed. This patch should
// correctly filter out those textures to avoid any log spam.
// ####################################################################

public static class SkipTexturesRelease
{

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(Resources), "UnloadAsset")]
    class ResourcesUnloadAssetPatch
    {
        static bool Prefix(Object assetToUnload)
        {
            if (assetToUnload == null) return true;
            if (assetToUnload.name == null) return true;
            if (assetToUnload.name.StartsWith("custom_")) return false;
            if (assetToUnload.name.StartsWith("extended_")) return false;
            return true; // Run the original function
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(TextureAtlasTerrain), "Cleanup")]
    static class TextureAtlasTerrainCleanupPatch
    {

        static void ReleaseTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (texture.name == null) return;
            if (texture.name.StartsWith("custom_")) return;
            if (texture.name.StartsWith("extended_")) return;
            Addressables.Release(texture);
        }

        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Call) continue;
                if (!(codes[i].operand is MethodInfo fn)) continue;
                if (fn.Name != "ReleaseAddressable") continue;
                ParameterInfo[] parameters = fn.GetParameters();
                if (parameters.Length != 1) continue;
                var ttype = typeof(Texture2D);
                var ptype = parameters[0].GetType();
                if (ptype.IsAssignableFrom(ttype)) continue;
                var stype = typeof(TextureAtlasTerrainCleanupPatch);
                codes[i].operand = AccessTools.Method(stype, "ReleaseTexture");
            }
            return codes;
        }
    }

    // ####################################################################
    // ####################################################################

}
