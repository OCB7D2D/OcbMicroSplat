using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class GameOptions
{

    // Our combo box (should only even have one)
    static XUiC_ComboBoxList<string> comboTessOpt = null;
    static XUiC_ComboBoxList<string> comboAntiTileOpt = null;
    // static XUiC_ComboBoxBool comboParallaxOpt = null;
    // static XUiC_ComboBoxBool comboParallaxOpt = null;

    static void DisableKeyword(MeshDescription mesh, string keyword)
    {
        mesh.material.DisableKeyword(keyword);
        mesh.materialDistant.DisableKeyword(keyword);
    }

    static void EnableKeyword(MeshDescription mesh, string keyword)
    {
        mesh.material.EnableKeyword(keyword);
        mesh.materialDistant.EnableKeyword(keyword);
    }

    static void SetFloat(MeshDescription mesh, string name, float value)
    {
        mesh.material.SetFloat(name, value);
        mesh.materialDistant.SetFloat(name, value);
    }

    static void SetVector(MeshDescription mesh, string name, Vector4 value)
    {
        mesh.material.SetVector(name, value);
        mesh.materialDistant.SetVector(name, value);
    }

    // Must be called by mod init too
    // Since our patch is too late for early init
    public static void ApplyTerrainOptions()
    {
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
        // Setup the two different material types
        mesh.material.EnableKeyword("_VERTEX7D2D");
        MicroSplatShader.UpdateShaderQuality(mesh.material);
        mesh.materialDistant.EnableKeyword("_DISTANT7D2D");
        MicroSplatShader.UpdateShaderQuality(mesh.materialDistant);
        // Disable all resampling keywords first
        DisableKeyword(mesh, "_QR_LOW");
        DisableKeyword(mesh, "_QR_MED");
        DisableKeyword(mesh, "_QR_HIGH");
        // DisableKeyword(mesh, "_QR_ULTRA");
        // Now enable exactly one of the set again
        switch (PlayerPrefs.GetInt("TerrainAntiTiling"))
        {
            case 1: EnableKeyword(mesh, "_QR_LOW"); break;
            case 2: EnableKeyword(mesh, "_QR_MED"); break;
            case 3: EnableKeyword(mesh, "_QR_HIGH"); break;
            // case 4: EnableKeyword(mesh, "_QR_ULTRA"); break;
        }
        /*
        switch (PlayerPrefs.GetInt("TerrainParallax"))
        {
            case 0: // Off
                // Log.Out("Disable Parallax 0");
                mesh.material.DisableKeyword("_RESAMPLECLUSTERS");
                mesh.materialDistant.DisableKeyword("_RESAMPLECLUSTERS");
                break;
            case 1: // On
                // Log.Out("Enable Parallax 0");
                mesh.material.EnableKeyword("_RESAMPLECLUSTERS");
                mesh.materialDistant.EnableKeyword("_RESAMPLECLUSTERS");
                break;
        }
        */
        // Set tessellation options according to game settings
        switch (PlayerPrefs.GetInt("TerrainTessellation"))
        {
            // _Tess = edgeLength
            case 1: // Lowest
                SetFloat(mesh, "_Tess", 128.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(3, 4, 15, 20));
                break;
            case 2: // Low
                SetFloat(mesh, "_Tess", 96.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(4, 4, 16, 20));
                break;
            case 3: // Medium
                SetFloat(mesh, "_Tess", 64.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(5, 4, 17, 20));
                break;
            case 4: // High
                SetFloat(mesh, "_Tess", 32.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(6, 3, 18, 20));
                break;
            case 5: // Ultra
                SetFloat(mesh, "_Tess", 16.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(8, 3, 19, 21));
                break;
            case 6: // Ultra+
                SetFloat(mesh, "_Tess", 8.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(10, 3, 22, 25));
                break;
            default:
                SetFloat(mesh, "_Tess", 256.0f);
                // tess, mipBias, fade dist min, max
                SetVector(mesh, "_TessParams1", new Vector4(5, 3, 17, 20));
                break;
        }

        // _Phong = move to bary-center
        SetFloat(mesh, "_Phong", 0.725f);
    }

    // Hook our new combo box into the ui workflow
    [HarmonyPatch(typeof(XUiC_OptionsVideo), "Init")]
    public class XUiC_OptionsVideo_Init
    {
        // The original handler (use reflection for protected)
        static readonly MethodInfo handler = AccessTools.Method(
            typeof(XUiC_OptionsVideo), "AnyPresetValueChanged");

        static void Prefix(XUiC_OptionsVideo __instance)
        {
            // The UI element with our combo box
            comboTessOpt = __instance
                .GetChildById("TerrainTessellation")
                .GetChildByType<XUiC_ComboBoxList<string>>();
            comboAntiTileOpt = __instance
                .GetChildById("TerrainAntiTiling")
                .GetChildByType<XUiC_ComboBoxList<string>>();
            // comboParallaxOpt = __instance
            //     .GetChildById("TerrainParallax")
            //     .GetChildByType<XUiC_ComboBoxBool>();
            // Hook into event to invoke the regular UI handler
            comboTessOpt.OnValueChangedGeneric += (XUiController sender) =>
                handler.Invoke(__instance, new object[] { sender });
            comboAntiTileOpt.OnValueChangedGeneric += (XUiController sender) =>
                handler.Invoke(__instance, new object[] { sender });

            // comboParallaxOpt.OnValueChangedGeneric += (XUiController sender) => {
            //     handler.Invoke(__instance, new object[] { sender });
            // };

        }
    }

    // Patch into function when apply is hit on the UI
    [HarmonyPatch(typeof(XUiC_OptionsVideo), "applyChanges")]
    public class XUiC_OptionsVideo_applyChanges
    {
        static void Prefix(XUiC_OptionsVideo __instance)
        {
            // Log.Out("Apply changes {0}", comboParallaxOpt.Value ? 0 : 1);
            PlayerPrefs.SetInt("TerrainTessellation", comboTessOpt.SelectedIndex);
            PlayerPrefs.SetInt("TerrainAntiTiling", comboAntiTileOpt.SelectedIndex);
            // PlayerPrefs.SetInt("TerrainParallax", comboParallaxOpt.Value ? 1 : 0);
        }
    }

    // Patch into function when video settings UI is loaded
    [HarmonyPatch(typeof(XUiC_OptionsVideo), "updateGraphicOptions")]
    public class XUiC_OptionsVideo_updateGraphicOptions
    {
        static void Prefix(XUiC_OptionsVideo __instance)
        {
            comboTessOpt.SelectedIndex = PlayerPrefs.GetInt("TerrainTessellation");
            comboAntiTileOpt.SelectedIndex = PlayerPrefs.GetInt("TerrainAntiTiling");
            // comboParallaxOpt.Value = PlayerPrefs.GetInt("TerrainParallax") != 0;
        }
    }

    // Patch into function when options are applied to materials
    [HarmonyPatch(typeof(GameOptionsManager), "ApplyTerrainOptions")]
    public class GameOptionsManager_ApplyTerrainOptions
    {
        static void Postfix()
        {
            ApplyTerrainOptions();
        }
    }

    // Patch into function when video settings are set to defaults
    [HarmonyPatch(typeof(GameOptionsManager), "SetGraphicsQuality")]
    public class GameOptionsManager_SetGraphicsQuality
    {
        static void Postfix()
        {
            switch (GamePrefs.GetInt(EnumGamePrefs.OptionsGfxQualityPreset))
            {
                case 1: PlayerPrefs.SetInt("TerrainTessellation", 1); break;
                case 2: PlayerPrefs.SetInt("TerrainTessellation", 2); break;
                case 3: PlayerPrefs.SetInt("TerrainTessellation", 3); break;
                case 4: PlayerPrefs.SetInt("TerrainTessellation", 4); break;
                default: PlayerPrefs.SetInt("TerrainTessellation", 0); break;
            }
            switch (GamePrefs.GetInt(EnumGamePrefs.OptionsGfxQualityPreset))
            {
                case 1: PlayerPrefs.SetInt("TerrainParallax", 1); break;
                case 2: PlayerPrefs.SetInt("TerrainParallax", 1); break;
                case 3: PlayerPrefs.SetInt("TerrainParallax", 2); break;
                case 4: PlayerPrefs.SetInt("TerrainParallax", 2); break;
                default: PlayerPrefs.SetInt("TerrainParallax", 1); break;
            }
        }
    }
    
}
