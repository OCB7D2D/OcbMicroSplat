using UnityEngine;

public static class OcbTextureUtils
{

    // ####################################################################
    // ####################################################################

    // Blit texture from GPU into RenderTexture and copy pixels back
    public static Texture2D TextureFromGPU(Texture src, int idx, bool linear)
    {
        if (src == null) return null;
        // Create render texture target
        RenderTexture rt = new RenderTexture(
            src.width, src.height, 32);
        // RenderTexture.active = rt;
        Graphics.Blit(src, rt, idx, 0);
        // Create CPU texture to hold the pixels
        Texture2D tex = new Texture2D(src.width, src.height,
            TextureFormat.RGBA32, src.mipmapCount, linear);
        // Read pixels from current render texture as set by Blit 
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0, false);
        // Apply new pixels to CPU texture
        tex.Apply(true, false);
        // Return decompressed
        return tex.DeCompress();
    }

    // ####################################################################
    // ####################################################################

}
