using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static MicroSplatPropData;

public class OcbMicroSplat : IModApi
{

    public static String Path = null;

    public static OcbMicroSplat Instance = null;

    public static MicroSplatXmlConfig Config
        = new MicroSplatXmlConfig();

    // public static string DecalBundlePath;
    // public static string DecalShaderBundle;

    const PerTexFloat CurveInterpolator = (PerTexFloat)(4 * 19 + 0);
    const PerTexFloat BlendWeightFactor = (PerTexFloat)(4 * 19 + 3);

    // ####################################################################
    // ####################################################################

    public void InitMod(Mod mod)
    {
        Path = mod.Path; // Store path for others
        if (GameManager.IsDedicatedServer) return;
        Log.Out("OCB Harmony Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        #if DEBUG
        Log.Error("This is a test version of OcbMicroSplat!");
        Log.Error("Do not redistribute or use in production!");
        #endif
        if (!PlayerPrefs.HasKey("TerrainTessellation"))
            PlayerPrefs.SetInt("TerrainTessellation", 2);
        // DecalShaderBundle = DecalBundlePath = System.IO.Path
        //     .Combine(mod.Path, "Resources/OcbDecalShader.unity3d");
        // if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
        //     DecalShaderBundle = System.IO.Path.Combine(mod.Path, "Resources/OcbDecalShader.metal.unity3d");
        Instance = this; // Remember static instance to use by patch below
    }

    // ####################################################################
    // ####################################################################

    // Call `HandleWorldChanged` after splatmaps are processed
    [HarmonyPatch(typeof(WorldBiomeProviderFromImage), MethodType.Constructor,
        new System.Type[] { typeof(string), typeof(WorldBiomes), typeof(int)})]
    public class WorldBiomeProviderFromImage_Ctor
    {
        public static void Prefix(WorldBiomeProviderFromImage __instance)
        => Instance.HandleWorldChanged(GameManager.Instance.World);
    }

    // ####################################################################
    // ####################################################################

    // Cleanup static data when world is unloaded
    [HarmonyPatch(typeof(GameManager), "SaveAndCleanupWorld")]
    public class GameManager_SaveAndCleanupWorld
    {
        static void Postfix()
        {
            // Reset static flags to re-init MicroSplat
            MicroSplatTextureUtils.TexQuality = -1;
            VoxelMeshTerrain.isInitStatic = false;
            Config = new MicroSplatXmlConfig();
        }
    }

    // ####################################################################
    // ####################################################################

    private void HandleWorldChanged(World _world)
    {
        if (_world == null) return; // ToDo: call reset when this happens?
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
        PrepareMicroSplatPatches(_world); // Do all the preparation once
        OcbMicroSplatDecals.WorldChanged(_world, terrain);
        Config.TerrainShaderConfig.WorldChanged(terrain);
        Config.MicroSplatVoxelConfigs.WorldChanged(terrain);
    }

    // ####################################################################
    // ####################################################################

    private static MicroSplatPropData msPropData = null;
    private static MicroSplatProceduralTextureConfig msProcData = null;

    // ####################################################################
    // ####################################################################

    // Static helper used by our WYSIWYG mod to query
    // original used settings to recreate xml configs
    public static string GetMicroSplatLayerName(int i)
    {
        var cfgs = Config.MicroSplatWorldConfig.BiomeLayers;
        if (i < 0) throw new Exception($"Layer {i} must be positive");
        if (i > cfgs.Count) throw new Exception($"Layer {i} out of bound");
        return cfgs[i].Name;
    }

    public static string GetMicroSplatTextureName(int i)
    {
        var cfgs = Config.MicroSplatWorldConfig.BiomeLayers;
        if (i < 0) throw new Exception($"Layer {i} must be positive");
        if (i > cfgs.Count) throw new Exception($"Layer {i} out of bound");
        return cfgs[i].MicroSplatName;
    }

    // ####################################################################
    // ####################################################################

    private static int GetFreeSlot(bool[] occupied, bool voxel = true)
    {
        for (int i = 0; i < occupied.Length; i++)
        {
            if (i == 1 && voxel) continue;
            // if (i > 31 && voxel) return -1;
            if (!occupied[i]) return i;
        }
        throw new Exception(string.Format(
            "Exceeded MicroSplat texture limit"));
    }

    // ####################################################################
    // ####################################################################

    public float GetPropValue(MicroSplatPropData prop, int x, int y, int channel)
    {
        return prop.values[y * 32 + x][channel];
    }

    public float GetPropFloat(MicroSplatPropData prop, int textureIndex, PerTexFloat channel)
    {
        float num = (float)channel / 4f; int num2 = (int)num;
        int channel2 = Mathf.RoundToInt((num - num2) * 4f);
        return GetPropValue(prop, textureIndex, num2, channel2);
    }

    public Vector2 GetPropVector2(MicroSplatPropData prop, int x, int y, int channel)
    {
        Color color = prop.values[y * 32 + x];
        if (channel == 0) return new Vector2(color.r, color.g);
        else return new Vector2(color.b, color.a);
    }

    public Vector2 GetPropVector2(MicroSplatPropData prop, int textureIndex, PerTexVector2 channel)
    {
        float num = (float)channel * 0.25f;
        return GetPropVector2(prop, textureIndex, (int)num,
            Mathf.RoundToInt((num - (int)num) * 4f));
    }

    public Color GetPropColor(MicroSplatPropData prop, int textureIndex, PerTexFloat channel)
    {
        int num = Mathf.RoundToInt((float)channel * 0.25f);
        return prop.values[textureIndex * 32 + num];
    }

    private void RegisterTexID(Block block, int texID)
    {
        if (Config.ReportBlocks.Contains(block.blockName))
            Log.Out("block {0} has {1}", block.blockName, texID);
        var slotIdx = MicroSplatRemaps.GetMicroSplatIndex(texID);
        if (slotIdx == -1) return; // Skip if texture ID is unknown
        slotIdx = MicroSplatRemaps.RemapFromSplat(slotIdx);
        if (!(Config.GetTextureConfig($"microsplat{slotIdx}") is MicroSplatTexture cfg)) return;
        cfg.RegisterVoxelUsage(block.blockName);
    }


    public void PrepareMicroSplatPatches(World world)
    {

        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;

        #if DEBUG
        Log.Out("#############################################");
        Log.Out("Prepare MicroSplat Patches");
        Log.Out("#############################################");
        #endif

        patches.Clear();

        var occupied = new bool[128];
        int voxels = -1, textures = -1;

        msPropData = LoadManager.LoadAssetFromAddressables<MicroSplatPropData>("TerrainTextures",
            "Microsplat/MicroSplatTerrainInGame_propdata.asset", _loadSync: true).Asset;
        msProcData = LoadManager.LoadAssetFromAddressables<MicroSplatProceduralTextureConfig>("TerrainTextures",
            "Microsplat/MicroSplatTerrainInGame_proceduraltexture.asset", _loadSync: true).Asset;

        string level = GamePrefs.GetString(EnumGamePrefs.GameWorld);
        var worldPath = PathAbstractions.WorldsSearchPaths.GetLocation(level);
        var splat3 = MicroSplatTextureUtils.GetChannelUsage(worldPath.FullPath + "/splat3_processed.png");
        var splat4 = MicroSplatTextureUtils.GetChannelUsage(worldPath.FullPath + "/splat4_processed.png");
        Config.MicroSplatTexturesConfigs.Textures["microsplat4"].IsUsedBySplat = splat3.r > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat5"].IsUsedBySplat = splat3.g > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat6"].IsUsedBySplat = splat3.b > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat7"].IsUsedBySplat = splat3.a > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat8"].IsUsedBySplat = splat4.r > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat9"].IsUsedBySplat = splat4.g > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat10"].IsUsedBySplat = splat4.b > 0;
        Config.MicroSplatTexturesConfigs.Textures["microsplat11"].IsUsedBySplat = splat4.a > 0;

        // Find voxel usage from all blocks
        Log.Out("Mark voxel usage from blocks");
        foreach (var block in Block.list)
        {
            if (block == null) continue;
            foreach (var info in block.textureInfos)
            {
                if (info.bTextureForEachSide)
                {
                    foreach (int texID in info.sideTextureIds)
                    {
                        RegisterTexID(block, texID);
                    }
                }
                else
                {
                    int texID = info.singleTextureId;
                    RegisterTexID(block, texID);
                }
            }
        }

        // Copy per-texture configs from current setup
        for (int i = 0; i < 24; i++)
        {
            if (!(Config.GetTextureConfig($"microsplat{i}") is MicroSplatTexture cfg)) continue;
            if (!cfg.HasSplatUVScale) cfg.SplatUVScale = GetPropVector2(msPropData, i, PerTexVector2.SplatUVScale);
            if (!cfg.HasSplatUVOffset) cfg.SplatUVOffset = GetPropVector2(msPropData, i, PerTexVector2.SplatUVOffset);
            // We know that we will never have any useful values for these in vanilla
            // cfg.TessDisplacementUpBias = GetPropFloat(msPropData, i, PerTexFloat.DisplacementBias);
            // cfg.TessDisplacementOffset = GetPropFloat(msPropData, i, PerTexFloat.DisplacementOffset);
            // cfg.TessDisplacementStrength = GetPropFloat(msPropData, i, PerTexFloat.DisplacementStength);
            // if (cfg.TessDisplacementStrength == 0) cfg.TessDisplacementStrength = 1;
            // cfg.Metallic = GetPropFloat(msPropData, i, PerTexFloat.Metallic);
        }

        // Evacuate a few save slots, as indexes 0 to 3 can only
        // be addressed by biome layers (maybe in the old days these
        // had another dedicated splat texture the shader was using?)
        // We can safely re-use the voxel specific textures for these
        for (int i = 0; i < msProcData.layers.Count; i++)
        {
            var layer = msProcData.layers[i];
            if (!OcbMicroSplat.Config.MicroSplatRemapConfig.Mappings
                .TryGetValue(layer.textureIndex, out int to)) continue;
            Log.Out("Map layer #{0} texture from #{1} to #{2}",
                i, layer.textureIndex, to);
            layer.textureIndex = to;
            // Free two slots for use by new custom biomes
            // if (layer.textureIndex == 1) layer.textureIndex = 19;
            // else if (layer.textureIndex == 3) layer.textureIndex = 20;

            // // These are mapped directly in the shader code
            // else if (layer.textureIndex == 4) layer.textureIndex = 16;
            // else if (layer.textureIndex == 6) layer.textureIndex = 16;
            // else if (layer.textureIndex == 5) layer.textureIndex = 14;
            // else if (layer.textureIndex == 8) layer.textureIndex = 23;
            // else if (layer.textureIndex == 9) layer.textureIndex = 13;
        }

        // Map all block terrain indexes
        foreach (var block in Block.list)
        {
            if (block == null) continue; // How can this happens? But it does!
            if (!OcbMicroSplat.Config.MicroSplatRemapConfig.Mappings
                .TryGetValue(block.TerrainTAIndex, out int to)) continue;
            // Only log if not the default value or for all terrain blocks
            // ToDo: maybe make this a bit smarter (e.g. check for shape)
            if (block.TerrainTAIndex != 1 || block.Properties.Contains("TerrainIndex"))
                Log.Out("Map {0} terrain texture from #{1} to #{2}",
                block.blockName, block.TerrainTAIndex, to);
            block.TerrainTAIndex = to;
        }

        // Truncate/Reset vanilla layers
        if (Config.MicroSplatWorldConfig.TruncateLayers >= 0)
        {
            Log.Out("Truncate {1} biome layers to {0}",
                Config.MicroSplatWorldConfig.TruncateLayers,
                msProcData.layers.Count);
            msProcData.layers.RemoveRange(
                Config.MicroSplatWorldConfig.TruncateLayers,
                msProcData.layers.Count - Config.MicroSplatWorldConfig.TruncateLayers);
        }
        if (Config.MicroSplatWorldConfig.ResetLayers)
        {
            Log.Out("Reset {0} biome layers",
                msProcData.layers.Count);
            msProcData.layers.Clear();
        }

        // Mark texture usage of remaining layers
        for (int i = 0; i < msProcData.layers.Count; i++)
        {
            var layer = msProcData.layers[i];
            var cfg = Config.GetTextureConfig($"microsplat{layer.textureIndex}");
            if (cfg != null) cfg.RegisterBiomeUsage(i);
        }

        Log.Out("Mark voxel texture usages");
        // Mark all voxel textures that are used by voxels
        Config.MicroSplatVoxelConfigs.MarkVoxelTextures();

        // Mark texture flag `IsUseByBiome` for in-use textures
        var config = Config.MicroSplatWorldConfig;
        if (config.BiomeLayers.Count == 0) return;
        foreach (MicroSplatBiomeLayer cfg in config.BiomeLayers)
        {
            var texture = cfg.MicroSplatName;
            var texcfg = Config.GetTextureConfig(texture);
            if (texcfg == null) continue;
            texcfg.RegisterBiomeUsage(cfg);
        }

        #if DEBUG
        Log.Out("Mark internal occupied slots");
        #endif

        // Make sure we copy splat textures
        // ToDo: check actual splat for usage
        for (int i = 0; i < 24; i++)
        {
            string key = $"microsplat{i}";
            var texture = Config.GetTextureConfig(key);
            if (texture == null) Log.Error("Couldn't find MicroSplat {0}", key);
            else
            {
                if (!texture.IsInUse) continue;
                if (!patches.Contains(texture))
                    patches.Add(texture);
                occupied[texture.SlotIdx] = true;
            }
        }

        #if DEBUG
        Log.Out("Patch voxel texture");
        #endif

        // Process all textures that are registered for in-use
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            var texture = kv.Value;
            // Skip all items that have an index
            if (texture.SrcIdx != -1)
            {
                #if DEBUG
                Log.Out("  Known {0} at {1} ({2}) -> occupied {3}",
                    kv.Key, texture.SlotIdx,
                    texture.GetUseString(),
                    occupied[texture.SlotIdx]);
                #endif
                // SetupPropData(texture.SlotIdx, texture);
                continue;
            }
            // Texture is only used as biome, so it can stay below index 12
            // As index below 12 can not be addressed by voxels via UVs
            if (texture.IsUsedByVoxel)
            {
                // Push textures (limit usage to voxels)
                int slotIdx = GetFreeSlot(occupied, true);
                // Check if voxel would exceed limit
                // We may be able to evacuate a biome only
                if (slotIdx > 31)
                {
                    Log.Out("  Trying to evacuate biome only texture");
                    TryToEvacuateBiomeOnlyLayer(occupied, slotIdx);
                    slotIdx = GetFreeSlot(occupied);
                    Log.Out("    Found target slot index {0}", slotIdx);
                }
                texture.SlotIdx = slotIdx;
                if (texture.SlotIdx == -1) throw new Exception(
                    "No more free slots in MicroSplat array");
                occupied[texture.SlotIdx] = true;
                #if DEBUG
                Log.Out("  Patch voxel {0} at {1} ({2})",
                    kv.Key, texture.SlotIdx,
                    texture.GetUseString());
                #endif
                if (!patches.Contains(texture))
                    patches.Add(texture);
            }
            // Skip unused ones
            else continue;
            // Apply given textures to slot
            #if DEBUG
            // Log.Out("  Voxel {0} at {1} ({2})",
            //     kv.Key, texture.SlotIdx,
            //     texture.GetUseString());
            #endif
            // Update the UvScale property
            // ToDo: Add more per-tex stuff?
            // SetupPropData(texture.SlotIdx, texture);

        }

        #if DEBUG
        Log.Out("Patch biome only texture");
        #endif

        // we only count voxels, biome only don't count
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            var texture = kv.Value;
            // Skip all items that have an index
            if (texture.SrcIdx != -1) continue;
            if (!texture.IsUsedByVoxel) continue;
            voxels = Mathf.Max(23, texture.SlotIdx);
        }
        // Enforce voxel texture slot limit
        if (voxels > 31)
        {
            throw new Exception(string.Format("Exceeded"
                + " MicroSplat voxel texture count by {0}",
                voxels - 31));
        }

        // Process all textures that are registered for in-use
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            var texture = kv.Value;
            // Skip all items that have an index
            if (texture.SrcIdx != -1) continue;
            // Texture is only used as biome, so it can stay below index 12
            // As index below 12 can not be addressed by voxels via UVs
            if (!texture.IsUsedByVoxel && texture.IsUsedByBiome)
            {
                // Push textures used by voxels above slot 2
                texture.SlotIdx = GetFreeSlot(occupied, false);
                if (texture.SlotIdx == -1) throw new Exception(
                    "No more free slots in MicroSplat array");
                occupied[texture.SlotIdx] = true;
                #if DEBUG
                Log.Out("  Patch biome {0} at {1} ({2})",
                    kv.Key, texture.SlotIdx,
                    texture.GetUseString());
                #endif
                if (!patches.Contains(texture))
                    patches.Add(texture);
            }
            // Skip unused ones
            else continue;
            // Apply given textures to slot
            // #if DEBUG
            // Log.Out("  Biome {0} at {1} ({2})",
            //     kv.Key, texture.SlotIdx,
            //     texture.GetUseString());
            // #endif
            // Update the UvScale property
            // ToDo: Add more per-tex stuff?
            // SetupPropData(texture.SlotIdx, texture);

        }

        // Get highest texture slot index
        for (int n = 0; n < occupied.Length; n++)
            if (occupied[n]) textures = n;
        textures = Mathf.Max(23, textures);
        // Enforce texture slot limit
        if (textures > 63)
        {
            throw new Exception(string.Format("Exceeded"
                + " MicroSplat texture count by {0}",
                textures - 63));
        }

        // Update certain configs after all slot ushering is done
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            // Skip over all unused textures
            if (kv.Value.IsInUse == false) continue;
            // Assert that the slot index is assigned
            if (kv.Value.SlotIdx == -1) Log.Error(
                "Invalid Texture Slot Index detected");
            // Update terrain indexes for registered blocks that need updating
            if (Config.MicroSplatTexturesConfigs.Blocks.TryGetValue(kv.Key, out var blocks))
            {
                foreach (var name in blocks)
                {
                    var block = Block.GetBlockByName(name);
                    block.TerrainTAIndex = kv.Value.SlotIdx;
                }
            }
            // Update per texture configs (e.g. UvScale)
            SetupPropData(kv.Value.SlotIdx, kv.Value);
        }

        foreach (var tex in Config.MicroSplatWorldConfig.TexPatches)
        {
            SetupPropData(tex.Key, tex.Value);
        }

        MicroSplatBiomeLayer.PatchMicroSplatLayers(msProcData.layers);

        Config.CalculateVoxelUvColors();

        Config.SetMaxTexturesCount(voxels);

        #if DEBUG
        Log.Out("#############################################");
        Log.Out("# Report final texture usage and statistics #");
        Log.Out("#############################################");
        KeyValuePair<string, MicroSplatTexture>[] usage = new
            KeyValuePair<string, MicroSplatTexture>[textures + 1];
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            var texture = kv.Value;
            if (texture.SlotIdx == -1) continue;
            var slot = usage[texture.SlotIdx];
            if (slot.Key == null || !slot.Value.IsInUse)
                usage[texture.SlotIdx] = kv;
            else Log.Error("Double slot usage detected");
        }
        foreach (var kv in usage)
        {
            var texture = kv.Value;
            Log.Out("  Texture {0} at {1} ({2})",
                kv.Key, texture.SlotIdx,
                texture.GetUseString());
            foreach (var layer in texture.BiomeLayers)
                Log.Out("    Biome Layer: #{0}", layer);
            foreach (var bname in texture.BlockNames)
            {
                var block = Block.GetBlockByName(bname);
                if (!Config.ReportBlocks.Contains(bname)) continue;
                if (block == null) Log.Out("    Other: {0}", bname);
                else Log.Out("    Block: {0} (TA #{1})",
                    bname, block.TerrainTAIndex);
            }
        }
        Log.Out("#############################################");
        Log.Out("#############################################");
        #endif

    }

    // Called when we want to assign a voxel beyond the limits
    // We might be able to evacuate a biome-only slot index
    // In that case we must also update referencing layers
    private bool TryToEvacuateBiomeOnlyLayer(bool[] occupied, int nxt)
    {
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            var texture = kv.Value;
            if (texture.SlotIdx < 0) continue;
            // Keep slot 1 reservered
            // Can't hold voxel info
            if (texture.SlotIdx == 1) continue;
            if (texture.SlotIdx > 31) continue;
            if (texture.IsUsedBySplat) continue;
            if (texture.IsUsedByVoxel) continue;
            // Skip unused vanilla texture slots
            if (!texture.IsUsedByBiome) continue;
            #if DEBUG
            Log.Out("Evacuated Biome texture from {0} to {1} (occ {2})",
                texture.SlotIdx, nxt, occupied[texture.SlotIdx]);
            #endif
            // This slot can be moved higher up
            int idx = texture.SlotIdx;
            // Find and update all referencing layers
            foreach (var layer in msProcData.layers)
                layer.textureIndex = nxt;
            // We already got passed next free slot
            texture.SlotIdx = nxt;
            // Update occupied array
            occupied[idx] = false;
            occupied[nxt] = true;
            // Signal success
            return true;
        }
        return false;
    }

    private static void SetupPropData(int idx, MicroSplatTexture texture)
    {
        msPropData.SetValue(idx,
            PerTexVector2.SplatUVOffset,
            texture.SplatUVOffset);
        msPropData.SetValue(idx,
            PerTexFloat.DisplacementBias,
            texture.TessDisplacementUpBias);
        msPropData.SetValue(idx,
            PerTexFloat.DisplacementOffset,
            texture.TessDisplacementOffset);
        msPropData.SetValue(idx,
            PerTexFloat.DisplacementStength,
            texture.TessDisplacementStrength);
        msPropData.SetValue(idx,
            CurveInterpolator,
            texture.CurveWeight);
        msPropData.SetValue(idx,
            BlendWeightFactor,
            texture.BlendWeight);
        msPropData.SetValue(idx,
            PerTexVector2.SplatUVScale,
            texture.SplatUVScale);
        // msPropData.SetValue(idx,
        //     PerTexFloat.Metallic,
        //     texture.Metallic);
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(GameOptionsManager), "ApplyTerrainOptions")]
    class GameOptionsManagerApplyTerrainOptionsPatch
    {
        static void Postfix()
        {
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
            var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
            // Those call will bail out early if they detect no quality change
            OcbMicroSplat.Config.TerrainShaderConfig.LoadTerrainShaders(terrain);
            MicroSplatTextureUtils.ApplyMicroSplatTextures(terrain, patches);
            OcbMicroSplat.Config.TerrainShaderConfig.InitMicroSplatMaterial(terrain.material);
            OcbMicroSplat.Config.TerrainShaderConfig.InitMicroSplatMaterial(terrain.materialDistant);
        }
    }

    // ####################################################################
    // ####################################################################


    // Re-use static list to avoid unecessary allocs
    private static readonly List<MicroSplatTexture>
        patches = new List<MicroSplatTexture>();

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(VoxelMeshTerrain), "InitMicroSplat")]
    class VoxelMeshTerrainInitMicroSplatPatch
    {
        static bool Prefix(ref MicroSplatProceduralTextureConfig ___msProcData,
            ref MicroSplatPropData ___msPropData, ref Texture2D ___msPropTex,
            ref Texture2D ___msProcCurveTex, ref Texture2D ___msProcParamTex)
        {

            bool isPrefabEditor = PrefabEditModeManager.Instance?.IsActive() ?? false;

            #if DEBUG
            Log.Out("#############################################");
            if (!isPrefabEditor) Log.Out("Apply MicroSplat patches on load/change"); 
            else Log.Out("Skipping MicroSplat patches for prefab editor");
            Log.Out("#############################################");
            #endif

            if (isPrefabEditor || GameManager.IsDedicatedServer) return false;
            if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return false;

            try
            {
                ___msPropData = msPropData;
                ___msProcData = msProcData;
                var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
                MicroSplatTextureUtils.ApplyMicroSplatTextures(mesh, patches);
                ___msProcCurveTex = ___msProcData.GetCurveTexture();
                ___msProcParamTex = ___msProcData.GetParamTexture();
                ___msPropTex = ___msPropData.GetTexture();
            }
            catch (Exception e)
            {
                Log.Error("Patching of MicroSplat had a fatal error!");
                Log.Error("Can't continue at this point, aborting!");
                Log.Error("Error: {0}", e.Message);
                Application.Quit();
            }

            #if DEBUG
            Log.Out("Using {0} Biome Layers", msProcData.layers.Count);
            Log.Out("#############################################");
            Log.Out("#############################################");
            #endif

            return false;
        }

    }

    // ####################################################################
    // ####################################################################

}
