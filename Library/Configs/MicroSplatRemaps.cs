using System;
using System.Collections.Generic;
using System.Xml.Linq;

public class MicroSplatRemaps
{

    // ####################################################################
    // ####################################################################

    public readonly Dictionary<int, int> Mappings
        = new Dictionary<int, int>();

    // ####################################################################
    // ####################################################################

    public static int GetMicroSplatIndex(int terrTexID)
    {
        switch (terrTexID)
        {
            // Slot 1 is evacuated to slot 19
            // Keep slot 1 unused to pass weight
            case 1: return 19; // Stone, Bedrock
            case 2: return 13; // Dirt (from 13)
            case 6: return 0; // Snow
            case 8: return 23; // Concrete (from 23)
            case 10: return 16; // Asphalt
            case 11: return 14; // Gravel
            case 33: return 17; // OreIron
            case 34: return 15; // OreCoal
            case 184: return 20; // SandStone
            case 185: return 7; // Sand, DesertGround, SandStone
            case 195: return 2; // TopSoil, ForrestGround
            case 288: return 10; // BurntForestGround, DestroyedGrass
            case 300: return 18; // OrePotassiumNitrate
            case 316: return 22; // OreLead
            case 438: return 23; // DestroyedStone
            case 440: return 21; // OreOilDeposit
                                 // case 559: return 19;
                                 // case 560: return 19;
                                 // Original filler has weird square muster
                                 // Probably used in the prefab editor
            case 403: return -1; // Filler
            default:
                // Log.Out("Unknown Voxel Terrain Texture ID {0}", terrTexID);
                return -1;
        }
    }

    public static int RemapFromSplat(int texID)
    {
        if (!OcbMicroSplat.Config.MicroSplatRemapConfig.Mappings
            .TryGetValue(texID, out int to)) return texID;
        return to;
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (!xml.HasAttribute("from")) throw new Exception(
            $"Mandatory attribute `from` missing on {xml.Name}");
        if (!xml.HasAttribute("to")) throw new Exception(
            $"Mandatory attribute `to` missing on {xml.Name}");
        int from = int.Parse(xml.GetAttribute("from"));
        int to = int.Parse(xml.GetAttribute("to"));
        Mappings[from] = to;
    }

    // ####################################################################
    // ####################################################################

    public void Reset()
    {
        Mappings.Clear();
    }

    // ####################################################################
    // ####################################################################

}
