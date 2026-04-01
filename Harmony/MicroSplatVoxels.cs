using HarmonyLib;
using System.Linq;
using System.Reflection;

public static class HarmonyMicroSplatVoxels
{

    // ####################################################################
    // Harmony Patch to support custom terrain blends
    // We basically create virtual texture IDs that we
    // query in the relevant function we are patching up.
    // When we see an existing virtual ID in the patched
    // function, we act accordingly to create a custom
    // blend setting for the MicroSplat shader.
    // ####################################################################

    [HarmonyPatch()]
    public class VoxelMeshTerrain_GetColorForTextureId
    {

        // Use function to select method to patch
        static MethodBase TargetMethod()
        {
            // Pretty unreliable, but works as long as source doesn't change
            return AccessTools.GetDeclaredMethods(typeof(VoxelMeshTerrain))
                .Find(x => x.Name == "GetColorForTextureId"
                    && x.GetParameters().Length == 2);
        }

        static bool Prefix(
            VoxelMeshTerrain __instance, int _subMeshIdx,
            ref Transvoxel.BuildVertex _data, ref bool __state)
        {
            __state = _data.bTopSoil;
            // Disable TopSoil rendering fully?
            // Otherwise sub-biomes will not render!
            // read TopSoil = show custom voxel texture
            _data.bTopSoil = false;
            // Nothing to be done on dedicated servers
            if (GameManager.IsDedicatedServer) return true;
            // This might be our fantasy ID, intercept and correct
            var texID = VoxelMeshTerrain.DecodeMainTexId(_data.texture);
            // Apply custom voxel configuration (best way)
            if (texID >= MicroSplatVoxels.VoxelIndexOffset &&
                texID < MicroSplatVoxels.VoxelIndexOffsetEnd)
            {
                // Get regular offset into our voxel config array
                var vid = texID - MicroSplatVoxels.VoxelIndexOffset;
                var voxel = OcbMicroSplat.Config.GetVoxelConfig(vid);
                if (voxel == null) Log.Warning("Found no voxel config for {0}", vid);
                if (voxel == null) return true;
                if (voxel.textures.Count == 0) {
                    Log.Error("Voxel config must have at least one texture assigned");
                    Log.Error("Please adjust the config for voxel {0}", voxel.Name);
                    return true;
                }
                if (voxel.textures.Count > 1)
                {
                    Log.Error("Multiple textures per voxel are no longer supported");
                    Log.Error("Please adjust the config for voxel {0}", voxel.Name);
                }
                var name = voxel.textures.First().Key;
                var cfg = OcbMicroSplat.Config.GetTextureConfig(name);
                _data.uv.x = voxel.SlotIdx;
                if (cfg != null)
                {
                    // Show biome if voxel is undamaged (To fade out voxel blending)
                    _data.uv.y = __state ? cfg.TopSoilVoxel.x : cfg.TopSoilVoxel.y;
                    // Set info if voxel is damaged (To fade out biome blending)
                    _data.color.g = __state ? cfg.TopSoilBiome.x : cfg.TopSoilBiome.y;
                }
                else
                {
                    Log.Out("No config for {0}", name);
                    _data.uv.y = __state ? 0 : 1;
                    _data.color.g = __state ? 1 : 0;
                }
                return false;
            }
            else
            {
                // texID comes from `property name="Texture" value="XY"`
                int index = MicroSplatRemaps.GetMicroSplatIndex(texID);
                index = MicroSplatRemaps.RemapFromSplat(index);
                string name = string.Format("microsplat{0}", index);
                var cfg = OcbMicroSplat.Config.GetTextureConfig(name);
                // if (index < 0) return false; // Show Biome
                _data.uv.x = index;
                if (cfg != null)
                {
                    // Show biome if voxel is undamaged (To fade out voxel blending)
                    _data.uv.y = __state ? cfg.TopSoilVoxel.x : cfg.TopSoilVoxel.y;
                    // Set info if voxel is damaged (To fade out biome blending)
                    _data.color.g = __state ? cfg.TopSoilBiome.x : cfg.TopSoilBiome.y;
                }
                else
                {
                    Log.Out("No config for {0}", name);
                    _data.uv.y = __state ? 0 : 1;
                    _data.color.g = __state ? 1 : 0;
                }
                // Skip regular code
                return false;
            }
        }
        static void Postfix(ref Transvoxel.BuildVertex _data, ref bool __state)
        {
            // Restore previous state
            _data.bTopSoil = __state;

            // Coal damaged => no biome blending, fully show voxel
            // Coal undamaged => some biome blending, fully show voxel
            // Gravel damaged => no biome blending, fully show voxel
            // Gravel undamaged => no biome blending, fully show voxel
            // Snow damaged => a little biome blending, fully show voxel
            // Snow undamaged => full biome blending, don't show voxel
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(Block), "Init")]
    class PatchMicroSplatTextureIdParser
    {
        static void Postfix(Block __instance)
        {
            if (__instance.Properties.Values.TryGetValue("MicroSplatVoxel", out string name))
            {
                MicroSplatVoxel voxel = OcbMicroSplat.Config.GetOrCreateVoxelConfig(name);
                if (!voxel.blocks.Contains(__instance)) voxel.blocks.Add(__instance);
                #if DEBUG
                Log.Out("Discovered voxel texture {0} in block {1} (voxel id {2}/{3})",
                    name, __instance.GetBlockName(), voxel.Index, voxel.GetTexId());
                #endif
                __instance.SetSideTextureId(voxel.GetTexId(), 0);
            }
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(BlocksFromXml), "CreateBlock")]
    private static class BlocksFromXmlCreateBlockPatch
    {
        static void Prefix(DynamicProperties properties, string blockName)
        {
            if (!properties.Contains("TerrainIndex")) return;
            string index = properties.GetString("TerrainIndex");
            if (int.TryParse(index, out int _)) return;
            var blocks = OcbMicroSplat.Config.MicroSplatTexturesConfigs;
            blocks.RegisterTerrainBlock(index, blockName);
            properties.Values["TerrainIndex"] = "19";
        }

        static void Postfix(Block __result)
        {
            // Called way to early, must do that somewhere else :-/
            if (!OcbMicroSplat.Config.MicroSplatRemapConfig.Mappings
                .TryGetValue(__result.TerrainTAIndex, out int to)) return;
            Log.Out("Map {0} terrain texture from #{2} to #{2}",
                __result.blockName, __result.TerrainTAIndex, to);
            __result.TerrainTAIndex = to;
            // Evacuate for the two freed slots for use by new custom biomes
            // IDs are still needed if used from withing the MicroSplat maps
            // if (__result.TerrainTAIndex == 1) __result.TerrainTAIndex = 19; // same
            // else if (__result.TerrainTAIndex == 3) __result.TerrainTAIndex = 20; // same
        }
    }
    
    // ####################################################################
    // ####################################################################

}