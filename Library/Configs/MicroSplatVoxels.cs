using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Xml.Linq;
using UnityEngine;

public class MicroSplatVoxels
{

    // ####################################################################
    // ####################################################################

    public const int VoxelIndexOffset = 42;
    public const int VoxelIndexOffsetEnd = 61;

    // ####################################################################
    // ####################################################################

    public readonly Dictionary<string, MicroSplatVoxel> Voxels
        = new Dictionary<string, MicroSplatVoxel>();

    public readonly List<MicroSplatVoxel> Voxel
        = new List<MicroSplatVoxel>();

    // ####################################################################
    // ####################################################################

    public MicroSplatVoxel GetOrCreateVoxelConfig(string name)
    {
        if (!Voxels.TryGetValue(name, out MicroSplatVoxel voxel))
            Voxels.Add(name, voxel = new MicroSplatVoxel(name, Voxels.Count));
        return voxel;
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (!xml.HasAttribute("name")) throw new Exception(
            $"Mandatory attribute `name` missing on {xml.Name}");
        string name = xml.GetAttribute("name");
        if (!Voxels.TryGetValue(name, out MicroSplatVoxel voxel))
            Log.Warning("Skipping unused voxel {0}", name);
        else
        {
            Voxel.Add(voxel);
            voxel.Parse(xml);
        }
    }

    // ####################################################################
    // ####################################################################

    public void GatherVoxelTextures(List<MicroSplatTexture> textures)
    {
        foreach (var voxel in Voxels)
        {
            foreach (var kv in voxel.Value.textures)
            {
                var texture = OcbMicroSplat.Config.GetTextureConfig(kv.Key);
                if (texture == null) Log.Error("Couldn't find MicroSplat {0}", kv.Key);
                else if (!textures.Contains(texture)) textures.Add(texture);
            }
        }
    }

    // ####################################################################
    // ####################################################################

    public void Reset()
    {
        Voxels.Clear();
        Voxel.Clear();
    }

    // ####################################################################
    // ####################################################################

    public class ResetQualitySettings : IDisposable
    {
        readonly int masterTextureLimit = 0;
        readonly int mipmapsReduction = 0;
        readonly bool streamingMips = false;
        readonly float mipSlope = 0.6776996f;
        public ResetQualitySettings()
        {
            masterTextureLimit = QualitySettings.masterTextureLimit;
            streamingMips = QualitySettings.streamingMipmapsActive;
            mipmapsReduction = QualitySettings.streamingMipmapsMaxLevelReduction;
            mipSlope = Shader.GetGlobalFloat("_MipSlope");
            QualitySettings.masterTextureLimit = 0;
            QualitySettings.streamingMipmapsActive = false;
            QualitySettings.streamingMipmapsMaxLevelReduction = 0;
            Shader.SetGlobalFloat("_MipSlope", 0.6776996f);
        }
        public void Dispose()
        {
            QualitySettings.masterTextureLimit = masterTextureLimit;
            QualitySettings.streamingMipmapsActive = streamingMips;
            QualitySettings.streamingMipmapsMaxLevelReduction = mipmapsReduction;
            Shader.SetGlobalFloat("_MipSlope", mipSlope);
        }
    }

    // Do the actual patching
    public void WorldChanged(MeshDescription terrain)
    {
        Log.Out("=============================================");
        if (!(terrain.textureAtlas is TextureAtlasTerrain atlas)) return;
        Log.Out($"Patch into old terrain texture atlas");
        Log.Out("=============================================");
        // using (ResetQualitySettings reset = new ResetQualitySettings())
        foreach (var kvv in Voxels)
        {
            var voxel = kvv.Value;
            // Find single texture with most weight
            float maxWeight = float.MinValue;
            string texture = string.Empty;
            foreach (var kvt in voxel.textures)
            {
                if (maxWeight > kvt.Value) continue;
                maxWeight = kvt.Value;
                texture = kvt.Key;
            }
            // Skip this voxel if there is no best texture
            if (string.IsNullOrEmpty(texture)) continue;
            // Get old index to patch to and involved texture
            var idx = voxel.Index + VoxelIndexOffset;
            var cfg = OcbMicroSplat.Config.GetTextureConfig(texture);
            // Patch the different texture into the old atlas
            // This is idempotent, worst case texture isn't used
            PatchVoxelTexture(cfg.Diffuse, ref atlas.diffuse[idx], atlas.diffuse[1]);
            PatchVoxelTexture(cfg.Normal, ref atlas.normal[idx], atlas.normal[1]);
            PatchVoxelTexture(cfg.Specular, ref atlas.specular[idx], atlas.specular[0]);
            // Extend the UV map for e.g. falling blocks
            atlas.uvMapping[idx] = new UVRectTiling()
            {
                index = 0, bGlobalUV = true,
                blockW = 8, blockH = 8,
                uv = new Rect(34, 34, 247, 247),
                textureName = voxel.Name,
                // Not sure it's worth to customize this!?
                material = MaterialBlock.fromString("stone"),
                // Are these even used for this case
                color = new Color(1, 0, 1),
            };
        }
        Log.Out("Finished patching old terrain atlas");
    }

    private static void PatchVoxelTexture(ResourceAssetUrl asset, ref Texture2D value, Texture2D tmpl)
    {
        Log.Out("Loading {0}", asset.Path.AssetName);
        Texture tex = MicroSplatTextureUtils.LoadTexture(
            asset.Path, out int srcidx);
        if (tex == null) Log.Error(
            "Error loading {1} from {0}",
            asset.Path.BundlePath,
            asset.Path.AssetName);
        if (tex is Texture2D texture) value = texture;
        else if (tex is Texture2DArray arr)
        {
            var copy = new Texture2D(arr.width, arr.height, arr.graphicsFormat, arr.mipmapCount,
                UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain);
            Graphics.CopyTexture(tex, srcidx, copy, 0);
            value = copy;
        }
    }

    // ####################################################################
    // ####################################################################

}
