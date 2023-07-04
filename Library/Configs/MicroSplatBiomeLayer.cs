using UnityEngine;
using System.Collections.Generic;
using System.Xml.Linq;

public class MicroSplatBiomeLayer
{

    // ####################################################################
    // ####################################################################

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


    // Patch MicroSplat textures according to xml config
    public static void PatchMicroSplatLayers(List<MicroSplatProceduralTextureConfig.Layer> layers)
    {
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        PatchMicroSplatLayers(layers, OcbMicroSplat.Config.MicroSplatWorldConfig);
    }

    private static void PatchMicroSplatLayers(List<MicroSplatProceduralTextureConfig.Layer> layers, MicroSplatWorld config)
    {
        Log.Out("+++ Patching Biome Layers {0}", config.BiomeLayers.Count);
        if (config.BiomeLayers.Count == 0) return;
        foreach (MicroSplatBiomeLayer cfg in config.BiomeLayers)
        {
            var texture = cfg.Props.GetString("microsplat-texture");
            var texcfg = OcbMicroSplat.Config.GetTextureConfig(texture);
            if (texcfg == null) continue;
            texcfg.IsUsedByBiome = true;
            int index = cfg.LayerIndex;
            while (index >= layers.Count) layers.Add(new MicroSplatProceduralTextureConfig.Layer());
            var layer = layers[index];
            var props = cfg.Props;
            layer.textureIndex = texcfg.SlotIdx;
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
            if (props.Contains("biome-weight-1")) layer.biomeWeights =
                StringParsers.ParseColor(props.GetString("biome-weight-1"));
            if (props.Contains("biome-weight-2")) layer.biomeWeights2 =
                StringParsers.ParseColor(props.GetString("biome-weight-2"));
            if (cfg.Heights != null) layer.heightCurve.keys = cfg.Heights.ToArray();
            if (cfg.Slopes != null) layer.slopeCurve.keys = cfg.Slopes.ToArray();
        }
    }


    // ####################################################################
    // ####################################################################

    private static List<Keyframe> ParseBiomeLayer(XElement xml,
        string name, List<Keyframe> frames = null)
    {
        foreach (XElement outer in xml.Elements())
        {
            frames = new List<Keyframe>();
            foreach (XElement child in outer.Elements())
            {
                if (!child.Name.Equals("keyframe")) continue;
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
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        Props = MicroSplatXmlConfig.GetDynamicProperties(xml, Props);
        Heights = ParseBiomeLayer(xml, "height-keyframes", Heights);
        Slopes = ParseBiomeLayer(xml, "slope-keyframes", Slopes);
        // ParseBiomeLayer(xml, "cavity-keyframes", Cavities);
        // ParseBiomeLayer(xml, "erosion-keyframes", Erosions);
    }

    // ####################################################################
    // ####################################################################

}
