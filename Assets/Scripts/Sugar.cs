using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class Sugar : MonoBehaviour
{
    [Header("Auto-find (recommended)")]
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] Rigidbody2D ballRb;

    [Header("Emission rules")]
    public float emitMinSpeed = 0.03f;

    [Header("Clear rules")]
    public float stopSpeed = 0.02f;
    public float stopHoldTime = 0.35f;

    ParticleSystem ps;
    ParticleSystem.EmissionModule em;

    bool wasDragging;
    bool stopClearedLatch;
    float stopTimer;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        em = ps.emission;

        ResolveRefs();
        ps.Play(true);
    }

    void OnEnable()
    {
        ResolveRefs();
        ps.Play(true);
    }

    void ResolveRefs()
    {
        // If the trail prefab is parented to the ball, this grabs refs instantly.
        if (!ballRb) ballRb = GetComponentInParent<Rigidbody2D>();
        if (!grab) grab = GetComponentInParent<FingerGrabInertia2D>();

        // Fallback: find in scene (fine for 1-ball game)
        if (!ballRb) ballRb = FindFirstObjectByType<Rigidbody2D>(FindObjectsInactive.Exclude);
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Exclude);
    }

    void Update()
    {
        if (!ps) return;

        // If we still couldn't find refs, don't hard-break: just emit.
        if (!ballRb && !grab)
        {
            em.enabled = true;
            return;
        }

        bool dragging = grab && grab.IsDragging;
        float speed = ballRb ? ballRb.linearVelocity.magnitude : 0f;

        // Emit while dragging, otherwise only emit when moving
        em.enabled = dragging || speed >= emitMinSpeed;

        // Clear once on catch (drag start)
        if (grab)
        {
            if (!wasDragging && dragging)
            {
                ClearSugar();
                stopTimer = 0f;
                stopClearedLatch = false;
            }
            wasDragging = dragging;
        }

        // Clear once when truly stopped (never while dragging)
        if (!dragging && ballRb)
        {
            if (speed <= stopSpeed) stopTimer += Time.deltaTime;
            else
            {
                stopTimer = 0f;
                stopClearedLatch = false;
            }

            if (!stopClearedLatch && stopTimer >= stopHoldTime)
            {
                ClearSugar();
                stopClearedLatch = true;
            }
        }
        else
        {
            stopTimer = 0f;
        }
    }

    void ClearSugar()
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);
        ps.Play(true);
    }
}