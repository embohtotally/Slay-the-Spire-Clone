using UnityEngine;
using UnityEngine.UI;

public class CardRewardOptionView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image cardImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text manaText;
    [SerializeField] private Text actionText;

    [Header("Labels")]
    [SerializeField] private string newCardActionLabel = "Choose Card";
    [SerializeField] private string upgradeActionLabel = "Choose Upgrade";

    private CardRewardController rewardController;
    private CardRewardOption option;

    private void Awake()
    {
        CacheReferences();
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

    public void Clear()
    {
        option = null;
        if (titleText != null) titleText.text = "Empty";
        if (descriptionText != null) descriptionText.text = "No reward.";
        if (manaText != null) manaText.text = "";
        if (actionText != null) actionText.text = "";

        if (cardImage != null)
        {
            cardImage.sprite = null;
            cardImage.enabled = false;
        }

        if (button != null) button.interactable = false;
    }

    public void Refresh()
    {
        if (option == null)
        {
            Clear();
            return;
        }

        CardData previewCard = option.PreviewCard;
        if (titleText != null) titleText.text = option.GetTitle();
        if (descriptionText != null) descriptionText.text = option.GetDescription();
        if (manaText != null) manaText.text = previewCard != null ? previewCard.Mana.ToString() : "";
        if (actionText != null) actionText.text = option.Type == CardRewardOptionType.UpgradeCard ? upgradeActionLabel : newCardActionLabel;

        if (cardImage != null)
        {
            cardImage.sprite = previewCard != null ? previewCard.Image : null;
            cardImage.enabled = cardImage.sprite != null;
        }

        if (button != null) button.interactable = previewCard != null;
    }

    private void SelectOption()
    {
        if (option == null) return;
        rewardController?.ChooseReward(option);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponent<Button>();

        Text[] texts = GetComponentsInChildren<Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[1];
        if (manaText == null && texts.Length > 2) manaText = texts[2];
        if (actionText == null && texts.Length > 3) actionText = texts[3];

        if (cardImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image.gameObject != gameObject)
                {
                    cardImage = image;
                    break;
                }
            }
        }
    }
}
