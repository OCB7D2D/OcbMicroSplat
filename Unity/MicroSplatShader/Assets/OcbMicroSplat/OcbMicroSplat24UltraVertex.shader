////////////////////////////////////////
// MicroSplat
// Copyright (c) Jason Booth
//
// Auto-generated shader code, don't hand edit!
//
//   Unity Version: 2020.3.14f1
//   MicroSplat Version: 3.9
//   Render Pipeline: Standard
//   Platform: WindowsEditor
////////////////////////////////////////


Shader "OcbMicroSplat24UltraVertex"
{

   Properties
   {
      [HideInInspector] _CustomControl0 ("Control0", 2D) = "red" {}
      [HideInInspector] _CustomControl1 ("Control1", 2D) = "black" {}

      // Splats
      [NoScaleOffset]_Diffuse ("Diffuse Array", 2DArray) = "white" {}
      [NoScaleOffset]_NormalSAO ("Normal Array", 2DArray) = "bump" {}
      [NoScaleOffset]_PerTexProps("Per Texture Properties", 2D) = "black" {}
      // [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}
      // [HideInInspector] _PerPixelNormal("Per Pixel Normal", 2D) = "bump" {}
      _Contrast("Blend Contrast", Range(0.01, 0.99)) = 0.4
      _UVScale("UV Scales", Vector) = (45, 45, 0, 0)

      // for Unity 2020.3 bug
      _MainTex("Unity Bug", 2D) = "white" {}
      [NoScaleOffset]_SmoothAO ("Smooth AO Array", 2DArray) = "black" {}

      // terrain
      [NoScaleOffset]_DetailNoise("Detail Noise (Lum/Normal)", 2D) = "grey" {}
      _DetailNoiseScaleStrengthFade("Detail Scale", Vector) = (4, 0.5, 5, 0)
      // distance noise
      [NoScaleOffset]_DistanceNoise("Distance Noise (Lum/Normal)", 2D) = "grey" {}
      _DistanceNoiseScaleStrengthFade("Distance Scale", Vector) = (0.25, 0.5, 100, 250)
      _NoiseHeight("Noise Texture", 2D) = "grey" {}
      _NoiseHeightData("Noise Height Data", Vector) = (1, 0.15, 0, 0)
      // distance resampling
      // uv scale, near, fast
      _ResampleDistanceParams("ResampleDistanceParams", Vector) = (0.25, 180, 500, 0)

      _DistanceResampleAlbedoStrength("Resampled Albedo Strength", Range(0.1, 1.0)) = 1
      _DistanceResampleNormalStrength("Resampled Normal Strength", Range(0.1, 1.3)) = 1
      // terrain
      [NoScaleOffset]_NormalNoise("Normal Noise", 2D) = "bump" {}
      _NormalNoiseScaleStrength("Normal Scale", Vector) = (8, 0.5, 0, 0)

      [NoScaleOffset]_NormalNoise2("Normal Noise 2", 2D) = "bump" {}
      _NormalNoiseScaleStrength2("Normal Scale 2", Vector) = (8, 0.5, 0, 0)

      [NoScaleOffset]_NormalNoise3("Normal Noise 3", 2D) = "bump" {}
      _NormalNoiseScaleStrength3("Normal Scale 3", Vector) = (8, 0.5, 0, 0)

      _WorldHeightRange("World Height Range", Vector) = (0, 500, 0, 0)
      // for custom 7D2D
      _WorldDim("World Dimension", Vector) = (6144, 6144, 0, 0)
      // _OriginPos("Origin Shift", Vector) = (0, 0, 0, 0)

      // geotexture
      _ProcTexCurves("ProcTextureCurves", 2D) = "black" {}
      _ProcTexParams("ProcTextureParams", 2D) = "black" {}
      _ProcTexNoise("Proc Noise Texture", 2D) = "white" {}
      _PCLayerCount("Layer Count", Int) = 0
      _ProcBiomeCurveWeight("Curve Weight for Biome mask", Range(0.01,0.5)) = 0.5
      _ProcTexBiomeMask("Biome Mask", 2D) = "white" {}
      _ProcTexBiomeMask2("Biome Mask 2", 2D) = "white" {}

      _TriplanarContrast("Triplanar Contrast", Range(1.0, 8)) = 4
      _TriplanarUVScale("Triplanar UV Scale", Vector) = (1, 1, 0, 0)

   }

   SubShader
   {
      Tags {"RenderType" = "Opaque" "Queue" = "Geometry+100" "IgnoreProjector" = "False"  "TerrainCompatible" = "true" "SplatCount" = "32"}

      Pass
      {
         Name "FORWARD"
         Tags { "LightMode" = "ForwardBase" }

         CGPROGRAM

         #include "Settings/Pragmas.cginc"
         #pragma multi_compile_fog
         #pragma multi_compile_fwdbase
         #include "HLSLSupport.cginc"

         #define _PASSFORWARD 1

         #include "UnityShaderVariables.cginc"
         #include "UnityShaderUtilities.cginc"

         #define _VERTEX7D2D 1

         #include "Settings/BaseSettings.cginc"
         #include "Settings/Max24Textures.cginc"
         #include "Settings/QualityUltra.cginc"

         #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd

         #define _STANDARD 1

         // If your looking in here and thinking WTF, yeah, I know. These are taken from the SRPs, to allow us to use the same
         // texturing library they use. However, since they are not included in the standard pipeline by default, there is no
         // way to include them in and they have to be inlined, since someone could copy this shader onto another machine without
         // MicroSplat installed. Unfortunate, but I'd rather do this and have a nice library for texture sampling instead
         // of the patchy one Unity provides being inlined/emulated in HDRP/URP. Strangely, PSSL and XBoxOne libraries are not
         // included in the standard SRP code, but they are in tons of Unity own projects on the web, so I grabbed them from there.

         #include "Includes/Shader/ForwardBase.cginc"

         ENDCG
      }

      // ---- forward rendering additive lights pass:
      Pass
      {
         Name "FORWARD"
         Tags { "LightMode" = "ForwardAdd" }
         ZWrite Off Blend One One

         CGPROGRAM

         #include "Settings/Pragmas.cginc"
         #pragma multi_compile_fog
         #pragma multi_compile_fwdadd_fullshadows
         #include "HLSLSupport.cginc"

         #include "UnityShaderVariables.cginc"
         #include "UnityShaderUtilities.cginc"

         #define _PASSFORWARD 1

         #define _VERTEX7D2D 1

         #include "Settings/BaseSettings.cginc"
         #include "Settings/Max24Textures.cginc"
         #include "Settings/QualityUltra.cginc"

         #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd

         #define _STANDARD 1

         // If your looking in here and thinking WTF, yeah, I know. These are taken from the SRPs, to allow us to use the same
         // texturing library they use. However, since they are not included in the standard pipeline by default, there is no
         // way to include them in and they have to be inlined, since someone could copy this shader onto another machine without
         // MicroSplat installed. Unfortunate, but I'd rather do this and have a nice library for texture sampling instead
         // of the patchy one Unity provides being inlined/emulated in HDRP/URP. Strangely, PSSL and XBoxOne libraries are not
         // included in the standard SRP code, but they are in tons of Unity own projects on the web, so I grabbed them from there.

         #include "Includes/Shader/ForwardAdd.cginc"

         ENDCG

      }

      // ---- deferred shading pass:
      Pass
      {
         Name "DEFERRED"
         Tags { "LightMode" = "Deferred" }

         CGPROGRAM

         #include "Settings/Pragmas.cginc"
         #pragma multi_compile_fog
         #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
         #pragma multi_compile_prepassfinal
         #include "HLSLSupport.cginc"

         #include "UnityShaderVariables.cginc"
         #include "UnityShaderUtilities.cginc"
         #include "UnityCG.cginc"

         #define _PASSGBUFFER 1

         #define _VERTEX7D2D 1

         #include "Settings/BaseSettings.cginc"
         #include "Settings/Max24Textures.cginc"
         #include "Settings/QualityUltra.cginc"

         #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd

         #define _STANDARD 1

         // If your looking in here and thinking WTF, yeah, I know. These are taken from the SRPs, to allow us to use the same
         // texturing library they use. However, since they are not included in the standard pipeline by default, there is no
         // way to include them in and they have to be inlined, since someone could copy this shader onto another machine without
         // MicroSplat installed. Unfortunate, but I'd rather do this and have a nice library for texture sampling instead
         // of the patchy one Unity provides being inlined/emulated in HDRP/URP. Strangely, PSSL and XBoxOne libraries are not
         // included in the standard SRP code, but they are in tons of Unity own projects on the web, so I grabbed them from there.

         #include "Includes/Shader/Deferred.cginc"

         ENDCG

      }

      Pass {
         Name "ShadowCaster"
         Tags { "LightMode" = "ShadowCaster" }
         ZWrite On ZTest LEqual

         CGPROGRAM

         #include "Settings/Pragmas.cginc"
         #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
         #pragma multi_compile_shadowcaster
         #include "HLSLSupport.cginc"

         #include "UnityShaderVariables.cginc"
         #include "UnityShaderUtilities.cginc"

         #include "UnityCG.cginc"
         #include "Lighting.cginc"
         #include "UnityPBSLighting.cginc"

         #define _PASSSHADOW 1

         #define _VERTEX7D2D 1

         #include "Settings/BaseSettings.cginc"
         #include "Settings/Max24Textures.cginc"
         #include "Settings/QualityUltra.cginc"

         #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd

         #define _STANDARD 1

         // If your looking in here and thinking WTF, yeah, I know. These are taken from the SRPs, to allow us to use the same
         // texturing library they use. However, since they are not included in the standard pipeline by default, there is no
         // way to include them in and they have to be inlined, since someone could copy this shader onto another machine without
         // MicroSplat installed. Unfortunate, but I'd rather do this and have a nice library for texture sampling instead
         // of the patchy one Unity provides being inlined/emulated in HDRP/URP. Strangely, PSSL and XBoxOne libraries are not
         // included in the standard SRP code, but they are in tons of Unity own projects on the web, so I grabbed them from there.

         #include "Includes/Shader/ShadowCaster.cginc"

         ENDCG

      }

      // ---- meta information extraction pass:
      Pass
      {
         Name "Meta"
         Tags { "LightMode" = "Meta" }
         Cull Off

         CGPROGRAM

         #include "Settings/Pragmas.cginc"
         #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
         #pragma shader_feature EDITOR_VISUALIZATION
         #include "HLSLSupport.cginc"

         #include "UnityShaderVariables.cginc"
         #include "UnityShaderUtilities.cginc"

         #include "UnityCG.cginc"

         #define _PASSMETA 1

         #define _VERTEX7D2D 1

         #include "Settings/BaseSettings.cginc"
         #include "Settings/Max24Textures.cginc"
         #include "Settings/QualityUltra.cginc"

         #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd

         #define _STANDARD 1

         // If your looking in here and thinking WTF, yeah, I know. These are taken from the SRPs, to allow us to use the same
         // texturing library they use. However, since they are not included in the standard pipeline by default, there is no
         // way to include them in and they have to be inlined, since someone could copy this shader onto another machine without
         // MicroSplat installed. Unfortunate, but I'd rather do this and have a nice library for texture sampling instead
         // of the patchy one Unity provides being inlined/emulated in HDRP/URP. Strangely, PSSL and XBoxOne libraries are not
         // included in the standard SRP code, but they are in tons of Unity own projects on the web, so I grabbed them from there.

         #include "Includes/Shader/Meta.cginc"

         ENDCG

      }

      UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
      UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"

   }


   CustomEditor "MicroSplatShaderGUI"
}
