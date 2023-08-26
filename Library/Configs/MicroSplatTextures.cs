using System;
using System.Collections.Generic;
using System.Xml.Linq;

public class MicroSplatTextures
{

    // ####################################################################
    // ####################################################################

    // Optional textures by name (reference them to make use of them)
    // Textures that are not references will not be copied to shader
    public readonly Dictionary<string, MicroSplatTexture> Textures
        = new Dictionary<string, MicroSplatTexture>();

    // Reference to blocks that need their TerrainIndex updated
    // We don't know the actual slot index until world is loaded
    public readonly Dictionary<string, List<string>> Blocks
        = new Dictionary<string, List<string>>();

    // ####################################################################
    // ####################################################################

    public MicroSplatTextures()
    {
        Reset();
    }

    // ####################################################################
    // ####################################################################

    public void RegisterTerrainBlock(string index, string block)
    {
        if (!Blocks.TryGetValue(index, out List<string> blocks))
            blocks = Blocks[index] = new List<string>();
        blocks.Add(block);
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (!xml.HasAttribute("name")) throw new Exception(
            $"Mandatory attribute `name` missing on {xml.Name}");
        string name = xml.GetAttribute("name");
        if (!Textures.TryGetValue(name, out MicroSplatTexture texture))
            Textures.Add(name, texture = new MicroSplatTexture());
        texture.Parse(xml);
    }

    // ####################################################################
    // ####################################################################

    public void Reset()
    {
        Blocks.Clear();
        Textures.Clear();
        // Initialize empty config for existing textures
        // You may also update these, but not remove them
        for (int i = 0; i < 24; i++)
        {
            string name = $"microsplat{i}";
            var texture = new MicroSplatTexture();
            /*if (i > 3)*/ texture.SlotIdx = i;
            Textures.Add(name, texture);
            texture.SrcIdx = i;
        }
    }

    // ####################################################################
    // ####################################################################

}
