using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardRewardOptionView : CardView
{
    [Header("Reward")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private string newCardActionLabel = "Choose Card";
    [SerializeField] private string upgradeActionLabel = "Choose Upgrade";

    private CardRewardController rewardController;
    private CardRewardOption option;

    protected override void Awake()
    {
        base.Awake();
    }

    public void Setup(CardRewardController controller, CardRewardOption newOption)
    {
        rewardController = controller;
        option = newOption;
        CacheReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(SelectOption);
            button.onClick.AddListener(SelectOption);
        }

        Refresh();
    }

    public override void ClearCardVisuals()
    {
        base.ClearCardVisuals();
        SetActionText(string.Empty);
        SetButtonInteractable(false);
    }

    public void Clear()
    {
        option = null;
        ClearCardVisuals();
    }

    public void Refresh()
    {
        if (option == null)
        {
            Clear();
            return;
        }

        CardData previewCard = option.PreviewCard;
        SetupCard(previewCard, option.GetTitle(), option.GetDescription());
        SetActionText(option.Type == CardRewardOptionType.UpgradeCard ? upgradeActionLabel : newCardActionLabel);
        SetButtonInteractable(previewCard != null);
    }

    private void SelectOption()
    {
        if (option == null) return;
        rewardController?.ChooseReward(option);
    }

    private void SetActionText(string text)
    {
        if (actionText != null) actionText.text = text ?? string.Empty;
    }

    private void SetButtonInteractable(bool enable)
    {
        if (button != null) button.interactable = enable;
    }
}
