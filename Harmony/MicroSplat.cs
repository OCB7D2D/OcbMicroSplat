using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

public class OcbMicroSplat : IModApi
{

    static string AssetBundlePath;

    public static string DecalBundlePath;

    public static MicroSplatXmlConfig Config
        = new MicroSplatXmlConfig();

    // Maximum textures for shader variant to load
    static int MaxTextures = 24;

    public void InitMod(Mod mod)
    {
		Log.Out("OCB Harmony Patch: " + GetType().ToString());
		Harmony harmony = new Harmony(GetType().ToString());
		harmony.PatchAll(Assembly.GetExecutingAssembly());
        AssetBundlePath = System.IO.Path.Combine(mod.Path, "Resources/OcbMicroSplat.unity3d");
        DecalBundlePath = System.IO.Path.Combine(mod.Path, "Resources/OcbDecalShader.unity3d");
    }

    // Our AccessTools is too old and doesn't have this
    // Modern HarmonyX has `AccessTool.EnumeratorMoveNext`
    public static MethodInfo GetEnumeratorMoveNext(MethodBase method)
    {
        if (method is null)
        {
            Log.Out("AccessTools.EnumeratorMoveNext: method is null");
            return null;
        }

        var codes = PatchProcessor.ReadMethodBody(method).Where(pair => pair.Key == OpCodes.Newobj);
        if (codes.Count() != 1)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no Newobj opcode");
            return null;
        }
        var ctor = codes.First().Value as ConstructorInfo;
        if (ctor == null)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no constructor");
            return null;
        }
        var type = ctor.DeclaringType;
        if (type == null)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} refers to a global type");
            return null;
        }
        return AccessTools.Method(type, nameof(IEnumerator.MoveNext));
    }

    static void InitMicroSplatProperties(Material mat)
    {
        AssetBundleManager.Instance.LoadAssetBundle(AssetBundlePath);
        var noisePerlin = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_perlin");
        var noiseDetail = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_detail_noise");
        var noiseNormal1 = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_detail_normal_01");
        var noiseNormal2 = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_detail_normal_02");
        var noiseNormal3 = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_detail_normal_03");

        mat.SetTexture("_NoiseHeight", noisePerlin);
        mat.SetVector("_NoiseHeightData", new Vector4(1, 0.15f, 0, 0));
        mat.SetFloat("_DistanceResampleAlbedoStrength", 1);
        mat.SetFloat("_DistanceResampleNormalStrength", 1);
        mat.SetTexture("_NormalNoise", noiseNormal1);
        mat.SetVector("_NormalNoiseScaleStrength", new Vector2(0.0113f, 0.223f));
        mat.SetTexture("_NormalNoise2", noiseNormal2);
        mat.SetVector("_NormalNoiseScaleStrength2", new Vector2(0.096f, 0.197f));
        mat.SetTexture("_NormalNoise3", noiseNormal3);
        mat.SetVector("_NormalNoiseScaleStrength3", new Vector2(0.081f, 0.173f));
        mat.SetVector("_WorldHeightRange", new Vector4(0, 250, 0, 0));
        mat.SetVector("_ResampleDistanceParams", new Vector4(0.25f, 0.5f, 100, 250));
        mat.SetTexture("_DetailNoise", noiseDetail);
        mat.SetVector("_DetailNoiseScaleStrengthFade", new Vector4(4, 0.5f, 5, 0));
        mat.SetTexture("_DistanceNoise", noiseDetail);
        mat.SetVector("_DistanceNoiseScaleStrengthFade", new Vector4(0.25f, 0.5f, 100, 250));
        // _TriplanarContrast("Triplanar Contrast", Range(1.0, 8)) = 4
        // _TriplanarUVScale("Triplanar UV Scale", Vector) = (1, 1, 0, 0)
        mat.SetFloat("_TriplanarContrast", 8);
        mat.SetVector("_TriplanarUVScale", new Vector4(1, 1, 0, 0));

        /*

        var snowDiff = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_snow_diff");
        var snowNorm = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_snow_normsao");
        var windGlitter = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_windglitter");
        var noiseSnowNorm = AssetBundleManager.Instance.Get<Texture>(AssetBundlePath, "microsplat_def_snow_normalnoise");

        mat.SetTexture("_SnowDiff", snowDiff);
        mat.SetTexture("_SnowNormal", snowNorm);
        mat.SetTexture("_SnowNormalNoise", noiseSnowNorm);
        mat.SetTexture("_GlitterWind", windGlitter);

        // _SnowDistanceResampleScaleStrengthFade("Snow Distance Resample", Vector) = (0.1, 0.5, 50, 100)
        mat.SetVector("_SnowDistanceResampleScaleStrengthFade", new Vector4(0.1f, 0.5f, 50, 100));
        // _SnowRimColor("Snow Rim Light Color", Color) = (0.5, 0.5, 1, 1.0)
        mat.SetColor("_SnowRimColor", new Color(0.85f, 0.85f, 1, 1));
        // _SnowRimPower("Snow Rim Light Power", Range(0.1, 10)) = 8
        mat.SetFloat("_SnowRimPower", 0.0f);
        // _SnowSparkleTint("Snow Sparkle Tint", Color) = (1, 1, 1, 1)
        mat.SetColor("_SnowSparkleTint", new Color(1, 1, 1, 1));
        // _SnowSparkleEmission("Snow Sparkle Emission", Range(0, 2)) = 0.25
        mat.SetFloat("_SnowSparkleEmission", 0.5f);
        // _SnowSparkleStrength("Snow Sparkle Strength", Range(0, 2)) = 1
        mat.SetFloat("_SnowSparkleStrength", 0.1f);

        // _SnowSparkleNoise("Snow Sparkle Tex", 2D) = "black" { }
        mat.SetTexture("_SnowSparkleNoise", noisePerlin);

        // _SnowSparkleSize("Snow Sparkle Size", Float) = 3
        mat.SetFloat("_SnowSparkleSize", 3.0f);
        // _SnowSparkleDensity("Snow Sparkle Density", Float) = 5
        mat.SetFloat("_SnowSparkleDensity", 5.0f);
        // _SnowSparkleNoiseDensity("Snow Noise Density", Float) = 1
        mat.SetFloat("_SnowSparkleNoiseDensity", 1.0f);
        // _SnowSparkleNoiseAmplitude("Snow Noise Amplitude", Float) = 10
        mat.SetFloat("_SnowSparkleNoiseAmplitude", 10.0f);
        // _SnowSparkleViewDependency("Snow View Dependency", Float) = 0.3
        mat.SetFloat("_SnowSparkleViewDependency", 0.3f);

        // _WindParticulateRotation("Rotation", Float) = 0
        mat.SetFloat("_WindParticulateRotation", 0.0f);

        // _WindParticulateStrength("Strength", Range(0, 3)) = 1
        mat.SetFloat("_WindParticulateStrength", 0.0f);
        // _WindParticulateParams("Speed/Power/UVScale", Vector) = (1, 1, 1, 0.25)
        mat.SetVector("_WindParticulateParams", new Vector4(1, 1, 1, 0.25f));
        // _WindParticulateColor("Color, strength", Color) = (1, 1, 1, 1)
        mat.SetColor("_WindParticulateColor", new Color(0.76f, 0.58f, 0.38f, 1));
        // _WindParticulateStretch("Stretch", Float) = 0.3
        mat.SetFloat("_WindParticulateStretch", 0.5f);
        // _WindParticulateShadow("Shadow Offset/Strength/Boost", Vector) = (0.01, 1, 0, 0)
        mat.SetVector("_WindParticulateShadow", new Vector4(0.01f, 1, 0, 0));
        // _WindParticulateShadowColor("Shadow Color", Color) = (0.0, 0.0, 0.0, 1.0)
        mat.SetColor("_WindParticulateShadowColor", new Color(0, 0, 0, 1));
        // _WindParticulateHeightMask("Wind Height Mask", Vector) = (0, 0, 99999, 99999)
        mat.SetVector("_WindParticulateHeightMask", new Vector4(-99999, -99999, 99999, 99999));
        // _WindParticulateAngleMask("Wind Angle Mask", Vector) = (-1, -1, 1, 1)
        mat.SetVector("_WindParticulateAngleMask", new Vector4(-1, -1, 1, 1));
        // _WindParticulateOcclusionStrength("Wind Occlusion Strength", Range(0, 1)) = 1
        mat.SetFloat("_WindParticulateOcclusionStrength", 1f);

        // _SnowParticulateStrength("Strength", Range(0, 3)) = 1
        mat.SetFloat("_SnowParticulateStrength", 0.6f);
        // _SnowParticulateParams("Speed/Power/UVScale", Vector) = (1, 1, 1, 0.25)
        mat.SetVector("_SnowParticulateParams", new Vector4(1, 1, 1, 1.25f));
        // _SnowParticulateColor("Color, strength", Color) = (1, 1, 1, 1)
        mat.SetColor("_SnowParticulateColor", Color.white);
        // _SnowParticulateStretch("Stetch", Float) = 0.3
        mat.SetFloat("_SnowParticulateStretch", 0.2f);
        // _SnowParticulateShadow("Shadow Offset/Strength/Boost", Vector) = (0.01, 1, 0, 0)
        mat.SetVector("_SnowParticulateShadow", new Vector4(0.01f, 1, 0, 0));
        // _SnowParticulateShadowColor("Shadow Color", Color) = (0.0, 0.0, 0.0, 1.0)
        mat.SetColor("_SnowParticulateShadowColor", new Color(0, 0, 0, 1));
        // _SnowParticulateHeightMask("Snow Height Mask", Vector) = (0, 0, 99999, 99999)
        mat.SetVector("_SnowParticulateHeightMask", new Vector4(-99999, -99999, 99999, 99999));
        // _SnowParticulateAngleMask("Snow Angle Mask", Vector) = (-1, -1, 1, 1)
        mat.SetVector("_SnowParticulateAngleMask", new Vector4(-1, -1, 1, 1));
        // _SnowParticulateOcclusionStrength("Wind Occlusion Strength", Range(0, 1)) = 1
        mat.SetFloat("_SnowParticulateOcclusionStrength", 1f);

        // _WindEmissive("Wind Emissive", Vector) = (0, 0, 0, 0)
        mat.SetVector("_WindEmissive", new Vector4(0, 0, 0, 0));
        // _WindParticulateUpMask("Up Mask", Vector) = (-1, -1, 1, 1)
        mat.SetVector("_WindParticulateUpMask", new Vector4(-1, -1, 1, 1));
        // _SnowParticulateUpMask("Up Mask", Vector) = (-1, -1, 1, 1)
        mat.SetVector("_SnowParticulateUpMask", new Vector4(-1, -1, 1, 1));

        // _GlitterGraininess("Graininess", Range(0, 2)) = 0.5
        mat.SetFloat("_GlitterGraininess", 0.5f);
        // _GlitterShininess("Shininess", Range(1, 90)) = 30
        mat.SetFloat("_GlitterShininess", 6.8f);
        // _GlitterViewDep("View Dependency", Range(0, 1)) = 0.5
        mat.SetFloat("_GlitterViewDep", 0.276f);
        // _GlitterUVScale("UVScale", Vector) = (300, 300, 0, 0)
        mat.SetVector("_GlitterUVScale", new Vector4(300, 300, 0, 0));
        // _GlitterStrength("Strength", Range(0, 1)) = 1
        mat.SetFloat("_GlitterStrength", 1f);
        // _GlitterThreshold("Threshold", Range(0, 1)) = 1
        mat.SetFloat("_GlitterThreshold", 0.666f);
        // _GlitterDistFade("Distance Fade", Vector) = (10, 100, 1, 1)
        mat.SetVector("_GlitterDistFade", new Vector4(10, 100, 1, 1));

        // _SnowGlitterGraininess("Graininess", Range(0, 2)) = 1
        mat.SetFloat("_SnowGlitterGraininess", 0.7f);
        // _SnowGlitterShininess("Shininess", Range(1, 90)) = 30
        mat.SetFloat("_SnowGlitterShininess", 8f);
        // _SnowGlitterViewDep("View Dependency", Range(0, 1)) = 0.5
        mat.SetFloat("_SnowGlitterViewDep", 0.5f);
        // _SnowGlitterUVScale("UVScale", Vector) = (300, 300, 0, 0)
        mat.SetVector("_SnowGlitterUVScale", new Vector4(300, 300, 0, 0));
        // _SnowGlitterStrength("Strength", Range(0, 1)) = 1
        mat.SetFloat("_SnowGlitterStrength", 1f);
        // _SnowGlitterThreshold("Threshold", Range(0, 1)) = 1
        mat.SetFloat("_SnowGlitterThreshold", 0.8f);
        // _SnowGlitterDistFade("Distance Fade", Vector) = (10, 100, 1, 1)
        mat.SetVector("_SnowGlitterDistFade", new Vector4(10, 100, 1, 1));

        // _SnowUpVector("Snow Up Vector", Vector) = (0, 1, 0, 0)
        mat.SetVector("_SnowUpVector", new Vector4(0, 1, 0, 0));
        // _SnowTint("Snow Tint Color", Color) = (1, 1, 1, 1)
        mat.SetColor("_SnowTint", Color.white);
        // _SnowAmount("Amount", Range(0, 1)) = 0.5
        mat.SetFloat("_SnowAmount", 0f);
        // _SnowUVScales("UV Scale", Vector) = (50, 50, 0, 0)
        mat.SetVector("_SnowUVScales", new Vector4(0.5f, 0.5f, 0, 0));
        // height range, min/max angle
        // _SnowHeightAngleRange("Height Range", Vector) = (50, 90, 0, 1)  
        mat.SetVector("_SnowHeightAngleRange", new Vector4(-30, 90, 0.375f, 0.99f));

        mat.SetVector("_SnowNormalNoiseScaleStrength", new Vector4(0.1f, 0.5f, 0, 0));

        // height influence, erosion, crystal, melt
        // _SnowParams("Params", Vector) = (0.0, 0.0, 0.3, 0.1)
        mat.SetVector("_SnowParams", new Vector4(0.2f, 0.1f, 0.35f, 0.25f));

        */

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

    static Shader CreateShader(string variant)
    {
        var asset = string.Format(
            "assets/OcbMicroSplat/OcbMicroSplat{0}{1}{2}",
             MaxTextures, GetShaderQuality(), variant);
        AssetBundleManager.Instance.LoadAssetBundle(AssetBundlePath);
        return AssetBundleManager.Instance.Get<Shader>(AssetBundlePath, asset);

    }

    static void UpdateMaterial(Material mat, string variant)
    {
        mat.shader = CreateShader(variant);
        InitMicroSplatProperties(mat);
    }

    static Material UpdateMaterialShader(Material src, string variant)
    {
        if (GameManager.IsDedicatedServer) return src;
        var mat = UnityEngine.Object.Instantiate(src);
        UpdateMaterial(mat, variant);
        return mat;
    }

    static void ReplaceShaders(Shader src, Shader dst)
    {
        if (src == dst) return;
        // Find all materials that are already using the src shader
        foreach (var mat in UnityEngine.Object.FindObjectsOfType<Material>())
            if (mat.shader == src) mat.shader = dst;
    }

    static void UpdateAllShaders(Material src, string variant)
    {
        var shader = CreateShader(variant);
        ReplaceShaders(src.shader, shader);
    }

    [HarmonyPatch(typeof(GameOptionsManager), "ApplyTerrainOptions")]
    class GameOptionsManagerApplyTerrainOptionsPatch
    {
        static void Postfix()
        {
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
            var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
            UpdateAllShaders(terrain.materialDistant, "Distant");
            UpdateAllShaders(terrain.material, "Vertex");
        }
    }

    [HarmonyPatch]
    class PatchMicroSplatShader
    {

        // Select the target dynamically to patch `MoveNext`
        // Coroutine/Enumerator is compiled to a hidden class
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return GetEnumeratorMoveNext(AccessTools.Method(typeof(MeshDescription), "Init"));
        }

        static Material CreateDistantShader(Material src)
            => UpdateMaterialShader(src, "Distant");

        static Material CreateVertexShader(Material src)
            => UpdateMaterialShader(src, "Vertex");

        // Main function handling the IL patching
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            int i = 0;
            var codes = new List<CodeInstruction>(instructions);
            // Find and remember `Color32[] cols1`
            for (; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Call) continue;
                if (!(codes[i].operand is MethodInfo fn)) continue;
                if (!typeof(Material).IsAssignableFrom(fn.ReturnType)) continue;
                if (fn.Name != "Instantiate") continue;
                codes[i] = CodeInstruction.Call(
                    typeof(PatchMicroSplatShader),
                     "CreateVertexShader",
                     new Type[] { typeof(Material) });

                int til = Mathf.Max(codes.Count, i + 10);
                for (; i < til; i++)
                {
                    if (codes[i].opcode != OpCodes.Newobj) continue;
                    if (++i >= codes.Count) break;
                    if (codes[i].opcode != OpCodes.Stfld) continue;
                    if (!(codes[i].operand is FieldInfo field)) continue;
                    if (!typeof(Material).IsAssignableFrom(field.FieldType)) continue;
                    codes.Insert(i, CodeInstruction.Call(
                        typeof(PatchMicroSplatShader),
                         "CreateDistantShader",
                         new Type[] { typeof(Material) }));
                    break; // Apply patch only once
                }

                break;

            }
            // Return the result
            return codes;
        }

    }

}
