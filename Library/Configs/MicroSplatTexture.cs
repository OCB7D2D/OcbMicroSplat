using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using static StringParsers;

public class ResourceAssetUrl
{

    // ####################################################################
    // ####################################################################

    // public string[] Assets;

    public DataLoader.DataPathIdentifier Path;

    // ####################################################################
    // ####################################################################

    public ResourceAssetUrl(string url)
    {
        Path = DataLoader.ParseDataPathIdentifier(url);
        // Try to load the (cached) asset bundle resource (once)
        if (Path.IsBundle) AssetBundleManager.Instance
                .LoadAssetBundle(Path.BundlePath);
        // Support different face textures
        // Assets = Path.AssetName?.Split(',');
    }

    // ####################################################################
    // ####################################################################

}

public class MicroSplatTexture
{

    // ####################################################################
    // ####################################################################

    // Index of originial vanilla source
    // When set, we copy from tex2Darray
    public int SrcIdx = -1;
    // The new microsplat slot index
    public int SlotIdx = -1;

    public bool SwitchNormal = false;
    public bool IsUsedByBiome = false;
    public bool IsUsedByVoxel = false;
    public bool IsUsedBySplat = false;

    // Per texture shader settings
    public UnityEngine.Vector2 SplatUVScale;
    public UnityEngine.Vector2 SplatUVOffset;
    // public float Metallic = 0f;

    // Config for MicroSplat assets
    public ResourceAssetUrl Diffuse = null;
    public ResourceAssetUrl Normal = null;
    public ResourceAssetUrl Specular = null;

    public string GetUseString()
    {
        List<string> used = new List<string>();
        if (IsUsedBySplat) used.Add("Splat");
        if (IsUsedByVoxel) used.Add("Voxel");
        if (IsUsedByBiome) used.Add("Biome");
        if (used.Count == 0) return "Unused";
        else return used.Join(null, ", ");
    }

    public bool IsInUse => IsUsedByBiome || IsUsedBySplat || IsUsedByVoxel;

    // ####################################################################
    // ####################################################################

    public MicroSplatTexture(int id = -1, bool voxel = false)
    {
        SlotIdx = id;
        IsUsedByVoxel = voxel;
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        foreach (XElement child in xml.Elements("property"))
        {
            if (!child.HasAttribute("name")) throw new Exception(
                $"Mandatory attribute `name` missing on {child.Name}");
            if (!child.HasAttribute("value")) throw new Exception(
                $"Mandatory attribute `value` missing on {child.Name}");
            string name = child.GetAttribute("name");
            string value = child.GetAttribute("value");
            if (name == "Diffuse") Diffuse = new ResourceAssetUrl(value);
            else if (name == "Normal") Normal = new ResourceAssetUrl(value);
            else if (name == "Specular") Specular = new ResourceAssetUrl(value);
            else if (name == "SwitchNormal") SwitchNormal = bool.Parse(value);
            else if (name == "SplatUVScale") SplatUVScale = ParseVector2(value);
            else if (name == "SplatUVOffset") SplatUVOffset = ParseVector2(value);
            // else if (name == "Metallic") Metallic = float.Parse(value);
        }
    }

    // ####################################################################
    // ####################################################################

}
