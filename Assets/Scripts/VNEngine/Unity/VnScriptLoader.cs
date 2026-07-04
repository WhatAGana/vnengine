using System;
using System.Collections.Generic;
using UnityEngine;

namespace VNEngine.Unity
{
    public static class VnScriptLoader
    {
        // resourcesSubfolder is relative to any Resources/ folder, e.g. "scripts"
        // loads Assets/Resources/scripts/*.vns (imported as TextAssets).
        public static VnProgram LoadAndCompile(string resourcesSubfolder)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesSubfolder);
            if (assets == null || assets.Length == 0)
                throw new VnException($"no .vns TextAssets found under Resources/{resourcesSubfolder}");

            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            var parsed = new List<List<Command>>();
            foreach (var ta in assets)
                parsed.Add(Parser.Parse(LineReader.Read(ta.text, ta.name)));

            return Compiler.Compile(parsed);
        }
    }
}
