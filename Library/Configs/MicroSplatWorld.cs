using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public class MicroSplatWorld
{

    // ####################################################################
    // ####################################################################

    // Patches by index to overwrite vanilla textures (e.g. splats)
    public readonly Dictionary<int, MicroSplatTexture> TexPatches
        = new Dictionary<int, MicroSplatTexture>();

    // Colors by biome name (will map to ids automatically)
    public readonly Dictionary<int, MicroSplatBiomeColor> BiomeColors
        = new Dictionary<int, MicroSplatBiomeColor>();

    // Biome layers by name (will map to ids automatically)
    public readonly List<MicroSplatBiomeLayer> BiomeLayers
        = new List<MicroSplatBiomeLayer>();

    // Keep the vanilla layer config?
    public bool ResetLayers = false;

    // ####################################################################
    // ####################################################################

    // Clear and init internal data
    public MicroSplatWorld()
    {
        Reset();
    }

    // ####################################################################
    // ####################################################################

    public void Reset()
    {
        TexPatches.Clear();
        BiomeColors.Clear();
        BiomeLayers.Clear();
        // Initialize empty config for existing layers
        // You may also update these, but not remove them
        for (int i = 0; i < 12; i++) BiomeLayers.Add(
            new MicroSplatBiomeLayer(i));
    }

    // ####################################################################
    // ####################################################################

    // Parsing a `microsplat-patch` to overwrite e.g. splat textures
    public void ParseMicroSplatPatch(XElement xml)
    {
        if (!xml.HasAttribute("index")) throw new Exception(
            $"Mandatory attribute `index` missing on {xml.Name}");
        int index = int.Parse(xml.GetAttribute("index"));
        if (!TexPatches.TryGetValue(index, out MicroSplatTexture config))
            TexPatches.Add(index, config = new MicroSplatTexture());
        config.Parse(xml);
    }
    
    // Parsing a `biome-color` element in `biome-config`
    public void ParseBiomeColor(XElement xml)
    {
        if (!xml.HasAttribute("name")) throw new Exception(
            $"Mandatory attribute `name` missing on {xml.Name}");
        string name = xml.GetAttribute("name");
        if (!BiomeDefinition.nameToId.TryGetValue(name, out byte index))
            throw new Exception($"Biome definition missing for {name}");
        #if DEBUG
        Log.Out("Registered biome color {0} with index {1}", name, index);
        #endif
        if (!BiomeColors.TryGetValue(index, out MicroSplatBiomeColor config))
            BiomeColors.Add(index, config = new MicroSplatBiomeColor());
        config.Parse(xml);
    }

    // Parsing a `biome-layer` element in `biome-config`
    public void ParseBiomeLayer(XElement xml)
    {
        int index = BiomeLayers.Count;
        if (xml.HasAttribute("index")) index = 
            int.Parse(xml.GetAttribute("index"));
        else if (xml.HasAttribute("name")) index =
            FindIndexByName(xml.GetAttribute("name"));
        while (index >= BiomeLayers.Count) BiomeLayers.Add(
            new MicroSplatBiomeLayer(BiomeLayers.Count));
        BiomeLayers[index].Parse(xml);
    }

    // Find existing index by given layer name
    private int FindIndexByName(string name)
    {
        for (int i = 0; i < BiomeLayers.Count; i++)
            if (BiomeLayers[i].Name == name) return i;
        return BiomeLayers.Count;
    }

    // Currently only used to set the `reset` flag
    public void ParseBiomeLayers(XElement xml)
    {
        if (!xml.HasAttribute("reset")) return;
        string reset = xml.GetAttribute("reset");
        ResetLayers = bool.Parse(reset);
        if (ResetLayers) BiomeLayers.Clear();
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (!xml.HasAttribute("world")) throw new Exception(
            $"Mandatory attribute `world` missing on {xml.Name}");
        var filters = xml.GetAttribute("world").Split(
            new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim());
        string level = GamePrefs.GetString(EnumGamePrefs.GameWorld);
        if (!filters.Contains("*") && !filters.Contains(level)) return;
        foreach (XElement node in xml.Elements())
        {
            if (node.Name == "microsplat-patch")
                ParseMicroSplatPatch(node);
            else if (node.Name == "biome-color")
                ParseBiomeColor(node);
            // Only used to reset vanilla so far
            else if (node.Name == "biome-layers")
                ParseBiomeLayers(node);
            else if (node.Name == "biome-layer")
                ParseBiomeLayer(node);
        }

    }

    // ####################################################################
    // ####################################################################

}
