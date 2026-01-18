using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WallBounceReporter : MonoBehaviour
{
    [Header("Refs (optional)")]
    [SerializeField] ActionManager actions;

    [Header("Filter")]
    [SerializeField] string wallsLayerName = "Walls";

    int wallsLayer;

    void Awake()
    {
        if (!actions)
            actions = FindFirstObjectByType<ActionManager>(FindObjectsInactive.Include);

        wallsLayer = LayerMask.NameToLayer(wallsLayerName);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (actions == null) return;
        if (wallsLayer == -1) return;
        if (collision.collider == null) return;
        if (collision.collider.gameObject.layer != wallsLayer) return;

        string n = collision.collider.gameObject.name;

        int wallId = -1;
        if (n.Contains("Left")) wallId = 0;
        else if (n.Contains("Right")) wallId = 1;
        else if (n.Contains("Top")) wallId = 2;
        else if (n.Contains("Bottom")) wallId = 3;

        if (wallId != -1)
            actions.OnWallBounce(wallId);
    }
}