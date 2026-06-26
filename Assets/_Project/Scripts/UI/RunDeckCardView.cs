using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RunDeckCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject countRoot;
    [SerializeField] private RectTransform hoverTarget;

    [Header("Juice")]
    [SerializeField] private bool enableHoverJuice = true;
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float tweenDuration = 0.12f;
    [SerializeField] private Ease hoverEase = Ease.OutBack;
    [SerializeField] private bool bringToFrontOnHover = true;

    private CardData cardData;
    private Vector3 baseScale = Vector3.one;
    private Tween scaleTween;

    public CardData CardData => cardData;

    private void Awake()
    {
        CacheReferences();
        CacheBaseScale();
    }

    private void OnEnable()
    {
        CacheBaseScale();
    }

    private void OnDisable()
    {
        scaleTween?.Kill();
        if (hoverTarget != null)
        {
            hoverTarget.localScale = baseScale;
        }
    }

    public void Setup(CardData newCardData, int copyCount, int displayIndex)
    {
        cardData = newCardData;
        CacheReferences();
        gameObject.name = cardData != null ? $"RunDeckCard_{displayIndex:00}_{cardData.Title}" : $"RunDeckCard_{displayIndex:00}_Empty";

        if (titleText != null) titleText.text = cardData != null ? cardData.Title : "Empty";
        if (descriptionText != null) descriptionText.text = cardData != null ? cardData.Description : "No card.";
        if (manaText != null) manaText.text = cardData != null ? cardData.Mana.ToString() : string.Empty;
        if (typeText != null) typeText.text = cardData != null ? cardData.Type.ToString() : string.Empty;

        bool hasMultipleCopies = copyCount > 1;
        if (countRoot != null) countRoot.SetActive(hasMultipleCopies);
        if (countText != null) countText.text = hasMultipleCopies ? $"x{copyCount}" : string.Empty;

        if (cardImage != null)
        {
            cardImage.sprite = cardData != null ? cardData.Image : null;
            cardImage.enabled = cardImage.sprite != null;
        }
    }

    public void Clear()
    {
        Setup(null, 0, 0);
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enableHoverJuice || hoverTarget == null) return;

        if (bringToFrontOnHover)
        {
            transform.SetAsLastSibling();
        }

        TweenScale(baseScale * hoverScale, hoverEase);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableHoverJuice || hoverTarget == null) return;
        TweenScale(baseScale, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enableHoverJuice || hoverTarget == null) return;
        TweenScale(baseScale * pressedScale, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!enableHoverJuice || hoverTarget == null) return;
        TweenScale(baseScale * hoverScale, hoverEase);
    }

    private void TweenScale(Vector3 targetScale, Ease ease)
    {
        scaleTween?.Kill();
        scaleTween = hoverTarget.DOScale(targetScale, tweenDuration).SetEase(ease).SetUpdate(true);
    }

    private void CacheReferences()
    {
        if (hoverTarget == null) hoverTarget = transform as RectTransform;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[1];
        if (manaText == null && texts.Length > 2) manaText = texts[2];
        if (typeText == null && texts.Length > 3) typeText = texts[3];
        if (countText == null && texts.Length > 4) countText = texts[4];

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

    private void CacheBaseScale()
    {
        if (hoverTarget == null) hoverTarget = transform as RectTransform;
        if (hoverTarget != null && hoverTarget.localScale != Vector3.zero)
        {
            baseScale = hoverTarget.localScale;
        }
    }
}
