using System.Xml.Linq;
using UnityEngine;
using static StringParsers;

public class MicroSplatShader
{

    // ####################################################################
    // ####################################################################

    public int MaxTextures = 24;

    // ####################################################################
    // ####################################################################

    private Texture2D TexNoiseDetail;
    private Texture2D TexNoiseDistant;
    private Texture2D TexNoisePerlin;
    private Texture2D TexNoiseNormal1;
    private Texture2D TexNoiseNormal2;
    private Texture2D TexNoiseNormal3;

    // ####################################################################
    // ####################################################################

    private DataLoader.DataPathIdentifier PathShaderDetail;
    private DataLoader.DataPathIdentifier PathShaderDistant;
    private DataLoader.DataPathIdentifier MetalShaderDetail;
    private DataLoader.DataPathIdentifier MetalShaderDistant;
    private DataLoader.DataPathIdentifier PathShaderDetailTess;
    private DataLoader.DataPathIdentifier PathShaderDistantTess;
    private DataLoader.DataPathIdentifier MetalShaderDetailTess;
    private DataLoader.DataPathIdentifier MetalShaderDistantTess;
    private DataLoader.DataPathIdentifier PathTexNoiseDetail;
    private DataLoader.DataPathIdentifier PathTexNoiseDistant;
    private DataLoader.DataPathIdentifier PathTexNoisePerlin;
    private DataLoader.DataPathIdentifier PathTexNoiseNormal1;
    private DataLoader.DataPathIdentifier PathTexNoiseNormal2;
    private DataLoader.DataPathIdentifier PathTexNoiseNormal3;

    // ####################################################################
    // ####################################################################

    private Vector2 NoiseHeightData = new Vector2(0.5f, 0.275f);
    private Vector2 WorldHeightRange = new Vector2(0.0f, 500f);

    private Vector2 NoiseNormal1Params = new Vector2(0.6f, 0.225f);
    private Vector2 NoiseNormal2Params = new Vector2(0.325f, 0.175f);
    private Vector2 NoiseNormal3Params = new Vector2(0.1350f, 0.125f);

    private float DistanceResampleAlbedoStrength = 0.85f;
    private float DistanceResampleNormalStrength = 0.65f;
    private float DistanceResampleMaterialStrength = 0.35f;

    private Vector3 ResampleDistanceParams = new Vector4(0.25f, 0.5f, 100);
    // Interestingly detail noise is really just a vec3 and distance vec4
    // Distance has an additional factor stored in the `w` component
    private Vector3 DetailNoiseScaleStrengthFade = new Vector4(4.0f, 0.5f, 75.0f);
    private Vector4 DistanceNoiseScaleStrengthFade = new Vector4(0.25f, 0.5f, 100f, 500f);

    private float TriplanarContrast = 8;
    private Vector4 TriplanarUVScale = new Vector4(1, 1, 1, 1);

    // ####################################################################
    // ####################################################################

    private DataLoader.DataPathIdentifier GetPath(
        DynamicProperties props, string name, string def = null)
    {
        if (props.Values.TryGetValue(name, out var path))
            return DataLoader.ParseDataPathIdentifier(path);
        return DataLoader.ParseDataPathIdentifier(def);
    }

    public static Vector4 ParseVector4(string _input)
    {
        SeparatorPositions separatorPositions = GetSeparatorPositions(_input, ',', 3);
        if (separatorPositions.TotalFound != 3) return Vector4.zero;
        return new Vector4(ParseFloat(_input, 0, separatorPositions.Sep1 - 1),
            ParseFloat(_input, separatorPositions.Sep1 + 1, separatorPositions.Sep2 - 1),
            ParseFloat(_input, separatorPositions.Sep2 + 1, separatorPositions.Sep3 - 1),
            ParseFloat(_input, separatorPositions.Sep3 + 1));
    }

    private void ParseVector4(DynamicProperties props, string _propName, ref Vector4 vector)
    {
        if (props.Values.TryGetValue(_propName, out var _value)) vector = ParseVector4(_value);
    }

    // ####################################################################
    // ####################################################################

    static string GetShaderQuality()
    {
        switch (GamePrefs.GetInt(EnumGamePrefs.OptionsGfxTerrainQuality))
        {
            case 0: return "Low";
            case 1: return "Low";
            case 2: return "Med";
            case 3: return "High";
            case 4: return "Ultra";
            default: return "Med";
        }
    }

    private void LoadAsset<T>(DataLoader.DataPathIdentifier path,
        ref T asset, bool reset = true) where T : Object
    {
        if (reset) asset = null; // Reset first?
        if (!string.IsNullOrEmpty(path.AssetName))
        {
            string name = string.Format(path.AssetName, GetShaderQuality(), MaxTextures);
            if (asset != null && asset.name == name) return; // Still valid?
            Log.Out("Loading {0}?{1}", System.IO.Path.GetFileName(path.BundlePath), name);
            AssetBundleManager.Instance.LoadAssetBundle(path.BundlePath);
            asset = AssetBundleManager.Instance.Get<T>(path.BundlePath, name);
            if (asset == null) Log.Warning("Failed to load {0}", name);
            if (asset != null) asset.name = name;
        }
    }

    // ####################################################################
    // ####################################################################

    private void LoadTextures()
    {
        LoadAsset(PathTexNoisePerlin, ref TexNoisePerlin, false);
        LoadAsset(PathTexNoiseDetail, ref TexNoiseDetail, false);
        LoadAsset(PathTexNoiseDistant, ref TexNoiseDistant, false);
        LoadAsset(PathTexNoiseNormal1, ref TexNoiseNormal1, false);
        LoadAsset(PathTexNoiseNormal2, ref TexNoiseNormal2, false);
        LoadAsset(PathTexNoiseNormal3, ref TexNoiseNormal3, false);
    }

    private void UnloadTexture(ref Texture2D texture)
    {
        // This seems to cause issues!?
        // Resources.UnloadAsset(texture);
        // Release the reference
        texture = null;
    }

    private void UnloadTextures()
    {
        UnloadTexture(ref TexNoisePerlin);
        UnloadTexture(ref TexNoiseDetail);
        UnloadTexture(ref TexNoiseDistant);
        UnloadTexture(ref TexNoiseNormal1);
        UnloadTexture(ref TexNoiseNormal2);
        UnloadTexture(ref TexNoiseNormal3);
    }

    // ####################################################################
    // ####################################################################

    public void LoadTerrainShaders(MeshDescription terrain)
    {
        // Skip using our shader e.g. in prefab editor
        if (GameManager.IsSplatMapAvailable() == false) return;
        Shader ShaderDetail = null; Shader ShaderDistant = null;
        // Try to load the shader assets (may load from platform specific asset bundle)
        bool IsMetal = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal;
        if (PlayerPrefs.GetInt("TerrainTessellation") > 0) {
            LoadAsset(IsMetal ? MetalShaderDetailTess : PathShaderDetailTess, ref ShaderDetail, false);
            LoadAsset(IsMetal ? MetalShaderDistantTess : PathShaderDistantTess, ref ShaderDistant, false);
        } else {
            LoadAsset(IsMetal ? MetalShaderDetail : PathShaderDetail, ref ShaderDetail, false);
            LoadAsset(IsMetal ? MetalShaderDistant : PathShaderDistant, ref ShaderDistant, false);
        }
        // Give error messages to the console if loading failed
        if (ShaderDetail == null) Log.Error("Could not load custom detail shader: {0}/{1}",
            PathShaderDetail.BundlePath, PathShaderDetail.AssetName);
        if (ShaderDistant == null) Log.Error("Could not load custom distant shader: {0}/{1}",
            PathShaderDistant.BundlePath, PathShaderDistant.AssetName);
        // Use the new loaded shaders for terrain if found in resources
        if (ShaderDetail != null && terrain.material != null)
            terrain.material.shader = ShaderDetail;
        if (ShaderDistant != null && terrain.materialDistant != null)
            terrain.materialDistant.shader = ShaderDistant;
        TessellationOption.ApplyTerrainOptions();
    }

    // ####################################################################
    // ####################################################################

    public void InitMicroSplatMaterial(Material mat)
    {
        if (mat == null) return;
        mat.SetTexture("_NoiseHeight", TexNoisePerlin);
        mat.SetVector("_NoiseHeightData", NoiseHeightData);
        mat.SetVector("_WorldHeightRange", WorldHeightRange);
        mat.SetFloat("_DistanceResampleAlbedoStrength", DistanceResampleAlbedoStrength);
        mat.SetFloat("_DistanceResampleNormalStrength", DistanceResampleNormalStrength);
        mat.SetFloat("_DistanceResampleMaterialStrength", DistanceResampleMaterialStrength);
        mat.SetTexture("_NormalNoise", TexNoiseNormal1);
        mat.SetTexture("_NormalNoise2", TexNoiseNormal2);
        mat.SetTexture("_NormalNoise3", TexNoiseNormal3);
        mat.SetVector("_NormalNoiseScaleStrength", NoiseNormal1Params);
        mat.SetVector("_NormalNoiseScaleStrength2", NoiseNormal2Params);
        mat.SetVector("_NormalNoiseScaleStrength3", NoiseNormal3Params);
        mat.SetTexture("_DetailNoise", TexNoiseDetail);
        mat.SetTexture("_DistanceNoise", TexNoiseDistant);
        mat.SetVector("_ResampleDistanceParams", ResampleDistanceParams);
        mat.SetVector("_DetailNoiseScaleStrengthFade", DetailNoiseScaleStrengthFade);
        mat.SetVector("_DistanceNoiseScaleStrengthFade", DistanceNoiseScaleStrengthFade);
        mat.SetFloat("_TriplanarContrast", TriplanarContrast);
        mat.SetVector("_TriplanarUVScale", TriplanarUVScale);
        var config = OcbMicroSplat.Config.MicroSplatWorldConfig;
        mat.SetInt("_PCLayerCount", config.BiomeLayers.Count);
    }

    public void WorldChanged(MeshDescription terrain)
    {
        #if DEBUG
        Log.Out("=============================================");
        Log.Out($"Load/enable custom microsplat shaders ({MaxTextures})");
        Log.Out("=============================================");
        #endif
        // Play safe, just in case
        if (terrain == null) return;
        // Load quality terrain shader
        LoadTerrainShaders(terrain);
        // Load resources now
        LoadTextures();
        // Apply config to the newly assigned shader
        InitMicroSplatMaterial(terrain.material);
        InitMicroSplatMaterial(terrain.materialDistant);
        // Unload once assigned
        UnloadTextures();
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement child)
    {
        var props = MicroSplatXmlConfig.GetDynamicProperties(child);
        PathShaderDetail = GetPath(props, "ShaderDetail", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}Vertex");
        PathShaderDistant = GetPath(props, "ShaderDistant", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}Distant");
        MetalShaderDetail = GetPath(props, "MetalShaderDetail", "#@modfolder:Resources/OcbMicroSplat.metal.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}Vertex");
        MetalShaderDistant = GetPath(props, "MetalShaderDistant", "#@modfolder:Resources/OcbMicroSplat.metal.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}Distant");
        PathShaderDetailTess = GetPath(props, "ShaderDetailTess", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}VertexTess");
        PathShaderDistantTess = GetPath(props, "ShaderDistantTess", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}DistantTess");
        MetalShaderDetailTess = GetPath(props, "MetalShaderDetailTess", "#@modfolder:Resources/OcbMicroSplat.metal.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}VertexTess");
        MetalShaderDistantTess = GetPath(props, "MetalShaderDistantTess", "#@modfolder:Resources/OcbMicroSplat.metal.unity3d?assets/OcbMicroSplat/OcbMicroSplat{1}{0}DistantTess");
        PathTexNoisePerlin = GetPath(props, "NoisePerlin", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/microsplat_def_perlin");
        PathTexNoiseDetail = GetPath(props, "NoiseDetail", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/microsplat_def_detail_noise");
        PathTexNoiseDistant = GetPath(props, "NoiseDistant", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/microsplat_def_detail_noise");
        PathTexNoiseNormal1 = GetPath(props, "NoiseNormal1", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/microsplat_def_detail_normal_01");
        PathTexNoiseNormal2 = GetPath(props, "NoiseNormal2", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/microsplat_def_detail_normal_02");
        PathTexNoiseNormal3 = GetPath(props, "NoiseNormal3", "#@modfolder:Resources/OcbMicroSplat.unity3d?assets/OcbMicroSplat/microsplat_def_detail_normal_03");
        props.ParseVec("NoiseHeightData", ref NoiseHeightData);
        props.ParseVec("WorldHeightRange", ref WorldHeightRange);
        props.ParseVec("NoiseNormal1Params", ref NoiseNormal1Params);
        props.ParseVec("NoiseNormal2Params", ref NoiseNormal2Params);
        props.ParseVec("NoiseNormal3Params", ref NoiseNormal3Params);
        props.ParseVec("ResampleDistanceParams", ref ResampleDistanceParams);
        props.ParseVec("DetailNoiseScaleStrengthFade", ref DetailNoiseScaleStrengthFade);
        ParseVector4(props, "DistanceNoiseScaleStrengthFade", ref DistanceNoiseScaleStrengthFade);
        props.ParseFloat("DistanceResampleAlbedoStrength", ref DistanceResampleAlbedoStrength);
        props.ParseFloat("DistanceResampleNormalStrength", ref DistanceResampleNormalStrength);
        props.ParseFloat("DistanceResampleMaterialStrength", ref DistanceResampleMaterialStrength);
        props.ParseFloat("TriplanarContrast", ref TriplanarContrast);
        ParseVector4(props, "TriplanarUVScale", ref TriplanarUVScale);
    }

    // ####################################################################
    // ####################################################################

}
