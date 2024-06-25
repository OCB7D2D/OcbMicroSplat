using HarmonyLib;
using System.Reflection;
using UnityEngine;

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
                    && x.GetParameters().Length == 8);
        }

        static bool Prefix(
            VoxelMeshTerrain __instance,
            int _subMeshIdx, ref int _fullTexId,
            ref bool _bTopSoil, ref Color _color,
            ref Vector2 _uv, ref Vector2 _uv2,
            ref Vector2 _uv3, ref Vector2 _uv4)
        {
            // Allow blend for new biomes
            if (_bTopSoil) return true;
            // Nothing to be done on dedicated servers
            if (GameManager.IsDedicatedServer) return true;
            // This might be our fantasy ID, intercept and correct
            var texID = VoxelMeshTerrain.DecodeMainTexId(_fullTexId);
            // Apply custom voxel configuration (best way)
            if (texID >= MicroSplatVoxels.VoxelIndexOffset &&
                texID < MicroSplatVoxels.VoxelIndexOffsetEnd)
            {
                // Get regular offset into our voxel config array
                var vid = texID - MicroSplatVoxels.VoxelIndexOffset;
                var voxel = OcbMicroSplat.Config.GetVoxelConfig(vid);
                if (voxel == null) Log.Warning(
                    "Found no voxel config for {0}", vid);
                if (voxel == null) return true;
                _bTopSoil = false;
                _color = voxel.color;
                _uv = voxel.uv; _uv2 = voxel.uv2;
                _uv3 = voxel.uv3; _uv4 = voxel.uv4;
                // Log.Out("Got Voxel {0} - {1} {2} {3} {4} {5}",
                //     texID, _color, _uv, _uv2, _uv3, _uv4);
                return false;
            }
            // Invoke regular code
            return true;
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
                Log.Out("Discovered voxel {0} in block {1} (voxel id {2}/{3})",
                    name, __instance.GetBlockName(), voxel.Index, voxel.GetTexId());
                #endif
                __instance.SetSideTextureId(voxel.GetTexId());
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
            // Evacuate for the two freed slots for use by new custom biomes
            // IDs are still needed if used from withing the MicroSplat maps
            if (__result.TerrainTAIndex == 1) __result.TerrainTAIndex = 19;
            else if (__result.TerrainTAIndex == 3) __result.TerrainTAIndex = 20;
        }
    }

    // ####################################################################
    // ####################################################################

}