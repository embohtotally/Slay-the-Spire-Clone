using NaughtyAttributes;
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
        [ShowNativeProperty] public bool IsBlocked { get; private set; }
        [ShowNativeProperty] public bool IsSelected { get; private set; }

        public void Setup(Card card)
        {
            if (card == null)
            {
                ClearCardVisuals();
                return;
            }

            Card = card;
            CardData = null;
            ApplyDisplay(new CardDisplayData(card.Title, card.Description, card.Mana,
                card.Type, card.Image, card.Tier));
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
                cardData.Mana, cardData.Type, cardData.Image, cardData.Tier));
        }

        public void Display(CardDisplayData data)
        {
            Card = null;
            CardData = null;
            ApplyDisplay(data);
        }

        public void SetDisplayedCost(int cost)
        {
            if (!HasContent) return;
            DisplayData = DisplayData.WithCost(Mathf.Max(0, cost));
            SetText(manaText, DisplayData.Mana.ToString());
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
            SetText(manaText, data.Mana.ToString());
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
