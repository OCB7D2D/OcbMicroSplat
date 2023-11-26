using HarmonyLib;
using System;
using System.Linq;
using System.Xml.Linq;

public static class MicroSplatXmlFilters
{

    // ####################################################################
    // ####################################################################

    // Mark the internal voxel textures according to block usage
    // Maybe some textures are no longer in use and up for grab?
    private static void ParseHardCodedVoxelUsages(XElement root, MicroSplatTextures config)
    {
        // fish out hard-coded texture ids
        // code is a bit crude, but gets it done
        foreach (var child in root.Elements("property"))
        {
            if (!child.HasAttribute("name")) continue;
            if (child.GetAttribute("name") != "Texture") continue;
            if (!child.HasAttribute("value")) continue;
            foreach (int id in child.GetAttribute("value").Split(
                    new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(textureId => int.Parse(textureId)))
            {
                switch (id)
                {
                    case 33: config.Textures["microsplat17"].IsUsedByVoxel = true; break;
                    case 1: config.Textures["microsplat19"].IsUsedByVoxel = true; break;
                    case 440: config.Textures["microsplat21"].IsUsedByVoxel = true; break;
                    case 438: config.Textures["microsplat23"].IsUsedByVoxel = true; break;
                    case 10: config.Textures["microsplat16"].IsUsedByVoxel = true; break;
                    case 300: config.Textures["microsplat18"].IsUsedByVoxel = true; break;
                    case 184: config.Textures["microsplat20"].IsUsedByVoxel = true; break;
                    case 316: config.Textures["microsplat22"].IsUsedByVoxel = true; break;
                    case 2: config.Textures["microsplat13"].IsUsedByVoxel = true; break;
                    case 11: config.Textures["microsplat14"].IsUsedByVoxel = true; break;
                    case 34: config.Textures["microsplat15"].IsUsedByVoxel = true; break;
                }
            }
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(BlocksFromXml), "CreateBlocks")]
    private static class BlocksFromXmlCreateBlocksPatch
    {
        static void Prefix(XmlFile _xmlFile)
        {
            string mapName = GamePrefs.GetString(EnumGamePrefs.GameWorld)?.Trim();
            var elements = _xmlFile.XmlDoc.Root.Elements("block");
            foreach (XElement block in elements.ToList())
            {
                ParseHardCodedVoxelUsages(block, OcbMicroSplat
                    .Config.MicroSplatTexturesConfigs);
                if (!block.HasAttribute("map-only")) continue;
                var filters = block.GetAttribute("map-only").Split(
                    new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim());
                if (filters.Contains(mapName)) continue;
                if (filters.Contains("*")) continue;
                Log.Out("Filter out block {0}",
                    block.GetAttribute("name"));
                block.Remove();
            }
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(WorldBiomes), "readXML")]
    private static class WorldBiomesReadXmlPatch
    {
        static void Prefix(XDocument _xml)
        {
            string mapName = GamePrefs.GetString(EnumGamePrefs.GameWorld)?.Trim();
            foreach (XElement root in _xml.Root.Elements()) // "biome"
            foreach (XElement biome in root.Elements().ToList())
            {
                if (!biome.HasAttribute("map-only")) continue;
                var filters = biome.GetAttribute("map-only").Split(
                    new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim());
                if (filters.Contains(mapName)) continue;
                if (filters.Contains("*")) continue;
                Log.Out("Filter out biome {0}",
                    biome.GetAttribute("name"));
                biome.Remove();
            }
        }
    }

    // ####################################################################
    // ####################################################################

    public static void FilterMapOnlyNodes(XElement root)
    {
        string mapName = GamePrefs.GetString(
            EnumGamePrefs.GameWorld)?.Trim();
        foreach (XElement node in root.Elements())
        {
            if (!node.HasAttribute("map-only")) continue;
            var filters = node.GetAttribute("map-only").Split(
                new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim());
            if (filters.Contains(mapName)) continue;
            if (filters.Contains("*")) continue;
            Log.Out("Filter out {0} (for {1})",
                node.Name, node.GetAttribute("map-only"));
            node.Remove();
        }

    }

    // ####################################################################
    // ####################################################################

}
