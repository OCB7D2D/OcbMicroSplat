using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class OcbMicroSplat : IModApi
{

    public void InitMod(Mod mod)
    {
        Debug.Log("Loading OCB MicroSplat Patch: " + GetType().ToString());
        new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());
    }

}
