﻿using HarmonyLib;
using System.Collections.Generic;
using System.Xml.Linq;

public class MicroSplatXmlConfig
{

    // ####################################################################
    // ####################################################################

    public readonly MicroSplatShader TerrainShaderConfig = new MicroSplatShader();
    public readonly MicroSplatTextures MicroSplatTexturesConfigs = new MicroSplatTextures();
    public readonly MicroSplatVoxels MicroSplatVoxelConfigs = new MicroSplatVoxels();
    public readonly MicroSplatWorld MicroSplatWorldConfig = new MicroSplatWorld();

    // ####################################################################
    // ####################################################################

    private void Parse(XElement xml)
    {
        foreach (XElement child in xml.Elements())
        {
            if (child.Name.Equals("microsplat-shader"))
                OcbMicroSplat.Config.TerrainShaderConfig.Parse(child);
            else if (child.Name.Equals("biomes-config"))
                OcbMicroSplat.Config.MicroSplatWorldConfig.Parse(child);
            else if (child.Name.Equals("microsplat-texture"))
                OcbMicroSplat.Config.MicroSplatTexturesConfigs.Parse(child);
            else if (child.Name.Equals("microsplat-voxel"))
                OcbMicroSplat.Config.MicroSplatVoxelConfigs.Parse(child);
        }
    }

    // ####################################################################
    // ####################################################################

    public MicroSplatVoxel GetOrCreateVoxelConfig(string name)
        => MicroSplatVoxelConfigs.GetOrCreateVoxelConfig(name);

    // ####################################################################
    // ####################################################################

    public List<MicroSplatTexture> GetAllVoxelTextures(
        List<MicroSplatTexture> textures = null)
    {
        if (textures == null) textures = new List<MicroSplatTexture>();
        MicroSplatVoxelConfigs.GatherVoxelTextures(textures);
        return textures;
    }

    // ####################################################################
    // ####################################################################

    public MicroSplatTexture GetTextureConfig(string name)
    {
        if (MicroSplatTexturesConfigs.Textures.TryGetValue(
            name, out MicroSplatTexture config))
                return config;
        return null;
    }

    public MicroSplatVoxel GetVoxelConfig(int idx)
    {
        if (idx >= 0 && idx < MicroSplatVoxelConfigs.Voxel.Count)
            return MicroSplatVoxelConfigs.Voxel[idx];
        Log.Error("Index out of range for voxel config {0}", idx);
        return null;
    }

    // ####################################################################
    // ####################################################################

    static public DynamicProperties GetDynamicProperties(XElement xml,
        DynamicProperties props = null)
    {
        if (props == null) props = new DynamicProperties();
        foreach (XElement childNode in xml.Elements())
        {
            if (childNode.Name.Equals("property") == false) continue;
            props.Add(childNode);
        }
        return props;
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(WorldGlobalFromXml), "Load")]
    static class WorldGlobalFromXmlLoadPatch
    {
        static void Prefix(XmlFile _xmlFile)
        {
            OcbMicroSplat.Config.MicroSplatWorldConfig.Reset();
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
            OcbMicroSplat.Config.Parse(_xmlFile.XmlDoc.Root);
        }
    }

    // ####################################################################
    // ####################################################################


    public void CalculateVoxelUvColors()
    {
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        foreach (var voxel in MicroSplatVoxelConfigs.Voxel)
            voxel?.CalculateUvColors();
    }

    public void SetMaxTexturesCount(int max)
    {
        if (max < 24) max = 24;
        else if (max < 28) max = 28;
        else if (max < 32) max = 32;
        else throw new System.Exception(
            "Exceeded MicroSplat texture count");
        TerrainShaderConfig.MaxTextures = max;
    }

    // ####################################################################
    // ####################################################################

}
