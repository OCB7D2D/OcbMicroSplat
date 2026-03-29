using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
    "IDE0051:Remove unused private members",
    Justification = "Harmony Patch Annoations")]

static class ChunkProviderBiomeColorsPatch
{

    [HarmonyPatch]
    static class GenerateWorldFromRawInitPatch
    {

        // ####################################################################
        // Draw additional biomes into splat maps according to
        // settings in `MicroSplatXmlConfig.GetWorldConfig(name)`
        // This can be different according to the map `name`
        // ####################################################################

        // Select the target dynamically to patch `MoveNext`
        // Coroutine/Enumerator is compiled to a hidden class
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return HarmonyUtils.GetEnumeratorMoveNext(AccessTools.
                Method(typeof(ChunkProviderGenerateWorldFromRaw), "Init"));
        }

        // ####################################################################
        // ####################################################################

        // Function will be executed at the patched position
        static void ExecutePatched(ref Color32 col1, ref Color32 col2, BiomeDefinition biome)
        {
            // Note: this is very much "sub-optimized"
            // Should optimize fetching of world config
            // Given that it only runs on init, kinda ok!?
            var config = OcbMicroSplat.Config.MicroSplatWorldConfig;
            if (config.BiomeColors.TryGetValue(biome.m_Id,
                out MicroSplatBiomeColor value))
            {
                col1 = value.Color1;
                col2 = value.Color2;
            }
            // Post process to support up to 16 or 32 biomes
            // Uses hidden `_PCBIOMEMASK16` MicroSplat feature
            // Possible since biomes never overlap and blend
            // col1 = Color.clear;
            // col2 = Color.clear;
        }

        // ####################################################################
        // ####################################################################

        // Main function handling the IL patching
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                // Search for first color assignment
                if (codes[i].opcode != OpCodes.Ldloca_S) continue;
                if (!(codes[i].operand is LocalBuilder col1)) continue;
                if (!typeof(Color32).IsAssignableFrom(col1.LocalType)) continue;
                if (++i >= codes.Count) break;
                if (codes[i].opcode != OpCodes.Initobj) continue;
                if (++i >= codes.Count) break;
                // Search for second color assignment
                if (codes[i].opcode != OpCodes.Ldloca_S) continue;
                if (!(codes[i].operand is LocalBuilder col2)) continue;
                if (!typeof(Color32).IsAssignableFrom(col2.LocalType)) continue;
                if (++i >= codes.Count) break;
                if (codes[i].opcode != OpCodes.Initobj) continue;
                if (++i >= codes.Count) break;
                // Search for future assignment
                for (int j = i; j < i + 20; j++)
                {
                    if (codes[j].opcode != OpCodes.Ldloc_S) continue;
                    if (!(codes[j].operand is LocalBuilder biome)) continue;
                    if (!typeof(BiomeDefinition).IsAssignableFrom(biome.LocalType)) continue;
                    // Search end of switch statement
                    for (int n = j; n < j + 40; n++)
                    {
                        if (codes[n + 0].opcode ==  OpCodes.Ldloca_S &&
                            codes[n + 1].opcode == OpCodes.Ldloc_S &&
                            codes[n + 2].opcode == OpCodes.Ldloc_S &&
                            codes[n + 3].opcode == OpCodes.Call)
                        {
                            List<Label> labels = new List<Label>();
                            codes[n].labels.CopyTo(labels); // Copy existing labels
                            codes[n].labels.Clear(); // Clear the original labels
                            codes.Insert(n + 0, new CodeInstruction(OpCodes.Ldloca_S, col1));
                            codes.Insert(n + 1, new CodeInstruction(OpCodes.Ldloca_S, col2));
                            codes.Insert(n + 2, new CodeInstruction(OpCodes.Ldloc_S, biome));
                            codes.Insert(n + 3, CodeInstruction.Call(
                                typeof(GenerateWorldFromRawInitPatch),
                                "ExecutePatched"));
                            // Assign previous jump labels
                            codes[n + 0].labels = labels;
                            return codes;
                        }
                    }
                }
                break;
            }
            return codes;
        }

        // ####################################################################
        // ####################################################################

    }

}
