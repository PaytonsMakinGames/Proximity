using UnityEngine;

public class PowerupRadialPadController : MonoBehaviour
{
    [Header("Refs (wire these)")]
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] Rigidbody2D ballRb;
    [SerializeField] Transform activationPadWorld;

    [Header("Open rules (world space)")]
    [SerializeField] float padRadiusWorld = 1.2f;
    [SerializeField] float holdTimeToOpen = 0.16f;

    public bool HasOpenRequest { get; private set; }
    public bool BlockOpenRequests { get; set; }

    float holdTimer;

    void Awake()
    {
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!ballRb && grab) ballRb = grab.GetComponent<Rigidbody2D>();

        ResetHold();
        HasOpenRequest = false;
    }

    public bool ConsumeOpenRequest()
    {
        if (!HasOpenRequest) return false;
        HasOpenRequest = false;
        return true;
    }

    void Update()
    {
        if (BlockOpenRequests)
        {
            holdTimer = 0f;
            HasOpenRequest = false;
            return;
        }

        if (HasOpenRequest) return;

        if (!grab || !ballRb || !activationPadWorld)
        {
            holdTimer = 0f;
            return;
        }

        if (GameInputLock.Locked)
        {
            holdTimer = 0f;
            return;
        }

        if (!grab.IsHeld)
        {
            holdTimer = 0f;
            return;
        }

        Vector2 ballPos = ballRb.position;
        Vector2 padPos = activationPadWorld.position;

        bool inside = Vector2.Distance(ballPos, padPos) <= padRadiusWorld;

        if (!inside)
        {
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.unscaledDeltaTime;

        if (holdTimer >= holdTimeToOpen)
        {
            HasOpenRequest = true; // latch
            holdTimer = 0f;
        }
    }

    void ResetHold()
    {
        holdTimer = 0f;
    }
}