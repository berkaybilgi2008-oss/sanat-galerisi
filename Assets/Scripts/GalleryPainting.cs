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

    public void SetDisplayName(string value) => displayName = value;
    public void SetPickupScale(float value) => holdScale = value;
    public void SetHorizontal(bool value) => isHorizontal = value;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
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

        body.isKinematic = true;
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        transform.SetParent(playerCamera, false);
        transform.localPosition = new Vector3(0.45f, -0.2f, 0.8f);

        // Elde de sahnedeki yönü aynen koru. Yan için üreticide kullanılan
        // aynı -90 derece yüzey dönüşünü kullanıyoruz.
        transform.localRotation = isHorizontal
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.identity;

        // Generator zaten fiziksel tabloya PaintingScale uyguladı. Elde tekrar
        // küçültüp/büyütmek yerine mevcut dünya ölçeğini koru.
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

        // Modelin yüzü duvarın önüne baksın.
        Quaternion faceWall = Quaternion.LookRotation(normal, Vector3.up);

        // OBJ doğal hali dikey; yatay tablo için Z ekseninde 90 derece.
        transform.rotation = faceWall * (isHorizontal
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.identity);

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