using System.IO;
using UnityEngine;
using static OcbTextureDumper;

public static class MicroSplatDump
{

    // ####################################################################
    // ####################################################################

    public static void DumpMicroSplat()
    {
        var path = "exports/microsplat";
        var rv = Directory.CreateDirectory(path);
        Log.Out("Exporting to {0}", rv.FullName);
        var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
        TextureAtlas atlas = mesh.textureAtlas;
        if (mesh.TexDiffuse is Texture2DArray t2diff)
        {
            for (int i = 0; i < t2diff.depth; i++) DumpTexure(
                string.Format("{0}/array.{1}.albedo.png", path, i),
                t2diff, i, true, RemoveHeightFromAlbedoTexture);
            for (int i = 0; i < t2diff.depth; i++) DumpTexure(
                string.Format("{0}/array.{1}.height.png", path, i),
                t2diff, i, true, ExtractHeightFromAlbedoTexture);
        }
        if (atlas.normalTexture is Texture2DArray t2norm)
        {
            for (int i = 0; i < t2norm.depth; i++) DumpTexure(
                string.Format("{0}/array.{1}.normal.png", path, i),
                t2norm, i, false, UnpackNormalPixelsSwitched);
        }
        if (atlas.specularTexture is Texture2DArray t2occl)
        {
            for (int i = 0; i < t2occl.depth; i++) DumpTexure(
                string.Format("{0}/array.{1}.occlusion.png", path, i),
                t2occl, i, true, ExtractAmbientOcclusionFromTexture);
            for (int i = 0; i < t2occl.depth; i++) DumpTexure(
                string.Format("{0}/array.{1}.roughness.png", path, i),
                t2occl, i, true, ExtractRoughnessFromTexture);
            for (int i = 0; i < t2occl.depth; i++) DumpTexure(
                string.Format("{0}/array.{1}.metallic.png", path, i),
                t2occl, i, true, ExtractBlueFromTexture);
        }

        if (mesh.TexDiffuse is Texture2DArray t2diffx)
        {
            if (atlas.normalTexture is Texture2DArray t2normx)
            {
                int len = Mathf.Min(t2diffx.depth, t2normx.depth);
                for (int i = 0; i < len; i++) DumpTexure2(
                    string.Format("{0}/array.{1}.emissive.png", path, i),
                    t2diffx, t2normx, i, true, ExtractEmissionFromTexture);
            }

        }


    }

    // ####################################################################
    // ####################################################################

    public static void DumpOldTerrain()
    {
        var path = "exports/terrain";
        var rv = Directory.CreateDirectory(path);
        Log.Out("Exporting to {0}", rv.FullName);
        var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
        if (mesh.textureAtlas is TextureAtlasTerrain terrain)
        {
            for (int i = 0; i < terrain.diffuse.Length; i++) DumpTexure(
                string.Format("{0}/terrain.{1}.diffuse.png", path, i),
                terrain.diffuse[i], true);
            for (int i = 0; i < terrain.normal.Length; i++) DumpTexure(
                string.Format("{0}/terrain.{1}.normal.png", path, i),
                terrain.normal[i], false, UnpackNormalPixels);
            // Doesn't look like there is any info here!?
            for (int i = 0; i < terrain.specular.Length; i++) DumpTexure(
                string.Format("{0}/terrain.{1}.specular.png", path, i),
                terrain.specular[i], true);
        }
    }

    // ####################################################################
    // ####################################################################

    static readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainPropTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msPropTex");
    static readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainProcCurveTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msProcCurveTex");
    static readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainProcParamTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msProcParamTex");

    public static void DumpSplatMaps()
    {
        var path = "exports/splatmaps";
        var rv = Directory.CreateDirectory(path);
        Log.Out("Exporting to {0}", rv.FullName);
        if (GameManager.Instance?.World?.ChunkCache?.ChunkProvider
            is ChunkProviderGenerateWorldFromRaw cpr)
        {
            DumpTexure(string.Format("{0}/world.biome.mask.1.png", path), cpr.procBiomeMask1);
            DumpTexure(string.Format("{0}/world.biome.mask.2.png", path), cpr.procBiomeMask2);
            for (int i = 0; i < cpr.splats.Length; i++)
            {
                DumpTexure(string.Format("{0}/world.splat.rgba.{1}.png", path, i),
                    cpr.splats[i], true, null);
                DumpTexure(string.Format("{0}/world.splat.{1}.r.png", path, i),
                    cpr.splats[i], true, ExtractRedChannel);
                DumpTexure(string.Format("{0}/world.splat.{1}.g.png", path, i),
                    cpr.splats[i], true, ExtractGreenChannel);
                DumpTexure(string.Format("{0}/world.splat.{1}.b.png", path, i),
                    cpr.splats[i], true, ExtractBlueChannel);
                DumpTexure(string.Format("{0}/world.splat.{1}.a.png", path, i),
                    cpr.splats[i], true, ExtractAlphaChannel);
            }
        }
        if (VoxelMeshTerrainPropTex.Get(null) is Texture2D msPropTex)
            DumpTexure(string.Format("{0}/mesh.prop.png", path), msPropTex);
        if (VoxelMeshTerrainProcCurveTex.Get(null) is Texture2D msProcCurveTex)
            DumpTexure(string.Format("{0}/mesh.proc.curve.png", path), msProcCurveTex);
        if (VoxelMeshTerrainProcParamTex.Get(null) is Texture2D msProcParamTex)
            DumpTexure(string.Format("{0}/mesh.proc.param.png", path), msProcParamTex);
    }

    // ####################################################################
    // ####################################################################

}
