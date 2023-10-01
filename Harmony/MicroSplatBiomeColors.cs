using HarmonyLib;
using Unity.Collections;
using UnityEngine;
static class ChunkProviderBiomeColorsPatch
{

    static ChunkProviderGenerateWorldFromRaw generateWorldFromRaw = null;

    private static readonly HarmonyFieldProxy<IBiomeProvider> BiomeProviderField =
        new HarmonyFieldProxy<IBiomeProvider>(typeof(ChunkProviderGenerateWorld), "m_BiomeProvider");

    [HarmonyPatch(typeof(ChunkProviderGenerateWorldFromRaw), "Init")]
    static class CaptureChunkProviderOnInitForLater
    {
        static void Prefix(ChunkProviderGenerateWorldFromRaw __instance)
            => generateWorldFromRaw = __instance;
    }

    [HarmonyPatch(typeof(Log), "Out", new System.Type[] { typeof(string) })]
    static class PatchProcBiomeMaskAfterVanillaPatching
    {
        static void Postfix(string _txt)
        {
            if (generateWorldFromRaw == null) return;
            // This is a very bad and mad way of patching stuff, but the transpiler
            // below tends to give headaches due to a transpiler bug with HarmonyX.
            // Overhead should be ok, given that logging is already quite expensive!
            if (_txt.StartsWith("Loading and creating shader control textures took"))
            {
                MicroStopwatch ms = new MicroStopwatch();
                int width = generateWorldFromRaw.GetWorldSize().x / 8;
                int height = generateWorldFromRaw.GetWorldSize().y / 8;
                generateWorldFromRaw.procBiomeMask1 = new Texture2D(width, height, TextureFormat.RGBA32, false);
                generateWorldFromRaw.procBiomeMask2 = new Texture2D(width, height, TextureFormat.RGBA32, false);
                NativeArray<Color32> pixelData1 = generateWorldFromRaw.procBiomeMask1.GetPixelData<Color32>(0);
                NativeArray<Color32> pixelData2 = generateWorldFromRaw.procBiomeMask2.GetPixelData<Color32>(0);
                generateWorldFromRaw.GetWorldExtent(out Vector3i _minSize, out Vector3i _maxSize);
                var biomeProvider = BiomeProviderField.Get(generateWorldFromRaw);
                var config = OcbMicroSplat.Config.MicroSplatWorldConfig;
                for (int z = _minSize.z; z < _maxSize.z; z += 8)
                {
                    int num = (z - _minSize.z) / 8 * width;
                    for (int x = _minSize.x; x < _maxSize.x; x += 8)
                    {
                        if (biomeProvider.GetBiomeAt(x, z) is BiomeDefinition biome)
                        {
                            Color32 color32_1 = new Color32();
                            Color32 color32_2 = new Color32();
                            int index = (x - _minSize.x) / 8 + num;
                            switch (biome.m_Id)
                            {
                                case 1:
                                    color32_1.r = byte.MaxValue;
                                    break;
                                case 3:
                                    color32_1.g = byte.MaxValue;
                                    break;
                                case 5:
                                    color32_2.r = byte.MaxValue;
                                    break;
                                case 8:
                                    color32_1.a = byte.MaxValue;
                                    break;
                                case 9:
                                    color32_1.b = byte.MaxValue;
                                    break;
                            }
                            if (config.BiomeColors.TryGetValue(biome.m_Id,
                                out MicroSplatBiomeColor value))
                            {
                                color32_1 = value.Color1;
                                color32_2 = value.Color2;
                            }
                            pixelData1[index] = color32_1;
                            pixelData2[index] = color32_2;
                        }
                    }
                }
                generateWorldFromRaw.procBiomeMask1.filterMode = FilterMode.Bilinear;
                generateWorldFromRaw.procBiomeMask1.Apply(false, true);
                generateWorldFromRaw.procBiomeMask2.filterMode = FilterMode.Bilinear;
                generateWorldFromRaw.procBiomeMask2.Apply(false, true);
                Log.Out("Re-creating shader control textures took " + ms.ElapsedMilliseconds.ToString() + "ms");
                generateWorldFromRaw = null;
            }
        }
    }
}

