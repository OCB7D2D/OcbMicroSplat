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
        mesh.material.SetFloat("_Phong", 0.725f);
    }

    // 7DTD 3.x: the "Terrain Tessellation" dropdown in Options > Video is removed for now. 3.x rebuilt the
    // video options UI on a new option-entry system (XUiC_OptionEntryAbs); XUiC_OptionsVideo no longer has
    // applyChanges / updateGraphicOptions / AnyPresetValueChanged, so the old hooks fail to patch at
    // startup. The setting itself still works: ApplyTerrainOptions below reads PlayerPrefs
    // "TerrainTessellation" (Off by default; values chosen on 2.x persist). Re-adding the dropdown means
    // building it on the new option-entry classes; Config/XUi_Menu/windows.xml is removed with it.

    // Patch into function when options are applied to materials
    [HarmonyPatch(typeof(GameOptionsManager), "ApplyTerrainOptions")]
    public class GameOptionsManager_ApplyTerrainOptions
    {
        static void Postfix()
        {
            ApplyTerrainOptions();
        }
    }

    // 7DTD 3.x: the SetGraphicsQuality postfix (graphics presets setting tessellation) is removed along
    // with the dropdown - without the dropdown, a preset could silently turn on tessellation (rated
    // "Performance impact: Very High") with no way to see or undo it in game.

}
