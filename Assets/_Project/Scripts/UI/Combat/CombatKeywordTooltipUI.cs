using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Displays Slay-the-Spire style keyword & status tooltip boxes docked beside the
    /// hovered card preview whenever a hovered card references combat keywords or statuses.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatKeywordTooltipUI : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CombatHandController _handController;
        [SerializeField] private RectTransform _hoverPreviewCard;
        [SerializeField] private TMP_FontAsset _font;

        private readonly struct KeywordEntry
        {
            public readonly string MatchToken;
            public readonly string Title;
            public readonly string Body;
            public readonly Color Accent;

            public KeywordEntry(string matchToken, string title, string body, Color accent)
            {
                MatchToken = matchToken;
                Title = title;
                Body = body;
                Accent = accent;
            }
        }

        private static readonly KeywordEntry[] Catalog =
        {
            new("Shield", "Shield", "Absorbs incoming damage until the start of your next turn.", new Color(0.31f, 0.76f, 0.97f)),
            new("Will", "Will", "Every played card grants +1 to +3 Will (RNG). At 75%+ triggers + High Will (+25% Attack & Shield). Below 25% triggers - Low Will (Attack cards blocked & Max HP penalty).", new Color(0.28f, 0.75f, 1f)),
            new("High Will", "+ High Will", "Active at 75%+ Will: grants +25% Attack Damage and Shield gain.", new Color(0.24f, 0.84f, 1f)),
            new("Low Will", "- Low Will", "Active below 25% Will: Attack cards are blocked and Effective Max HP is reduced until Will recovers.", new Color(1f, 0.34f, 0.38f)),
            new("Will Shield", "Will Shield", "Immune to incoming Will drain.", new Color(0.36f, 0.88f, 1f)),
            new("Vulnerable", "Vulnerable", "Target takes 50% more damage from attacks.", new Color(1f, 0.42f, 0.42f)),
            new("Weak", "Weak", "Afflicted combatant deals 25% less attack damage.", new Color(1f, 0.72f, 0.30f)),
            new("Strength", "Strength", "Increases attack damage dealt by each hit.", new Color(1f, 0.52f, 0.36f)),
            new("Poison", "Poison", "Deals stack damage at the start of the target's turn, then reduces by 1.", new Color(0.50f, 0.88f, 0.45f)),
            new("Stun", "Stun", "Target skips their next turn.", new Color(0.56f, 0.79f, 0.98f)),
            new("Taunt", "Taunt", "Single-target attacks must target a Taunted enemy first.", new Color(1f, 0.84f, 0.31f)),
            new("Exhaust", "Exhaust", "Removed from play into the Exhaust Pile until the end of combat.", new Color(0.75f, 0.70f, 0.85f)),
            new("Retain", "Retain", "Not discarded at the end of your turn.", new Color(0.45f, 0.90f, 0.82f)),
            new("Ethereal", "Ethereal", "If still in hand at the end of your turn, this card is Exhausted.", new Color(0.69f, 0.62f, 1f)),
            new("Innate", "Innate", "Always starts in your opening hand on Turn 1.", new Color(1f, 0.88f, 0.52f)),
            new("Unplayable", "Unplayable", "Cannot be played from your hand.", new Color(0.62f, 0.62f, 0.68f)),
            new("X-Cost", "X-Cost", "Consumes all remaining Mana when played. Effects multiply by the Mana spent (X).", new Color(0.98f, 0.78f, 0.34f)),
            new("Power", "Power", "Dissolves from your deck for the rest of combat and grants a permanent combat passive.", new Color(0.95f, 0.78f, 0.30f)),
            new("Scry", "Scry", "Inspect the top cards of your Draw Pile and discard any of them.", new Color(0.45f, 0.85f, 1f)),
            new("Discover", "Discover", "Choose 1 of 3 cards to add to your hand for this battle.", new Color(0.62f, 0.88f, 0.96f)),
            new("Status", "Status", "Clutter card added during battle that vanishes when combat ends.", new Color(0.84f, 0.56f, 0.42f)),
            new("Curse", "Curse", "Harmful card that clutters your hand or triggers negative effects.", new Color(0.74f, 0.38f, 0.84f)),
            new("Dexterity", "Dexterity", "Increases Shield gained from cards.", new Color(0.40f, 0.86f, 0.75f)),
            new("Frail", "Frail", "Reduces Shield gained from cards by 25%.", new Color(0.95f, 0.62f, 0.42f)),
            new("Thorns", "Thorns", "When hit by an attack, deals return damage to the attacker.", new Color(0.96f, 0.48f, 0.48f)),
            new("Artifact", "Artifact", "Negates the next incoming debuff.", new Color(1f, 0.84f, 0.35f)),
            new("Barricade", "Barricade", "Shield is no longer cleared at the start of your turn.", new Color(0.42f, 0.74f, 1f)),
            new("Redirect", "Redirect", "Redirects incoming enemy attacks to the protected ally.", new Color(0.55f, 0.85f, 1f)),
            new("Power", "Power", "Remains active for the rest of combat after being played (does not enter Discard or Exhaust).", new Color(1f, 0.84f, 0.36f)),
            new("Scry", "Scry", "Look at the top cards of your Draw Pile and choose any to Discard.", new Color(0.54f, 0.86f, 1f)),
            new("Discover", "Discover", "Choose 1 of 3 cards to add directly to your hand for this combat.", new Color(0.96f, 0.78f, 0.42f)),
            new("Upgrade", "Combat Upgrade", "Enhances the card for the rest of this battle (+3 potency and -1 Mana cost or +1 X).", new Color(0.42f, 0.96f, 0.56f)),
            new("Status", "Status Card", "Temporary clutter card added during combat. Removed automatically when combat ends.", new Color(0.84f, 0.54f, 0.44f)),
            new("Curse", "Curse Card", "Unplayable negative card that clutters your deck until removed at a Merchant or Event.", new Color(0.76f, 0.38f, 0.88f)),
            new("AP", "Mana / AP", "Action Points spent to play cards each turn.", new Color(1f, 0.80f, 0.35f))
        };

        private static Sprite _solidSprite;
        private RectTransform _container;
        private readonly List<GameObject> _tipCards = new();

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_handController == null) _handController = FindFirstObjectByType<CombatHandController>();
        }

        private void OnEnable()
        {
            if (_handController != null)
            {
                _handController.HoverCardChanged += OnHoverCardChanged;
                _handController.HoverCardEnded += OnHoverCardEnded;
            }
            Hide();
        }

        private void OnDisable()
        {
            if (_handController != null)
            {
                _handController.HoverCardChanged -= OnHoverCardChanged;
                _handController.HoverCardEnded -= OnHoverCardEnded;
            }
            Hide();
        }

        private void OnHoverCardChanged(CardView cardView, Vector2 screenPosition)
        {
            if (cardView == null || !cardView.HasContent)
            {
                Hide();
                return;
            }

            List<KeywordEntry> matches = FindMatches(cardView.Card, cardView.DisplayData.Description);
            if (matches.Count == 0)
            {
                Hide();
                return;
            }

            EnsureContainer();
            if (_container == null) return;

            ClearTipCards();
            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;

            int limit = Mathf.Min(matches.Count, 4);
            for (int i = 0; i < limit; i++)
            {
                _tipCards.Add(CreateTipCard(_container, matches[i], solid, font));
            }

            _container.gameObject.SetActive(true);
            _container.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            PositionNearCard(cardView, screenPosition);
        }

        private void OnHoverCardEnded()
        {
            Hide();
        }

        public void Hide()
        {
            if (_container != null && _container.gameObject.activeSelf)
                _container.gameObject.SetActive(false);
        }

        private static List<KeywordEntry> FindMatches(Card card, string description)
        {
            List<KeywordEntry> results = new();
            HashSet<string> added = new(StringComparer.OrdinalIgnoreCase);

            void TryAdd(string token)
            {
                foreach (KeywordEntry entry in Catalog)
                {
                    if (string.Equals(entry.MatchToken, token, StringComparison.OrdinalIgnoreCase) && added.Add(entry.Title))
                    {
                        results.Add(entry);
                        return;
                    }
                }
            }

            if (card != null)
            {
                if (card.IsXCost) TryAdd("X-Cost");
                if (card.Keywords != CardKeywords.None)
                {
                    if (card.HasKeyword(CardKeywords.Exhaust)) TryAdd("Exhaust");
                    if (card.HasKeyword(CardKeywords.Retain)) TryAdd("Retain");
                    if (card.HasKeyword(CardKeywords.Ethereal)) TryAdd("Ethereal");
                    if (card.HasKeyword(CardKeywords.Innate)) TryAdd("Innate");
                    if (card.HasKeyword(CardKeywords.Unplayable)) TryAdd("Unplayable");
                }
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                foreach (KeywordEntry entry in Catalog)
                {
                    if (Regex.IsMatch(description, $@"\b{Regex.Escape(entry.MatchToken)}\b", RegexOptions.IgnoreCase) && added.Add(entry.Title))
                    {
                        results.Add(entry);
                    }
                }
            }

            return results;
        }

        private void PositionNearCard(CardView cardView, Vector2 screenPosition)
        {
            if (_canvas == null || _canvas.transform is not RectTransform canvasRect) return;

            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 anchorLocal;

            if (_hoverPreviewCard != null && _hoverPreviewCard.gameObject.activeInHierarchy)
            {
                Vector3 previewWorld = _hoverPreviewCard.position;
                Vector2 previewScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, previewWorld);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, previewScreen, uiCamera, out anchorLocal);
            }
            else if (cardView != null && cardView.transform is RectTransform cardRect)
            {
                Vector2 cardScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, cardRect.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cardScreen, uiCamera, out anchorLocal);
                // Match the raised hovered card visual position (+122px lift)
                anchorLocal += new Vector2(0f, 150f);
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out anchorLocal);
                anchorLocal += new Vector2(0f, 180f);
            }

            bool placeOnLeft = anchorLocal.x > 260f;
            float xOffset = placeOnLeft ? -340f : 340f;
            Vector2 targetPos = new(anchorLocal.x + xOffset, anchorLocal.y + 120f);

            Rect bounds = canvasRect.rect;
            float halfW = _container.rect.width * 0.5f;
            float height = _container.rect.height;
            targetPos.x = Mathf.Clamp(targetPos.x, bounds.xMin + halfW + 16f, bounds.xMax - halfW - 16f);
            targetPos.y = Mathf.Clamp(targetPos.y, bounds.yMin + height + 16f, bounds.yMax - 16f);

            _container.anchoredPosition = targetPos;
        }

        private void EnsureContainer()
        {
            if (_container != null) return;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null) return;

            GameObject go = new("CombatKeywordTooltips", typeof(RectTransform), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.layer = _canvas.gameObject.layer;
            _container = (RectTransform)go.transform;
            _container.SetParent(_canvas.transform, false);
            _container.anchorMin = _container.anchorMax = new Vector2(0.5f, 0.5f);
            _container.pivot = new Vector2(0.5f, 1f);
            _container.sizeDelta = new Vector2(310f, 100f);

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            VerticalLayoutGroup vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            go.SetActive(false);
        }

        private void ClearTipCards()
        {
            foreach (GameObject tip in _tipCards)
            {
                if (tip != null)
                {
                    tip.SetActive(false);
                    Destroy(tip);
                }
            }
            _tipCards.Clear();
        }

        private static GameObject CreateTipCard(RectTransform parent, KeywordEntry entry, Sprite solid, TMP_FontAsset font)
        {
            GameObject cardGo = new($"Tip_{entry.Title}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            cardGo.layer = parent.gameObject.layer;
            RectTransform cardRect = (RectTransform)cardGo.transform;
            cardRect.SetParent(parent, false);

            Image bg = cardGo.GetComponent<Image>();
            bg.sprite = solid;
            bg.color = new Color(0.06f, 0.07f, 0.11f, 0.93f);
            bg.raycastTarget = false;

            VerticalLayoutGroup vlg = cardGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = cardGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject titleGo = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.layer = cardGo.layer;
            titleGo.transform.SetParent(cardRect, false);
            TextMeshProUGUI titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
            titleTxt.text = entry.Title;
            titleTxt.fontSize = 19f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = entry.Accent;
            titleTxt.raycastTarget = false;
            if (font != null) titleTxt.font = font;

            GameObject bodyGo = new("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyGo.layer = cardGo.layer;
            bodyGo.transform.SetParent(cardRect, false);
            TextMeshProUGUI bodyTxt = bodyGo.GetComponent<TextMeshProUGUI>();
            bodyTxt.text = entry.Body;
            bodyTxt.fontSize = 15.5f;
            bodyTxt.color = new Color(0.90f, 0.92f, 0.96f, 1f);
            bodyTxt.textWrappingMode = TextWrappingModes.Normal;
            bodyTxt.raycastTarget = false;
            if (font != null) bodyTxt.font = font;

            return cardGo;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }
    }
}
