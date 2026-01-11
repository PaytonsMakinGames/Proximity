using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class FingerGrabInertia2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RunScoring2D scoring; // optional, auto-found if empty

    [Header("Throw")]
    [SerializeField] float throwMultiplier = 1.0f;
    [SerializeField] float maxThrowSpeed = 15f;

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

    [Header("Touch safety")]
    [Tooltip("If 3+ fingers are down, we cancel drag so pause/cancel gestures don't fling the ball.")]
    [SerializeField, Range(2, 6)] int cancelDragAtTouchCount = 3;

    Camera cam;
    Rigidbody2D rb;
    CircleCollider2D circle;

    bool isDragging;
    public bool IsDragging => isDragging;

    public bool WasThrown { get; private set; }
    public bool WasDropped { get; private set; }

    public bool LastPickupWasCatch { get; private set; }
    public float LastPickupSpeed { get; private set; }

    Vector2 dragOffsetWorld;

    Vector2[] posSamples;
    float[] timeSamples;
    int sampleIndex;
    int sampleFilled;

    RigidbodyType2D savedBodyType;

    // Touch tracking (IMPORTANT)
    int draggingTouchId = -1;

    struct Bounds2D
    {
        public float minX, maxX, minY, maxY, r;
    }

    void Awake()
    {
        cam = Camera.main;
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

        // If player is doing a 3/4/5-finger gesture, DO NOT keep dragging.
        if (active >= cancelDragAtTouchCount)
        {
            if (isDragging) CancelDragNoThrow();
            return;
        }

        // If not dragging: we only start on primaryTouch press.
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

        // If dragging: keep using the SAME touch id that started the drag.
        if (!TryGetTouchById(draggingTouchId, out TouchControl dragTouch))
        {
            // That finger disappeared (rare); safely end drag with no throw.
            // You can choose to ReleaseDrag() here instead, but "no finger" usually means we should stop.
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

            int tid = t.touchId.ReadValue();
            if (tid == id)
            {
                touch = t;
                return true;
            }
        }

        // Also allow finding it even if it's still pressed but no edges happened.
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

        float speed = rb.linearVelocity.magnitude;
        LastPickupSpeed = speed;

        Vector2 fingerWorld = ScreenToWorld(screenPos);
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
        WasThrown = false;
        WasDropped = false;

        dragOffsetWorld = rb.position - fingerWorld;

        // Stop physics motion immediately
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (kinematicWhileDragging)
        {
            savedBodyType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (LastPickupWasCatch && speed > vfxMinSpeedToCountAsCatch)
            PlayCatchVfxAt(rb.position);

        sampleIndex = 0;
        sampleFilled = 0;

        PushSample(fingerWorld + dragOffsetWorld, Time.unscaledTime);

        DragStep(screenPos);
    }

    void DragStep(Vector2 screenPos)
    {
        Vector2 fingerWorld = ScreenToWorld(screenPos);

        Vector2 desired = fingerWorld + dragOffsetWorld;

        Bounds2D b = ComputeBounds();

        Vector2 target = desired;
        target.x = Mathf.Clamp(target.x, b.minX, b.maxX);
        target.y = Mathf.Clamp(target.y, b.minY, b.maxY);

        rb.position = target;

        // Sample desired (not clamped target)
        PushSample(desired, Time.unscaledTime);
    }

    void ReleaseDrag()
    {
        if (!isDragging) return;
        isDragging = false;
        draggingTouchId = -1;

        if (kinematicWhileDragging)
            rb.bodyType = savedBodyType;

        Vector2 avgVel = ComputeAverageVelocity(Time.unscaledTime);
        Vector2 throwVel = avgVel * throwMultiplier;

        float mag = throwVel.magnitude;
        if (mag > maxThrowSpeed)
            throwVel = throwVel / mag * maxThrowSpeed;

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
        }

        sampleFilled = 0;
    }

    void CancelDragNoThrow()
    {
        isDragging = false;
        draggingTouchId = -1;

        if (kinematicWhileDragging)
            rb.bodyType = savedBodyType;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        sampleFilled = 0;

        WasThrown = false;
        WasDropped = false;
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
        if (!cam) cam = Camera.main;

        float z = cam.WorldToScreenPoint(rb.position).z;

        Vector2 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, z));
        Vector2 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));

        float r = BallRadiusWorld();

        return new Bounds2D
        {
            r = r,
            minX = bl.x + boundsInset + r,
            maxX = tr.x - boundsInset - r,
            minY = bl.y + boundsInset + r,
            maxY = tr.y - boundsInset - r
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

    Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (!cam) cam = Camera.main;
        float z = cam.WorldToScreenPoint(rb.position).z;

        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        return new Vector2(w.x, w.y);
    }
}