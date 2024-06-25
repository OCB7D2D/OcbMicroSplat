using HarmonyLib;
using UnityEngine;

public class FixMeshGeneratorNormals
{


    // ####################################################################
    // ####################################################################

    // Copied from `MeshGeneratorMC2.getDensity`
    private static sbyte getDensity(IChunk[] ___chunksCache, int _x, int _y, int _z)
    {
        if (_y < 0) return MarchingCubes.DensityTerrain;
        if (_y >= 256) return MarchingCubes.DensityAir;
        sbyte density = MarchingCubes.DensityAir;
        if (___chunksCache[_x + 1 + (_z + 1) * 19] is Chunk chunk)
        {
            density = chunk.GetDensity(_x & 15, _y, _z & 15);
            if (density == 0)
            {
                // Log.Warning("Density is zero??");
                density = 1;
            }
        }
        return density;
    }

    // ####################################################################
    // ####################################################################

    // Copied from `MeshGeneratorMC2.CalculateNormalUnscaled`
    private static Vector3 CalculateNormalUnscaled(
        IChunk[] ___chunksCache, int ___startY, Vector3i coord)
    {
        int x = coord.x;
        int _y1 = coord.y + ___startY;
        int z = coord.z;
        int left = x - 1;
        int right = x + 1;
        int below = _y1 - 1;
        int above = _y1 + 1;
        int back = z - 1;
        int front = z + 1;
        int densityLeft = (int)getDensity(___chunksCache, left, _y1, z);
        int densityRight = (int)getDensity(___chunksCache, right, _y1, z);
        int densityBelow = (int)getDensity(___chunksCache, x, below, z);
        int densityAbove = (int)getDensity(___chunksCache, x, above, z);
        int densityBack = (int)getDensity(___chunksCache, x, _y1, back);
        int densityFront = (int)getDensity(___chunksCache, x, _y1, front);
        Vector3 normalUnscaled;
        normalUnscaled.x = (float)(densityRight - densityLeft);
        normalUnscaled.y = (float)(densityAbove - densityBelow);
        normalUnscaled.z = (float)(densityFront - densityBack);
        return normalUnscaled;
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(MeshGeneratorMC2), "CalculateNormal",
    new System.Type[] { typeof(Vector3i), typeof(Vector3i),
        typeof(int), typeof(int), typeof(bool) })]
    class CalculateNormalMeshGeneratorMC2Patch
    {

        static bool Prefix(ref Vector3 __result, Vector3i coord0,
            Vector3i coord1, int t, int u, bool is0Main,
            IChunk[] ___chunksCache, int ___startY)
        {
            Vector3 normalUnscaled1 = CalculateNormalUnscaled(___chunksCache, ___startY, coord0);
            Vector3 normalUnscaled2 = CalculateNormalUnscaled(___chunksCache, ___startY, coord1);
            Vector3 vectorDelta = (is0Main ? coord1 - coord0 : coord0 - coord1).ToVector3();
            vectorDelta = vectorDelta.normalized;
            if (t == u && (normalUnscaled1.magnitude < 128 ||
                           normalUnscaled2.magnitude < 128))
            {
                __result = vectorDelta;
            }
            else
            {
                Vector3 vectorScaled = normalUnscaled1 * t + normalUnscaled2 * u;
                vectorScaled = vectorScaled.normalized;
                float dot = Vector3.Dot(vectorDelta, vectorScaled);
                float f = Mathf.InverseLerp(-0.75f, 0.25f, dot);
                __result = Vector3.LerpUnclamped(vectorDelta, vectorScaled, f);
            }
            return false;
        }
    }

    // ####################################################################
    // ####################################################################

}
