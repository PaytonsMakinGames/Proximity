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
    [SerializeField] RectTransform radialRoot;      // PowerupRadialRoot (stretched full screen)
    [SerializeField] RectTransform center;          // RadialCenter (just a transform marker)
    [SerializeField] RectTransform itemsParent;     // Items (parent of instantiated RadialItem prefabs)
    [SerializeField] PowerupRadialItemUI itemPrefab;

    [Header("Canvas root (wire this)")]
    [SerializeField] RectTransform canvasRoot;      // RadialPadCanvas RectTransform (Overlay canvas root)

    [Header("Snap source (wire ONE)")]
    [SerializeField] Transform activationPadWorld;  // ActivationPadWorld (world sprite behind player)

    [Header("Layout")]
    [SerializeField] float radiusPx = 230f;         // distance from center to icons
    [SerializeField] float deadzonePx = 55f;        // no selection near center
    [SerializeField] float pickRadiusPx = 95f;      // how close to an icon counts as "hover"
    [SerializeField] float hoverConfirmTime = 0.25f;

    [Header("Close behavior")]
    [SerializeField] bool closeAfterConfirm = true;

    [SerializeField] TMPro.TextMeshProUGUI armedLabel;

    readonly List<PowerupRadialItemUI> items = new();

    CanvasGroup group;

    bool isOpen = false;
    bool dragEndedPending = false;
    bool builtThisOpen = false;

    int hoveredIndex = -1;
    float hoverTimer = 0f;

    void Awake()
    {
        // Auto-find basics if missing (safe)
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

    void RefreshArmedLabel()
    {
        if (!armedLabel) return;

        if (!manager || string.IsNullOrEmpty(manager.ArmedId))
        {
            armedLabel.text = "ARMED: none";
            return;
        }

        // Prefer display name if available
        var def = manager.GetArmedDef();
        string name = (def && !string.IsNullOrEmpty(def.displayName)) ? def.displayName : manager.ArmedId;

        armedLabel.text = $"ARMED: {name}";
    }

    void HandleDragEnded(bool thrown)
    {
        dragEndedPending = true;
    }

    void OnInventoryChanged()
    {
        if (isOpen) builtThisOpen = false;
    }

    void Update()
    {
        if (Camera.main == null) return;
        if (!pad || !radialRoot || !center || !itemsParent || !itemPrefab) return;
        if (!canvasRoot || !activationPadWorld) return;

        if (GameInputLock.Locked)
        {
            CloseNow();
            return;
        }

        // OPEN: only when pad requests it
        if (!isOpen)
        {
            if (!pad.RequestOpenThisFrame) return;
            OpenNow();
        }

        // CLOSE: only when drag ends
        if (dragEndedPending)
        {
            dragEndedPending = false;
            CloseNow();
            return;
        }

        // Build once per open (or rebuild if inventory changed)
        if (!builtThisOpen)
        {
            SnapCenterToPad();
            BuildMenu();
            builtThisOpen = true;
        }

        TickHoverAndConfirm();
    }

    void OpenNow()
    {
        isOpen = true;
        builtThisOpen = false;

        // Prevent the pad from re-triggering open logic while menu is open
        pad.BlockOpenRequests = true;

        ClearHover();
        SetVisible(true);
    }

    void CloseNow()
    {
        isOpen = false;
        builtThisOpen = false;

        pad.BlockOpenRequests = false;

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

    void SnapCenterToPad()
    {
        // World -> screen px
        Vector2 padScreen = Camera.main.WorldToScreenPoint(activationPadWorld.position);

        // Screen px -> canvas local
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot, padScreen, null, out Vector2 local))
            return;

        center.anchoredPosition = local;
        itemsParent.anchoredPosition = local;
    }

    void BuildMenu()
    {
        ClearMenu();

        if (!db || !inventory) return;

        // Only show owned powerups (count > 0)
        List<PowerupDefinition> owned = new();
        foreach (var p in db.powerups)
        {
            if (!p) continue;
            if (string.IsNullOrEmpty(p.id)) continue;
            if (inventory.GetCount(p.id) > 0)
                owned.Add(p);
        }

        if (owned.Count == 0) return;

        float step = 360f / owned.Count;
        float startAngle = 90f; // 12 o'clock

        for (int i = 0; i < owned.Count; i++)
        {
            var def = owned[i];
            var ui = Instantiate(itemPrefab, itemsParent);
            ui.powerupId = def.id;

            if (ui.iconImage)
            {
                ui.iconImage.sprite = def.icon;
                ui.iconImage.enabled = (def.icon != null);
            }

            ui.SetCount(inventory.GetCount(def.id));
            ui.SetHighlighted(false);

            float angle = (startAngle - step * i) * Mathf.Deg2Rad; // clockwise placement
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusPx;

            var rt = (RectTransform)ui.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = pos;

            items.Add(ui);
        }

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

        // Ball world -> screen px
        Vector2 ballScreen = (Vector2)Camera.main.WorldToScreenPoint(ballRb.position);

        // Screen px -> canvas local (same space as itemsParent.anchoredPosition)
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot, ballScreen, null, out Vector2 ballLocal))
        {
            ClearHover();
            return;
        }

        // Must be outside center deadzone (prevents constant neighbor flips near center)
        Vector2 deltaFromCenter = ballLocal - center.anchoredPosition;
        if (deltaFromCenter.magnitude < deadzonePx)
        {
            ClearHover();
            return;
        }

        // Nearest-icon picking (intuitive)
        int bestIndex = -1;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = (RectTransform)items[i].transform;

            // Icon position in canvasRoot local space:
            // itemsParent is anchored at center, icons are offset from itemsParent.
            Vector2 iconLocal = itemsParent.anchoredPosition + rt.anchoredPosition;

            float d = Vector2.Distance(ballLocal, iconLocal);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        // Require being close enough to an icon
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

        if (closeAfterConfirm)
            CloseNow();
        else
            ClearHover();
    }
}