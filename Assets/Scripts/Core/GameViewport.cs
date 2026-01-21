using UnityEngine;

/// <summary>
/// Central system for screen/viewport management.
/// Ensures consistent world-space calculations across any resolution/aspect ratio.
/// All distance, scale, and gameplay metrics should reference this for normalization.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameViewport : MonoBehaviour
{
    [SerializeField] bool lockWorldHeight = true;
    [SerializeField] float targetWorldHeight = 10f;

    static GameViewport instance;

    Camera cam;
    Vector2 cachedMin, cachedMax;
    float cachedWorldWidth, cachedWorldHeight;
    int lastScreenW, lastScreenH;
    bool isDirty = true;

    // Cached reference plane (z = 0)
    const float RefZ = 0f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        cam = GetComponent<Camera>();
        if (!cam) cam = Camera.main;

        if (!cam)
        {
            Debug.LogError("GameViewport: No camera found!");
            return;
        }

        Recalculate();
    }

    void Update()
    {
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
            isDirty = true;

        if (isDirty)
        {
            Recalculate();
            isDirty = false;
        }
    }

    void Recalculate()
    {
        if (!cam) return;

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;

        // Get world bounds at z = RefZ
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, RefZ));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, RefZ));

        cachedMin = new Vector2(bl.x, bl.y);
        cachedMax = new Vector2(tr.x, tr.y);

        cachedWorldWidth = cachedMax.x - cachedMin.x;
        cachedWorldHeight = cachedMax.y - cachedMin.y;
    }

    /// <summary>
    /// Get the playable world bounds. min and max are world-space coordinates.
    /// </summary>
    public static void GetWorldBounds(out Vector2 min, out Vector2 max)
    {
        if (!instance) { min = Vector2.zero; max = Vector2.one; return; }
        min = instance.cachedMin;
        max = instance.cachedMax;
    }

    /// <summary>
    /// Get just the center point of the playable area.
    /// </summary>
    public static Vector2 GetWorldCenter()
    {
        GetWorldBounds(out var min, out var max);
        return (min + max) * 0.5f;
    }

    /// <summary>
    /// Get world dimensions of the playable area.
    /// </summary>
    public static Vector2 GetWorldDimensions()
    {
        if (!instance) return Vector2.one;
        return new Vector2(instance.cachedWorldWidth, instance.cachedWorldHeight);
    }

    /// <summary>
    /// Get world width.
    /// </summary>
    public static float GetWorldWidth()
    {
        if (!instance) return 1f;
        return instance.cachedWorldWidth;
    }

    /// <summary>
    /// Get world height.
    /// </summary>
    public static float GetWorldHeight()
    {
        if (!instance) return 1f;
        return instance.cachedWorldHeight;
    }

    /// <summary>
    /// Aspect ratio (width / height). Use for responsive tuning.
    /// </summary>
    public static float GetAspectRatio()
    {
        float h = GetWorldHeight();
        return h > 0.0001f ? GetWorldWidth() / h : 1f;
    }

    /// <summary>
    /// Convert screen position to world position at reference plane.
    /// </summary>
    public static Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (!instance || !instance.cam) return Vector2.zero;
        Vector3 world = instance.cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, RefZ));
        return new Vector2(world.x, world.y);
    }

    /// <summary>
    /// Convert world position to screen position.
    /// </summary>
    public static Vector2 WorldToScreen(Vector2 worldPos)
    {
        if (!instance || !instance.cam) return Vector2.zero;
        Vector3 screen = instance.cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, RefZ));
        return new Vector2(screen.x, screen.y);
    }

    /// <summary>
    /// Get normalized scale factor (1.0 = baseline). Use to scale UI/particles/etc by resolution.
    /// </summary>
    public static float GetNormalizedScale()
    {
        if (!instance) return 1f;
        float h = instance.cachedWorldHeight;
        return instance.lockWorldHeight ? (h / instance.targetWorldHeight) : 1f;
    }

    public Camera GetCamera() => cam;
}
