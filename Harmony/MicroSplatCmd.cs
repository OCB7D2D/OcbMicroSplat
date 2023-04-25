using UnityEngine;
using System.Collections.Generic;
using static StringParsers;

public class OcbMicroSplatCmd : ConsoleCmdAbstract
{

    private static string info = "microsplat";
    public override string[] GetCommands()
    {
        return new string[2] { info, "msplat" };
    }

    public override string GetDescription()
    {
        return "Adjust settings for MicroSplat shaders";
    }

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;

    public static Vector4 ParseVector4(string _input)
    {
        SeparatorPositions separatorPositions = GetSeparatorPositions(_input, ',', 3);
        if (separatorPositions.TotalFound != 3) return Vector4.zero;
        return new Vector4(ParseFloat(_input, 0, separatorPositions.Sep1 - 1),
            ParseFloat(_input, separatorPositions.Sep1 + 1, separatorPositions.Sep2 - 1),
            ParseFloat(_input, separatorPositions.Sep2 + 1, separatorPositions.Sep3 - 1),
            ParseFloat(_input, separatorPositions.Sep3 + 1));
    }


    void SetMatProperty(Material mat, string type, string name, string value)
    {
        if (mat.HasProperty(name))
        {
            switch (type)
            {
                case "f":
                case "float":
                    mat.SetFloat(name, ParseFloat(value));
                    break;
                case "v2":
                case "vec2":
                case "vector2":
                    mat.SetVector(name, ParseVector2(value));
                    break;
                case "v3":
                case "vec3":
                case "vector3":
                    mat.SetVector(name, ParseVector3(value));
                    break;
                case "v4":
                case "vec4":
                case "vector4":
                    mat.SetVector(name, ParseVector4(value));
                    break;
            }
        }
        else
        {
            Log.Warning("Material {0} missing {1}",
                mat.name, name);
        }
    }

    object GetMatProperty(Material mat, string type, string name)
    {
        if (mat.HasProperty(name))
        {
            switch (type)
            {
                case "f":
                case "float":
                    return mat.GetFloat(name);
                case "v2":
                case "vec2":
                case "vector2":
                case "v3":
                case "vec3":
                case "vector3":
                case "v4":
                case "vec4":
                case "vector4":
                    return mat.GetVector(name);
                default:
                    return $"Unknown type {type}";
            }
        }
        return $"missing {name}";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        // set v4 _SnowDistanceResampleScaleStrengthFade 1,2,3,4
        var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];

        if (_params.Count == 3)
            switch (_params[0])
            {
                case "get":
                    Log.Out("Showing current value of {0}", _params[0]);
                    Log.Out(" details: {1}", GetMatProperty(mesh.material, _params[1], _params[2]));
                    Log.Out(" distant: {1}", GetMatProperty(mesh.material, _params[1], _params[2]));
                    return;
            }
        if (_params.Count == 4)
            switch (_params[0])
            {
                case "set":
                    SetMatProperty(mesh.material, _params[1], _params[2], _params[3]);
                    SetMatProperty(mesh.materialDistant, _params[1], _params[2], _params[3]);
                    return;
            }

        // If not returned by now we have not done
        Log.Warning("Invalid `microsplat` command");

    }

}
