using UnityEditor;

namespace ZombieHouse.EditorTools
{
    /// <summary>
    /// Import settings for the Blender heads in Assets/Resources/Heads.
    ///
    /// Readable, because Test Heads fires rays through the imported meshes to prove the eyes
    /// sit in their sockets and the teeth are not buried -- and it has to test the mesh Unity
    /// actually imported, after Unity's own handedness conversion, rather than the .obj text,
    /// or it could not catch a face that arrived on the back of its head. The CPU-side copy
    /// this keeps is a few hundred kilobytes across every variant.
    /// </summary>
    public class HeadImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/Heads/")) return;

            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
