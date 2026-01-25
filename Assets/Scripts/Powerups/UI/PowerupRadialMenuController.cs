using System.Collections.Generic;
using UnityEngine;

public class PowerupRadialMenuController : MonoBehaviour
{
    [Header("Refs (wire these)")]
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] Rigidbody2D ballRb;
    [SerializeField] PowerupDatabase db;
    [SerializeField] PowerupInventory inventory;
    [SerializeField] PowerupManager manager;
    [SerializeField] PowerupRadialPadController pad;

    [Header("UI (wire these)")]
    [SerializeField] RectTransform radialRoot;      // full-screen rect under the same Canvas
    [SerializeField] RectTransform center;          // RadialCenter (child of radialRoot)
    [SerializeField] RectTransform itemsParent;     // Items (child of radialRoot)
    [SerializeField] PowerupRadialItemUI itemPrefab;
    [SerializeField] TMPro.TextMeshProUGUI armedLabel;

    [Header("Layout")]
    [SerializeField] float radiusPx = 230f;
    [SerializeField] float deadzonePx = 55f;
    [SerializeField] float pickRadiusPx = 95f;
    [SerializeField] float hoverConfirmTime = 0.25f;

    [Header("Close behavior")]
    [SerializeField] bool closeAfterConfirm = true;

    [Header("Snap source (world)")]
    [SerializeField] Transform activationPadWorld; // same object used by PadController

    [Header("Disarm when leaving menu area")]
    [SerializeField] float leaveAreaRadiusPx = 340f;
    [SerializeField] float disarmDelay = 0.50f;

    readonly List<PowerupRadialItemUI> items = new();

    CanvasGroup group;

    bool isOpen;
    bool builtThisOpen;
    bool dragEndedPending;

    int hoveredIndex = -1;
    float hoverTimer;

    float leaveTimer;

    void Awake()
    {
        if (!pad) pad = GetComponent<PowerupRadialPadController>();
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!ballRb && grab) ballRb = grab.GetComponent<Rigidbody2D>();

        if (!inventory) inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
        if (!manager) manager = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
        if (!db) db = FindFirstObjectByType<PowerupDatabase>(FindObjectsInactive.Include);

        if (radialRoot)
        {
            group = radialRoot.GetComponent<CanvasGroup>();
            if (!group) group = radialRoot.gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
        }
    }

    void OnEnable()
    {
        if (inventory) inventory.OnChanged += OnInventoryChanged;
        if (grab) grab.OnDragEnded += HandleDragEnded;
        if (manager) manager.OnArmedChanged += RefreshArmedLabel;
        RefreshArmedLabel();
    }

    void OnDisable()
    {
        if (inventory) inventory.OnChanged -= OnInventoryChanged;
        if (grab) grab.OnDragEnded -= HandleDragEnded;
        if (manager) manager.OnArmedChanged -= RefreshArmedLabel;
    }

    void HandleDragEnded(bool thrown) => dragEndedPending = true;

    void OnInventoryChanged()
    {
        if (isOpen) builtThisOpen = false;
    }

    void Update()
    {
        if (Camera.main == null) return;
        if (!pad || !radialRoot || !center || !itemsParent || !itemPrefab) return;

        if (GameInputLock.Locked)
        {
            CloseNow();
            return;
        }

        // OPEN
        if (!isOpen)
        {
            if (!pad.ConsumeOpenRequest()) return;
            OpenNow();
        }

        // CLOSE
        if (dragEndedPending)
        {
            dragEndedPending = false;
            CloseNow();
            return;
        }

        if (!builtThisOpen)
        {
            SnapToPadWorld();
            BuildMenu();
            builtThisOpen = true;
        }

        TickHoverAndConfirm();
        TickLeaveToDisarm();
    }

    void OpenNow()
    {
        isOpen = true;
        builtThisOpen = false;

        pad.BlockOpenRequests = true;

        leaveTimer = 0f;
        ClearHover();
        SetVisible(true);
    }

    void CloseNow()
    {
        if (!isOpen) return;

        isOpen = false;
        builtThisOpen = false;

        pad.BlockOpenRequests = false;

        leaveTimer = 0f;
        ClearHover();
        ClearMenu();
        SetVisible(false);
    }

    void SetVisible(bool on)
    {
        if (!group) return;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    void SnapToPadWorld()
    {
        if (!activationPadWorld) return;

        Camera cam = Camera.main;
        if (!cam) return;

        Vector2 padScreen = RectTransformUtility.WorldToScreenPoint(cam, activationPadWorld.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            radialRoot,
            padScreen,
            null,
            out Vector2 local
        );

        center.anchoredPosition = local;
        itemsParent.anchoredPosition = local;
    }

    void BuildMenu()
    {
        ClearMenu();
        if (!db || !inventory) return;

        List<PowerupDefinition> owned = new();
        foreach (var p in db.powerups)
        {
            if (!p) continue;
            if (string.IsNullOrEmpty(p.id)) continue;

            // Only show if player owns it AND it's unlocked
            if (inventory.GetCount(p.id) > 0)
            {
                // Check if unlocked
                if (manager && !manager.IsPowerupUnlocked(p.id))
                    continue; // Skip locked powerups

                owned.Add(p);
            }
        }

        if (owned.Count == 0) return;

        float step = 360f / owned.Count;
        float startAngle = 90f;

        for (int i = 0; i < owned.Count; i++)
        {
            var def = owned[i];
            var ui = Instantiate(itemPrefab, itemsParent);
            ui.powerupId = def.id;

            if (ui.iconImage)
            {
                ui.iconImage.sprite = def.icon;
                ui.iconImage.enabled = (def.icon != null);
                ui.iconImage.color = def.uiColor;
            }
            if (ui.highlight)
            {
                Color c = def.uiColor;
                c.a = 1f;                 // full alpha for highlight
                ui.highlight.color = c;
            }
            if (ui.background)
            {
                Color c = def.uiColor;
                c.a = 0.45f;              // tweak to taste
                ui.background.color = c;
                ui.background.enabled = true;
            }

            ui.SetCount(inventory.GetCount(def.id));
            ui.SetHighlighted(false);

            float ang = (startAngle - step * i) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radiusPx;

            RectTransform rt = (RectTransform)ui.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = pos;

            items.Add(ui);
        }

        RefreshArmedLabel();
        ClearHover();
    }

    void ClearMenu()
    {
        for (int i = 0; i < items.Count; i++)
            if (items[i]) Destroy(items[i].gameObject);
        items.Clear();
    }

    void TickHoverAndConfirm()
    {
        if (items.Count == 0 || !ballRb) { ClearHover(); return; }

        Vector2 ballScreen = Camera.main.WorldToScreenPoint(ballRb.position);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(radialRoot, ballScreen, null, out Vector2 ballLocal))
        {
            ClearHover();
            return;
        }

        Vector2 delta = ballLocal - center.anchoredPosition;
        if (delta.magnitude < deadzonePx)
        {
            ClearHover();
            return;
        }

        int bestIndex = -1;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = (RectTransform)items[i].transform;
            Vector2 iconLocal = itemsParent.anchoredPosition + rt.anchoredPosition;

            float d = Vector2.Distance(ballLocal, iconLocal);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        if (bestIndex < 0 || bestDist > pickRadiusPx)
        {
            ClearHover();
            return;
        }

        if (bestIndex != hoveredIndex)
        {
            SetHovered(bestIndex);
            hoverTimer = 0f;
            return;
        }

        hoverTimer += Time.unscaledDeltaTime;
        if (hoverTimer >= hoverConfirmTime)
            ConfirmHovered();
    }

    void TickLeaveToDisarm()
    {
        if (!ballRb) { leaveTimer = 0f; return; }

        // If currently hovering an item, don't auto-close/disarm.
        if (hoveredIndex != -1)
        {
            leaveTimer = 0f;
            return;
        }

        Vector2 ballScreen = Camera.main.WorldToScreenPoint(ballRb.position);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(radialRoot, ballScreen, null, out Vector2 ballLocal))
        {
            leaveTimer = 0f;
            return;
        }

        float dist = Vector2.Distance(ballLocal, center.anchoredPosition);

        // Inside the general menu area: no leave timer
        if (dist <= leaveAreaRadiusPx)
        {
            leaveTimer = 0f;
            return;
        }

        // Outside the menu area: start timer
        leaveTimer += Time.unscaledDeltaTime;

        if (leaveTimer >= disarmDelay)
        {
            // Disarm only if something is armed
            if (manager && manager.HasArmed)
            {
                manager.Disarm_NoConsume();
                RefreshArmedLabel();
            }

            leaveTimer = 0f;

            // Always close
            CloseNow();
        }
    }

    void SetHovered(int index)
    {
        hoveredIndex = index;
        for (int i = 0; i < items.Count; i++)
            items[i].SetHighlighted(i == hoveredIndex);
    }

    void ClearHover()
    {
        hoveredIndex = -1;
        hoverTimer = 0f;
        for (int i = 0; i < items.Count; i++)
            items[i].SetHighlighted(false);
    }

    void ConfirmHovered()
    {
        if (hoveredIndex < 0 || hoveredIndex >= items.Count) return;

        string id = items[hoveredIndex].powerupId;
        if (!string.IsNullOrEmpty(id) && manager)
            manager.TryArm(id);

        RefreshArmedLabel();

        if (closeAfterConfirm) CloseNow();
        else ClearHover();
    }

    void RefreshArmedLabel()
    {
        if (!armedLabel) return;

        if (!manager || string.IsNullOrEmpty(manager.ArmedId))
        {
            armedLabel.text = "";
            return;
        }

        var def = manager.GetArmedDef();
        string name = (def && !string.IsNullOrEmpty(def.displayName)) ? def.displayName : manager.ArmedId;
        armedLabel.text = $"{name}";
    }
}