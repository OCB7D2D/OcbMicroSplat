/* MIT License

Copyright (c) 2022-2023 OCB7D2D

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class OcbTextureUtils
{

    // ####################################################################
    // ####################################################################

    // Blit texture from GPU into RenderTexture and copy pixels back
    public static Texture2D TextureFromGPU(Texture src, int idx, bool linear)
    {
        if (src == null) return null;
        // Create render texture target
        var fmt = SystemInfo.GetCompatibleFormat(
            src.graphicsFormat, FormatUsage.Render);
        RenderTexture rt = RenderTexture.GetTemporary(
            src.width, src.height, 32, fmt);
        RenderTexture active = RenderTexture.active;
        Graphics.Blit(src, rt, idx, 0);
        // Create CPU texture to hold the pixels
        Texture2D tex = new Texture2D(src.width, src.height,
            fmt, src.mipmapCount, TextureCreationFlags.MipChain);
        // Read pixels from current render texture as set by Blit 
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0, true);
        // Apply new pixels to CPU texture
        tex.Apply(false, false);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    // ####################################################################
    // ####################################################################

}
