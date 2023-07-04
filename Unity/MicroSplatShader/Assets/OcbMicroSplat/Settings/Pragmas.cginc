// Requires 3.5 for metal support?
#pragma target 3.0

// Declare custom shaders
#pragma vertex Vert
#pragma fragment Frag

// Force the hardware tier variants
// Don't rely on unity project setting
// https://docs.unity3d.com/Manual/SL-ShaderCompilationAPIs.html
#pragma only_renderers d3d9 d3d11 gles3 metal glcore vulkan
#pragma exclude_renderers gles ps4 xboxone switch ps5

// Enable/Disable some variants
#pragma multi_compile_instancing
#pragma multi_compile_local __ _ALPHATEST_ON
