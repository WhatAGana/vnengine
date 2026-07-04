using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace VNEngine.Editor
{
    // Imports *.vns files as TextAsset so they can be placed under
    // Assets/Resources/scripts and loaded with Resources.LoadAll<TextAsset>
    // on every platform (Android/iOS included).
    [ScriptedImporter(version: 1, ext: "vns")]
    public class VnsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            var asset = new TextAsset(text);
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}
