using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class TessellationOption
{

    // Must be called by mod init too
    // Since our patch is too late for early init
    public static void ApplyTerrainOptions()
    {
        if (GameManager.IsDedicatedServer) return; // Nothing to do here
        if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
        var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
        // Set tessellation options according to game settings
        switch (PlayerPrefs.GetInt("TerrainTessellation"))
        {
            // _Tess = edgeLength
            // _Phong = move to bary
            case 1: // Lowest
                mesh.material.SetFloat("_Tess", 128.0f);
                mesh.materialDistant.SetFloat("_Tess", 128.0f);
                break;
            case 2: // Low
                mesh.material.SetFloat("_Tess", 96.0f);
                mesh.materialDistant.SetFloat("_Tess", 96.0f);
                break;
            case 3: // Medium
                mesh.material.SetFloat("_Tess", 64.0f);
                mesh.materialDistant.SetFloat("_Tess", 64.0f);
                break;
            case 4: // High
                mesh.material.SetFloat("_Tess", 32.0f);
                mesh.materialDistant.SetFloat("_Tess", 32.0f);
                break;
            case 5: // Ultra
                mesh.material.SetFloat("_Tess", 16.0f);
                mesh.materialDistant.SetFloat("_Tess", 16.0f);
                break;
            case 6: // Ultra+
                mesh.material.SetFloat("_Tess", 8.0f);
                mesh.materialDistant.SetFloat("_Tess", 8.0f);
                break;
            default:
                mesh.material.SetFloat("_Tess", 256.0f);
                mesh.materialDistant.SetFloat("_Tess", 256.0f);
                break;
        }
    }

    // Our combo box (should only even have one)
    static XUiC_ComboBoxList<string> comboTessOpt = null;

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
            // Hook into event to invoke the regular UI handler
            comboTessOpt.OnValueChangedGeneric += (XUiController sender) => {
                handler.Invoke(__instance, new object[] { sender });
            };

        }
    }

    // Patch into function when apply is hit on the UI
    [HarmonyPatch(typeof(XUiC_OptionsVideo), "applyChanges")]
    public class XUiC_OptionsVideo_applyChanges
    {
        static void Prefix(XUiC_OptionsVideo __instance)
        {
            PlayerPrefs.SetInt("TerrainTessellation", comboTessOpt.SelectedIndex);
        }
    }

    // Patch into function when video settings UI is loaded
    [HarmonyPatch(typeof(XUiC_OptionsVideo), "updateGraphicOptions")]
    public class XUiC_OptionsVideo_updateGraphicOptions
    {
        static void Prefix(XUiC_OptionsVideo __instance)
        {
            comboTessOpt.SelectedIndex = PlayerPrefs.GetInt("TerrainTessellation");
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
        }
    }
    
}
