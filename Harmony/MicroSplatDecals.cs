using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OcbMicroSplatDecals
{

    // ####################################################################
    // ####################################################################

    // Render Texture holding the dynamic decals splatmap
    public static RenderTexture decalsTexture = null;

    // Texture holding single pixel to draw
    private static Texture2D pixel = null;

    // The work queue holdig all positions to draw decal "pixels"
    private static readonly List<Vector3i> decals = new List<Vector3i>();

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
                if (OcbMicroSplatDecals.decalsTexture != null)
                {
                    // decals queue is comming from background thrad
                    // therefore aquire a lock for syncronization
                    lock (decals)
                    {
                        // Queue might be empty
                        if (decals.Count > 0)
                        {
                            int w = OcbMicroSplatDecals.decalsTexture.width / 2;
                            int h = OcbMicroSplatDecals.decalsTexture.height / 2;
                            foreach (var offset in decals)
                            {
                                // Get a random rotation to draw into green channel
                                float rot = GameManager.Instance.World.RandomRange(0, 1f);
                                // Set the color for the pixel to draw into the decal splatmap
                                // We store the decals index into the red channel (0 = no decal)
                                pixel.SetPixel(0, 0, new Color((1f + offset.z) / 255f, rot, 0, 1));
                                // Apply the color change on CPU
                                pixel.Apply(false, false);
                                // Copy CPU texture pixel into decals splatmap
                                Graphics.CopyTexture(pixel, 0, 0, 0, 0, 1, 1,
                                    OcbMicroSplatDecals.decalsTexture, 0, 0,
                                    offset.x + w, offset.y + h);
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
            AssetBundleManager.Instance.LoadAssetBundle(OcbMicroSplat.DecalBundlePath);
            AssetBundleManager.Instance.LoadAssetBundle(OcbMicroSplat.DecalShaderBundle);
            // var decal_n = AssetBundleManager.Instance.Get<Texture2D>(
            //     OcbMicroSplat.DecalBundlePath, "ta_decals_n");
            var shader = AssetBundleManager.Instance.Get<Shader>(
                OcbMicroSplat.DecalShaderBundle, "OcbDecalShader");
            // if (shader == null || decal_n == null) return;
            decals.material.shader = shader;
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
        static bool Prefix(Vector3i _worldPos, BlockValue _blockValue, Vector3 _drawPos)
        {
            if (!_blockValue.hasdecal) return false;
            lock (decals)
            {
                decals.Add(new Vector3i(
                    _worldPos.x + (int)_drawPos.x,
                    _worldPos.z + (int)_drawPos.z,
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
        static bool Prefix(Vector3i _worldPos, BlockValue _blockValue, Vector3 _drawPos)
        {
            if (!_blockValue.hasdecal) return false;
            lock (decals)
            {
                decals.Add(new Vector3i(
                    _worldPos.x + (int)_drawPos.x,
                    _worldPos.z + (int)_drawPos.z,
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
                decals.Add(new Vector3i(
                    _blockPos.x,
                    _blockPos.z,
                    -1));
            }
        }
    }

    // ####################################################################
    // ####################################################################

}
