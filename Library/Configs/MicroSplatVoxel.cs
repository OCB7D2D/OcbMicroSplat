using System;
using System.Collections.Generic;
using System.Xml.Linq;

public class MicroSplatVoxel
{

    // ####################################################################
    // ####################################################################

    public string Name = null;

    public int Index = -1;
    public int SlotIdx = -1;

    // List of MicroSplat textures with weights
    public Dictionary<string, float> textures =
        new Dictionary<string, float>();

    public List<Block> blocks = new List<Block>();

    // ####################################################################
    // ####################################################################

    public MicroSplatVoxel(string name, int index)
    {
        Name = name;
        Index = index;
        SlotIdx = -1;
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (xml.HasAttribute("color")) Log.Error("Passing Color is no longer supported");
        if (xml.HasAttribute("uv")) Log.Error("Passing UVs is no longer supported");
        if (xml.HasAttribute("uv2")) Log.Error("Passing UVs is no longer supported");
        if (xml.HasAttribute("uv3")) Log.Error("Passing UVs is no longer supported");
        if (xml.HasAttribute("uv4")) Log.Error("Passing UVs is no longer supported");
        foreach (XElement child in xml.Elements("texture"))
        {
            if (!child.HasAttribute("name")) throw new Exception(
                $"Mandatory attribute `name` missing on {child.Name}");
            if (!child.HasAttribute("weight")) throw new Exception(
                $"Mandatory attribute `weight` missing on {child.Name}");
            string name = child.GetAttribute("name");
            string weight = child.GetAttribute("weight");
            textures[name] = float.Parse(weight);
        }
    }

    // ####################################################################
    // ####################################################################

    public void CalculateUvColors()
    {
        foreach (var kv in textures)
        {
            var texture = OcbMicroSplat.Config.GetTextureConfig(kv.Key);
            if (texture == null) throw new Exception(
                $"MicroSplat texture missing {kv.Key}");
            // SetMicroSplatWeight(texture.SlotIdx, kv.Value);
            SlotIdx = texture.SlotIdx;
        }
    }

    // ####################################################################
    // ####################################################################

    public int GetTexId() => Index +
        MicroSplatVoxels.VoxelIndexOffset;

    // ####################################################################
    // ####################################################################

}
