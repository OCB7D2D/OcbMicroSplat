using UnityEngine;
using static StringParsers;

public class MicroSplatTerrainConfig
{

    readonly DataLoader.DataPathIdentifier PathShaderDetail;
    readonly DataLoader.DataPathIdentifier PathShaderDistant;
    readonly DataLoader.DataPathIdentifier PathTexNoiseDetail;
    readonly DataLoader.DataPathIdentifier PathTexNoiseDistant;
    readonly DataLoader.DataPathIdentifier PathTexNoisePerlin;
    readonly DataLoader.DataPathIdentifier PathTexNoiseNormal1;
    readonly DataLoader.DataPathIdentifier PathTexNoiseNormal2;
    readonly DataLoader.DataPathIdentifier PathTexNoiseNormal3;

    private Shader ShaderDetail;
    private Shader ShaderDistant;
    private Texture2D TexNoiseDetail;
    private Texture2D TexNoiseDistant;
    private Texture2D TexNoisePerlin;
    private Texture2D TexNoiseNormal1;
    private Texture2D TexNoiseNormal2;
    private Texture2D TexNoiseNormal3;

    private float TriplanarContrast = 8;
    private float DistanceResampleAlbedoStrength = 1;
    private float DistanceResampleNormalStrength = 1;

    private Vector2 NoiseNormal1Params = new Vector2(0.113f, 0.223f);
    private Vector2 NoiseNormal2Params = new Vector2(0.096f, 0.197f);
    private Vector2 NoiseNormal3Params = new Vector2(0.081f, 0.173f);

    private Vector2 WorldHeightRange = new Vector2(0, 250f);
    private Vector2 NoiseHeightData = new Vector2(1, 0.15f);

    private Vector3 ResampleDistanceParams = new Vector4(0.25f, 0.5f, 100);

    private Vector3 DetailNoiseScaleStrengthFade = new Vector4(4, 0.5f, 5);

    private Vector4 DistanceNoiseScaleStrengthFade = new Vector4(0.25f, 0.5f, 100, 250);

    private Vector4 TriplanarUVScale = new Vector4(1, 1, 1, 1);

    private DataLoader.DataPathIdentifier GetPath(
        DynamicProperties props, string name)
    {
        if (props.Values.TryGetString(name, out var path))
            return DataLoader.ParseDataPathIdentifier(path);
        return DataLoader.ParseDataPathIdentifier(null);
    }

    public MicroSplatTerrainConfig(DynamicProperties props)
    {
        PathShaderDetail = GetPath(props, "ShaderDetail");
        PathShaderDistant = GetPath(props, "ShaderDistant");
        PathTexNoiseDetail = GetPath(props, "NoiseDetail");
        PathTexNoiseDistant = GetPath(props, "NoiseDistant");
        PathTexNoisePerlin = GetPath(props, "NoisePerlin");
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
        props.ParseFloat("TriplanarContrast", ref TriplanarContrast);
        props.ParseFloat("DistanceResampleAlbedoStrength", ref DistanceResampleAlbedoStrength);
        props.ParseFloat("DistanceResampleNormalStrength", ref DistanceResampleNormalStrength);
        ParseVector4(props, "TriplanarUVScale", ref TriplanarUVScale);
        ParseVector4(props, "DistanceNoiseScaleStrengthFade", ref DistanceNoiseScaleStrengthFade);
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
            string name = string.Format(path.AssetName, GetShaderQuality());
            if (asset != null && asset.name == name) return; // Still valid?
            AssetBundleManager.Instance.LoadAssetBundle(path.BundlePath);
            asset = AssetBundleManager.Instance.Get<T>(path.BundlePath, name);
            if (asset == null) Log.Warning("Failed to load {0}", name);
            if (asset != null) asset.name = name;
        }
    }

    private void LoadResources()
    {
        LoadAsset(PathShaderDetail, ref ShaderDetail, false);
        LoadAsset(PathShaderDistant, ref ShaderDistant, false);
        LoadAsset(PathTexNoiseDetail, ref TexNoiseDetail, false);
        LoadAsset(PathTexNoiseDistant, ref TexNoiseDistant, false);
        LoadAsset(PathTexNoisePerlin, ref TexNoisePerlin, false);
        LoadAsset(PathTexNoiseNormal1, ref TexNoiseNormal1, false);
        LoadAsset(PathTexNoiseNormal2, ref TexNoiseNormal2, false);
        LoadAsset(PathTexNoiseNormal3, ref TexNoiseNormal3, false);
    }

    static void ReplaceShaders(Shader src, Shader dst)
    {
        if (src == dst) return;
        // Find all materials that are already using the src shader
        foreach (var mat in Object.FindObjectsOfType<Material>())
            if (mat.shader == src) mat.shader = dst;
    }

    private void ApplyToMaterial(Material mat, Shader shader)
    {
        if (shader != null) ReplaceShaders(mat.shader, shader);
        mat.SetTexture("_NoiseHeight", TexNoisePerlin);
        mat.SetVector("_NoiseHeightData", NoiseHeightData);
        mat.SetVector("_NormalNoiseScaleStrength", NoiseNormal1Params);
        mat.SetVector("_NormalNoiseScaleStrength2", NoiseNormal2Params);
        mat.SetVector("_NormalNoiseScaleStrength3", NoiseNormal3Params);
        mat.SetTexture("_NormalNoise", TexNoiseNormal1);
        mat.SetTexture("_NormalNoise2", TexNoiseNormal2);
        mat.SetTexture("_NormalNoise3", TexNoiseNormal3);
        mat.SetFloat("_DistanceResampleAlbedoStrength", DistanceResampleAlbedoStrength);
        mat.SetFloat("_DistanceResampleNormalStrength", DistanceResampleNormalStrength);
        mat.SetVector("_WorldHeightRange", WorldHeightRange);
        mat.SetVector("_ResampleDistanceParams", ResampleDistanceParams);
        mat.SetTexture("_DetailNoise", TexNoiseDetail);
        mat.SetTexture("_DistanceNoise", TexNoiseDistant);
        mat.SetVector("_DetailNoiseScaleStrengthFade", DetailNoiseScaleStrengthFade);
        mat.SetVector("_DistanceNoiseScaleStrengthFade", DistanceNoiseScaleStrengthFade);
        mat.SetFloat("_TriplanarContrast", TriplanarContrast);
        mat.SetVector("_TriplanarUVScale", TriplanarUVScale);
    }

    public void ApplyToTerrain(MeshDescription terrain)
    {
        LoadResources();
        ApplyToMaterial(terrain.material, ShaderDetail);
        ApplyToMaterial(terrain.materialDistant, ShaderDistant);
    }

}
