using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class FingerGrabInertia2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RunScoring2D scoring; // optional, auto-found if empty

    [Header("Throw")]
    [SerializeField] float throwMultiplier = 1.4f;
    [SerializeField] float maxThrowSpeed = 1000f;

    [Header("Velocity sampling (for consistent throws)")]
    [SerializeField, Range(3, 20)] int velocitySampleCount = 8;
    [SerializeField, Range(0.03f, 0.25f)] float velocitySampleWindow = 0.10f;

    [Header("Bounds")]
    [SerializeField] float boundsInset = 0f;

    [Header("Pickup assist (between runs only)")]
    [SerializeField] bool allowPickupNearBall = true;
    [SerializeField, Min(0f)] float pickupAssistExtraRadius = 0.35f;

    [Header("Catch VFX")]
    [SerializeField] ParticleSystem catchVfx;
    [SerializeField] float vfxMinSpeedToCountAsCatch = 0.02f;

    [Header("Tap-to-catch forgiveness (world units, speed-scaled)")]
    [SerializeField] float tapForgiveness = 0.20f;
    [SerializeField] float tapForgivenessExtraAtMax = 0.60f;
    [SerializeField] float forgivenessSpeedMin = 0.5f;
    [SerializeField] float forgivenessSpeedMax = 6.0f;
    [SerializeField] float forgivenessExponent = 0.55f;

    [Header("Telekinesis")]
    [SerializeField] bool allowTelekinesisWhenIdle = true;

    [Header("Release assist (magnitude preserving)")]
    [SerializeField, Min(0f)] float wallTouchEpsilon = 0.02f;
    [SerializeField, Min(0f)] float minInwardNormalSpeed = 0.75f;

    [Header("Held motion")]
    [Tooltip("While dragging, ball becomes kinematic and we set rb.position directly.")]
    [SerializeField] bool kinematicWhileDragging = true;

    [Header("Flight Damping")]
    [SerializeField, Min(0f)] float flightLinearDamping = 1.4f;

    [Header("Touch safety")]
    [Tooltip("If 3+ fingers are down, we cancel drag so pause/cancel gestures don't fling the ball.")]
    [SerializeField, Range(2, 6)] int cancelDragAtTouchCount = 3;

    [Header("Pause blocking")]
    [Tooltip("Block pause gesture for this duration after releasing a throw.")]
    [SerializeField, Min(0f)] float pauseBlockDurationAfterThrow = 0.3f;

    float pauseBlockTimer;

    Rigidbody2D rb;
    CircleCollider2D circle;

    bool isDragging;
    public bool IsDragging => isDragging;

    // Stable "held" state (for radial menu pad logic)
    public bool IsHeld { get; private set; }

    public event Action OnDragBegan;
    public event Action<bool> OnDragEnded; // bool = thrown

    public bool WasThrown { get; private set; }
    public bool WasDropped { get; private set; }

    public bool LastPickupWasCatch { get; private set; }
    public float LastPickupSpeed { get; private set; }
    public Vector2 LastPickupVelocity { get; private set; }
    public Vector2 LastPickupPosition { get; private set; }

    public Vector2 CurrentDragScreenPos { get; private set; }
    public Vector2 CurrentDragWorldPos { get; private set; }

    public bool ShouldBlockPauseGesture => isDragging || pauseBlockTimer > 0f;

    Vector2 dragOffsetWorld;

    Vector2[] posSamples;
    float[] timeSamples;
    int sampleIndex;
    int sampleFilled;

    RigidbodyType2D savedBodyType;

    // Touch tracking
    int draggingTouchId = -1;

    struct Bounds2D
    {
        public float minX, maxX, minY, maxY, r;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circle = GetComponent<CircleCollider2D>();

        rb.gravityScale = 0f;

        if (!scoring)
            scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);

        velocitySampleCount = Mathf.Clamp(velocitySampleCount, 3, 20);
        posSamples = new Vector2[velocitySampleCount];
        timeSamples = new float[velocitySampleCount];
    }

    public void ConsumeWasThrown() => WasThrown = false;
    public void ConsumeWasDropped() => WasDropped = false;

    void Update()
    {
        // Tick pause block timer
        if (pauseBlockTimer > 0f)
            pauseBlockTimer -= Time.deltaTime;

        if (GameInputLock.Locked)
        {
            if (isDragging) CancelDragNoThrow();
            return;
        }

        if (Touchscreen.current != null)
        {
            TickTouch();
            return;
        }

        TickMouse();
    }

    void TickTouch()
    {
        int active = ActiveTouchCount();

        if (active >= cancelDragAtTouchCount)
        {
            if (isDragging) CancelDragNoThrow();
            return;
        }

        if (!isDragging)
        {
            var t = Touchscreen.current.primaryTouch;
            if (t == null) return;

            if (t.press.wasPressedThisFrame)
            {
                draggingTouchId = t.touchId.ReadValue();
                BeginDragFromScreenPress(t.position.ReadValue());
            }
            return;
        }

        if (!TryGetTouchById(draggingTouchId, out TouchControl dragTouch))
        {
            CancelDragNoThrow();
            return;
        }

        bool isDown = dragTouch.press.isPressed;
        bool upThisFrame = dragTouch.press.wasReleasedThisFrame;

        Vector2 screen = dragTouch.position.ReadValue();

        if (isDown) DragStep(screen);
        if (upThisFrame) ReleaseDrag();
    }

    void TickMouse()
    {
        if (Mouse.current == null) return;

        bool downThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
        bool isDown = Mouse.current.leftButton.isPressed;
        bool upThisFrame = Mouse.current.leftButton.wasReleasedThisFrame;

        Vector2 screen = Mouse.current.position.ReadValue();

        if (downThisFrame) BeginDragFromScreenPress(screen);
        if (isDragging && isDown) DragStep(screen);
        if (isDragging && upThisFrame) ReleaseDrag();
    }

    bool TryGetTouchById(int id, out TouchControl touch)
    {
        touch = null;
        var ts = Touchscreen.current;
        if (ts == null) return false;

        foreach (var t in ts.touches)
        {
            if (t == null) continue;
            if (!t.press.isPressed && !t.press.wasReleasedThisFrame && !t.press.wasPressedThisFrame) continue;

            if (t.touchId.ReadValue() == id)
            {
                touch = t;
                return true;
            }
        }

        foreach (var t in ts.touches)
        {
            if (t == null) continue;
            if (t.touchId.ReadValue() == id)
            {
                touch = t;
                return true;
            }
        }

        return false;
    }

    int ActiveTouchCount()
    {
        var ts = Touchscreen.current;
        if (ts == null) return 0;

        int c = 0;
        foreach (var t in ts.touches)
        {
            if (t != null && t.press.isPressed) c++;
        }
        return c;
    }

    void BeginDragFromScreenPress(Vector2 screenPos)
    {
        if (isDragging) return;

        if (!scoring)
            scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);

        if (scoring != null && !scoring.CanPickUpBallNow())
            return;

        Vector2 v = rb.linearVelocity;
        float speed = v.magnitude;

        // If any system pinned the ball (ex: Sticky Ball), clear it so dragging/throwing works normally.
        if (rb.constraints != RigidbodyConstraints2D.None)
            rb.constraints = RigidbodyConstraints2D.None;

        LastPickupSpeed = speed;
        LastPickupVelocity = v;
        LastPickupPosition = rb.position;

        Vector2 fingerWorld = ScreenToWorld(screenPos);
        CurrentDragScreenPos = screenPos;

        bool startedOnBall = IsTapOnBall(screenPos, speed);
        bool runActive = (scoring != null && scoring.RunActive);

        if (!runActive && !startedOnBall && !allowTelekinesisWhenIdle)
        {
            if (allowPickupNearBall)
            {
                float r = BallRadiusWorld();
                float allowed = r + pickupAssistExtraRadius;
                if ((fingerWorld - rb.position).sqrMagnitude > allowed * allowed)
                    return;
            }
            else return;
        }

        LastPickupWasCatch = startedOnBall;

        isDragging = true;
        IsHeld = true;
        OnDragBegan?.Invoke();

        WasThrown = false;
        WasDropped = false;

        // Stop physics motion immediately
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (kinematicWhileDragging)
        {
            savedBodyType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Reset flight damping when picking up
        rb.linearDamping = 0f;

        // NEW: If this was a miss, teleport ball under finger (clamped), zero offset.
        if (!LastPickupWasCatch)
        {
            Bounds2D b = ComputeBounds();

            Vector2 snapped = fingerWorld;
            snapped.x = Mathf.Clamp(snapped.x, b.minX, b.maxX);
            snapped.y = Mathf.Clamp(snapped.y, b.minY, b.maxY);

            rb.position = snapped;
            dragOffsetWorld = Vector2.zero;

            // Clear trail to prevent line from old position to teleported position
            ClearAllTrails();

            // Temporarily disable trails so they don't capture the teleport transition
            StartCoroutine(ResetTrailsAfterTeleport());
        }
        else
        {
            dragOffsetWorld = rb.position - fingerWorld;

            if (speed > vfxMinSpeedToCountAsCatch)
                PlayCatchVfxAt(rb.position);
        }

        CurrentDragWorldPos = fingerWorld + dragOffsetWorld;

        sampleIndex = 0;
        sampleFilled = 0;

        // Seed samples with the initial desired position
        PushSample(fingerWorld + dragOffsetWorld, Time.unscaledTime);

        DragStep(screenPos);
    }

    void DragStep(Vector2 screenPos)
    {
        CurrentDragScreenPos = screenPos;
        Vector2 fingerWorld = ScreenToWorld(screenPos);

        Vector2 desired = fingerWorld + dragOffsetWorld;
        CurrentDragWorldPos = desired;

        Bounds2D b = ComputeBounds();

        Vector2 target = desired;
        target.x = Mathf.Clamp(target.x, b.minX, b.maxX);
        target.y = Mathf.Clamp(target.y, b.minY, b.maxY);

        rb.position = target;

        // Sample desired (not clamped target) for throw feel
        PushSample(desired, Time.unscaledTime);
    }

    void ReleaseDrag()
    {
        if (!isDragging) return;

        IsHeld = false;
        isDragging = false;

        draggingTouchId = -1;

        // Block pause gesture briefly after throwing
        pauseBlockTimer = pauseBlockDurationAfterThrow;

        if (kinematicWhileDragging)
            rb.bodyType = savedBodyType;

        Vector2 avgVel = ComputeAverageVelocity(Time.unscaledTime);
        Vector2 throwVel = avgVel * throwMultiplier;

        float mag = throwVel.magnitude;
        if (mag > maxThrowSpeed)
            throwVel = throwVel / mag * maxThrowSpeed;

        // Keep your old behavior: only assist if "overlapping wall" (as your older build did)
        if (IsActuallyOverlappingWall())
            throwVel = ApplyWallReleaseAssistPreserveSpeed(throwVel);

        rb.linearVelocity = throwVel;

        if (throwVel.sqrMagnitude <= 0.0000001f)
        {
            WasDropped = true;
            WasThrown = false;
        }
        else
        {
            WasThrown = true;
            WasDropped = false;

            // Apply flight damping to the thrown ball
            rb.linearDamping = flightLinearDamping;
        }

        sampleFilled = 0;

        OnDragEnded?.Invoke(WasThrown);
    }

    void CancelDragNoThrow()
    {
        isDragging = false;
        IsHeld = false;

        draggingTouchId = -1;

        if (kinematicWhileDragging)
            rb.bodyType = savedBodyType;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        sampleFilled = 0;

        WasThrown = false;
        WasDropped = false;

        OnDragEnded?.Invoke(false);
    }

    bool IsTapOnBall(Vector2 screenPos, float speed)
    {
        Vector2 tapWorld = ScreenToWorld(screenPos);

        float r = BallRadiusWorld();

        float t = Mathf.InverseLerp(forgivenessSpeedMin, forgivenessSpeedMax, speed);
        t = Mathf.Clamp01(t);
        t = Mathf.Pow(t, forgivenessExponent);

        float forgiveness = tapForgiveness + tapForgivenessExtraAtMax * t;

        float allowed = r + forgiveness;
        return (tapWorld - rb.position).sqrMagnitude <= allowed * allowed;
    }

    float BallRadiusWorld()
    {
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        return circle.radius * scale;
    }

    void PlayCatchVfxAt(Vector2 worldPos)
    {
        if (!catchVfx) return;

        var tr = catchVfx.transform;
        tr.position = new Vector3(worldPos.x, worldPos.y, tr.position.z);

        catchVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        catchVfx.Play(true);
    }

    bool IsActuallyOverlappingWall()
    {
        Bounds2D b = ComputeBounds();
        Vector2 p = rb.position;

        return
            p.x < b.minX || p.x > b.maxX ||
            p.y < b.minY || p.y > b.maxY;
    }

    Bounds2D ComputeBounds()
    {
        GameViewport.GetWorldBounds(out var min, out var max);

        float r = BallRadiusWorld();

        return new Bounds2D
        {
            r = r,
            minX = min.x + boundsInset + r,
            maxX = max.x - boundsInset - r,
            minY = min.y + boundsInset + r,
            maxY = max.y - boundsInset - r
        };
    }

    void PushSample(Vector2 pos, float t)
    {
        posSamples[sampleIndex] = pos;
        timeSamples[sampleIndex] = t;

        sampleIndex = (sampleIndex + 1) % velocitySampleCount;
        sampleFilled = Mathf.Min(sampleFilled + 1, velocitySampleCount);
    }

    Vector2 ComputeAverageVelocity(float now)
    {
        if (sampleFilled < 2) return Vector2.zero;

        int newestIndex = (sampleIndex - 1 + velocitySampleCount) % velocitySampleCount;
        Vector2 newestPos = posSamples[newestIndex];
        float newestTime = timeSamples[newestIndex];

        float windowStart = now - velocitySampleWindow;

        int bestIndex = newestIndex;
        for (int i = 1; i < sampleFilled; i++)
        {
            int idx = (newestIndex - i + velocitySampleCount) % velocitySampleCount;
            bestIndex = idx;

            if (timeSamples[idx] <= windowStart)
                break;
        }

        Vector2 oldestPos = posSamples[bestIndex];
        float oldestTime = timeSamples[bestIndex];

        float dt = newestTime - oldestTime;
        if (dt <= 0.0001f) return Vector2.zero;

        return (newestPos - oldestPos) / dt;
    }

    Vector2 ApplyWallReleaseAssistPreserveSpeed(Vector2 v)
    {
        if (minInwardNormalSpeed <= 0f) return v;

        Bounds2D b = ComputeBounds();
        Vector2 p = rb.position;

        bool nearLeft = p.x <= b.minX + wallTouchEpsilon;
        bool nearRight = p.x >= b.maxX - wallTouchEpsilon;
        bool nearBottom = p.y <= b.minY + wallTouchEpsilon;
        bool nearTop = p.y >= b.maxY - wallTouchEpsilon;

        if (!nearLeft && !nearRight && !nearBottom && !nearTop)
            return v;

        float speed = v.magnitude;
        if (speed <= 0.00001f)
            return v;

        Vector2 inward = Vector2.zero;

        if (nearLeft) inward += Vector2.right;
        if (nearRight) inward += Vector2.left;
        if (nearBottom) inward += Vector2.up;
        if (nearTop) inward += Vector2.down;

        if (inward == Vector2.zero)
            return v;

        inward.Normalize();

        float vn = Vector2.Dot(v, inward);
        Vector2 vNormal = inward * vn;
        Vector2 vTangent = v - vNormal;

        if (vn >= minInwardNormalSpeed)
            return v;

        float targetVn = minInwardNormalSpeed;
        float maxTangentMag = Mathf.Sqrt(Mathf.Max(0f, speed * speed - targetVn * targetVn));

        Vector2 tangentDir = vTangent.sqrMagnitude > 0.0000001f ? vTangent.normalized : Perp(inward);
        Vector2 outV = (inward * targetVn) + (tangentDir * maxTangentMag);

        if (outV.sqrMagnitude <= 0.0000001f)
            return inward * speed;

        return outV.normalized * speed;
    }

    static Vector2 Perp(Vector2 n) => new Vector2(-n.y, n.x);

    void ClearAllTrails()
    {
        // Clear any TrailRenderer components on this GameObject or children
        var trails = GetComponentsInChildren<TrailRenderer>(true);
        foreach (var trail in trails)
        {
            if (trail) trail.Clear();
        }
    }

    System.Collections.IEnumerator ResetTrailsAfterTeleport()
    {
        // Disable trails to prevent them from recording during the teleport
        var trails = GetComponentsInChildren<TrailRenderer>(true);
        foreach (var trail in trails)
        {
            if (trail) trail.emitting = false;
        }

        // Wait a frame to let physics settle
        yield return null;

        // Re-enable trails to start fresh
        foreach (var trail in trails)
        {
            if (trail)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }
    }

    Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return GameViewport.ScreenToWorld(screenPos);
    }
}