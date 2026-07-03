using DG.Tweening;
using NaughtyAttributes;
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
    [SerializeField] private string notEnoughGoldLabel = "Not Enough Gold";
    [SerializeField] private string goldSuffix = "g";

    [Header("Purchase Behaviour")]
    [Tooltip("Keep unaffordable offers clickable so the player gets a shake/SFX instead of a silent disabled button.")]
    [SerializeField] private bool allowUnaffordableClickFeedback = true;

    [Foldout("Juice")]
    [SerializeField] private bool enableSlotFeedback = true;
    [Foldout("Juice")]
    [Tooltip("Usually the card visual root. If empty, this object's transform is used.")]
    [SerializeField] private Transform feedbackTarget;
    [Foldout("Juice")]
    [SerializeField] private Graphic feedbackTintGraphic;
    [Foldout("Juice")]
    [SerializeField] private float punchScale = 0.12f;
    [Foldout("Juice")]
    [SerializeField] private float punchDuration = 0.18f;
    [Foldout("Juice")]
    [Tooltip("Rotation punch strength in degrees. Kept layout-safe: this never changes the slot sibling order or anchored position.")]
    [SerializeField] private float shakeStrength = 8f;
    [Foldout("Juice")]
    [SerializeField] private float shakeDuration = 0.22f;
    [Foldout("Juice")]
    [SerializeField] private Color purchaseFlashColor = new(1f, 0.86f, 0.25f, 1f);
    [Foldout("Juice")]
    [SerializeField] private Color unavailableFlashColor = new(1f, 0.25f, 0.25f, 1f);
    [Foldout("Juice")]
    [SerializeField] private float flashDuration = 0.12f;

    private MerchantController merchantController;
    private MerchantOffer offer;
    private Color baseTintColor = Color.white;
    private bool hasBaseTintColor;

    protected override void Awake()
    {
        base.Awake();
        CacheFeedbackReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheFeedbackReferences();
    }

    protected override void OnDisable()
    {
        KillFeedbackTweens();
        base.OnDisable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        CacheFeedbackReferences();
    }

    public void Setup(MerchantController controller, MerchantOffer newOffer)
    {
        merchantController = controller;
        offer = newOffer;
        CacheReferences();
        CacheFeedbackReferences();

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
        KillFeedbackTweens();
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
        bool canAfford = merchantController == null || merchantController.CanAfford(offer);
        bool merchantAcceptsInput = merchantController == null || merchantController.PurchasesEnabled;
        bool canClick = merchantAcceptsInput && !offer.IsSold && previewCard != null && (canAfford || allowUnaffordableClickFeedback);

        SetupCard(previewCard, offer.GetTitle(), offer.GetDescription());
        SetActionText(GetActionLabel(canAfford));
        SetButtonInteractable(canClick);
    }

    public void PlayPurchasedFeedback()
    {
        if (!enableSlotFeedback) return;

        Transform target = GetFeedbackTarget();
        target.DOKill();
        target.DOPunchScale(Vector3.one * punchScale, punchDuration, 8, 0.6f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        FlashTint(purchaseFlashColor);
    }

    public void PlayUnavailableFeedback()
    {
        if (!enableSlotFeedback) return;

        Transform target = GetFeedbackTarget();
        target.DOKill();
        target.DOPunchRotation(Vector3.forward * shakeStrength, shakeDuration, 12, 0.6f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        FlashTint(unavailableFlashColor);
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

    private string GetActionLabel(bool canAfford)
    {
        if (offer == null) return string.Empty;
        if (offer.IsSold) return soldLabel;
        if (!canAfford) return notEnoughGoldLabel;

        string actionLabel = offer.Type == MerchantOfferType.UpgradeCard ? upgradeActionLabel : newCardActionLabel;
        return $"{actionLabel} - {offer.Price}{goldSuffix}";
    }

    private void CacheFeedbackReferences()
    {
        if (feedbackTarget == null) feedbackTarget = transform;

        if (feedbackTintGraphic == null)
        {
            feedbackTintGraphic = actionText != null ? actionText : GetComponentInChildren<Graphic>(true);
        }

        if (feedbackTintGraphic != null && !hasBaseTintColor)
        {
            baseTintColor = feedbackTintGraphic.color;
            hasBaseTintColor = true;
        }
    }

    private Transform GetFeedbackTarget()
    {
        return feedbackTarget != null ? feedbackTarget : transform;
    }

    private void FlashTint(Color color)
    {
        if (feedbackTintGraphic == null) return;

        feedbackTintGraphic.DOKill();
        feedbackTintGraphic.color = color;
        feedbackTintGraphic.DOColor(hasBaseTintColor ? baseTintColor : Color.white, flashDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void KillFeedbackTweens()
    {
        if (feedbackTarget != null) feedbackTarget.DOKill();
        if (feedbackTintGraphic != null) feedbackTintGraphic.DOKill();
    }
}
