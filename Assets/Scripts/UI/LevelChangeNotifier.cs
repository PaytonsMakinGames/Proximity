using UnityEngine;
using TMPro;

public class LevelChangeNotifier : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] XpManager xp;
    [SerializeField] Canvas canvas;
    [SerializeField] GameObject levelUpPrefab;
    [SerializeField] GameObject levelDownPrefab;

    [Header("Position")]
    [SerializeField] Vector2 screenCenterOffset = new Vector2(0f, 100f);

    [Header("Animation")]
    [SerializeField] float displayDuration = 2f;
    [SerializeField] float fadeInTime = 0.2f;
    [SerializeField] float fadeOutTime = 0.5f;
    [SerializeField] float scaleUpAmount = 1.2f;
    [SerializeField] float scaleUpTime = 0.3f;

    [Header("Size")]
    [SerializeField] float fontSize = 72f;
    [SerializeField] Vector2 popupSize = new Vector2(600, 150);

    [Header("Colors")]
    [SerializeField] Color levelUpColor = new Color(0.3f, 1f, 0.3f, 1f); // green
    [SerializeField] Color levelDownColor = new Color(1f, 0.3f, 0.3f, 1f); // red

    void Awake()
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!canvas) canvas = GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!xp) return;

        xp.OnLevelUp += HandleLevelUp;
        xp.OnLevelDown += HandleLevelDown;
    }

    void OnDisable()
    {
        if (!xp) return;
        xp.OnLevelUp -= HandleLevelUp;
        xp.OnLevelDown -= HandleLevelDown;
    }

    void HandleLevelUp(int newLevel)
    {
        ShowLevelChangePopup($"LEVEL {newLevel}!", levelUpColor, levelUpPrefab);
    }

    void HandleLevelDown(int newLevel)
    {
        ShowLevelChangePopup($"LEVEL {newLevel}", levelDownColor, levelDownPrefab);
    }

    void ShowLevelChangePopup(string text, Color color, GameObject prefab)
    {
        if (!canvas) return;

        // Use prefab if provided, otherwise create simple text
        GameObject popup = prefab ? Instantiate(prefab, canvas.transform) : CreateDefaultPopup();

        RectTransform rt = popup.GetComponent<RectTransform>();
        if (rt)
        {
            rt.anchoredPosition = screenCenterOffset;
        }

        // Find and configure text
        TextMeshProUGUI tmp = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp)
        {
            tmp.text = text;
            tmp.color = color;
        }

        // Animate
        StartCoroutine(AnimatePopup(popup, rt));
    }

    GameObject CreateDefaultPopup()
    {
        GameObject popup = new GameObject("LevelChangePopup");
        popup.transform.SetParent(canvas.transform, false);

        RectTransform rt = popup.AddComponent<RectTransform>();
        rt.sizeDelta = popupSize;

        TextMeshProUGUI tmp = popup.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        CanvasGroup cg = popup.AddComponent<CanvasGroup>();
        cg.alpha = 0;

        return popup;
    }

    System.Collections.IEnumerator AnimatePopup(GameObject popup, RectTransform rt)
    {
        if (!popup) yield break;

        CanvasGroup cg = popup.GetComponent<CanvasGroup>();
        if (!cg) cg = popup.AddComponent<CanvasGroup>();

        Vector3 startScale = Vector3.one;
        Vector3 targetScale = Vector3.one * scaleUpAmount;

        // Fade in + scale up
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInTime;

            if (cg) cg.alpha = t;
            if (rt) rt.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        if (cg) cg.alpha = 1f;
        if (rt) rt.localScale = targetScale;

        // Scale back to normal
        elapsed = 0f;
        while (elapsed < scaleUpTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleUpTime;

            if (rt) rt.localScale = Vector3.Lerp(targetScale, startScale, t);

            yield return null;
        }

        if (rt) rt.localScale = startScale;

        // Hold
        yield return new WaitForSeconds(displayDuration - fadeInTime - scaleUpTime);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutTime;

            if (cg) cg.alpha = 1f - t;

            yield return null;
        }

        Destroy(popup);
    }
}
