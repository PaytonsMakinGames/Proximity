using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WallBounceReporter : MonoBehaviour
{
    [Header("Refs (optional)")]
    [SerializeField] ActionManager actions;
    [SerializeField] RunScoring2D scoring;

    [Header("Filter")]
    [SerializeField] string wallsLayerName = "Walls";

    int wallsLayer;

    void Awake()
    {
        if (!actions)
            actions = FindFirstObjectByType<ActionManager>(FindObjectsInactive.Include);

        if (!scoring)
            scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);

        wallsLayer = LayerMask.NameToLayer(wallsLayerName);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
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
        {
            if (actions != null)
                actions.OnWallBounce(wallId);

            // Also let RunScoring2D process wall-contact-triggered powerups (Sticky Ball).
            if (scoring != null)
            {
                Vector2 p = transform.position;
                if (collision.contactCount > 0)
                    p = collision.GetContact(0).point;

                scoring.OnWallContact(wallId, p);
            }
        }
    }
}