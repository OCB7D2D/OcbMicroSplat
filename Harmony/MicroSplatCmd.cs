using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using static MicroSplatPropData;
using static StringParsers;

public class OcbMicroSplatCmd : ConsoleCmdAbstract
{

    private static string info = "microsplat";
    protected override string[] getCommands()
    {
        return new string[2] { info, "msplat" };
    }

    protected override string getDescription()
    {
        return "Adjust settings for MicroSplat shaders";
    }

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;

    public static Vector4 ParseVector4(string _input)
    {
        SeparatorPositions separatorPositions = GetSeparatorPositions(_input, ',', 3);
        if (separatorPositions.TotalFound != 3) return Vector4.zero;
        return new Vector4(ParseFloat(_input, 0, separatorPositions.Sep1 - 1),
            ParseFloat(_input, separatorPositions.Sep1 + 1, separatorPositions.Sep2 - 1),
            ParseFloat(_input, separatorPositions.Sep2 + 1, separatorPositions.Sep3 - 1),
            ParseFloat(_input, separatorPositions.Sep3 + 1));
    }

    private AnimationCurve ParseAnimationKeys(string v)
    {
        var curve = new AnimationCurve();
        foreach (var pair in v.Split(';'))
        {
            var frame = Array.ConvertAll(pair.Split(','),
                value => float.Parse(value));
            switch (frame.Length)
            {
                case 2: curve.AddKey(new Keyframe(frame[0], frame[1])); break;
                case 4: curve.AddKey(new Keyframe(frame[0], frame[1], frame[2], frame[3])); break;
                case 6: curve.AddKey(new Keyframe(frame[0], frame[1], frame[2], frame[3], frame[4], frame[6])); break;
                default: Log.Warning("Invalid number of parameters for keyframe"); break;
            }
        }
        return curve;
    }

    void SetMatProperty(Material mat, string type, string name, string value)
    {
        if (mat.HasProperty(name))
        {
            switch (type)
            {
                case "f":
                case "float":
                    mat.SetFloat(name, ParseFloat(value));
                    break;
                case "v2":
                case "vec2":
                case "vector2":
                    mat.SetVector(name, ParseVector2(value));
                    break;
                case "v3":
                case "vec3":
                case "vector3":
                    mat.SetVector(name, ParseVector3(value));
                    break;
                case "v4":
                case "vec4":
                case "vector4":
                    mat.SetVector(name, ParseVector4(value));
                    break;
            }
        }
        else
        {
            Log.Warning("Material {0} missing {1}",
                mat.name, name);
        }
    }

    object GetMatProperty(Material mat, string type, string name)
    {
        if (mat.HasProperty(name))
        {
            switch (type)
            {
                case "f":
                case "float":
                    return mat.GetFloat(name);
                case "v2":
                case "vec2":
                case "vector2":
                case "v3":
                case "vec3":
                case "vector3":
                case "v4":
                case "vec4":
                case "vector4":
                    return mat.GetVector(name);
                default:
                    return $"Unknown type {type}";
            }
        }
        return $"missing {name}";
    }

    static readonly HarmonyFieldProxy<MicroSplatPropData> VoxelMeshTerrainPropData =
        new HarmonyFieldProxy<MicroSplatPropData>(typeof(VoxelMeshTerrain), "msPropData");
    static readonly HarmonyFieldProxy<MicroSplatProceduralTextureConfig> VoxelMeshTerrainProcData =
        new HarmonyFieldProxy<MicroSplatProceduralTextureConfig>(typeof(VoxelMeshTerrain), "msProcData");

    static readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainProcCurveTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msProcCurveTex");
    static readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainProcParamTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msProcParamTex");

    public float GetValue(MicroSplatPropData prop, int textureIndex, MicroSplatPropData.PerTexFloat channel)
    {
        int y = (int)((double)channel / 4.0);
        int channel1 = Mathf.RoundToInt((int)channel - y * 4);
        // Mathf.RoundToInt((float)(((double)(y) - (double)y) * 4.0));
        // Log.Out("Getting value {0} -> {1}[{2}]", (int)channel, y, channel1);
        return prop.GetValue(textureIndex, y)[channel1];
    }

    void DumpeShaderConfig(Material mat)
    {
        Log.Out("NoiseHeight: {0}", mat.GetTexture("_NoiseHeight"));
        Log.Out("NoiseHeightData: {0}", mat.GetVector("_NoiseHeightData"));
        Log.Out("WorldHeightRange: {0}", mat.GetVector("_WorldHeightRange"));
        Log.Out("DistanceResampleAlbedoStrength: {0}", mat.GetFloat("_DistanceResampleAlbedoStrength"));
        Log.Out("DistanceResampleNormalStrength: {0}", mat.GetFloat("_DistanceResampleNormalStrength"));
        Log.Out("DistanceResampleMaterialStrength: {0}", mat.GetFloat("_DistanceResampleMaterialStrength"));
        Log.Out("NormalNoise: {0}", mat.GetTexture("_NormalNoise"));
        Log.Out("NormalNoise2: {0}", mat.GetTexture("_NormalNoise2"));
        Log.Out("NormalNoise3: {0}", mat.GetTexture("_NormalNoise3"));
        Log.Out("NormalNoiseScaleStrength: {0}", mat.GetVector("_NormalNoiseScaleStrength"));
        Log.Out("NormalNoiseScaleStrength2: {0}", mat.GetVector("_NormalNoiseScaleStrength2"));
        Log.Out("NormalNoiseScaleStrength3: {0}", mat.GetVector("_NormalNoiseScaleStrength3"));
        Log.Out("DetailNoise: {0}", mat.GetTexture("_DetailNoise"));
        Log.Out("DistanceNoise: {0}", mat.GetTexture("_DistanceNoise"));
        Log.Out("ResampleDistanceParams: {0}", mat.GetVector("_ResampleDistanceParams"));
        Log.Out("DetailNoiseScaleStrengthFade: {0}", mat.GetVector("_DetailNoiseScaleStrengthFade"));
        Log.Out("DistanceNoiseScaleStrengthFade: {0}", mat.GetVector("_DistanceNoiseScaleStrengthFade"));
        Log.Out("TriplanarContrast: {0}", mat.GetFloat("_TriplanarContrast"));
        Log.Out("TriplanarUVScale: {0}", mat.GetVector("_TriplanarUVScale"));
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];

        if (_params.Count == 1)
            switch (_params[0])
            {
                case "dump":
                    Log.Out("Dumping all microsplat textures");
                    var watch = new Stopwatch();
                    watch.Start();
                    MicroSplatDump.DumpSplatMaps();
                    MicroSplatDump.DumpMicroSplat();
                    MicroSplatDump.DumpOldTerrain();
                    watch.Stop();
                    Log.Out("Export took {0} seconds",
                        watch.ElapsedMilliseconds / 1000f);
                    return;
                case "shader":
                    DumpeShaderConfig(mesh.material);
                    return;
                case "layers":
                    if (VoxelMeshTerrainProcData.Get(null) is MicroSplatProceduralTextureConfig cfg)
                    {
                        for (int i = 0; i < cfg.layers.Count; i++)
                        {
                            var layer = cfg.layers[i];
                            Log.Out("Layer {0} => {1}/{2} #{3}", i,
                                layer.biomeWeights, layer.biomeWeights2,
                                layer.textureIndex);
                        }
                    }
                    return;
            }

        if (_params.Count == 2)
            switch (_params[0])
            {
                case "prop":
                    var slot = int.Parse(_params[1]);
                    MicroSplatPropData prop = VoxelMeshTerrainPropData.Get(null);
                    foreach (PerTexFloat channel in Enum.GetValues(typeof(MicroSplatPropData.PerTexFloat)))
                    {
                        Log.Out(" {0} => {1}", channel, GetValue(prop, slot, channel));
                    }
                    return;
                case "layer":
                    var idx = int.Parse(_params[1]);
                    var data = VoxelMeshTerrainProcData.Get(null);
                    var layer = data.layers[idx];
                    Log.Out("<property name=\"weight\" value=\"{0}\"/>", layer.weight);
                    Log.Out("<property name=\"biome-weight-1\" value=\"{0}\"/>", layer.biomeWeights);
                    Log.Out("<property name=\"biome-weight-2\" value=\"{0}\"/>", layer.biomeWeights2);
                    Log.Out("<property name=\"noise-active\" value=\"{0}\"/>", layer.noiseActive);
                    Log.Out("<property name=\"noise-frequency\" value=\"{0}\"/>", layer.noiseFrequency);
                    Log.Out("<property name=\"noise-offset\" value=\"{0}\"/>", layer.noiseOffset);
                    Log.Out("<property name=\"noise-range\" value=\"{0}\"/>", layer.noiseRange);
                    Log.Out("<property name=\"microsplat-index\" value=\"{0}\"/>", layer.textureIndex);
                    Log.Out("<property name=\"slope-active\" value=\"{0}\"/>", layer.slopeActive);
                    Log.Out("<property name=\"slope-curve-mode\" value=\"{0}\"/>", layer.slopeCurveMode);
                    if (layer.slopeCurve?.length >= 0)
                    {
                        Log.Out("<slope-keyframes>");
                        foreach (var frame in layer.slopeCurve.keys)
                            Log.Out("  <keyframe time=\"{0}\" value=\"{1}\" inTangent=\"{2}\" outTangent=\"{3}\" inWeight=\"{4}\" outWeight=\"{5}\" weightedMode=\"{6}\" />",
                                frame.time, frame.value, frame.inTangent, frame.outTangent, frame.inWeight, frame.outWeight, frame.weightedMode);
                        Log.Out("</slope-keyframes>");
                    }
                    Log.Out("<property name=\"height-active\" value=\"{0}\"/>", layer.heightActive);
                    Log.Out("<property name=\"height-curve-mode\" value=\"{0}\"/>", layer.heightCurveMode);
                    if (layer.heightCurve?.length >= 0)
                    {
                        Log.Out("<height-keyframes>");
                        foreach (var frame in layer.heightCurve.keys)
                            Log.Out("  <keyframe time=\"{0}\" value=\"{1}\" inTangent=\"{2}\" outTangent=\"{3}\" inWeight=\"{4}\" outWeight=\"{5}\" weightedMode=\"{6}\" />",
                                frame.time, frame.value, frame.inTangent, frame.outTangent, frame.inWeight, frame.outWeight, frame.weightedMode);
                        Log.Out("</height-keyframes>");
                    }
                    Log.Out("<property name=\"erosion-active\" value=\"{0}\"/>", layer.erosionMapActive);
                    Log.Out("<property name=\"erosion-curve-mode\" value=\"{0}\"/>", layer.erosionCurveMode);
                    Log.Out("<property name=\"cavity-active\" value=\"{0}\"/>", layer.cavityMapActive);
                    Log.Out("<property name=\"cavity-curve-mode\" value=\"{0}\"/>", layer.cavityCurveMode);
                    return;
            }

        if (_params.Count == 3)
            switch (_params[0])
            {
                case "prop":
                    MicroSplatPropData prop = VoxelMeshTerrainPropData.Get(null);
                    for (int i = 0; i < 32; i += 1)
                    {
                        prop.SetValue(i, (PerTexFloat)int.Parse(_params[1]), float.Parse(_params[2]));
                    }
                    var tex = prop.GetTexture();
                    mesh.material.SetTexture("_PerTexProps", tex);
                    mesh.materialDistant.SetTexture("_PerTexProps", tex);
                    return;
                case "get":
                    Log.Out("Showing current value of {0}", _params[0]);
                    Log.Out(" details: {0}", GetMatProperty(mesh.material, _params[1], _params[2]));
                    Log.Out(" distant: {0}", GetMatProperty(mesh.material, _params[1], _params[2]));
                    return;
                case "splat0":
                    int channel = int.Parse(_params[1]);
                    int value = int.Parse(_params[2]);
                    string levelName = GamePrefs.GetString(EnumGamePrefs.GameWorld)?.Trim();
                    var worldPath = PathAbstractions.WorldsSearchPaths.GetLocation(levelName);
                    Texture2D splat3 = TextureUtils.LoadTexture(worldPath.FullPath + "/splat3_processed.png");
                    if (value >= 0)
                    {
                        var pixels = splat3.GetPixels();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            pixels[i][channel] = value;
                        }
                        splat3.SetPixels(pixels);
                        splat3.Apply(true, false);
                    }
                    mesh.materialDistant.SetTexture("_CustomControl0", splat3);
                    mesh.material.SetTexture("_CustomControl0", splat3);
                    return;
            }
        if (_params.Count == 4)
            switch (_params[0])
            {
                case "set":
                    // E.g. set v4 _SnowDistanceResampleScaleStrengthFade 1,2,3,4
                    SetMatProperty(mesh.material, _params[1], _params[2], _params[3]);
                    SetMatProperty(mesh.materialDistant, _params[1], _params[2], _params[3]);
                    return;
                case "layer":
                    var idx = int.Parse(_params[1]);
                    var data = VoxelMeshTerrainProcData.Get(null);
                    var layer = data.layers[idx];
                    switch (_params[2])
                    {
                        case "weight": layer.weight = float.Parse(_params[3]); break;
                        case "noise-active": layer.noiseActive = bool.Parse(_params[3]); break;
                        case "noise-frequency": layer.noiseFrequency = float.Parse(_params[3]); break;
                        case "noise-offset": layer.noiseOffset = float.Parse(_params[3]); break;
                        case "noise-range": layer.noiseRange = StringParsers.ParseVector2(_params[3]); break;
                        case "height-active": layer.heightActive = bool.Parse(_params[3]); break;
                        case "slope-active": layer.slopeActive = bool.Parse(_params[3]); break;
                        case "erosion-active": layer.erosionMapActive = bool.Parse(_params[3]); break;
                        case "cavity-active": layer.cavityMapActive = bool.Parse(_params[3]); break;
                        case "height-curve-mode": layer.heightCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "slope-curve-mode": layer.slopeCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "erosion-curve-mode": layer.erosionCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "cavity-curve-mode": layer.cavityCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "microsplat-index": layer.textureIndex = int.Parse(_params[3]); break;
                        case "biome-weight-1": layer.biomeWeights = ParseVector4(_params[3]); break;
                        case "biome-weight-2": layer.biomeWeights2 = ParseVector4(_params[3]); break;
                        case "slope-keyframes": layer.slopeCurve = ParseAnimationKeys(_params[3]); break;
                        case "height-keyframes": layer.heightCurve = ParseAnimationKeys(_params[3]); break;
                        case "erosion-keyframes": layer.erosionMapCurve = ParseAnimationKeys(_params[3]); break;
                        case "cavity-keyframes": layer.cavityMapCurve = ParseAnimationKeys(_params[3]); break;
                        default: Log.Warning("Unknown param " + _params[2]); break;
                    }
                    var curveTex = data.GetCurveTexture();
                    var paramTex = data.GetParamTexture();
                    //MicroSplatBiome._msProcCurveTex = curveTex;
                    //MicroSplatBiome._msProcParamTex = paramTex;
                    VoxelMeshTerrainProcCurveTex.Set(null, curveTex);
                    VoxelMeshTerrainProcParamTex.Set(null, paramTex);
                    mesh.material.SetTexture("_ProcTexCurves", curveTex);
                    mesh.material.SetTexture("_ProcTexParams", paramTex);
                    mesh.materialDistant.SetTexture("_ProcTexCurves", curveTex);
                    mesh.materialDistant.SetTexture("_ProcTexParams", paramTex);
                    // DynamicMeshManager.Instance.RefreshAll();
                    // mesh.ReloadTextureArrays(true);
                    return;
            }

        // If not returned by now we have not done
        Log.Warning("Invalid `microsplat` command");

    }

}
