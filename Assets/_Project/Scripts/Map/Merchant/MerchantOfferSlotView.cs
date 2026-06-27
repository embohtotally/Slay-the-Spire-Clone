using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MerchantOfferSlotView : CardView
{
    [Header("Merchants")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private string newCardActionLabel = "Take Card";
    [SerializeField] private string upgradeActionLabel = "Upgrade";
    [SerializeField] private string soldLabel = "Sold";

    private MerchantController merchantController;
    private MerchantOffer offer;

    protected override void Awake()
    {
        base.Awake();
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

    public override void ClearCardVisuals()
    {
        base.ClearCardVisuals();
        SetActionText(string.Empty);
        SetButtonInteractable(false);
    }

    public void Clear()
    {
        offer = null;
        ClearCardVisuals();
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
        SetupCard(previewCard, offer.GetTitle(), offer.GetDescription());
        SetActionText(offer.IsSold ? soldLabel : offer.Type == MerchantOfferType.UpgradeCard ? upgradeActionLabel : newCardActionLabel);
        SetButtonInteractable(canBuy);
    }

    private void SelectOffer()
    {
        if (offer == null || offer.IsSold) return;
        merchantController?.BuyOffer(offer, this);
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
