using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LockerUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PlayerInventory inventory;

    [Header("Optional navigation (recommended)")]
    [SerializeField] PauseMenuController pauseMenu; // drag your PauseMenuController in here

    [Header("Locker visibility (if not using PauseMenuController)")]
    [SerializeField] CanvasGroup lockerGroup; // CanvasGroup on LockerCanvas (or LockerPanel root)

    [Header("Slot Buttons")]
    [SerializeField] Button ballSlotButton;
    [SerializeField] Button trailSlotButton;

    [Header("Item List")]
    [SerializeField] Transform itemListRoot;
    [SerializeField] Button itemButtonPrefab; // template prefab
    [SerializeField] TextMeshProUGUI debugHeaderText; // optional

    EquipSlot currentSlot = EquipSlot.Ball;

    // Pool (prevents destroy/instantiate churn)
    readonly List<Button> buttonPool = new List<Button>();
    int activeButtonCount = 0;

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        SetGroup(lockerGroup, false);
    }

    void OnEnable()
    {
        if (ballSlotButton) ballSlotButton.onClick.AddListener(SelectBall);
        if (trailSlotButton) trailSlotButton.onClick.AddListener(SelectTrail);

        if (inventory) inventory.OnChanged += Refresh;

        // If it gets enabled while visible, populate
        Refresh();
    }

    void OnDisable()
    {
        if (ballSlotButton) ballSlotButton.onClick.RemoveListener(SelectBall);
        if (trailSlotButton) trailSlotButton.onClick.RemoveListener(SelectTrail);

        if (inventory) inventory.OnChanged -= Refresh;
    }

    // Hook this to the Locker button if you want:
    public void Open()
    {
        // Preferred: let PauseMenuController handle which canvas is visible
        if (pauseMenu)
        {
            pauseMenu.OpenLocker();
        }
        else
        {
            SetGroup(lockerGroup, true);
        }

        Refresh();
    }

    // Hook this to the Locker Back button if you want:
    public void Close()
    {
        if (pauseMenu)
        {
            pauseMenu.CloseLocker();
        }
        else
        {
            SetGroup(lockerGroup, false);
        }
    }

    public void SelectBall()
    {
        currentSlot = EquipSlot.Ball;
        Refresh();
    }

    public void SelectTrail()
    {
        currentSlot = EquipSlot.Trail;
        Refresh();
    }

    void Refresh()
    {
        // Disable all active pooled buttons first
        for (int i = 0; i < activeButtonCount; i++)
        {
            if (buttonPool[i])
                buttonPool[i].gameObject.SetActive(false);
        }
        activeButtonCount = 0;

        if (!inventory) return;
        if (inventory.Save == null || inventory.Save.owned == null) return;

        var equipped = inventory.GetEquipped(currentSlot);

        if (debugHeaderText)
        {
            string slotName = currentSlot == EquipSlot.Ball ? "Ball" : "Trail";
            string equippedName = equipped ? equipped.displayName : "None";
            debugHeaderText.text = slotName + " | Equipped: " + equippedName;
        }

        foreach (var ownedId in inventory.GetOwnedIds())
        {
            var def = inventory.GetById(ownedId);
            if (def == null) continue;
            if (def.slot != currentSlot) continue;

            bool isEquipped = equipped != null && def.id == equipped.id;
            var btn = GetButtonFromPool();
            ConfigureButton(btn, def, isEquipped);
        }
    }

    Button GetButtonFromPool()
    {
        if (!itemButtonPrefab || !itemListRoot) return null;

        Button btn;

        if (activeButtonCount < buttonPool.Count)
        {
            btn = buttonPool[activeButtonCount];
        }
        else
        {
            btn = Instantiate(itemButtonPrefab, itemListRoot);
            buttonPool.Add(btn);
        }

        activeButtonCount++;

        if (btn)
        {
            btn.gameObject.SetActive(true);
            btn.onClick.RemoveAllListeners();
        }

        return btn;
    }

    void ConfigureButton(Button btn, ItemDef def, bool isEquipped)
    {
        if (!btn) return;

        var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label)
        {
            string eq = isEquipped ? " (Equipped)" : "";
            string bonus = def.xpBonus01 > 0f ? "  +" + Mathf.RoundToInt(def.xpBonus01 * 100f) + "% XP" : "";
            label.text = def.displayName + bonus + eq;
        }

        var img = btn.GetComponentInChildren<Image>(true);
        if (img && def.icon) img.sprite = def.icon;

        btn.onClick.AddListener(() =>
        {
            inventory.Equip(def.id);
            Refresh();
        });
    }

    static void SetGroup(CanvasGroup g, bool on)
    {
        if (!g) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}