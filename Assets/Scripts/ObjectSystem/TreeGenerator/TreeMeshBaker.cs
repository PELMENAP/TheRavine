#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class TreeMeshBaker : EditorWindow
{
    private const string BakeShaderName = "Hidden/TreeBillboardBake";

    private MtreeComponent _source;
    private TreeMeshCollection _targetCollection;
    private int _count = 8;
    private string _savePath = "Assets/Trees/Baked";
    private float _billboardPitch = 30f;
    private int _cellResolution = 256;
    private int _dilationPasses = 8;

    [MenuItem("Tools/Tree Mesh Baker")]
    private static void Open() => GetWindow<TreeMeshBaker>("Tree Mesh Baker");

    private void OnGUI()
    {
        _source = (MtreeComponent)EditorGUILayout.ObjectField("MtreeComponent", _source, typeof(MtreeComponent), true);
        _targetCollection = (TreeMeshCollection)EditorGUILayout.ObjectField("Collection", _targetCollection, typeof(TreeMeshCollection), false);
        _count = Mathf.Max(1, EditorGUILayout.IntField("Variant Count", _count));
        _billboardPitch = EditorGUILayout.Slider("Billboard Pitch", _billboardPitch, 0f, 89f);
        _cellResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(EditorGUILayout.IntField("Billboard Cell", _cellResolution), 32, 2048));
        _dilationPasses = Mathf.Clamp(EditorGUILayout.IntField("Dilation Passes", _dilationPasses), 0, 64);

        EditorGUILayout.BeginHorizontal();
        _savePath = EditorGUILayout.TextField("Save Path", _savePath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFolderPanel("Save folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
                _savePath = "Assets" + path.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(_source == null || _targetCollection == null);
        if (GUILayout.Button("Bake"))
            Bake();
        EditorGUI.EndDisabledGroup();
    }

    private void Bake()
    {
        Shader bakeShader = Shader.Find(BakeShaderName);
        if (bakeShader == null)
        {
            Debug.LogError($"[TreeMeshBaker] Shader {BakeShaderName} not found");
            return;
        }

        if (!Directory.Exists(_savePath))
            Directory.CreateDirectory(_savePath);

        MeshFilter filter = _source.GetComponent<MeshFilter>();
        MeshRenderer renderer = _source.GetComponent<MeshRenderer>();
        Mesh originalMesh = filter.sharedMesh;
        int originalLod = _source.LodIndex;
        var originalSeeds = new int[_source.treeFunctionsAssets.Count];
        for (int i = 0; i < originalSeeds.Length; i++)
            originalSeeds[i] = _source.treeFunctionsAssets[i].seed;

        int lodCount = Mathf.Max(1, _source.LODs.Count);
        int atlasWidth = _cellResolution * 2;
        int atlasHeight = _cellResolution * _count;
        var atlasAlbedo = new Color32[atlasWidth * atlasHeight];
        var atlasNormal = new Color32[atlasWidth * atlasHeight];
        var variants = new TreeMeshCollection.TreeVariant[_count];
        var bakeMaterial = new Material(bakeShader) { hideFlags = HideFlags.HideAndDontSave };

        try
        {
            _source.ResetGlobalWind();

            for (int i = 0; i < _count; i++)
            {
                var rng = new System.Random(i);
                foreach (var func in _source.treeFunctionsAssets)
                    func.seed = rng.Next(0, 10000);

                var lodMeshes = new Mesh[lodCount];
                for (int l = 0; l < lodCount; l++)
                {
                    _source.LodIndex = l;
                    lodMeshes[l] = Object.Instantiate(_source.GenerateTree(instantAo: true));
                }

                Mesh combined = Combine(lodMeshes, _source.slope, _source.bias, out TreeMeshCollection.IndexRange[] ranges);
                combined.name = $"tree_{i}";
                string meshPath = $"{_savePath}/tree_{i}.mesh";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(combined, meshPath);

                Bounds bounds = lodMeshes[0].bounds;
                Material[] materials = renderer.sharedMaterials;

                for (int k = 0; k < 2; k++)
                {
                    RenderView(lodMeshes[0], materials, bounds, k == 0 ? 1 : -1, bakeMaterial,
                        out Color32[] albedo, out Color32[] normal);
                    Dilate(albedo, normal, _cellResolution, _dilationPasses);
                    Blit(albedo, atlasAlbedo, k * _cellResolution, i * _cellResolution, atlasWidth);
                    Blit(normal, atlasNormal, k * _cellResolution, i * _cellResolution, atlasWidth);
                }

                float p = _billboardPitch * Mathf.Deg2Rad;
                variants[i] = new TreeMeshCollection.TreeVariant
                {
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath),
                    ranges = ranges,
                    billboardBox = new Vector4(
                        bounds.size.x,
                        bounds.size.y * Mathf.Cos(p) + bounds.size.z * Mathf.Sin(p),
                        bounds.center.y,
                        bounds.center.z),
                    billboardCell = i
                };

                for (int l = 0; l < lodCount; l++)
                    Object.DestroyImmediate(lodMeshes[l]);

                EditorUtility.DisplayProgressBar("Baking trees", $"{i + 1}/{_count}", (float)(i + 1) / _count);
            }

            Texture2D albedoAtlas = SaveAtlas(atlasAlbedo, atlasWidth, atlasHeight, $"{_savePath}/tree_billboard_albedo.png", true);
            Texture2D normalAtlas = SaveAtlas(atlasNormal, atlasWidth, atlasHeight, $"{_savePath}/tree_billboard_normal.png", false);

            _targetCollection.SetBakeResult(variants, lodCount, albedoAtlas, normalAtlas, _billboardPitch);
            _targetCollection.ApplyBillboardMaterial();
            EditorUtility.SetDirty(_targetCollection);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TreeMeshBaker] Baked {_count} variants x {lodCount} LODs to {_savePath}");
        }
        finally
        {
            for (int i = 0; i < originalSeeds.Length; i++)
                _source.treeFunctionsAssets[i].seed = originalSeeds[i];
            _source.LodIndex = originalLod;
            filter.sharedMesh = originalMesh;
            Object.DestroyImmediate(bakeMaterial);
            EditorUtility.ClearProgressBar();
        }
    }

    private static Mesh Combine(Mesh[] lods, float slope, float bias, out TreeMeshCollection.IndexRange[] ranges)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var bark = new List<int>();
        var leaves = new List<int>();
        var barkStart = new int[lods.Length + 1];
        var leafStart = new int[lods.Length + 1];

        for (int l = 0; l < lods.Length; l++)
        {
            Mesh m = lods[l];
            int baseVertex = vertices.Count;
            barkStart[l] = bark.Count;
            leafStart[l] = leaves.Count;

            vertices.AddRange(m.vertices);
            normals.AddRange(m.normals);
            uvs.AddRange(m.uv);

            Color[] c = m.colors;
            if (c.Length == m.vertexCount) colors.AddRange(c);
            else for (int v = 0; v < m.vertexCount; v++) colors.Add(Color.white);

            AppendIndices(bark, m.GetIndices(0), baseVertex);
            if (m.subMeshCount > 1)
                AppendIndices(leaves, m.GetIndices(1), baseVertex);
        }

        barkStart[lods.Length] = bark.Count;
        leafStart[lods.Length] = leaves.Count;

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.subMeshCount = TreeMeshCollection.Submeshes;
        mesh.SetTriangles(bark, 0);
        mesh.SetTriangles(leaves, 1);
        mesh.lodCount = lods.Length;

        ranges = new TreeMeshCollection.IndexRange[lods.Length * TreeMeshCollection.Submeshes];
        uint leafBase = (uint)bark.Count;

        for (int l = 0; l < lods.Length; l++)
        {
            uint barkCount = (uint)(barkStart[l + 1] - barkStart[l]);
            uint leafCount = (uint)(leafStart[l + 1] - leafStart[l]);

            mesh.SetLod(0, l, new MeshLodRange((uint)barkStart[l], barkCount));
            mesh.SetLod(1, l, new MeshLodRange((uint)leafStart[l], leafCount));

            ranges[l * TreeMeshCollection.Submeshes] = new TreeMeshCollection.IndexRange { start = (uint)barkStart[l], count = barkCount };
            ranges[l * TreeMeshCollection.Submeshes + 1] = new TreeMeshCollection.IndexRange { start = leafBase + (uint)leafStart[l], count = leafCount };
        }

        mesh.lodSelectionCurve = new Mesh.LodSelectionCurve(slope, bias);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AppendIndices(List<int> target, int[] indices, int offset)
    {
        for (int i = 0; i < indices.Length; i++)
            target.Add(indices[i] + offset);
    }

    private void RenderView(Mesh mesh, Material[] materials, Bounds bounds, int facing, Material bakeMaterial,
        out Color32[] albedo, out Color32[] normal)
    {
        float p = _billboardPitch * Mathf.Deg2Rad;
        Vector3 dir = new(0f, -Mathf.Sin(p), facing * Mathf.Cos(p));
        Vector3 up = new(0f, Mathf.Cos(p), facing * Mathf.Sin(p));

        float width = bounds.size.x;
        float height = bounds.size.y * Mathf.Cos(p) + bounds.size.z * Mathf.Sin(p);
        float depth = bounds.size.magnitude + 1f;
        Vector3 cameraPos = bounds.center - dir * depth;

        Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                         Matrix4x4.TRS(cameraPos, Quaternion.LookRotation(dir, up), Vector3.one).inverse;
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(
            Matrix4x4.Ortho(-width * 0.5f, width * 0.5f, -height * 0.5f, height * 0.5f, 0.01f, depth * 2f), false);

        var albedoRT = RenderTexture.GetTemporary(new RenderTextureDescriptor(_cellResolution, _cellResolution, RenderTextureFormat.ARGB32, 24) { sRGB = true });
        var normalRT = RenderTexture.GetTemporary(new RenderTextureDescriptor(_cellResolution, _cellResolution, RenderTextureFormat.ARGB32, 0) { sRGB = false });

        var cmd = new CommandBuffer { name = "TreeBillboardBake" };
        cmd.SetRenderTarget(new RenderTargetIdentifier[] { albedoRT, normalRT }, albedoRT.depthBuffer);
        cmd.ClearRenderTarget(true, true, Color.clear);
        cmd.SetViewProjectionMatrices(view, proj);

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            Material source = sub < materials.Length ? materials[sub] : null;
            var props = new MaterialPropertyBlock();
            Texture texture = source != null ? source.mainTexture : null;
            props.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
            props.SetColor("_Color", source != null && (source.HasProperty("_Color") || source.HasProperty("_BaseColor")) ? source.color : Color.white);
            props.SetFloat("_Cutoff", source != null && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : (sub == 0 ? 0f : 0.5f));
            cmd.DrawMesh(mesh, Matrix4x4.identity, bakeMaterial, sub, 0, props);
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();

        albedo = ReadBack(albedoRT, false);
        normal = ReadBack(normalRT, true);

        RenderTexture.ReleaseTemporary(albedoRT);
        RenderTexture.ReleaseTemporary(normalRT);
    }

    private Color32[] ReadBack(RenderTexture rt, bool linear)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var texture = new Texture2D(_cellResolution, _cellResolution, TextureFormat.RGBA32, false, linear);
        texture.ReadPixels(new Rect(0, 0, _cellResolution, _cellResolution), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        Color32[] pixels = texture.GetPixels32();
        Object.DestroyImmediate(texture);
        return pixels;
    }

    private static void Dilate(Color32[] albedo, Color32[] normal, int size, int passes)
    {
        var filled = new bool[albedo.Length];
        for (int i = 0; i < albedo.Length; i++) filled[i] = albedo[i].a > 0;

        var next = new bool[albedo.Length];
        for (int pass = 0; pass < passes; pass++)
        {
            System.Array.Copy(filled, next, filled.Length);
            bool changed = false;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (filled[idx]) continue;

                int r = 0, g = 0, b = 0, nr = 0, ng = 0, nb = 0, n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = x + dx, sy = y + dy;
                    if ((uint)sx >= (uint)size || (uint)sy >= (uint)size) continue;
                    int s = sy * size + sx;
                    if (!filled[s]) continue;
                    r += albedo[s].r; g += albedo[s].g; b += albedo[s].b;
                    nr += normal[s].r; ng += normal[s].g; nb += normal[s].b;
                    n++;
                }

                if (n == 0) continue;
                albedo[idx] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                normal[idx] = new Color32((byte)(nr / n), (byte)(ng / n), (byte)(nb / n), 0);
                next[idx] = true;
                changed = true;
            }

            System.Array.Copy(next, filled, filled.Length);
            if (!changed) break;
        }
    }

    private void Blit(Color32[] source, Color32[] atlas, int offsetX, int offsetY, int atlasWidth)
    {
        for (int y = 0; y < _cellResolution; y++)
            System.Array.Copy(source, y * _cellResolution, atlas, (offsetY + y) * atlasWidth + offsetX, _cellResolution);
    }

    private static Texture2D SaveAtlas(Color32[] pixels, int width, int height, string path, bool sRGB)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !sRGB);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = sRGB;
        importer.alphaIsTransparency = sRGB;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
        importer.textureCompression = sRGB ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
