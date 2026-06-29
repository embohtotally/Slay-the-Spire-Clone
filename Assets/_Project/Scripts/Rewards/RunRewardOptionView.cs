using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RunRewardOptionView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private GameObject claimedRoot;
    [SerializeField] private string claimLabel = "Claim";
    [SerializeField] private string claimedLabel = "Claimed";

    private RunRewardController controller;
    private int rewardIndex = -1;

    private void Awake()
    {
        CacheReferences();
    }

    public void Setup(RunRewardController newController, int newRewardIndex, RunRewardDefinition reward, bool claimed)
    {
        controller = newController;
        rewardIndex = newRewardIndex;
        CacheReferences();

        if (titleText != null) titleText.text = reward != null ? reward.GetTitle() : "Reward";
        if (descriptionText != null) descriptionText.text = reward != null ? reward.GetDescription() : string.Empty;
        if (actionText != null) actionText.text = claimed ? claimedLabel : claimLabel;
        if (claimedRoot != null) claimedRoot.SetActive(claimed);

        if (button != null)
        {
            button.onClick.RemoveListener(ClaimReward);
            button.onClick.AddListener(ClaimReward);
            button.interactable = !claimed && reward != null;
        }

        gameObject.SetActive(reward != null);
    }

    public void Clear()
    {
        controller = null;
        rewardIndex = -1;
        if (button != null) button.onClick.RemoveListener(ClaimReward);
        gameObject.SetActive(false);
    }

    private void ClaimReward()
    {
        if (rewardIndex < 0) return;
        controller?.ClaimReward(rewardIndex);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponentInChildren<Button>(true);

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[1];
        if (actionText == null && texts.Length > 2) actionText = texts[2];
    }
}
