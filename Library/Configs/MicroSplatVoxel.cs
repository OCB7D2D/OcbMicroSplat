using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class MicroSplatVoxel
{

    // ####################################################################
    // ####################################################################

    public string Name = null;

    public int Index = -1;

    // Calculated color values
    // Directly passed to shader
    public Vector4 color;
    public Vector2 uv;
    public Vector2 uv2;
    public Vector2 uv3;
    public Vector2 uv4;

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
        color = Color.clear;
        uv = uv2 = uv3 = uv4 = Vector2.zero;
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (xml.HasAttribute("color")) color = StringParsers.
                ParseColor(xml.GetAttribute("color"));
        if (xml.HasAttribute("uv")) uv = StringParsers.
                ParseVector2(xml.GetAttribute("uv"));
        if (xml.HasAttribute("uv2")) uv2 = StringParsers
                .ParseVector2(xml.GetAttribute("uv2"));
        if (xml.HasAttribute("uv3")) uv3 = StringParsers
                .ParseVector2(xml.GetAttribute("uv3"));
        if (xml.HasAttribute("uv4")) uv4 = StringParsers
                .ParseVector2(xml.GetAttribute("uv4"));
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
            SetMicroSplatWeight(texture.SlotIdx, kv.Value);
        }
        // Enable w/a component if we have voxel info
        // ToDo: check out in detail how this works
        if (color.w != 0) return; // keep original value
        if (uv.x != 0 || uv.y != 0 || uv2.x != 0 || uv2.y != 0 ||
            uv3.x != 0 || uv3.y != 0 || uv4.x != 0 || uv4.y != 0)
            color.w = 1f;
    }

    // ####################################################################
    // ####################################################################

    public void SetMicroSplatWeight(int idx, float weight)
    {
        if (weight < 0.0f) throw new Exception(
            $"Weight must be positive {weight}");
        switch (idx)
        {
            case 16: uv.x = weight; break;
            case 17: uv.y = weight; break;
            case 18: uv2.x = weight; break;
            case 19: uv2.y = weight; break;
            case 20: uv3.x = weight; break;
            case 21: uv3.y = weight; break;
            case 22: uv4.x = weight; break;
            case 23: uv4.y = weight; break;
            case 24: uv.x = - weight; break;
            case 25: uv.y = - weight; break;
            case 26: uv2.x = - weight; break;
            case 27: uv2.y = - weight; break;
            case 28: uv3.x = - weight; break;
            case 29: uv3.y = - weight; break;
            case 30: uv4.x = - weight; break;
            case 31: uv4.y = - weight; break;
            default: throw new Exception(
                $"Invalid texture index {idx}");
        }
    }

    // ####################################################################
    // ####################################################################

    public int GetTexId() => Index +
        MicroSplatVoxels.VoxelIndexOffset;

    // ####################################################################
    // ####################################################################

}
