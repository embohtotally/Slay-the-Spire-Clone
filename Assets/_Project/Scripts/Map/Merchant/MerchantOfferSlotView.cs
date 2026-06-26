using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MerchantOfferSlotView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text actionText;

    [Header("Visuals")]
    [SerializeField] private string newCardActionLabel = "Take Card";
    [SerializeField] private string upgradeActionLabel = "Upgrade";
    [SerializeField] private string soldLabel = "Sold";

    private MerchantController merchantController;
    private MerchantOffer offer;

    private void Awake()
    {
        CacheReferences();
    }

    public void Setup(MerchantController controller, MerchantOffer newOffer)
    {
        merchantController = controller;
        offer = newOffer;
        CacheReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(SelectOffer);
            button.onClick.AddListener(SelectOffer);
        }

        Refresh();
    }

    public void Clear()
    {
        offer = null;
        if (titleText != null) titleText.text = "Empty";
        if (descriptionText != null) descriptionText.text = "No offer.";
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
        if (offer == null)
        {
            Clear();
            return;
        }

        CardData previewCard = offer.PreviewCard;
        bool canBuy = !offer.IsSold && previewCard != null;

        if (titleText != null) titleText.text = offer.GetTitle();
        if (descriptionText != null) descriptionText.text = offer.GetDescription();
        if (manaText != null) manaText.text = previewCard != null ? previewCard.Mana.ToString() : "";

        if (actionText != null)
        {
            actionText.text = offer.IsSold ? soldLabel : offer.Type == MerchantOfferType.UpgradeCard ? upgradeActionLabel : newCardActionLabel;
        }

        if (cardImage != null)
        {
            cardImage.sprite = previewCard != null ? previewCard.Image : null;
            cardImage.enabled = cardImage.sprite != null;
        }

        if (button != null) button.interactable = canBuy;
    }

    private void SelectOffer()
    {
        if (offer == null || offer.IsSold) return;
        merchantController?.BuyOffer(offer, this);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponent<Button>();

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
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
