using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a modal window when player levels up, displaying the level and all rewards granted.
/// Blocks interaction with game until player dismisses it.
/// </summary>
public class LevelUpModalWindow : MonoBehaviour
{
    public static bool IsModalOpen { get; private set; }

    [Header("Refs")]
    [SerializeField] LevelRewardManager levelRewardManager;
    [SerializeField] CanvasGroup canvasGroup;  // For fade in/out
    [SerializeField] RectTransform windowPanel;

    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI levelText;  // "LEVEL 15"
    [SerializeField] Transform rewardsContainer;  // Parent for reward items
    [SerializeField] GameObject rewardItemPrefab;  // Prefab with reward name + description
    [SerializeField] Button closeButton;

    [Header("Animation")]
    [SerializeField] float fadeInDuration = 0.3f;
    [SerializeField] AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float scaleInDuration = 0.3f;
    [SerializeField] AnimationCurve scaleInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    float fadeTimer;
    float scaleTimer;
    bool isAnimatingIn;

    void Awake()
    {
        if (!levelRewardManager) levelRewardManager = FindFirstObjectByType<LevelRewardManager>(FindObjectsInactive.Include);
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!windowPanel) windowPanel = GetComponent<RectTransform>();

        // Start invisible
        canvasGroup.alpha = 0f;
        if (windowPanel) windowPanel.localScale = Vector3.one * 0.5f;

        // Subscribe to events in Awake so it works even if inactive
        if (levelRewardManager)
            levelRewardManager.OnLevelUpRewardsGranted += ShowLevelUpWindow;

        if (closeButton)
            closeButton.onClick.AddListener(HideLevelUpWindow);
    }

    void OnEnable()
    {
        // (subscriptions moved to Awake)
    }

    void OnDisable()
    {
        if (levelRewardManager)
            levelRewardManager.OnLevelUpRewardsGranted -= ShowLevelUpWindow;

        if (closeButton)
            closeButton.onClick.RemoveListener(HideLevelUpWindow);
    }

    void Update()
    {
        if (!isAnimatingIn) return;

        fadeTimer += Time.deltaTime / fadeInDuration;
        scaleTimer += Time.deltaTime / scaleInDuration;

        canvasGroup.alpha = fadeInCurve.Evaluate(Mathf.Clamp01(fadeTimer));

        if (windowPanel)
        {
            float scale = Mathf.Lerp(0.5f, 1f, scaleInCurve.Evaluate(Mathf.Clamp01(scaleTimer)));
            windowPanel.localScale = Vector3.one * scale;
        }

        if (fadeTimer >= 1f && scaleTimer >= 1f)
            isAnimatingIn = false;
    }

    void ShowLevelUpWindow(int level, List<Reward> rewards)
    {
        // Clear previous rewards
        foreach (Transform child in rewardsContainer)
            Destroy(child.gameObject);

        // Set level text
        if (levelText)
            levelText.text = $"LEVEL {level}";

        // Populate rewards
        foreach (var reward in rewards)
        {
            var item = Instantiate(rewardItemPrefab, rewardsContainer);
            var uiItem = item.GetComponent<LevelUpRewardItem>();
            if (uiItem)
            {
                uiItem.SetReward(reward.displayName, reward.description);
            }
        }

        // Block game input
        GameInputLock.Locked = true;
        IsModalOpen = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        // Start fade in animation
        fadeTimer = 0f;
        scaleTimer = 0f;
        isAnimatingIn = true;
    }

    public void HideLevelUpWindow()
    {
        GameInputLock.Locked = false;
        IsModalOpen = false;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        isAnimatingIn = false;
    }
}
