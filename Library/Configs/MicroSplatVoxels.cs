using System;
using System.Collections.Generic;
using System.Xml.Linq;

public class MicroSplatVoxels
{

    // ####################################################################
    // ####################################################################

    public const int VoxelIndexOffset = 200;
    public const int VoxelIndexOffsetEnd = 219;

    // ####################################################################
    // ####################################################################

    public readonly Dictionary<string, MicroSplatVoxel> Voxels
        = new Dictionary<string, MicroSplatVoxel>();

    public readonly List<MicroSplatVoxel> Voxel
        = new List<MicroSplatVoxel>();

    // ####################################################################
    // ####################################################################

    public MicroSplatVoxel GetOrCreateVoxelConfig(string name)
    {
        if (!Voxels.TryGetValue(name, out MicroSplatVoxel voxel))
            Voxels.Add(name, voxel = new MicroSplatVoxel(name, Voxels.Count));
        return voxel;
    }

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (!xml.HasAttribute("name")) throw new Exception(
            $"Mandatory attribute `name` missing on {xml.Name}");
        string name = xml.GetAttribute("name");
        if (!Voxels.TryGetValue(name, out MicroSplatVoxel voxel))
            Log.Warning("Skipping unused voxel {0}", name);
        else
        {
            Voxel.Add(voxel);
            voxel.Parse(xml);
        }
    }

    // ####################################################################
    // ####################################################################

    public void GatherVoxelTextures(List<MicroSplatTexture> textures)
    {
        foreach (var voxel in Voxels)
        {
            foreach (var kv in voxel.Value.textures)
            {
                var texture = OcbMicroSplat.Config.GetTextureConfig(kv.Key);
                if (texture == null) Log.Error("Couldn't find MicroSplat {0}", kv.Key);
                else if (!textures.Contains(texture)) textures.Add(texture);
            }
        }
    }

    // ####################################################################
    // ####################################################################

    public void Reset()
    {
        Voxels.Clear();
        Voxel.Clear();
    }

    // ####################################################################
    // ####################################################################

}
