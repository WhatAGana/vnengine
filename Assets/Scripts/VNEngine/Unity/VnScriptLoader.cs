using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VNEngine.Unity
{
    public static class VnScriptLoader
    {
        // resourcesSubfolder is relative to any Resources/ folder, e.g. "scripts"
        // loads Assets/Resources/scripts/*.vns (imported as TextAssets).
        public static VnProgram LoadAndCompile(string resourcesSubfolder)
            => LoadAndCompile(resourcesSubfolder, out _);

        // Also yields a stable fingerprint of the loaded scripts for save compat.
        public static VnProgram LoadAndCompile(string resourcesSubfolder, out string programHash)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesSubfolder);
            if (assets == null || assets.Length == 0)
                throw new VnException($"no .vns TextAssets found under Resources/{resourcesSubfolder}");

            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            var joined = new StringBuilder();
            var parsed = new List<List<Command>>();
            foreach (var ta in assets)
            {
                joined.Append(ta.name).Append('\n').Append(ta.text).Append('\n');
                parsed.Add(Parser.Parse(LineReader.Read(ta.text, ta.name)));
            }

            programHash = VnHash.Fnv1a(joined.ToString());
            return Compiler.Compile(parsed);
        }
    }
}
