using UnityEngine;

public class EquippedVisualsApplier : MonoBehaviour
{
    [SerializeField] PlayerInventory inventory;

    [Header("Ball Visual")]
    [SerializeField] SpriteRenderer ballRenderer;

    [Header("Trail Visual")]
    [SerializeField] TrailRenderer trailRenderer;

    [Header("Ball Prefab Skin")]
    [SerializeField] Transform visualRoot;

    GameObject activeBallSkinInstance;

    GameObject activeTrailInstance;

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);

        if (!visualRoot)
        {
            var t = transform.Find("VisualRoot");
            if (t) visualRoot = t;
        }
    }

    void Start()
    {
        Apply();
    }

    void OnEnable()
    {
        if (inventory) inventory.OnChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        if (inventory) inventory.OnChanged -= Apply;
    }

    public void Apply()
    {
        var ballItem = inventory.GetEquipped(EquipSlot.Ball);

        if (ballItem != null && ballItem.ballSkinPrefab != null)
        {
            ReplaceBallSkin(ballItem.ballSkinPrefab);
        }
        else
        {
            // No prefab means: remove prefab skin and optionally fall back to ballRenderer (if you still use it)
            RemoveBallSkin();
        }

        var trailItem = inventory.GetEquipped(EquipSlot.Trail);

        if (trailItem != null && trailItem.trailPrefab != null)
        {
            ReplaceTrail(trailItem.trailPrefab);
        }
        else
        {
            RemoveTrail();
        }
    }

    void ReplaceBallSkin(GameObject prefab)
    {
        if (!visualRoot) return;

        RemoveBallSkin();

        activeBallSkinInstance = Instantiate(prefab, visualRoot);
        activeBallSkinInstance.transform.localPosition = Vector3.zero;
        activeBallSkinInstance.transform.localRotation = Quaternion.identity;
        activeBallSkinInstance.transform.localScale = Vector3.one;
    }

    void RemoveBallSkin()
    {
        if (activeBallSkinInstance)
        {
            Destroy(activeBallSkinInstance);
            activeBallSkinInstance = null;
        }
    }

    void ReplaceTrail(GameObject prefab)
    {
        RemoveTrail();

        activeTrailInstance = Instantiate(prefab, transform);
        activeTrailInstance.transform.localPosition = Vector3.zero;
        activeTrailInstance.transform.localRotation = Quaternion.identity;
    }

    void RemoveTrail()
    {
        if (activeTrailInstance)
        {
            Destroy(activeTrailInstance);
            activeTrailInstance = null;
        }
    }
}