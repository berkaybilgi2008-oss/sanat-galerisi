using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GalleryPainting : MonoBehaviour
{
    [SerializeField] private string displayName = "Tablo";
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField] private float holdScale = 0.55f;

    private Rigidbody body;
    private Transform playerCamera;
    private Transform wallAnchor;
    private bool held;
    private bool mounted;
    private bool isHorizontal;
    private bool artworkCompensated;

    public void SetDisplayName(string value) => displayName = value;
    public void SetPickupScale(float value) => holdScale = value;
    public void SetHorizontal(bool value) => isHorizontal = value;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
    }

    private void LateUpdate()
    {
        if (!held || playerCamera == null)
            return;

        // Elde iken Rigidbody'nin fizik güncellemesi tabloyu geride bırakmasın.
        // Tablo doğrudan kameranın sabit local noktasında tutulur.
        transform.localPosition = new Vector3(0.45f, -0.2f, 0.8f);
        // Kameranın +Z yönü oyuncudan dışarı baktığı için tabloyu 180° Y
        // döndürerek yüzünü oyuncuya çeviriyoruz. Yan ise ayrıca 90° yatay.
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f) *
            (isHorizontal
                ? Quaternion.Euler(0f, 0f, -90f)
                : Quaternion.identity);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current[interactKey].wasPressedThisFrame)
            return;

        if (mounted)
            return;

        if (held)
        {
            TryMountOrDrop();
            return;
        }

        TryPickUp();
    }

    private void RotateArtworkUvForHorizontalFrame()
    {
        if (!isHorizontal || artworkCompensated)
            return;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sharedMaterial == null ||
                renderer.sharedMaterial.name.IndexOf("Tuval", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            Mesh mesh = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
            mesh.name = meshFilter.sharedMesh.name + " - Held Yan UV";
            Vector2[] uv = mesh.uv;

            // Spawn'da Yan resmi dik çerçevede yatay göstermek için UV döndürülmüştü.
            // Çerçeveyi şimdi 90° fiziksel döndürürken resmi dünya üzerinde yatay
            // tutmak için bunun tersini uyguluyoruz.
            for (int i = 0; i < uv.Length; i++)
                uv[i] = new Vector2(uv[i].y, 1f - uv[i].x);

            mesh.uv = uv;
            meshFilter.sharedMesh = mesh;
            artworkCompensated = true;
            break;
        }
    }

    private void TryPickUp()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
            return;

        if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            return;

        playerCamera = camera.transform;
        held = true;

        // Yan tabloda çerçeveyi fiziksel olarak yatay çevirirken,
        // spawn'da doğru görünen resmi yatay tut.
        RotateArtworkUvForHorizontalFrame();

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        transform.SetParent(playerCamera, false);

        // Elde taşıma noktası: tablo kameraya child olur ve oyuncuyla birebir
        // birlikte hareket eder. Bu yüzden dünya koordinatına değil, kameranın
        // local uzayındaki sabit bir noktaya bağlıdır.
        transform.localPosition = new Vector3(0.45f, -0.2f, 0.8f);

        // Spawn'da bütün çerçeveler dikti. Elde ise Yan klasöründeki
        // tablolar fiziksel olarak 90° yatay çevrilir; Dik olanlar dik kalır.
        // İç resmin UV'sine burada tekrar dokunmuyoruz.
        transform.localRotation = isHorizontal
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.identity;

        transform.localScale = Vector3.one * holdScale;

        Debug.Log("Tablo alındı: " + displayName);
    }

    private void TryMountOrDrop()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 2.5f))
        {
            if (hit.collider != null && hit.collider.name == "Gallery Wall")
            {
                MountToWall(hit.point, hit.normal);
                return;
            }
        }

        Drop();
    }

    private void MountToWall(Vector3 hitPoint, Vector3 wallNormal)
    {
        Transform anchor = GetOrCreateMountAnchor();

        transform.SetParent(anchor, true);

        Vector3 normal = wallNormal.normalized;
        transform.position = hitPoint + normal * 0.08f;

        // Önce tabloyu duvara bakacak şekilde yerleştiriyoruz.
        // Ardından yalnızca Yan tabloları kendi yüzey eksenlerinde 90°
        // çeviriyoruz. Böylece spawn dik, elde yatay ve duvarda yatay olur.
        Quaternion faceWall = Quaternion.LookRotation(normal, Vector3.up);
        Quaternion orientation = isHorizontal
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.identity;

        transform.rotation = faceWall * orientation;

        transform.localScale = Vector3.one * holdScale;

        body.isKinematic = true;
        body.useGravity = false;

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = true;

        held = false;
        mounted = true;
        playerCamera = null;
        wallAnchor = anchor;

        Debug.Log("Tablo duvara asıldı: " + displayName);
    }

    private void Drop()
    {
        transform.SetParent(null, true);

        body.isKinematic = false;
        body.useGravity = true;
        body.WakeUp();

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = true;

        held = false;
        playerCamera = null;

        Debug.Log("Tablo bırakıldı: " + displayName);
    }

    private Transform GetOrCreateMountAnchor()
    {
        GameObject existing = GameObject.Find("Wall Mounted Paintings");
        if (existing != null)
            return existing.transform;

        GameObject root = new GameObject("Wall Mounted Paintings");
        return root.transform;
    }
}