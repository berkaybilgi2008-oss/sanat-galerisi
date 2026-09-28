#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GalleryPaintingGenerator
{
    private const string ImageRoot = "Assets/Resimler";
    private const string ModelPath = "Assets/tablo/tablo.obj";
    private const string GeneratedRootName = "Generated Paintings";
    private const int MaxPaintings = 15;

    private static readonly Vector2 FloorMin = new Vector2(-5.5f, -1.5f);
    private static readonly Vector2 FloorMax = new Vector2(5.5f, 2.8f);

    [MenuItem("Gallery/Generate 15 Paintings")]
    public static void GenerateFromMenu()
    {
        Generate(force: true);
    }

    public static void GenerateIfNeeded()
    {
        if (GameObject.Find(GeneratedRootName) != null)
            return;

        Generate(force: false);
    }

    public static void CreateWallMountPoint()
    {
        GameObject old = GameObject.Find("Painting Wall Mount Point");
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);

        GameObject anchor = new GameObject("Painting Wall Mount Point");
        anchor.transform.position = new Vector3(0f, 1.8f, 4.28f);
        anchor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        GameObject horizontal = new GameObject("Horizontal");
        horizontal.transform.SetParent(anchor.transform, false);
        horizontal.transform.localRotation = Quaternion.identity;

        GameObject vertical = new GameObject("Vertical");
        vertical.transform.SetParent(anchor.transform, false);
        vertical.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        EditorUtility.SetDirty(anchor);
    }

    private static void Generate(bool force)
    {
        RemoveOldPrototypePaintings();

        GameObject existing = GameObject.Find(GeneratedRootName);
        if (force && existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("Tablo modeli bulunamadı: " + ModelPath);
            return;
        }

        List<PaintingSource> sources = FindPaintingSources();
        if (sources.Count == 0)
        {
            Directory.CreateDirectory(ImageRoot);
            AssetDatabase.Refresh();
            Debug.LogWarning("Assets/Resimler altında Yan/Dik klasörlerinde resim bulunamadı. Klasör yapısını oluşturduktan sonra Gallery > Generate 15 Paintings çalıştır.");
            return;
        }

        GameObject root = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Generate Gallery Paintings");

        int count = Mathf.Min(MaxPaintings, sources.Count);
        System.Random random = new System.Random(20260928);

        List<Vector3> positions = CreateRandomPositions(count, random);

        for (int i = 0; i < count; i++)
        {
            PaintingSource source = sources[i];
            GameObject painting = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (painting == null)
                painting = UnityEngine.Object.Instantiate(model);

            painting.name = $"{source.Artist} - {source.FileName}";
            painting.transform.SetParent(root.transform, true);
            painting.transform.position = positions[i];
            painting.transform.rotation = source.IsVertical
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.identity;

            ConfigurePainting(painting, source);
        }

        CreateWallMountPoint();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"Gallery: {count} tablo oluşturuldu. Kaynak: Assets/Resimler");
    }

    private static void RemoveOldPrototypePaintings()
    {
        string[] oldNames = { "Gallery Painting V2", "Gallery Painting" };
        foreach (string oldName in oldNames)
        {
            GameObject old = GameObject.Find(oldName);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
        }
    }

    private static List<PaintingSource> FindPaintingSources()
    {
        List<PaintingSource> result = new List<PaintingSource>();

        if (!AssetDatabase.IsValidFolder(ImageRoot))
            return result;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImageRoot });
        Array.Sort(guids, StringComparer.Ordinal);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
            string relative = path.Substring(ImageRoot.Length).TrimStart('/');
            string[] parts = relative.Split('/');

            if (parts.Length < 3)
                continue;

            string artist = parts[0];
            string orientationFolder = parts[1];

            bool isHorizontal = string.Equals(orientationFolder, "Yan", StringComparison.OrdinalIgnoreCase);
            bool isVertical = string.Equals(orientationFolder, "Dik", StringComparison.OrdinalIgnoreCase);

            if (!isHorizontal && !isVertical)
                continue;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                continue;

            result.Add(new PaintingSource
            {
                Texture = texture,
                Artist = artist,
                FileName = Path.GetFileNameWithoutExtension(path),
                IsVertical = isVertical
            });

            if (result.Count >= MaxPaintings)
                break;
        }

        return result;
    }

    private static List<Vector3> CreateRandomPositions(int count, System.Random random)
    {
        List<Vector3> positions = new List<Vector3>();
        int attempts = 0;

        while (positions.Count < count && attempts < 5000)
        {
            attempts++;

            float x = Mathf.Lerp(FloorMin.x, FloorMax.x, (float)random.NextDouble());
            float z = Mathf.Lerp(FloorMin.y, FloorMax.y, (float)random.NextDouble());
            Vector3 candidate = new Vector3(x, 0.72f, z);

            bool tooClose = false;
            foreach (Vector3 existing in positions)
            {
                if (Vector2.Distance(
                        new Vector2(candidate.x, candidate.z),
                        new Vector2(existing.x, existing.z)) < 1.35f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                positions.Add(candidate);
        }

        return positions;
    }

    private static void ConfigurePainting(GameObject painting, PaintingSource source)
    {
        Renderer[] renderers = painting.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material sourceMaterial = materials[i];
                if (sourceMaterial == null)
                    continue;

                if (sourceMaterial.name.IndexOf("Tuval", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Material materialCopy = new Material(sourceMaterial);
                    materialCopy.name = "Tuval - " + source.FileName;
                    materialCopy.mainTexture = source.Texture;
                    materials[i] = materialCopy;
                }
            }

            renderer.sharedMaterials = materials;
        }

        Collider collider = painting.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider box = painting.AddComponent<BoxCollider>();
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            Renderer[] childRenderers = painting.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            foreach (Renderer renderer in childRenderers)
            {
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (initialized)
            {
                box.center = painting.transform.InverseTransformPoint(bounds.center);
                Vector3 size = bounds.size;
                Vector3 localScale = painting.transform.lossyScale;
                box.size = new Vector3(
                    localScale.x == 0f ? size.x : size.x / Mathf.Abs(localScale.x),
                    localScale.y == 0f ? size.y : size.y / Mathf.Abs(localScale.y),
                    localScale.z == 0f ? size.z : size.z / Mathf.Abs(localScale.z));
            }
        }

        GalleryPainting interaction = painting.GetComponent<GalleryPainting>();
        if (interaction == null)
            interaction = painting.AddComponent<GalleryPainting>();

        interaction.SetDisplayName(source.Artist + " - " + source.FileName);
    }

    private sealed class PaintingSource
    {
        public Texture2D Texture;
        public string Artist;
        public string FileName;
        public bool IsVertical;
    }
}
#endif
