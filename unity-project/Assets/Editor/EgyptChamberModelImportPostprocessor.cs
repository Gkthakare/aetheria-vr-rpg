#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Applies conservative <see cref="ModelImporter"/> settings for the Egypt chamber FBX so Unity's
/// mesh import step does less aggressive optimization (which can surface "self-intersecting polygon"
/// warnings on messy authored quads/ngons). Some warnings may still appear if the source mesh is
/// genuinely degenerate — fix in Blender/Max and re-export if colliders or lighting look wrong.
/// </summary>
public sealed class EgyptChamberModelImportPostprocessor : AssetPostprocessor
{
    static bool IsEgyptChamberFbx(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var p = path.Replace('\\', '/');
        return p.IndexOf("ImportedContent/Environments/egypt-chamber-v1", System.StringComparison.OrdinalIgnoreCase) >= 0
               && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    void OnPreprocessModel()
    {
        if (!IsEgyptChamberFbx(assetPath))
            return;

        var mi = assetImporter as ModelImporter;
        if (mi == null)
            return;

        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.optimizeMeshPolygons = false;
        mi.optimizeMeshVertices = false;
        mi.importNormals = ModelImporterNormals.Import;
        // Source FBX is missing tangents on some sub-meshes; calculate to avoid import spam.
        mi.importTangents = ModelImporterTangents.CalculateMikk;
    }
}
#endif
