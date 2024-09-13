using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static OcbTextureUtils;

public static class MicroSplatTextureUtils
{

    public static int TexQuality = -1;

    // ####################################################################
    // ####################################################################

    static int GetMipMapOffset()
    {
        int quality = GameOptionsManager.GetTextureQuality();
        return quality > 2 ? 2 : quality;
    }

    // ####################################################################
    // ####################################################################

    public static Texture LoadTexture(DataLoader.DataPathIdentifier path, out int idx)
    {
        idx = 0; // Unity APIs will accept zero
        if (path.AssetName.EndsWith("]"))
        {
            var start = path.AssetName.LastIndexOf("[");
            if (start != -1)
            {
                idx = int.Parse(path.AssetName.Substring(
                    start + 1, path.AssetName.Length - start - 2));
                return AssetBundleManager.Instance.Get<Texture>(
                    path.BundlePath, path.AssetName.Substring(0, start));
            }
            else
            {
                throw new Exception("Missing `[` to match ending `]`");
            }
        }
        else if (path.IsBundle)
        {
            return AssetBundleManager.Instance.Get<Texture>(
                path.BundlePath, path.AssetName);
        }
        else
        {
            Log.Error("Can't load texture from disk {0}", path.AssetName);
        }
        return null;
    }

    // ####################################################################
    // ####################################################################

    public static NativeArray<byte> GetPixelData(Texture src, int idx, int mip = 0)
    {
        if (src is Texture2DArray arr) return arr.GetPixelData<byte>(idx, mip);
        else if (src is Texture2D tex) return tex.GetPixelData<byte>(mip);
        else throw new Exception("Ivalid texture type to get pixel data");
    }

    public static void SetPixelData(NativeArray<byte> pixels, Texture src, int idx, int mip = 0)
    {
        if (src is Texture2DArray arr) arr.SetPixelData(pixels, mip, idx);
        else if (src is Texture2D tex) tex.SetPixelData(pixels, mip);
        else Log.Error("Invalid texture type to set pixels");
    }

    public static void ApplyPixelData(
        Texture src, bool updateMipmaps = true,
        bool makeNoLongerReadable = false)
    {
        if (src is Texture2DArray arr) arr.Apply(updateMipmaps, makeNoLongerReadable);
        else if (src is Texture2D tex) tex.Apply(updateMipmaps, makeNoLongerReadable);
        else Log.Error("Invalid texture type to apply pixels");
    }

    // Copy `src` into `dst[idx]`, assuming that
    // `src` is full 2k texture into array that is
    // quality constrained (e.g. 1024 for half).
    // Thus we copy the appropriate mipmaps only!
    // E.g. for 2k into 1k we skip one mipmap level
    public static void PatchMicroSplatTexture(
        CommandBuffer cmds,
        Texture dst, int dstidx,
        Texture src, int srcidx = 0)
    {
        var offset = GetMipMapOffset();
        // Copy all mips individually, could optimize ideal case
        // Given that we don't do this often, not much to gain
        if (dst.isReadable && src.isReadable)
        {
            for (int m = 0; m < dst.mipmapCount; m++)
                SetPixelData(GetPixelData(src, srcidx, offset + m), dst, dstidx, m);
            ApplyPixelData(dst, false, false);
        }
        else
        {
            for (int m = 0; m < dst.mipmapCount; m++) cmds.
                CopyTexture(src, srcidx, m + offset, dst, dstidx, m);
        }
        Log.Out(" apply {0}[{1}] to {2}[{3}]",
            src.name, srcidx, dst.name, dstidx);
    }

    public static void PatchMicroSplatTexture(
        CommandBuffer cmds, Texture2DArray dst,
        int dstidx, DataLoader.DataPathIdentifier path)
    {
        var tex = LoadTexture(path, out int srcidx);
        if (tex == null) Log.Error(
            "Error loading {1} from {0}",
            path.BundlePath, path.AssetName);
        PatchMicroSplatTexture(cmds, dst, dstidx, tex, srcidx);
    }

    public static void PatchMicroSplatNormal(
        CommandBuffer cmds, Texture2DArray dst,
        int dstidx, DataLoader.DataPathIdentifier path,
        bool convert = false)
    {
        var gpu = LoadTexture(path, out int srcidx);
        if (convert)
        {
            var tex = TextureFromGPU(gpu, srcidx, true);
            for (var m = 0; m < tex.mipmapCount; m++)
            {
                var pixels = tex.GetPixels(m);
                for (var i = 0; i < pixels.Length; i++)
                    (pixels[i].g, pixels[i].a) =
                        (pixels[i].a, pixels[i].g);
                tex.SetPixels(pixels, m);
            }
            tex.Compress(true);
            tex.Apply(false, false);
            PatchMicroSplatTexture(cmds, dst, dstidx, tex, 0);
        }
        else
        {
            // Just copy from one array slot to another
            PatchMicroSplatTexture(cmds, dst, dstidx, gpu, srcidx);
        }
    }

    // ####################################################################
    // ####################################################################

    public static void ApplyMicroSplatTextures(MeshDescription terrain,
        List<MicroSplatTexture> patches)
    {

        if (!terrain.IsSplatmap(MeshDescription.MESH_TERRAIN)) return;
        if (TexQuality == GamePrefs.GetInt(EnumGamePrefs.OptionsGfxTexQuality)) return;

        int size = 0;
        // Find maximum array index we need to patch via xml
        foreach (var texture in patches) size
            = Math.Max(size, texture.SlotIdx + 1);
        if (terrain == null) throw new Exception("MESH MISSING");
        if (!terrain.IsSplatmap(MeshDescription.MESH_TERRAIN)) return;
        if (!(terrain.textureAtlas is TextureAtlasTerrain atlas))
            throw new Exception("TextureAtlasTerrain format error");
        if (!(atlas.diffuseTexture is Texture2DArray albedos))
            throw new Exception("Expected Texture2DArray for diffuse");
        if (!(atlas.normalTexture is Texture2DArray normals))
            throw new Exception("Expected Texture2DArray for normal");
        if (!(atlas.specularTexture is Texture2DArray speculars))
            throw new Exception("Expected Texture2DArray for specular");
        var cmds = new CommandBuffer();
        cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        Log.Out("Extend microsplat texture arrays to size {0} (from {1})", size, albedos.depth);
        albedos = ResizeTextureArray(cmds, albedos, size, patches, false, false);
        normals = ResizeTextureArray(cmds, normals, size, patches, true, false);
        speculars = ResizeTextureArray(cmds, speculars, size, patches, false, false);
        #if DEBUG
        Log.Out("Apply indexed textures (overwrite existing slots)");
        #endif
        foreach (var indexed in OcbMicroSplat.Config.MicroSplatWorldConfig.TexPatches)
        {
            if (indexed.Value.Diffuse != null) PatchMicroSplatTexture(cmds,
                albedos, indexed.Key, indexed.Value.Diffuse.Path);
            if (indexed.Value.Normal != null) PatchMicroSplatNormal(cmds, normals,
                indexed.Key, indexed.Value.Normal.Path, indexed.Value.SwitchNormal);
            if (indexed.Value.Specular != null) PatchMicroSplatTexture(cmds,
                speculars, indexed.Key, indexed.Value.Specular.Path);
        }
        #if DEBUG
        Log.Out("Apply custom textures (as needed by world's xml config)");
        #endif
        foreach (var patch in patches) if (patch.Diffuse != null && patch.Diffuse.Path.AssetName != null)
            PatchMicroSplatTexture(cmds, albedos, patch.SlotIdx, patch.Diffuse.Path);
        foreach (var patch in patches) if (patch.Normal != null && patch.Normal.Path.AssetName != null)
            PatchMicroSplatNormal(cmds, normals, patch.SlotIdx, patch.Normal.Path, patch.SwitchNormal);
        foreach (var patch in patches) if (patch.Specular != null && patch.Specular.Path.AssetName != null)
            PatchMicroSplatTexture(cmds, speculars, patch.SlotIdx, patch.Specular.Path);
        #if DEBUG
        Log.Out("Execute command buffer (applying all copy requests async)");
        #endif
        Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Default);
        TexQuality = GamePrefs.GetInt(EnumGamePrefs.OptionsGfxTexQuality);
        terrain.TexDiffuse = atlas.diffuseTexture = albedos;
        terrain.TexNormal = atlas.normalTexture = normals;
        terrain.TexSpecular = atlas.specularTexture = speculars;
        // Update materials to use new texture arrays
        terrain.ReloadTextureArrays(true);
        #if DEBUG
        Log.Out("MicroSplat patching finished");
        #endif
    }

    // ####################################################################
    // ####################################################################

    public static Texture2DArray ResizeTextureArray(CommandBuffer cmds, Texture2DArray array, int size,
        List<MicroSplatTexture> patches, bool linear = true, bool destroy = false)
    {
        if (array.depth == size) return array;
        // Create a copy and add space for more textures
        var tex = new Texture2DArray(array.width, array.height,
            size, array.graphicsFormat, TextureCreationFlags.MipChain,
            array.mipmapCount);
        // Keep readable state same as original
        if (!array.isReadable) tex.Apply(false, true);
        if (!tex.name.Contains("extended_"))
            tex.name = "extended_" + array.name;
        // Copy old textures to new copy (any better way?)
        foreach (var copy in patches)
        {
            if (copy.SrcIdx == -1) continue; // No source slot
            cmds.CopyTexture(array, copy.SrcIdx, tex, copy.SlotIdx);
            Log.Out(" copy {0}[{1}] to {2}[{3}]",
                array.name, copy.SrcIdx, tex.name, copy.SlotIdx);
        }
        // Copy settings from old array
        tex.filterMode = array.filterMode;
        tex.mipMapBias = array.mipMapBias;
        tex.anisoLevel = array.anisoLevel;
        tex.wrapModeU = array.wrapModeU;
        tex.wrapModeV = array.wrapModeV;
        tex.wrapModeW = array.wrapModeW;
        // Optionally destroy the original object
        if (destroy) UnityEngine.Object.Destroy(array);
        // Return the copy
        return tex;
    }

    // ####################################################################
    // ####################################################################

    public static Color GetChannelUsage(string path)
    {
        Color usage = new Color(0, 0, 0, 0);
        string fname = System.IO.Path.GetFileName(path);
        if (!System.IO.File.Exists(path))
        {
            Log.Error("SplatMap {0} not found", fname);
            return usage; // Will produce visual glitch
        }
        Texture2D tex = TextureUtils.LoadTexture(path);
        if (tex.isReadable == false) throw new Exception(
            "Can only get channel usage from readable textures");
        foreach (var pixel in tex.GetPixels())
        {
            usage.r = Mathf.Max(usage.r, pixel.r);
            usage.g = Mathf.Max(usage.g, pixel.g);
            usage.b = Mathf.Max(usage.b, pixel.b);
            usage.a = Mathf.Max(usage.a, pixel.a);
        }
        Log.Out("SplatMap {0} usage: {1}/{2}/{3}/{4}",
            fname, usage.r, usage.g, usage.b, usage.a);
        return usage;
    }

    // ####################################################################
    // ####################################################################

}
