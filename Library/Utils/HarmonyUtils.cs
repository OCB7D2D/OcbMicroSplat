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

using HarmonyLib;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

public static class HarmonyUtils
{
    // Our AccessTools is too old and doesn't have this
    // Modern HarmonyX has `AccessTool.EnumeratorMoveNext`
    public static MethodInfo GetEnumeratorMoveNext(MethodBase method)
    {
        if (method is null)
        {
            Log.Out("AccessTools.EnumeratorMoveNext: method is null");
            return null;
        }

        var codes = PatchProcessor.ReadMethodBody(method).Where(pair => pair.Key == OpCodes.Newobj);
        if (codes.Count() != 1)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no Newobj opcode");
            return null;
        }
        var ctor = codes.First().Value as ConstructorInfo;
        if (ctor == null)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no constructor");
            return null;
        }
        var type = ctor.DeclaringType;
        if (type == null)
        {
            Log.Out($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} refers to a global type");
            return null;
        }
        return AccessTools.Method(type, nameof(IEnumerator.MoveNext));
    }

}
