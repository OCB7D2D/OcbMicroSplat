using System;
using System.Xml.Linq;
using UnityEngine;

public class MicroSplatBiomeColor
{

    // ####################################################################
    // ####################################################################

    public Color Color1 = Color.black;
    public Color Color2 = Color.black;

    // ####################################################################
    // ####################################################################

    public void Parse(XElement xml)
    {
        if (!xml.HasAttribute("color1")) throw new Exception(
            $"Mandatory attribute `color1` missing on {xml.Name}");
        if (!xml.HasAttribute("color2")) throw new Exception(
            $"Mandatory attribute `color2` missing on {xml.Name}");
        Color1 = StringParsers.ParseColor(xml.GetAttribute("color1"));
        Color2 = StringParsers.ParseColor(xml.GetAttribute("color2"));
        // Log.Out("Color1 {0} Color2 {1}", Color1, Color2);
    }

    // ####################################################################
    // ####################################################################

}
