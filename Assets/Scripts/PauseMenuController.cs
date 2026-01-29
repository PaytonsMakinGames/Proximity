using UnityEngine;

[DefaultExecutionOrder(-200)]
public class PauseMenuController : MonoBehaviour
{
    [Header("UI Groups")]
    [SerializeField] CanvasGroup pauseGroup;   // PauseCanvas root CanvasGroup
    [SerializeField] CanvasGroup lockerGroup;  // LockerCanvas root CanvasGroup
    [SerializeField] CanvasGroup hudGroup;     // HUDCanvas root CanvasGroup (optional)

    [Header("Refs")]
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] RunScoring2D scoring;

    [Header("PC simulate")]
    [SerializeField] KeyCode toggleKey = KeyCode.P;

    bool gestureArmed = true;
    bool pauseGestureBlockedThisTouch = false; // NEW: blocks pause for the rest of the touch if 5+ ever occurs

    public bool IsPaused { get; private set; }
    public bool LockerOpen { get; private set; }

    void Awake()
    {
        if (!grab)
            grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!scoring)
            scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        ForceAllClosed();
    }

    void Update()
    {
        // PC / Editor toggle
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape))
            TogglePause();

        CheckPauseGesture();
    }

    void CheckPauseGesture()
    {
        // Block pause if ball is being dragged or just released
        if (grab && grab.ShouldBlockPauseGesture)
            return;

        int count = Input.touchCount;

        // Fully released -> re-arm gesture + clear block
        if (count == 0)
        {
            gestureArmed = true;
            pauseGestureBlockedThisTouch = false;
            return;
        }

        // If 5+ fingers ever happens, block pause until released.
        // This prevents "5 fingers cancels AND pauses".
        if (count >= 5)
        {
            pauseGestureBlockedThisTouch = true;
            return;
        }

        // If this touch sequence was blocked, do nothing until released.
        if (pauseGestureBlockedThisTouch)
            return;

        // Toggle pause on 3 or 4 fingers once per gesture
        if ((count == 3 || count == 4) && gestureArmed)
        {
            gestureArmed = false;
            TogglePause();
        }
    }

    public void TogglePause()
    {
        // Block pause during early onboarding (until Phase 1 full reveal)
        if (scoring && !scoring.CanPauseNow())
            return;

        // Block pause while level-up modal is open
        if (LevelUpModalWindow.IsModalOpen)
            return;

        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        LockerOpen = false;
        GameInputLock.Locked = true;

        SetGroup(pauseGroup, true);
        SetGroup(lockerGroup, false);

        if (hudGroup) hudGroup.blocksRaycasts = false;
    }

    public void Resume()
    {
        ForceAllClosed();
    }

    // Hook to Locker button
    public void OpenLocker()
    {
        if (!IsPaused) Pause();

        LockerOpen = true;
        SetGroup(pauseGroup, false);
        SetGroup(lockerGroup, true);
    }

    // Hook to Locker back button
    public void CloseLocker()
    {
        if (!IsPaused) return;

        LockerOpen = false;
        SetGroup(lockerGroup, false);
        SetGroup(pauseGroup, true);
    }

    void ForceAllClosed()
    {
        IsPaused = false;
        LockerOpen = false;
        GameInputLock.Locked = false;

        SetGroup(pauseGroup, false);
        SetGroup(lockerGroup, false);

        if (hudGroup) hudGroup.blocksRaycasts = false;
    }

    static void SetGroup(CanvasGroup g, bool on)
    {
        if (!g) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}