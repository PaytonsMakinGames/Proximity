using UnityEngine;

public class ScreenBounds2D : MonoBehaviour
{
    [Header("Wall Shape")]
    public float wallThickness = 1f;
    public float inset = 0f;

    [Header("Physics")]
    public PhysicsMaterial2D wallMaterial;   // assign WallBouncy here
    public bool useWallsLayer = true;
    public string wallsLayerName = "Walls";

    float left, right, bottom, top;

    int lastW, lastH;
    Camera cam;

    void Awake()
    {
        cam = Camera.main;
        lastW = Screen.width;
        lastH = Screen.height;
        Rebuild();
    }

    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            Rebuild();
        }
    }

    void Rebuild()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        Vector2 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector2 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        left = bl.x + inset;
        right = tr.x - inset;
        bottom = bl.y + inset;
        top = tr.y - inset;

        float width = right - left;
        float height = top - bottom;

        float cx = (left + right) * 0.5f;
        float cy = (bottom + top) * 0.5f;
        float t = Mathf.Max(0.0001f, wallThickness);

        // NO OVERLAP at corners (important)
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

        // Parent it, but then force identity so parent transform can't skew walls
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // Because we're using world positions from ViewportToWorldPoint, set world position explicitly
        go.transform.position = new Vector3(pos.x, pos.y, 0f);

        if (useWallsLayer)
        {
            int layer = LayerMask.NameToLayer(wallsLayerName);
            if (layer != -1) go.layer = layer;
        }

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.sharedMaterial = wallMaterial;
    }

    public void GetPlayableWorldRect(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(left, bottom);
        max = new Vector2(right, top);
    }
}