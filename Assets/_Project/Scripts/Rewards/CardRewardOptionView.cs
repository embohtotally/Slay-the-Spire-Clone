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

    public CardRewardOption Option => option;
    public RectTransform RectTransform => transform as RectTransform;

    protected override void Awake()
    {
        base.Awake();
    }

    public void Setup(CardRewardController controller, CardRewardOption newOption)
    {
        gameObject.SetActive(true);
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
        gameObject.SetActive(false);
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

    public void SetInteractable(bool interactable)
    {
        SetButtonInteractable(interactable && option?.PreviewCard != null);
    }

    private void SelectOption()
    {
        if (option == null) return;
        rewardController?.ChooseReward(this);
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
