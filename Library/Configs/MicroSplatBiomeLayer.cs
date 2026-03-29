using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class MicroSplatBiomeLayer
{

    // ####################################################################
    // ####################################################################

    // Optional name for referencing
    // Allows to overwrite configs
    // Mainly for WYSIWYG helper
    public string Name = "Layer";

    // Assumption is that layers configs are specific for a map.
    // Therefore we know all the layers involved and can address
    // them directly, without any naming indirections required.
    // ToDo: add reset toggle to free vanilla layers.
    public int LayerIndex = -1;

    // Stored properties and configs to apply later
    // To fill `MicroSplatProceduralTextureConfig.Layer`
    private DynamicProperties Props = null;
    private List<Keyframe> Heights = null;
    private List<Keyframe> Slopes = null;
    // Not supported by shader (yet!?)
    // public List<Keyframe> Cavities;
    // public List<Keyframe> Erosions;

    public string MicroSplatName => Props?.GetString("microsplat-texture");

    // ####################################################################
    // ####################################################################

    // Patch MicroSplat textures according to xml config
    public static void PatchMicroSplatLayers(List<MicroSplatProceduralTextureConfig.Layer> layers)
    {
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        PatchMicroSplatLayers(layers, OcbMicroSplat.Config.MicroSplatWorldConfig);
    }

    // Ensure we start with sensible defaults
    private static MicroSplatProceduralTextureConfig.Layer CreateLayer()
    {
        var layer = new MicroSplatProceduralTextureConfig.Layer();
        layer.biomeWeights = Vector4.zero;
        layer.biomeWeights2 = Vector4.zero;
        return layer;
    }

    private static void PatchMicroSplatLayers(List<MicroSplatProceduralTextureConfig.Layer> layers, MicroSplatWorld config)
    {
        if (config.BiomeLayers.Count == 0) return;
        foreach (MicroSplatBiomeLayer cfg in config.BiomeLayers)
        {
            var texture = cfg.Props.GetString("microsplat-texture");
            var texcfg = OcbMicroSplat.Config.GetTextureConfig(texture);
            int index = cfg.LayerIndex;
            while (index >= layers.Count) layers.Add(CreateLayer());
            var layer = layers[index];
            var props = cfg.Props;
            if (texcfg != null)
            {
                texcfg.IsUsedByBiome = true;
                layer.textureIndex = texcfg.SlotIdx;
            }
            props.ParseFloat("weight", ref layer.weight);
            props.ParseBool("noise-active", ref layer.noiseActive);
            props.ParseFloat("noise-frequency", ref layer.noiseFrequency);
            props.ParseFloat("noise-offset", ref layer.noiseOffset);
            props.ParseVec("noise-range", ref layer.noiseRange);
            props.ParseBool("height-active", ref layer.heightActive);
            props.ParseBool("slope-active", ref layer.slopeActive);
            props.ParseBool("erosion-active", ref layer.erosionMapActive);
            props.ParseBool("cavity-active", ref layer.cavityMapActive);
            props.ParseEnum("height-curve-mode", ref layer.heightCurveMode);
            props.ParseEnum("slope-curve-mode", ref layer.slopeCurveMode);
            props.ParseEnum("erosion-curve-mode", ref layer.erosionCurveMode);
            props.ParseEnum("cavity-curve-mode", ref layer.cavityCurveMode);
            // props.ParseInt("microsplat-index", ref layer.textureIndex);
            if (props.Contains("biome-mask-color")) Log.Error("biome-mask-color "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weights-b")) Log.Error("biome-weights-b "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-index"))
            {
                int bidx = props.GetInt("biome-index");
                layer.biomeWeights = layer.biomeWeights2 = Vector4.zero;
                if (bidx >= 0 && bidx < MicroSplatBiomeColor.BiomeColMap.Length)
                    layer.biomeWeights = MicroSplatBiomeColor.BiomeColMap[bidx];
                else Log.Error("Biome Index is out of bounds");
            }
            // Allow to set weights for only a certain biome
            if (props.Contains("biome-weight-1")) Log.Error("biome-weight-1 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-2")) Log.Error("biome-weight-2 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-3")) Log.Error("biome-weight-3 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-4")) Log.Error("biome-weight-4 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-5")) Log.Error("biome-weight-5 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-6")) Log.Error("biome-weight-6 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-7")) Log.Error("biome-weight-7 "
                + "no longer supported, use biome-index instead");
            if (props.Contains("biome-weight-8")) Log.Error("biome-weight-8 "
                + "no longer supported, use biome-index instead");
            if (cfg.Heights != null) layer.heightCurve.keys = cfg.Heights.ToArray();
            if (cfg.Slopes != null) layer.slopeCurve.keys = cfg.Slopes.ToArray();
        }
    }

    // ####################################################################
    // ####################################################################

    private static List<Keyframe> ParseBiomeLayer(XElement xml,
        string name, List<Keyframe> frames = null)
    {
        foreach (XElement outer in xml.Elements(name))
        {
            frames = new List<Keyframe>();
            foreach (XElement child in outer.Elements("keyframe"))
            {
                Keyframe frame = new Keyframe();
                if (child.HasAttribute("time")) frame.time =
                    StringParsers.ParseFloat(child.GetAttribute("time"));
                if (child.HasAttribute("value")) frame.value =
                    StringParsers.ParseFloat(child.GetAttribute("value"));
                if (child.HasAttribute("inTangent")) frame.inTangent =
                    StringParsers.ParseFloat(child.GetAttribute("inTangent"));
                if (child.HasAttribute("outTangent")) frame.outTangent =
                    StringParsers.ParseFloat(child.GetAttribute("outTangent"));
                if (child.HasAttribute("inWeight")) frame.inWeight =
                    StringParsers.ParseFloat(child.GetAttribute("inWeight"));
                if (child.HasAttribute("outWeight")) frame.outWeight =
                    StringParsers.ParseFloat(child.GetAttribute("outWeight"));
                frames.Add(frame);
            }
        }
        return frames;
    }

    // ####################################################################
    // ####################################################################

    public MicroSplatBiomeLayer(int idx)
    {
        LayerIndex = idx;
        if (idx < 0) throw new System.Exception(
            "Invalid index for Biome Layer");
        Props = new DynamicProperties();
        Name = $"Layer{idx}";
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        Props = MicroSplatXmlConfig.GetDynamicProperties(xml, Props);
        if (xml.HasAttribute("name")) Name = xml.GetAttribute("name");
        Heights = ParseBiomeLayer(xml, "height-keyframes", Heights);
        Slopes = ParseBiomeLayer(xml, "slope-keyframes", Slopes);
        // ParseBiomeLayer(xml, "cavity-keyframes", Cavities);
        // ParseBiomeLayer(xml, "erosion-keyframes", Erosions);
    }

    // ####################################################################
    // ####################################################################

}
