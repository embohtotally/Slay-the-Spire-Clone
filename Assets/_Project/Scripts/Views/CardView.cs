using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Data;
using TMPro;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [RequireComponent(typeof(RectTransform))]
    public class CardView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private TMP_Text typeText;

        [Header("Artwork")]
        [SerializeField] private UnityEngine.UI.Image cardImage;
        [SerializeField] private UnityEngine.UI.Image frameImage;
        [SerializeField] private Sprite basicFrameSprite;
        [SerializeField] private Sprite rareFrameSprite;
        [SerializeField] private Sprite specialFrameSprite;

        [Header("Optional States")]
        [Tooltip("Child overlay; do not assign the card root.")]
        [SerializeField] private GameObject _blockedOverlay;
        [Tooltip("Child overlay; do not assign the card root.")]
        [SerializeField] private GameObject _selectedOverlay;

        public Card Card { get; private set; }
        public CardData CardData { get; private set; }
        public CardDisplayData DisplayData { get; private set; }
        public bool HasContent { get; private set; }
        public bool IsBlocked { get; private set; }
        public bool IsSelected { get; private set; }

        private Color _defaultManaColor = Color.white;
        private bool _capturedManaColor;

        public void Setup(Card card)
        {
            if (card == null)
            {
                ClearCardVisuals();
                return;
            }

            CaptureDefaultManaColor();
            Card = card;
            CardData = null;
            ApplyDisplay(new CardDisplayData(card.Title, card.Description, card.Mana,
                card.Type, card.Image, card.Tier, card.IsXCost));
        }

        public void RefreshFromCard()
        {
            if (Card == null) return;
            SetText(titleText, Card.Title);
            SetText(descriptionText, CardDynamicDescriptionFormatter.FormatForHand(Card));
            if (Card.IsXCost)
            {
                SetText(manaText, "X");
            }
        }

        public void SetLiveDescription(string formattedDescription)
        {
            if (!HasContent) return;
            SetText(descriptionText, formattedDescription);
        }

        public void Setup(CardData cardData)
        {
            SetupCard(cardData);
        }

        public void SetupCard(CardData cardData, string titleOverride = null, string descriptionOverride = null)
        {
            if (cardData == null)
            {
                ClearCardVisuals();
                return;
            }

            Card = null;
            CardData = cardData;
            ApplyDisplay(new CardDisplayData(
                string.IsNullOrWhiteSpace(titleOverride) ? cardData.Title : titleOverride,
                string.IsNullOrWhiteSpace(descriptionOverride) ? cardData.Description : descriptionOverride,
                cardData.Mana, cardData.Type, cardData.Image, cardData.Tier, cardData.IsXCost));
        }

        public void Display(CardDisplayData data)
        {
            Card = null;
            CardData = null;
            ApplyDisplay(data);
        }

        public void SetDisplayedCost(int cost)
        {
            SetDisplayedCost(cost, Card != null ? Card.Mana : DisplayData.Mana, canAfford: true);
        }

        public void SetDisplayedCost(int effectiveCost, int baseCost, bool canAfford)
        {
            if (!HasContent) return;
            CaptureDefaultManaColor();

            if (DisplayData.IsXCost)
            {
                SetText(manaText, DisplayData.CostLabel);
                if (manaText != null)
                {
                    int avail = Systems.ManaSystem.Instance != null ? Systems.ManaSystem.Instance.AvailableMana : 0;
                    manaText.color = avail > 0 ? new Color(1f, 0.88f, 0.36f, 1f) : new Color(1f, 0.44f, 0.44f, 1f);
                }
                return;
            }

            int clamped = Mathf.Max(0, effectiveCost);
            DisplayData = DisplayData.WithCost(clamped);
            SetText(manaText, DisplayData.CostLabel);
            if (manaText != null)
            {
                if (!canAfford) manaText.color = new Color(1f, 0.38f, 0.38f, 1f);
                else if (clamped < baseCost) manaText.color = new Color(0.35f, 1f, 0.54f, 1f);
                else if (clamped > baseCost) manaText.color = new Color(1f, 0.56f, 0.38f, 1f);
                else manaText.color = _defaultManaColor;
            }
        }

        private void CaptureDefaultManaColor()
        {
            if (_capturedManaColor || manaText == null) return;
            _capturedManaColor = true;
            _defaultManaColor = manaText.color;
        }

        public void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
            if (_blockedOverlay != null && _blockedOverlay != gameObject) _blockedOverlay.SetActive(blocked);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_selectedOverlay != null && _selectedOverlay != gameObject) _selectedOverlay.SetActive(selected);
        }

        public virtual void ClearCardVisuals()
        {
            Card = null;
            CardData = null;
            DisplayData = default;
            HasContent = false;
            SetText(titleText, string.Empty);
            SetText(descriptionText, string.Empty);
            SetText(manaText, string.Empty);
            SetText(typeText, string.Empty);
            SetImage(cardImage, null);
            SetImage(frameImage, null);
            SetBlocked(false);
            SetSelected(false);
        }

        private void ApplyDisplay(CardDisplayData data)
        {
            DisplayData = data;
            HasContent = true;
            SetText(titleText, data.Title);
            SetText(descriptionText, data.Description);
            SetText(manaText, data.CostLabel);
            SetText(typeText, data.Type.ToString());
            SetImage(cardImage, data.Image);
            SetImage(frameImage, data.Tier switch
            {
                Tier.Rare => rareFrameSprite,
                Tier.Special => specialFrameSprite,
                _ => basicFrameSprite
            });
            SetBlocked(false);
            SetSelected(false);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? string.Empty;
        }

        private static void SetImage(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
