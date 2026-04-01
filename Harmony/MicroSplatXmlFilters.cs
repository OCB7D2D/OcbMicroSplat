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
            OcbMicroSplat.Config.ReportBlocks.Add(bname);
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

    [HarmonyPatch(typeof(BlocksFromXml), "InitBlock")]
    private static class BlocksFromXmlInitBlockPatch
    {
        static void Prefix(Block block)
        {
            if (!block.Properties.Contains("Texture")) return;
            if (!(block.shape is BlockShapeTerrain)) return;
            OcbMicroSplat.Config.ReportBlocks.Add(block.blockName);
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
