using TMPro;
using UnityEngine;

/// <summary>
/// Single reward item in the level up modal window.
/// Shows reward name and description.
/// </summary>
public class LevelUpRewardItem : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI rewardNameText;
    [SerializeField] TextMeshProUGUI rewardDescriptionText;

    public void SetReward(string name, string description)
    {
        if (rewardNameText)
            rewardNameText.text = name;

        if (rewardDescriptionText)
            rewardDescriptionText.text = description;
    }
}
