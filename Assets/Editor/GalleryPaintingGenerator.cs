#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GalleryPaintingGenerator
{
    private const string ImageRoot = "Assets/resim";
    private const string ModelPath = "Assets/tablo/tablo.obj";
    private const string GeneratedRootName = "Generated Paintings";
    private const string OldMountPointName = "Painting Wall Mount Point";
    private const int MaxPaintings = 15;
    private const float PaintingScale = 0.55f;

    private static readonly Vector2 FloorMin = new Vector2(-5.0f, -1.0f);
    private static readonly Vector2 FloorMax = new Vector2(5.0f, 2.5f);

    [MenuItem("Gallery/Generate 15 Paintings")]
    public static void GenerateFromMenu() => Generate(true);

    public static void GenerateIfNeeded()
    {
        if (GameObject.Find(GeneratedRootName) != null) return;
        Generate(false);
    }

    private static void Generate(bool force)
    {
        RemoveOldPrototypePaintings();
        RemoveOldMountPoint();

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
            AssetDatabase.Refresh();
            Debug.LogWarning("Assets/resim altında sanatçı/yan veya sanatçı/dik klasörlerinde resim bulunamadı.");
            return;
        }

        GameObject root = new GameObject(GeneratedRootName);
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

            // OBJ'nin ön yüzü +Z yönündedir. Yön bilgisini yalnızca klasörden
            // alıyoruz: Dik = 0 derece, Yan = 90 derece yüzey dönüşü.
            // Resmin UV'sine ayrıca dönüş uygulamıyoruz; çerçeve ve resim
            // aynı fiziksel dönüşü paylaşır.
            painting.transform.rotation = source.IsHorizontal
                ? Quaternion.Euler(0f, 0f, -90f)
                : Quaternion.identity;
            painting.transform.localScale = Vector3.one * PaintingScale;

            ConfigurePainting(painting, source);
        }

        Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Gallery: {count} fiziksel tablo oluşturuldu.");
    }

    private static void RemoveOldPrototypePaintings()
    {
        foreach (string oldName in new[] { "Gallery Painting V2", "Gallery Painting" })
        {
            GameObject old = GameObject.Find(oldName);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
        }
    }

    private static void RemoveOldMountPoint()
    {
        GameObject old = GameObject.Find(OldMountPointName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);
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
            string folder = parts[1];

            bool isHorizontal = string.Equals(folder, "yan", StringComparison.OrdinalIgnoreCase);
            bool isVertical = string.Equals(folder, "dik", StringComparison.OrdinalIgnoreCase);
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

        while (positions.Count < count && attempts++ < 10000)
        {
            Vector3 candidate = new Vector3(
                Mathf.Lerp(FloorMin.x, FloorMax.x, (float)random.NextDouble()),
                1.15f,
                Mathf.Lerp(FloorMin.y, FloorMax.y, (float)random.NextDouble()));

            bool tooClose = false;
            foreach (Vector3 existing in positions)
            {
                if (Vector2.Distance(
                    new Vector2(candidate.x, candidate.z),
                    new Vector2(existing.x, existing.z)) < 0.95f)
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
        foreach (Renderer renderer in painting.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null ||
                    material.name.IndexOf("Tuval", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Material copy = new Material(material);
                copy.name = "Tuval - " + source.FileName;
                copy.mainTexture = source.Texture;
                materials[i] = copy;
            }

            renderer.sharedMaterials = materials;

            if (HasCanvasMaterial(materials))
                CreateWhiteCanvasBack(renderer, painting);
        }

        BoxCollider box = painting.GetComponent<BoxCollider>();
        if (box == null)
            box = painting.AddComponent<BoxCollider>();

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;

        foreach (Renderer renderer in painting.GetComponentsInChildren<Renderer>(true))
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
            Vector3 lossyScale = painting.transform.lossyScale;
            box.size = new Vector3(
                lossyScale.x == 0f ? bounds.size.x : bounds.size.x / Mathf.Abs(lossyScale.x),
                lossyScale.y == 0f ? bounds.size.y : bounds.size.y / Mathf.Abs(lossyScale.y),
                lossyScale.z == 0f ? bounds.size.z : bounds.size.z / Mathf.Abs(lossyScale.z));
        }

        Rigidbody body = painting.GetComponent<Rigidbody>();
        if (body == null)
            body = painting.AddComponent<Rigidbody>();

        body.mass = 0.8f;
        body.linearDamping = 0.15f;
        body.angularDamping = 0.5f;
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GalleryPainting interaction = painting.GetComponent<GalleryPainting>();
        if (interaction == null)
            interaction = painting.AddComponent<GalleryPainting>();

        interaction.SetDisplayName(source.Artist + " - " + source.FileName);
        interaction.SetHorizontal(source.IsHorizontal);
    }

    private static bool HasCanvasMaterial(Material[] materials)
    {
        foreach (Material material in materials)
        {
            if (material != null &&
                material.name.IndexOf("Tuval", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static void CreateWhiteCanvasBack(Renderer frontRenderer, GameObject painting)
    {
        MeshFilter sourceFilter = frontRenderer.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return;

        Transform sourceTransform = frontRenderer.transform;
        GameObject backObject = new GameObject("Tuval Back");
        backObject.transform.SetParent(sourceTransform.parent, false);
        // Keep the white sheet just behind the real canvas to avoid z-fighting.
        // OBJ'nin tuval kalınlığının dışına çıkmaması için back yüzünü
        // gerçek mesh yüzeyinin hemen arkasına değil, çok küçük bir epsilon ile
        // aynı yüzeyde tutuyoruz.
        backObject.transform.localPosition = sourceTransform.localPosition;
        backObject.transform.localRotation = sourceTransform.localRotation;
        backObject.transform.localScale = sourceTransform.localScale;

        Mesh backMesh = UnityEngine.Object.Instantiate(sourceFilter.sharedMesh);
        backMesh.name = sourceFilter.sharedMesh.name + " - White Back";

        int[] triangles = backMesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int temp = triangles[i];
            triangles[i] = triangles[i + 2];
            triangles[i + 2] = temp;
        }
        backMesh.triangles = triangles;
        backMesh.RecalculateNormals();

        MeshFilter backFilter = backObject.AddComponent<MeshFilter>();
        backFilter.sharedMesh = backMesh;

        MeshRenderer backRenderer = backObject.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material white = new Material(shader);
        white.name = "Tuval Back - White";
        white.color = Color.white;
        backRenderer.sharedMaterial = white;
    }

    private sealed class PaintingSource
    {
        public Texture2D Texture;
        public string Artist;
        public string FileName;
        public bool IsVertical;
        public bool IsHorizontal => !IsVertical;
    }
}
#endif