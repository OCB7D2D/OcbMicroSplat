// Requires 3.5 for metal support?
#pragma target 3.0

// Declare custom shaders
#pragma vertex Vert
#pragma fragment Frag

// Force the hardware tier variants
// Don't rely on unity project setting
// https://docs.unity3d.com/Manual/SL-ShaderCompilationAPIs.html
// #pragma only_renderers d3d9 d3d11 gles3 metal glcore vulkan
// #pragma exclude_renderers gles ps4 xboxone switch ps5

// Enable instancing variants
#pragma multi_compile_instancing
// Enable certain instancing options
#pragma instancing_options assumeuniformscaling nolightprobe nolightmap
// nomatrices forwardadd -> options doesn't seem to be supported by unity?

// _ALPHATEST_ON is only required for terrain holes
// #pragma multi_compile_local __ _ALPHATEST_ON

// We know we only ever use exponential fog
// #pragma skip_variants FOG_LINEAR FOG_EXP2
