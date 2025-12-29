using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static MicroSplatPropData;

public class OcbMicroSplat : IModApi
{

    public static OcbMicroSplat Instance = null;

    public static MicroSplatXmlConfig Config
        = new MicroSplatXmlConfig();

    public static string DecalBundlePath;
    public static string DecalShaderBundle;

    const PerTexFloat CurveInterpolator = (PerTexFloat)(4 * 19 + 0);
    const PerTexFloat BlendWeightFactor = (PerTexFloat)(4 * 19 + 3);

    // ####################################################################
    // ####################################################################

    public void InitMod(Mod mod)
    {
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
        DecalShaderBundle = DecalBundlePath = System.IO.Path
            .Combine(mod.Path, "Resources/OcbDecalShader.unity3d");
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
            DecalShaderBundle = System.IO.Path.Combine(mod.Path, "Resources/OcbDecalShader.metal.unity3d");
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

    private static int GetFreeSlot(bool[] occupied, int off = 0)
    {
        for (int i = off; i < occupied.Length; i++)
            if (!occupied[i]) return i;
        return -1;
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

        var occupied = new bool[32];

        msPropData = LoadManager.LoadAssetFromAddressables<MicroSplatPropData>("TerrainTextures",
            "Microsplat/MicroSplatTerrainInGame_propdata.asset", _loadSync: true).Asset;
        msProcData = LoadManager.LoadAssetFromAddressables<MicroSplatProceduralTextureConfig>("TerrainTextures",
            "Microsplat/MicroSplatTerrainInGame_proceduraltexture.asset", _loadSync: true).Asset;

        if (Config.MicroSplatWorldConfig.ResetLayers) msProcData.layers.Clear();

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
            // Free two slots for use by new custom biomes
            if (layer.textureIndex == 1) layer.textureIndex = 19;
            else if (layer.textureIndex == 3) layer.textureIndex = 20;
            // These are mapped directly in the shader code
            else if (layer.textureIndex == 4) layer.textureIndex = 16;
            else if (layer.textureIndex == 6) layer.textureIndex = 16;
            else if (layer.textureIndex == 5) layer.textureIndex = 14;
            else if (layer.textureIndex == 8) layer.textureIndex = 23;
            else if (layer.textureIndex == 9) layer.textureIndex = 13;
            var cfg = Config.GetTextureConfig($"microsplat{layer.textureIndex}");
            if (cfg != null) cfg.IsUsedByBiome = true;
        }

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
            texcfg.IsUsedByBiome = true;
        }

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

        // Process all textures that are registered for in-use
        foreach (var kv in Config.MicroSplatTexturesConfigs.Textures)
        {
            var texture = kv.Value;
            // Skip all items that have an index
            if (texture.SrcIdx != -1)
            {
                #if DEBUG
                Log.Out("Known {0} at {1} ({2})",
                    kv.Key, texture.SlotIdx,
                    texture.GetUseString());
                #endif
                SetupPropData(texture.SlotIdx, texture);
                continue;
            }
            // Texture is only used as biome, so it can stay below index 12
            // As index below 12 can not be addressed by voxels via UVs
            if (texture.IsUsedByVoxel || texture.IsUsedByBiome)
            {
                // Push textures used by voxels above slot 2
                int min = texture.IsUsedByVoxel ? 2 : 0;
                texture.SlotIdx = GetFreeSlot(occupied, min);
                if (texture.SlotIdx == -1) throw new Exception(
                    "No more free slots in MicroSplat array");
                occupied[texture.SlotIdx] = true;
                if (!patches.Contains(texture))
                    patches.Add(texture);
            }
            // Skip unused ones
            else
            {
                #if DEBUG
                Log.Out("Skip {0} at {1} ({2})",
                    kv.Key, texture.SlotIdx,
                    texture.GetUseString());
                #endif
                continue;
            }
            // Apply given textures to slot
            #if DEBUG
            Log.Out("Apply {0} at {1} ({2})",
                kv.Key, texture.SlotIdx,
                texture.GetUseString());
            #endif
            // Update the UvScale property
            // ToDo: Add more per-tex stuff?
            SetupPropData(texture.SlotIdx, texture);

            // Update terrain indexes for registered blocks that need updating
            if (Config.MicroSplatTexturesConfigs.Blocks.TryGetValue(kv.Key, out var blocks))
            {
                foreach (var name in blocks)
                {
                    var block = Block.GetBlockByName(name);
                    block.TerrainTAIndex = kv.Value.SlotIdx;
                }
            }
        }

        foreach (var tex in Config.MicroSplatWorldConfig.TexPatches)
        {
            SetupPropData(tex.Key, tex.Value);
        }

        MicroSplatBiomeLayer.PatchMicroSplatLayers(msProcData.layers);

        Config.CalculateVoxelUvColors();

        int max = 23; // Figure out max index used
        for (int n = 0; n < occupied.Length; n++)
            if (occupied[n]) max = Mathf.Max(max, n);
        Config.SetMaxTexturesCount(max);

        #if DEBUG
        Log.Out("#############################################");
        Log.Out("#############################################");
        #endif

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
        msPropData.SetValue(texture.SlotIdx,
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
            Log.Out("#############################################");
            Log.Out("#############################################");
            #endif

            return false;
        }

    }

    // ####################################################################
    // ####################################################################

}
