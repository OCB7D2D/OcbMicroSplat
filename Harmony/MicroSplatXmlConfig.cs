using HarmonyLib;
using System.Xml;


static class MicroSplatXmlConfig
{

    public static MicroSplatTerrainConfig Terrain;

    [HarmonyPatch(typeof(WorldGlobalFromXml), "Load")]
    static class WorldGlobalFromXmlLoadPatch
    {
        static public DynamicProperties GetDynamicProperties(XmlNode xml)
        {
            DynamicProperties props = new DynamicProperties();
            foreach (XmlNode childNode in xml.ChildNodes)
            {
                if (childNode.NodeType != XmlNodeType.Element) continue;
                if (childNode.Name.Equals("property") == false) continue;
                props.Add(childNode);
            }
            return props;
        }

        static void Prefix(XmlFile _xmlFile)
        {
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
            foreach (XmlNode node in _xmlFile.XmlDoc.DocumentElement.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element) continue;
                XmlElement child = node as XmlElement;
                if (child.Name.Equals("microsplat-terrain"))
                {
                    DynamicProperties props = GetDynamicProperties(child);
                    Terrain = new MicroSplatTerrainConfig(props);
                    var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
                    Terrain.ApplyToTerrain(terrain); // Apply settings once read
                }
                else
                {
                    Log.Warning("Unknown microsplat-terrain config {0}", child.Name);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameOptionsManager), "ApplyTerrainOptions")]
    class GameOptionsManagerApplyTerrainOptionsPatch
    {
        static void Postfix()
        {
            if (Terrain == null) return; // No custom config available
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            if (MeshDescription.meshes.Length < MeshDescription.MESH_TERRAIN) return;
            Terrain.ApplyToTerrain(MeshDescription.meshes[MeshDescription.MESH_TERRAIN]);
        }
    }


}
