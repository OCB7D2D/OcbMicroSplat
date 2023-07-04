using System.Xml.Linq;
using UnityEngine;
using static StringParsers;

public class MicroSplatShader
{

    // ####################################################################
    // ####################################################################

    public int MaxTextures = 24;

    public int TerQuality = -1;

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
    private DataLoader.DataPathIdentifier PathTexNoiseDetail;
    private DataLoader.DataPathIdentifier PathTexNoiseDistant;
    private DataLoader.DataPathIdentifier PathTexNoisePerlin;
    private DataLoader.DataPathIdentifier PathTexNoiseNormal1;
    private DataLoader.DataPathIdentifier PathTexNoiseNormal2;
    private DataLoader.DataPathIdentifier PathTexNoiseNormal3;

    // ####################################################################
    // ####################################################################

    private Vector2 WorldHeightRange = new Vector2(0, 250f);
    private Vector2 NoiseHeightData = new Vector2(1, 0.15f);

    private Vector2 NoiseNormal1Params = new Vector2(0.113f, 0.223f);
    private Vector2 NoiseNormal2Params = new Vector2(0.096f, 0.197f);
    private Vector2 NoiseNormal3Params = new Vector2(0.081f, 0.173f);

    private float DistanceResampleAlbedoStrength = 1;
    private float DistanceResampleNormalStrength = 1;
    private float DistanceResampleMaterialStrength = 1;

    private Vector3 ResampleDistanceParams = new Vector4(0.25f, 0.5f, 100);
    // Interestingly detail noise is really just a vec3 and distance vec4
    // Distance has an additional factor stored in the `w` component
    private Vector3 DetailNoiseScaleStrengthFade = new Vector4(4, 0.5f, 5);
    private Vector4 DistanceNoiseScaleStrengthFade = new Vector4(0.25f, 0.5f, 100, 250);

    private float TriplanarContrast = 8;
    private Vector4 TriplanarUVScale = new Vector4(1, 1, 1, 1);

    // ####################################################################
    // ####################################################################

    private DataLoader.DataPathIdentifier GetPath(
        DynamicProperties props, string name)
    {
        if (props.Values.TryGetString(name, out var path))
            return DataLoader.ParseDataPathIdentifier(path);
        return DataLoader.ParseDataPathIdentifier(null);
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
        if (props.Values.TryGetString(_propName, out var _value)) vector = ParseVector4(_value);
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
        if (TerQuality == GamePrefs.GetInt(EnumGamePrefs.OptionsGfxTerrainQuality)) return;
        TerQuality = GamePrefs.GetInt(EnumGamePrefs.OptionsGfxTerrainQuality);
        Shader ShaderDetail = null;
        Shader ShaderDistant = null;
        // Try to load the shader assets
        LoadAsset(PathShaderDetail, ref ShaderDetail, false);
        LoadAsset(PathShaderDistant, ref ShaderDistant, false);
        // Give some debug messages to the console to check
        Log.Out("Loaded detail shader: {0}", ShaderDetail?.name);
        Log.Out("Loaded distance shader: {0}", ShaderDistant?.name);
        // Use the new loaded shaders for terrain
        terrain.material.shader = ShaderDetail;
        terrain.materialDistant.shader = ShaderDistant;
    }

    // ####################################################################
    // ####################################################################

    private void InitMicroSplatMaterial(Material mat)
    {
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
    }

    public void WorldChanged(MeshDescription terrain)
    {
        Log.Out("=============================================");
        Log.Out($"Load/enable custom microsplat shaders ({MaxTextures})");
        Log.Out("=============================================");
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
        PathShaderDetail = GetPath(props, "ShaderDetail");
        PathShaderDistant = GetPath(props, "ShaderDistant");
        PathTexNoisePerlin = GetPath(props, "NoisePerlin");
        PathTexNoiseDetail = GetPath(props, "NoiseDetail");
        PathTexNoiseDistant = GetPath(props, "NoiseDistant");
        PathTexNoiseNormal1 = GetPath(props, "NoiseNormal1");
        PathTexNoiseNormal2 = GetPath(props, "NoiseNormal2");
        PathTexNoiseNormal3 = GetPath(props, "NoiseNormal3");
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
