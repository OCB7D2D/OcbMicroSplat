﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class OcbDecalShader
{

    // Apply custom micro splat textures when xml is loaded
    [HarmonyPatch(typeof(WorldEnvironment), "OnXMLChanged")]
    static class PatchWorldEnvironmentOnXMLChanged
    {

        static void Postfix()
        {
            if (GameManager.IsDedicatedServer) return; // Nothing to do here
            if (MeshDescription.meshes.Length < MeshDescription.MESH_DECALS) return;
            var decals = MeshDescription.meshes[MeshDescription.MESH_DECALS];

            AssetBundleManager.Instance.LoadAssetBundle(OcbMicroSplat.DecalBundlePath);
            var decal_n = AssetBundleManager.Instance.Get<Texture2D>(
                OcbMicroSplat.DecalBundlePath, "ta_decals_n");
            var shader = AssetBundleManager.Instance.Get<Shader>(
                OcbMicroSplat.DecalBundlePath, "OcbDecalShader");
            if (shader == null || decal_n == null) return;
            decals.material.shader = shader;
            decals.textureAtlas.normalTexture = decal_n;
            decals.material.SetTexture("_BumpMap", decal_n);
        }

    }

}
