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
        string bname = root.GetAttribute("name");
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
                    case 1: config.RegisterVoxelUsage("microsplat19", bname); break;
                    case 2: config.RegisterVoxelUsage("microsplat13", bname); break;
                    case 6: config.RegisterVoxelUsage("microsplat0", bname); break;
                    case 8: config.RegisterVoxelUsage("microsplat4", bname); break;
                    case 10: config.RegisterVoxelUsage("microsplat4", bname); break;
                    // case 10: config.RegisterVoxelUsage("microsplat16", bname); break;
                    case 11: config.RegisterVoxelUsage("microsplat5", bname); break;
                    // case 11: config.RegisterVoxelUsage("microsplat14", bname); break;
                    case 33: config.RegisterVoxelUsage("microsplat17", bname); break;
                    case 34: config.RegisterVoxelUsage("microsplat15", bname); break;
                    case 184: config.RegisterVoxelUsage("microsplat20", bname); break;
                    case 185: config.RegisterVoxelUsage("microsplat7", bname); break;
                    case 195: config.RegisterVoxelUsage("microsplat2", bname); break;
                    case 288: config.RegisterVoxelUsage("microsplat10", bname); break;
                    case 300: config.RegisterVoxelUsage("microsplat18", bname); break;
                    case 316: config.RegisterVoxelUsage("microsplat22", bname); break;
                    case 438: config.RegisterVoxelUsage("microsplat23", bname); break;
                    case 440: config.RegisterVoxelUsage("microsplat21", bname); break;
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
