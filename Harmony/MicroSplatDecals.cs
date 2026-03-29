using HarmonyLib;
using OCB;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OcbMicroSplatDecals
{

    // ####################################################################
    // ####################################################################

    private struct DecalItem
    {
        public int x;
        public float y;
        public int z;
        public int idx;

        public DecalItem(int x, float y, int z, int idx)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.idx = idx;
        }
    }

    // ####################################################################
    // ####################################################################

    // Render Texture holding the dynamic decals splatmap
    public static RenderTexture decalsTexture = null;

    // Texture holding single pixel to draw
    private static Texture2D pixel = null;

    // The work queue holdig all positions to draw decal "pixels"
    private static readonly List<DecalItem> decals = new List<DecalItem>();

    // ####################################################################
    // ####################################################################

    public static void WorldChanged(World world, MeshDescription terrain)
    {

        if (world.ChunkCache.ChunkProvider is
            ChunkProviderAbstract chunkProvider)
        {
            int w = chunkProvider.WorldInfo.WorldSize.x;
            int h = chunkProvider.WorldInfo.WorldSize.y;
            // Create linear render texture
            decalsTexture = new RenderTexture(w, h,
                0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                // We only sample LOD level 0
                autoGenerateMips = false,
                useMipMap = false
            };
            // Attach the render texture to microsplat shaders
            terrain.material.SetTexture("_DecalSplat", decalsTexture);
            terrain.materialDistant.SetTexture("_DecalSplat", decalsTexture);
        }

    }

    // ####################################################################
    // ####################################################################

    // ####################################################################
    // ####################################################################

    // https://en.wikipedia.org/wiki/Mersenne_Twister#Initialization
    public static uint rx = 0, ry = 0, rz = 0, rw = 0;
    const uint MT19937 = 1812433253;

    // Apply custom micro splat textures when xml is loaded
    [HarmonyPatch(typeof(WorldEnvironment), "OnXMLChanged")]
    static class PatchWorldEnvironmentOnXMLChanged
    {
        // Worker coroutine to draw decal pixels
        private static IEnumerator DecalQueueWorker()
        {
            while (true)
            {
                // Draw very seldomly (to accumulate work)
                yield return new WaitForSeconds(0.25f);
                #if DEBUG
                var watch = System.Diagnostics.Stopwatch.StartNew();
                #endif
                if (OcbMicroSplatDecals.decalsTexture != null
                    && GameManager.Instance.World != null)
                {
                    // decals queue is comming from background thrad
                    // therefore aquire a lock for syncronization
                    lock (decals)
                    {
                        // Queue might be empty
                        if (decals != null && decals.Count > 0)
                        {
                            int w = OcbMicroSplatDecals.decalsTexture.width / 2;
                            int h = OcbMicroSplatDecals.decalsTexture.height / 2;
                            foreach (DecalItem offset in decals)
                            {
                                // rx = (uint)offset.x + (uint)offset.z;
                                // ry = (uint)(MT19937 * rx + 1);
                                // rz = (uint)(MT19937 * ry + 1);
                                // rw = (uint)(MT19937 * rz + 1);
                                ulong seed = 0;
                                StaticRandom.HashSeed(ref seed, offset.z + h);
                                StaticRandom.HashSeed(ref seed, offset.x + w);
                                // Get a random rotation to draw into green channel
                                float rot = StaticRandom.Range(0f, 1f, seed);
                                // Log.Out("Add decal at height", offset.y);
                                // Set the color for the pixel to draw into the decal splatmap
                                // We store the decals index into the red channel (0 = no decal)
                                // Rotation is stored in the green channel (range 0 to 1 inclusive)
                                pixel.SetPixel(0, 0, new Color((1f + offset.idx) / 255f, rot,
                                    // Encode render height into 16 bits (blue and alpha channel)
                                    (int)offset.y / 255f, offset.y - (int)offset.y));
                                // Apply the color change on CPU
                                pixel.Apply(false, false);
                                // Copy CPU texture pixel into decals splatmap
                                Graphics.CopyTexture(pixel, 0, 0, 0, 0, 1, 1,
                                    OcbMicroSplatDecals.decalsTexture, 0, 0,
                                    offset.x + w, offset.z + h);
                            }
                            #if DEBUG
                            watch.Stop();
                            // Report unexpected long draw calls
                            if (watch.ElapsedMilliseconds > 5) Log.Out(
                                "Decals drawing took {0}ms (with {1} draws)",
                                watch.ElapsedMilliseconds, decals.Count);
                            #endif
                            decals.Clear();
                        }
                    }
                }
            }
        }

        static void Postfix()
        {
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            // Create shared texture (single pixel) to draw into decals splat
            pixel = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
            // Start coroutine to periodically draw decal pixel
            GameManager.Instance.StartCoroutine(DecalQueueWorker());
            if (MeshDescription.meshes.Length < MeshDescription.MESH_DECALS) return;
            var decals = MeshDescription.meshes[MeshDescription.MESH_DECALS];
            // AssetBundleManager.Instance.LoadAssetBundle(OcbMicroSplat.DecalBundlePath);
            // AssetBundleManager.Instance.LoadAssetBundle(OcbMicroSplat.DecalShaderBundle);
            // var decal_n = AssetBundleManager.Instance.Get<Texture2D>(
            //     OcbMicroSplat.DecalBundlePath, "ta_decals_n");
            // var shader = AssetBundleManager.Instance.Get<Shader>(
            //     OcbMicroSplat.DecalShaderBundle, "OcbDecalShader");
            // // if (shader == null || decal_n == null) return;
            // decals.material.shader = shader;
            // decals.textureAtlas.normalTexture = decal_n;
            // decals.material.SetTexture("_BumpMap", decal_n);
        }

    }

    // ####################################################################
    // ####################################################################

    // Listener to register where to draw decals
    // Also disables rendering of original quad faces
    [HarmonyPatch(typeof(BlockShapeTerrain), "renderFace")]
    static class BlockShapeTerrainRenderFacePatch
    {
        static bool Prefix(Vector3i _worldPos, BlockValue _blockValue, Vector3 _drawPos, Vector3[] _vertices)
        {
            // if (_worldPos.z + (int)_drawPos.z != 538) return false;
            if (!_blockValue.hasdecal) return false;
            lock (decals)
            {
                float height = _vertices[0].y * 0.25f + _vertices[1].y * 0.25f +
                    _vertices[2].y * 0.25f + _vertices[3].y * 0.25f;
                // Log.Out("Add decal {0} at {1}", _worldPos.z + _drawPos.z, height);
                // _world.GetDensity(_clrIdx, _blockPos)
                decals.Add(new DecalItem(
                    _worldPos.x + (int)(_drawPos.x + 0.5f),
                    height,
                    _worldPos.z + (int)(_drawPos.z + 0.5f),
                    _blockValue.decaltex));
            }
            return false;
        }

    }

    // Listener to register where to draw decals
    // Also disables rendering of original quad faces
    [HarmonyPatch(typeof(BlockShapeCube), "renderFace")]
    static class BlockShapeCubeRenderFacePatch
    {
        static bool Prefix(Vector3i _worldPos, BlockValue _blockValue, Vector3 _drawPos, Vector3[] _vertices)
        {
            // if (_worldPos.z + (int)_drawPos.z != 538) return false;
            if (!_blockValue.hasdecal) return false;
            lock (decals)
            {
                float height = _vertices[0].y * 0.25f + _vertices[1].y * 0.25f +
                    _vertices[2].y * 0.25f + _vertices[3].y * 0.25f;
                // Log.Out("Add decal {0} at {1}", _worldPos.z + _drawPos.z, height);
                decals.Add(new DecalItem(
                    _worldPos.x + (int)(_drawPos.x + 0.5f),
                    height,
                    _worldPos.z + (int)(_drawPos.z + 0.5f),
                    _blockValue.decaltex));
            }
            return false;
        }
    }


    // Listener to register where to clear decals
    [HarmonyPatch(typeof(BlockShape), "OnBlockRemoved")]
    static class BlockShapeOnBlockRemovedPatch
    {
        static void Prefix(Vector3i _blockPos, BlockValue _blockValue)
        {
            if (!_blockValue.hasdecal) return;
            lock (decals)
            {
                decals.Add(new DecalItem(
                    _blockPos.x,
                    _blockPos.y,
                    _blockPos.z,
                    -1));
            }
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(BlockValue), "decalface", MethodType.Getter)]
    static class BlockValueGetDecalFace
    {
        static bool Prefix(BlockValueV3 __instance, ref BlockFace __result)
        {
            __result = BlockFace.Top;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockValue), "decalface", MethodType.Setter)]
    static class BlockValueSetDecalFace
    {
        static bool Prefix(BlockValueV3 __instance, BlockFace value)
        {
            return false;
        }
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(BlockValue), "decaltex", MethodType.Getter)]
    static class BlockValueGetDecalTex
    {
        static bool Prefix(ref BlockValue __instance, ref byte __result)
        {
            __result = __instance.meta2and1;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockValue), "decaltex", MethodType.Setter)]
    static class BlockValueSetDecalTex
    {
        static bool Prefix(ref BlockValue __instance, byte value)
        {
            __instance.meta2and1 = value;
            return false;
        }
    }

    // ####################################################################
    // ####################################################################

}
