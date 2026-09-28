using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GalleryPainting : MonoBehaviour
{
    [SerializeField] private string displayName = "Mavi Galeri";
    [SerializeField] private Key toggleKey = Key.E;
    [SerializeField] private Vector3 holdPosition = new Vector3(0.35f, -0.15f, 0.8f);
    [SerializeField] private Vector3 holdRotation = new Vector3(0f, 0f, 0f);
    [SerializeField] private float holdScale = 0.72f;

    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Transform playerCamera;
    private bool held;

    public void SetDisplayName(string value)
    {
        displayName = value;
    }

    public void SetPickupScale(float value)
    {
        holdScale = value;
    }

    private void Awake()
    {
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (held)
                Drop();
            else
                TryPickUp();
        }
    }

    private void TryPickUp()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, 3f);
        bool hitPainting = false;
        foreach (RaycastHit candidate in hits)
        {
            if (candidate.transform == transform || candidate.transform.IsChildOf(transform))
            {
                hitPainting = true;
                break;
            }
        }

        if (!hitPainting)
            return;

        playerCamera = camera.transform;
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;

        transform.SetParent(playerCamera, false);
        transform.localPosition = new Vector3(0.35f, -0.18f, 0.75f);
        transform.localRotation = Quaternion.Euler(holdRotation);
        transform.localScale = Vector3.one * holdScale;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        held = true;
        Debug.Log("Tablo alındı: " + displayName);
    }

    private void Drop()
    {
        transform.SetParent(originalParent, true);
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        transform.localScale = originalScale;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = true;

        held = false;
        playerCamera = null;
        Debug.Log("Tablo bırakıldı: " + displayName);
    }
}
