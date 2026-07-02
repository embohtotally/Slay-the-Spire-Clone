using DG.Tweening;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum CardViewMode
{
    CanvasDisplay,
    WorldCombat,
}

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [BoxGroup("Mode")]
    [SerializeField] private CardViewMode viewMode = CardViewMode.CanvasDisplay;

    [BoxGroup("Text References (TMP World or UI)")]
    [SerializeField] private TMP_Text titleText;

    [BoxGroup("Text References (TMP World or UI)")]
    [SerializeField] private TMP_Text descriptionText;

    [BoxGroup("Text References (TMP World or UI)")]
    [SerializeField] private TMP_Text manaText;

    [BoxGroup("Text References (TMP World or UI)")]
    [SerializeField] private TMP_Text typeText;


    [BoxGroup("Visual References")]
    [ShowIf(nameof(viewMode), CardViewMode.CanvasDisplay)]
    [SerializeField] private Image cardImage;

    [BoxGroup("Visual References")]
    [ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private SpriteRenderer cardSpriteRenderer;


    [BoxGroup("Optional Combat References"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private GameObject worldWrapper;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private bool enableCombatInteractions = true;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private bool enableCombatHoverPreview = true;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private bool enableFeebleTint = true;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private LayerMask dropAreaLayer;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private float cardHoverYOffset = -2f;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private float mousePositionZValue = -1f;

    [BoxGroup("Combat Interaction"), ShowIf(nameof(viewMode), CardViewMode.WorldCombat)]
    [SerializeField] private float mouseUpRaycastDistance = 10f;

    [Foldout("Hover / Press Juice")]
    [SerializeField] private bool enableHoverJuice;

    [Foldout("Hover / Press Juice")]
    [ShowIf(nameof(enableHoverJuice))]
    [SerializeField] private Transform hoverTarget;

    [Foldout("Hover / Press Juice")]
    [ShowIf(nameof(enableHoverJuice))]
    [SerializeField] private float hoverScale = 1.08f;

    [Foldout("Hover / Press Juice")]
    [ShowIf(nameof(enableHoverJuice))]
    [SerializeField] private float pressedScale = 0.97f;

    [Foldout("Hover / Press Juice")]
    [ShowIf(nameof(enableHoverJuice))]
    [SerializeField] private float tweenDuration = 0.12f;

    [Foldout("Hover / Press Juice")]
    [ShowIf(nameof(enableHoverJuice))]
    [SerializeField] private Ease hoverEase = Ease.OutBack;

    [Foldout("Hover / Press Juice")]
    [ShowIf(nameof(enableHoverJuice))]
    [SerializeField] private bool bringToFrontOnHover = true;

    [Foldout("Audio")]
    [SerializeField] private TuneSfxCue hoverSfx;
    [Foldout("Audio")]
    [SerializeField] private TuneSfxCue pickUpSfx;
    [Foldout("Audio")]
    [SerializeField] private TuneSfxCue targetStartSfx;
    [Foldout("Audio")]
    [SerializeField] private TuneSfxCue releaseValidSfx;
    [Foldout("Audio")]
    [SerializeField] private TuneSfxCue releaseInvalidSfx;

    public Card Card { get; private set; }
    public CardData CardData { get; private set; }

    private Vector3 dragStartPosition;
    private Quaternion dragStartRotation;
    private bool isFeebleBlocked;
    private Vector3 baseHoverScale = Vector3.one;
    private Tween hoverTween;
    private int currentDisplayedMana = -1;

    protected virtual void Awake()
    {
        CacheReferences();
        CacheBaseHoverScale();
    }

    protected virtual void OnValidate()
    {
        CacheReferences();

        if (viewMode == CardViewMode.CanvasDisplay)
        {
            enableCombatInteractions = false;
        }
        else if (viewMode == CardViewMode.WorldCombat)
        {
            enableCombatInteractions = true;
        }
    }

    protected virtual void OnEnable()
    {
        CacheBaseHoverScale();
    }

    protected virtual void OnDisable()
    {
        hoverTween?.Kill();
        ResetHoverScale();
    }

    protected virtual void Update()
    {
        RefreshCombatTint();
        RefreshManaCost();
    }

    public void Setup(Card card)
    {
        Card = card;
        CardData = null;
        currentDisplayedMana = -1;

        if (card == null)
        {
            ClearCardVisuals();
            return;
        }

        ApplyVisuals(card.Title, card.Description, card.Mana, card.Type, card.Image);
    }

    public void Setup(CardData cardData)
    {
        Card = null;
        CardData = cardData;
        currentDisplayedMana = -1;

        if (cardData == null)
        {
            ClearCardVisuals();
            return;
        }

        ApplyVisuals(cardData.Title, cardData.Description, cardData.Mana, cardData.Type, cardData.Image);
    }

    public void SetupCard(CardData cardData, string titleOverride = null, string descriptionOverride = null)
    {
        Card = null;
        CardData = cardData;
        currentDisplayedMana = -1;

        if (cardData == null)
        {
            ClearCardVisuals();
            return;
        }

        string displayTitle = string.IsNullOrWhiteSpace(titleOverride) ? cardData.Title : titleOverride;
        string displayDescription = string.IsNullOrWhiteSpace(descriptionOverride) ? cardData.Description : descriptionOverride;
        ApplyVisuals(displayTitle, displayDescription, cardData.Mana, cardData.Type, cardData.Image);
    }

    public void SetHoverJuiceEnabled(bool enabled)
    {
        enableHoverJuice = enabled;
    }

    public void SetCombatInteractionsEnabled(bool enabled)
    {
        enableCombatInteractions = enabled;
    }

    public void SetCombatHoverPreviewEnabled(bool enabled)
    {
        enableCombatHoverPreview = enabled;
    }

    public virtual void ClearCardVisuals()
    {
        ApplyText(titleText, "Empty");
        ApplyText(descriptionText, string.Empty);
        ApplyText(manaText, string.Empty);
        ApplyText(typeText, string.Empty);

        SetImage(null);
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (enableCombatHoverPreview && Card != null && CanCombatHover())
        {
            Vector3 pos = new(transform.position.x, cardHoverYOffset, 0);
            CardViewHoverSystem.Instance.Show(Card, pos);
            if (worldWrapper != null) worldWrapper.SetActive(false);
        }

        if (Card != null && Card.HoverSfx != Gameseed26.SfxID.None)
        {
            Gameseed26.Tune.SFX(Card.HoverSfx);
        }
        else if (CardData != null && CardData.HoverSfx != Gameseed26.SfxID.None)
        {
            Gameseed26.Tune.SFX(CardData.HoverSfx);
        }

        PlayHoverJuice(baseHoverScale * hoverScale, hoverEase, bringToFrontOnHover);
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (enableCombatHoverPreview && Card != null && CanCombatHover())
        {
            CardViewHoverSystem.Instance.Hide();
            if (worldWrapper != null) worldWrapper.SetActive(true);
        }

        PlayHoverJuice(baseHoverScale, Ease.OutQuad, false);
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        PlayHoverJuice(baseHoverScale * pressedScale, Ease.OutQuad, false);

        if (!CanStartCombatInteraction()) return;

        pickUpSfx?.Play(this, transform);

        if (Card.ManualTargetEffect != null)
        {
            targetStartSfx?.Play(this, transform);
            ManualTargetingSystem.Instance.StartTargeting(MouseUtils.GetMousePositionInWorldSpace(mousePositionZValue));
        }
        else
        {
            Interactions.Instance.PlayerIsDragging = true;
            if (worldWrapper != null) worldWrapper.SetActive(true);
            CardViewHoverSystem.Instance.Hide();
            dragStartPosition = transform.position;
            dragStartRotation = transform.rotation;
            transform.SetPositionAndRotation(MouseUtils.GetMousePositionInWorldSpace(mousePositionZValue), Quaternion.Euler(0, 0, 0));
            ManualTargetingSystem.Instance.StartAutoTargeting(Card, transform.position);
        }
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        PlayHoverJuice(baseHoverScale * hoverScale, hoverEase, false);

        if (!CanStartCombatInteraction()) return;

        if (Card.ManualTargetEffect != null)
        {
            CombatantView target = ManualTargetingSystem.Instance.EndTargeting(MouseUtils.GetMousePositionInWorldSpace(mousePositionZValue), Card.TargetType);
            if (target != null && ManaSystem.Instance.HasEnoughMana(Card.Mana))
            {
                releaseValidSfx?.Play(this, transform);
                PlayCardGA playCardGA = new(Card, target);
                ActionSystem.Instance.Perform(playCardGA);
            }
            else
            {
                releaseInvalidSfx?.Play(this, transform);
            }
        }
        else
        {
            ManualTargetingSystem.Instance.EndAutoTargeting();
            PlayCardOrResetPosition();

            Interactions.Instance.PlayerIsDragging = false;
        }
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (!CanStartCombatInteraction()) return;
        if (Card.ManualTargetEffect != null) return;

        transform.position = MouseUtils.GetMousePositionInWorldSpace(mousePositionZValue);
        ManualTargetingSystem.Instance.UpdateAutoTargeting(transform.position);
    }

    protected void CacheReferences()
    {
        if (hoverTarget == null) hoverTarget = transform;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (typeText == null && texts.Length > 3) typeText = texts[1];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[2];
        if (manaText == null && texts.Length > 2) manaText = texts[3];

        if (cardImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image.gameObject != gameObject && image.name == "Card Sprite")
                {
                    cardImage = image;
                    break;
                }
            }
        }

        if (cardSpriteRenderer == null)
        {
            cardSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void ApplyVisuals(string cardTitle, string cardDescription, int cardMana, CardType cardType, Sprite cardSprite)
    {
        ApplyText(titleText, cardTitle);
        ApplyText(descriptionText, cardDescription);
        ApplyText(manaText, cardMana.ToString());
        ApplyText(typeText, cardType.ToString());
        SetImage(cardSprite);
    }

    private void SetImage(Sprite sprite)
    {
        if (cardImage != null)
        {
            cardImage.sprite = sprite;
            cardImage.enabled = sprite != null;
        }

        if (cardSpriteRenderer != null)
        {
            cardSpriteRenderer.sprite = sprite;
            cardSpriteRenderer.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }

    private void RefreshCombatTint()
    {
        if (!enableFeebleTint || Card == null || cardSpriteRenderer == null) return;

        isFeebleBlocked = Card.Type == CardType.Attack && HeroSystem.Instance != null && HeroSystem.Instance.HeroView.HealthPenalty > 0;
        cardSpriteRenderer.color = isFeebleBlocked ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
    }

    private void RefreshManaCost()
    {
        if (manaText == null) return;

        string title = Card != null ? Card.Title : (CardData != null ? CardData.Title : null);
        if (title == null) return;

        int baseMana = Card != null ? Card.Mana : CardData.Mana;
        int currentMana = baseMana;

        if (RunManager.Instance != null && RunManager.Instance.CardCostModifiers.TryGetValue(title, out int modifier))
        {
            currentMana = Mathf.Max(0, baseMana - modifier);
        }

        if (currentDisplayedMana != currentMana)
        {
            currentDisplayedMana = currentMana;
            manaText.text = currentMana.ToString();
        }
    }

    private bool CanCombatHover()
    {
        return enableCombatInteractions && Interactions.Instance != null && Interactions.Instance.PlayerCanHover() && CardViewHoverSystem.Instance != null;
    }

    private bool CanStartCombatInteraction()
    {
        if (!enableCombatInteractions || Card == null || isFeebleBlocked) return false;
        return Interactions.Instance != null && Interactions.Instance.PlayerCanInteract();
    }

    private bool CanPlayCard()
    {
        int cost = Card.Mana;
        if (RunManager.Instance != null && RunManager.Instance.CardCostModifiers.TryGetValue(Card.Title, out int modifier))
        {
            cost = Mathf.Max(0, cost - modifier);
        }
        return ManaSystem.Instance.HasEnoughMana(cost) && Physics.Raycast(transform.position, Vector3.forward, out _, mouseUpRaycastDistance, dropAreaLayer);
    }

    private void PlayCardOrResetPosition()
    {
        if (CanPlayCard())
        {
            releaseValidSfx?.Play(this, transform);
            PlayCardGA playCardGA = new(Card);
            ActionSystem.Instance.Perform(playCardGA);
        }
        else
        {
            releaseInvalidSfx?.Play(this, transform);
            transform.SetPositionAndRotation(dragStartPosition, dragStartRotation);
        }
    }

    private void PlayHoverJuice(Vector3 targetScale, Ease ease, bool bringToFront)
    {
        if (!enableHoverJuice || hoverTarget == null) return;

        if (bringToFront)
        {
            transform.SetAsLastSibling();
        }

        hoverTween?.Kill();
        hoverTween = hoverTarget.DOScale(targetScale, tweenDuration).SetEase(ease).SetUpdate(true);
    }

    private void CacheBaseHoverScale()
    {
        if (hoverTarget == null) hoverTarget = transform;
        if (hoverTarget != null && hoverTarget.localScale != Vector3.zero)
        {
            baseHoverScale = hoverTarget.localScale;
        }
    }

    private void ResetHoverScale()
    {
        if (hoverTarget != null)
        {
            hoverTarget.localScale = baseHoverScale;
        }
    }

    private static void ApplyText(TMP_Text targetText, string value)
    {
        if (targetText != null) targetText.text = value ?? string.Empty;
    }
}
