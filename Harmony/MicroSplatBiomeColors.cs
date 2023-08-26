using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;

[HarmonyPatch(typeof(WorldBiomes), "readXML")]
static class WorldBiomesReadXmlPatch
{
    // ####################################################################
    // ####################################################################

    static void Prefix(XDocument _xml)
    {
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (BiomeDefinition.nameToId == null) BiomeDefinition
            .nameToId = new Dictionary<string, byte>();
        // Parse the vanilla biome mapping configs (to extend up on it)
        // You may also use this to hard-code a specific biome id for all maps
        foreach (XElement xmlElement in _xml.Root.Elements("biomemap"))
        {
            if (BiomeDefinition.nameToId.ContainsKey(xmlElement.GetAttribute("name"))) continue;
            BiomeDefinition.nameToId.Add(xmlElement.GetAttribute("name"), byte.Parse(xmlElement.GetAttribute("id")));
            Log.Out("Parsed Biome definition {0} => {1}", xmlElement.GetAttribute("name"), xmlElement.GetAttribute("id"));
        }
        byte CustomBiomeIndex = 24; // ToDo: maybe make it more dynamic?
        foreach (XElement biomeElement in _xml.Root.Elements("biome"))
        {
            string name = biomeElement.GetAttribute("name");
            if (string.IsNullOrEmpty(name)) continue;
            if (BiomeDefinition.nameToId.ContainsKey(name)) continue;
            BiomeDefinition.nameToId.Add(name, CustomBiomeIndex);
            Log.Out("New Biome {0} at {1}", name, CustomBiomeIndex);
            CustomBiomeIndex += 1;
        }
    }

    // ####################################################################
    // ####################################################################

}

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

                codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, col1));
                codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, col2));
                codes.Insert(i++, new CodeInstruction(OpCodes.Ldloc_S, biome));
                codes.Insert(i++, CodeInstruction.Call(
                    typeof(GenerateWorldFromRawInitPatch),
                    "ExecutePatched"));
                return codes;
            }


            // cols1 = field;
            break;
        }

        return codes;
    }

    // ####################################################################
    // ####################################################################

}
