using HarmonyLib;

public class SubMeshTopSoil
{

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(Chunk), "IsTopSoil")]
    private static class ChunkIsTopSoilPatch
    {
        static bool Prefix(Chunk __instance,
            int _x, int _z, ref bool __result)
        {
            int x = __instance.m_X * 16 + _x;
            int z = __instance.m_Z * 16 + _z;
            World world = GameManager.Instance.World;
            IChunkProvider provider = world.ChunkCache.ChunkProvider;
            IBiomeProvider biomeProvider = provider.GetBiomeProvider();
            BiomeDefinition bd = biomeProvider.GetBiomeAt(x, z);
            if (bd == null) return true; // Skip if no biome found
            int idx = biomeProvider.GetSubBiomeIdxAt(bd, x, 0, z);
            if (idx < 0) return true; // Skip if not in a sub-biome
            __result = false; // Mark sub-biome blocks as NOT top-soil
            return false; // Do not run the original function
        }

    }

    // ####################################################################
    // ####################################################################

}
