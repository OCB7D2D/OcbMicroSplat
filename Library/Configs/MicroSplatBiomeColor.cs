using System;
using System.Xml.Linq;
using UnityEngine;

public class MicroSplatBiomeColor
{

    // ####################################################################
    // ####################################################################

    public static Vector4[] BiomeColMap = new Vector4[16]
    {
        new Vector4(1,0,0,0), 
        new Vector4(0,1,0,0), 
        new Vector4(0,0,1,0), 
        new Vector4(0,0,0,1), 
        new Vector4(0,0,0,0),
        new Vector4(1,1,0,0),
        new Vector4(1,0,1,0),
        new Vector4(1,0,0,1),
        new Vector4(0,1,1,0),
        new Vector4(0,1,0,1),
        new Vector4(0,0,1,1),
        new Vector4(1,1,1,0),
        new Vector4(1,1,0,1),
        new Vector4(1,0,1,1),
        new Vector4(0,1,1,1),
        new Vector4(1,1,1,1),
    };

    public static int GetBiomeIndex(Vector4 color)
    {
        for (int i = 0; i < BiomeColMap.Length; i++)
        {
            if (BiomeColMap[i] == color) return i;
        }
        return -1;
    }

    // ####################################################################
    // ####################################################################

    public Color Color1 = Color.clear;
    public Color Color2 = Color.clear;

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (xml.HasAttribute("color1")) throw new Exception(
            $"biome-map.color1 in {xml.Name} no longer "
            + "supported, use biome-index instead");
        if (xml.HasAttribute("color2")) throw new Exception(
            $"biome-map.color2 in {xml.Name} no longer "
            + "supported, use biome-index instead");

        if (!xml.HasAttribute("biome-index")) throw new Exception(
            $"Mandatory attribute `biome-index` missing on {xml.Name}");

        byte bidx = byte.Parse(xml.GetAttribute("biome-index"));
        Color1 = BiomeColMap[bidx];
        Color2 = Color.clear;
    }

    // ####################################################################
    // ####################################################################

}
