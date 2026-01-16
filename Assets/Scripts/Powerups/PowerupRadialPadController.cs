using UnityEngine;

public class PowerupRadialPadController : MonoBehaviour
{
    [Header("Refs (wire these)")]
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] Rigidbody2D ballRb;

    [Header("Pad source (pick ONE for OPEN detection)")]
    [SerializeField] RectTransform activationPadRect; // UI pad
    [SerializeField] Transform activationPadWorld;    // world pad (your current choice)

    [Header("Pad size")]
    [Tooltip("Used when activationPadWorld is assigned, or as fallback when activationPadRect is missing.")]
    [SerializeField] float padRadiusPx = 140f;

    [Header("Hold still to open")]
    [SerializeField, Min(0.01f)] float stillTimeToOpen = 0.22f;
    [SerializeField, Min(0f)] float stillnessPx = 14f;

    // One-frame pulse. Menu owns open state.
    public bool RequestOpenThisFrame { get; private set; }

    public bool BlockOpenRequests { get; set; }

    float stillTimer;
    Vector2 stillRefPos;
    bool hasStillRef;

    void Awake()
    {
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!ballRb && grab) ballRb = grab.GetComponent<Rigidbody2D>();
        ResetStill();
    }

    void Update()
    {
        if (BlockOpenRequests)
        {
            RequestOpenThisFrame = false;
            return;
        }

        RequestOpenThisFrame = false;

        if (Camera.main == null || grab == null || ballRb == null)
        {
            ResetStill();
            return;
        }

        if (GameInputLock.Locked)
        {
            ResetStill();
            return;
        }

        // Gate only: can only request open while actively dragging
        if (!grab.IsDragging)
        {
            ResetStill();
            return;
        }

        Vector2 padCenter = GetPadCenterScreenPx();
        float padRadius = GetPadRadiusScreenPx();

        Vector2 ballScreen = (Vector2)Camera.main.WorldToScreenPoint(ballRb.position);
        if (Vector2.Distance(ballScreen, padCenter) > padRadius)
        {
            ResetStill();
            return;
        }

        TickStillness(ballScreen);

        if (stillTimer >= stillTimeToOpen)
        {
            RequestOpenThisFrame = true; // pulse once
            if (BlockOpenRequests) return;
            ResetStill();
        }
    }

    void TickStillness(Vector2 ballScreen)
    {
        if (!hasStillRef)
        {
            hasStillRef = true;
            stillRefPos = ballScreen;
            stillTimer = 0f;
            return;
        }

        float moved = Vector2.Distance(ballScreen, stillRefPos);
        if (moved > stillnessPx)
        {
            stillRefPos = ballScreen;
            stillTimer = 0f;
            return;
        }

        stillTimer += Time.unscaledDeltaTime;
    }

    void ResetStill()
    {
        stillTimer = 0f;
        hasStillRef = false;
    }

    Vector2 GetPadCenterScreenPx()
    {
        // Prefer world pad (your current setup)
        if (activationPadWorld)
            return Camera.main.WorldToScreenPoint(activationPadWorld.position);

        if (activationPadRect)
        {
            Canvas canvas = activationPadRect.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            Vector3[] corners = new Vector3[4];
            activationPadRect.GetWorldCorners(corners);

            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return (bl + tr) * 0.5f;
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.18f);
    }

    float GetPadRadiusScreenPx()
    {
        // World pad uses explicit radius
        if (activationPadWorld || !activationPadRect)
            return padRadiusPx;

        Canvas canvas = activationPadRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector3[] corners = new Vector3[4];
        activationPadRect.GetWorldCorners(corners);

        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float w = Mathf.Abs(tr.x - bl.x);
        float h = Mathf.Abs(tr.y - bl.y);
        return Mathf.Min(w, h) * 0.5f;
    }
}