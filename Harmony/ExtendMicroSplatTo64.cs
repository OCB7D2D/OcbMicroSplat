using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ExtendMicroSplatTo64
{

    [HarmonyPatch(typeof(MicroSplatPropData), "RevisionData")]
    class MicroSplatPropDataRevisionData
    {
        static bool Prefix(MicroSplatPropData __instance,
            ref Color[] ___values)
        {
            if (___values.Length == 256)
            {
                Color[] array = new Color[2048];
                for (int i = 0; i < 16; i++)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        array[j * 32 + i] = ___values[j * 32 + i];
                    }
                }

                ___values = array;
            }
            else if (___values.Length == 512)
            {
                Color[] array2 = new Color[2048];
                for (int k = 0; k < 32; k++)
                {
                    for (int l = 0; l < 16; l++)
                    {
                        array2[l * 32 + k] = ___values[l * 32 + k];
                    }
                }

                ___values = array2;
            }
            else if (___values.Length == 1024)
            {
                Color[] array2 = new Color[2048];
                for (int k = 0; k < 64; k++)
                {
                    for (int l = 0; l < 16; l++)
                    {
                        array2[l * 32 + k] = ___values[l * 32 + k];
                    }
                }

                ___values = array2;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(MicroSplatPropData), "GetTexture")]
    class MicroSplatPropDataGetTexture
    {
        static bool Prefix(MicroSplatPropData __instance,
            ref Texture2D ___tex, ref Color[] ___values,
            ref Texture2D __result)
        {
            __instance.RevisionData();
            if (___tex == null)
            {
                if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
                {
                    ___tex = new Texture2D(32, 64, TextureFormat.RGBAFloat, mipChain: false, linear: true);
                }
                else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))
                {
                    ___tex = new Texture2D(32, 64, TextureFormat.RGBAHalf, mipChain: false, linear: true);
                }
                else
                {
                    Debug.LogError("Could not create RGBAFloat or RGBAHalf format textures, per texture properties will be clamped to 0-1 range, which will break things");
                    ___tex = new Texture2D(32, 64, TextureFormat.RGBA32, mipChain: false, linear: true);
                }

                ___tex.hideFlags = HideFlags.HideAndDontSave;
                ___tex.wrapMode = TextureWrapMode.Clamp;
                ___tex.filterMode = FilterMode.Point;
            }
            ___tex.SetPixels(___values);
            ___tex.Apply();
            __result = ___tex;
            return false;
        }
    }


}
