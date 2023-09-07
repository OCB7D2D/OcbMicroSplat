Shader "OcbDecalShader"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" { }
        _BumpMap("Bump", 2D) = "white" { }
        _FadeDistance("FadeDistance", Range(1, 400)) = 33
        // There is no information stored in those textures
        // So we skip them completely to safe sample calls
        // _MetallicGlossMap("Metallic", 2D) = "black" { }
        // _OcclusionMap("Occlusion", 2D) = "white" { }
        // Light Power is actually emission power
        // But since we have no emission map we
        // are not using this deprecated uniform
        // _EmissionMap("Emission", 2D) = "black" { }
        // _LightPower("Legacy - Light Power", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM

        // Physically based Standard lighting model
        #pragma surface surf Standard vertex:vert

        // Use shader model 3.0 target
        // to get nicer looking lighting
        #pragma target 3.0

        // Force the hardware tier variants
        // Don't rely on unity project setting
        // https://docs.unity3d.com/Manual/SL-ShaderCompilationAPIs.html
        // #pragma only_renderers d3d9 d3d11 gles3 metal glcore vulkan
        // #pragma exclude_renderers gles ps4 xboxone switch ps5

        sampler2D _MainTex;
        sampler2D _BumpMap;
        float _FadeDistance;

        // Unused, but in original shader
        // sampler2D _MetallicGlossMap;
        // sampler2D _OcclusionMap;
        // sampler2D _EmissionMap;
        // float _LightPower;

        sampler2D unity_DitherMask;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float3 worldPos;
            float4 screenPos;
            float dist;
            // Not used, so skip these
            // float2 uv_MetallicGlossMap;
            // float2 uv_OcclusionMap;
            // float2 uv_EmissionMap;
        };

        // Calculate distance to camera once in the vertex shader
        // We will use the interpolated value for distance dithering
        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Calculate distance from object to camera
            // But only in xz direction, as it is enough
            float dist = length(ObjSpaceViewDir(v.vertex).xz);
            float lower = dist - _FadeDistance * 0.4 / 0.6;
            o.dist = 1.0 - lower / _FadeDistance;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a main diffuse texture
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            // Immplement alpha cutout via defined limit
            clip(c.w - 0.575);
            // Calculate dithering level for distance fade
            clip(IN.dist - tex2D(unity_DitherMask,
                IN.screenPos.xy / IN.screenPos.ww
                    * _ScreenParams.xy * 0.25).a);
            // Attone albedo for max brightness
            c.rgb = pow(c.rgb, 0.65) * 0.35;
            // Apply the albedo color
            o.Albedo = c.rgb;
            // No further alpha support
            o.Alpha = c.w;
            // Get the normals from newly correctly packed texture
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            // There is no information stored there, so skip sampling it
            // fixed4 sao = tex2D(_MetallicGlossMap, IN.uv_MetallicGlossMap);
            o.Smoothness = 0.075;
            o.Metallic = 0.025;
        }
        ENDCG
    }
}
