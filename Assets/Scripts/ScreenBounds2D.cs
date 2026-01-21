using UnityEngine;

/// <summary>
/// Manages wall colliders around the playable area.
/// Uses GameViewport for responsive bounds calculation.
/// </summary>
[DefaultExecutionOrder(-150)]
public class ScreenBounds2D : MonoBehaviour
{
    [Header("Wall Shape")]
    [SerializeField] float wallThickness = 1f;
    [SerializeField] float inset = 0f;

    [Header("Physics")]
    [SerializeField] PhysicsMaterial2D wallMaterial;
    [SerializeField] bool useWallsLayer = true;
    [SerializeField] string wallsLayerName = "Walls";

    int wallsLayer;
    int lastScreenW, lastScreenH;

    void Awake()
    {
        wallsLayer = useWallsLayer ? LayerMask.NameToLayer(wallsLayerName) : -1;
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        Rebuild();
    }

    void Update()
    {
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            Rebuild();
        }
    }

    void Rebuild()
    {
        // Clear old walls
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        GameViewport.GetWorldBounds(out var min, out var max);

        float left = min.x + inset;
        float right = max.x - inset;
        float bottom = min.y + inset;
        float top = max.y - inset;

        float width = right - left;
        float height = top - bottom;

        float cx = (left + right) * 0.5f;
        float cy = (bottom + top) * 0.5f;
        float t = Mathf.Max(0.0001f, wallThickness);

        // Create walls (no corner overlap)
        CreateWall("Wall_Top",
            new Vector2(cx, top + t * 0.5f),
            new Vector2(width, t));

        CreateWall("Wall_Bottom",
            new Vector2(cx, bottom - t * 0.5f),
            new Vector2(width, t));

        CreateWall("Wall_Left",
            new Vector2(left - t * 0.5f, cy),
            new Vector2(t, height));

        CreateWall("Wall_Right",
            new Vector2(right + t * 0.5f, cy),
            new Vector2(t, height));
    }

    void CreateWall(string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.transform.position = new Vector3(pos.x, pos.y, 0f);

        if (wallsLayer != -1)
            go.layer = wallsLayer;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.sharedMaterial = wallMaterial;
    }
}