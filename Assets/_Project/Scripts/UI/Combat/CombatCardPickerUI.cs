using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Mid-Combat Modal Card Picker supporting:
    /// - Scry (inspect top N draw pile cards, toggle any to Discard, confirm)
    /// - Discover (choose 1 of 3 candidate cards to add to hand)
    /// - Hand Choice: Discard, Exhaust, or In-Combat Upgrade
    /// Built with existing project art assets (Panel.png, button1 .png, CardView prefab).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatCardPickerUI : MonoBehaviour
    {
        public static CombatCardPickerUI Instance { get; private set; }
        public static bool IsAnyOpen { get; private set; }

        private Canvas _canvas;
        private GameObject _modalRoot;
        private RectTransform _windowRect;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private RectTransform _gridContent;
        private Button _confirmButton;
        private TextMeshProUGUI _confirmLabel;
        private Button _skipButton;
        private TextMeshProUGUI _skipLabel;

        private readonly List<GameObject> _spawnedCells = new();
        private readonly HashSet<int> _selectedIndices = new();
        private readonly List<Image> _cellHighlights = new();
        private readonly List<GameObject> _cellTags = new();

        private bool _isMultiToggleMode;
        private int _requiredCount;
        private int _maxCount;
        private Action<List<int>> _onCompleteIndices;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                IsAnyOpen = false;
                Instance = null;
            }
        }

        public static CombatCardPickerUI EnsureInstance()
        {
            if (Instance != null) return Instance;
            CombatCardPickerUI existing = FindFirstObjectByType<CombatCardPickerUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            GameObject go = new("CombatCardPickerUI");
            return go.AddComponent<CombatCardPickerUI>();
        }

        public void OpenScry(IReadOnlyList<Card> topCards, Action<List<Card>> onResolved)
        {
            if (topCards == null || topCards.Count == 0)
            {
                onResolved?.Invoke(new List<Card>());
                return;
            }

            List<Card> snapshot = new(topCards);
            OpenModal(
                title: $"SCRY {snapshot.Count}",
                subtitle: "Click any cards to mark them for DISCARD. Unselected cards stay on top of your Draw Pile.",
                cards: snapshot,
                cardDatas: null,
                tagText: "DISCARD",
                tagColor: new Color(0.92f, 0.26f, 0.28f, 0.96f),
                multiToggle: true,
                minSelect: 0,
                maxSelect: snapshot.Count,
                confirmText: "CONFIRM SCRY",
                showSkip: false,
                onIndices: indices =>
                {
                    List<Card> discarded = new();
                    foreach (int idx in indices)
                    {
                        if (idx >= 0 && idx < snapshot.Count) discarded.Add(snapshot[idx]);
                    }
                    onResolved?.Invoke(discarded);
                });
        }

        public void OpenDiscover(IReadOnlyList<CardData> choices, bool zeroCostThisTurn, Action<CardData> onPicked)
        {
            if (choices == null || choices.Count == 0)
            {
                onPicked?.Invoke(null);
                return;
            }

            List<CardData> snapshot = new(choices);
            string costNote = zeroCostThisTurn ? " It costs 0 Mana this turn!" : string.Empty;
            OpenModal(
                title: "DISCOVER A CARD",
                subtitle: $"Choose 1 of {snapshot.Count} cards to add to your hand.{costNote}",
                cards: null,
                cardDatas: snapshot,
                tagText: "PICK",
                tagColor: new Color(0.32f, 0.88f, 0.56f, 0.96f),
                multiToggle: false,
                minSelect: 1,
                maxSelect: 1,
                confirmText: "ADD TO HAND",
                showSkip: true,
                onIndices: indices =>
                {
                    CardData chosen = indices.Count > 0 && indices[0] >= 0 && indices[0] < snapshot.Count
                        ? snapshot[indices[0]]
                        : null;
                    onPicked?.Invoke(chosen);
                });
        }

        public void OpenHandChoice(HandChoiceMode mode, IReadOnlyList<Card> eligibleCards, int count, Action<List<Card>> onResolved)
        {
            if (eligibleCards == null || eligibleCards.Count == 0)
            {
                onResolved?.Invoke(new List<Card>());
                return;
            }

            List<Card> snapshot = new(eligibleCards);
            int target = Mathf.Clamp(count, 1, snapshot.Count);

            (string title, string sub, string tag, Color tagCol, string confirm) = mode switch
            {
                HandChoiceMode.Exhaust => (
                    $"EXHAUST {target} CARD{(target > 1 ? "S" : string.Empty)}",
                    $"Select {target} card{(target > 1 ? "s" : string.Empty)} from your hand to Exhaust.",
                    "EXHAUST", new Color(0.72f, 0.42f, 0.96f, 0.96f), "EXHAUST SELECTED"),
                HandChoiceMode.UpgradeInCombat => (
                    $"UPGRADE {target} CARD{(target > 1 ? "S" : string.Empty)}",
                    $"Choose {target} card{(target > 1 ? "s" : string.Empty)} in your hand to Upgrade for the rest of combat.",
                    "UPGRADE+", new Color(0.30f, 0.90f, 0.50f, 0.96f), "FORGE UPGRADE"),
                _ => (
                    $"DISCARD {target} CARD{(target > 1 ? "S" : string.Empty)}",
                    $"Select {target} card{(target > 1 ? "s" : string.Empty)} from your hand to Discard.",
                    "DISCARD", new Color(0.95f, 0.56f, 0.24f, 0.96f), "DISCARD SELECTED")
            };

            OpenModal(
                title: title,
                subtitle: sub,
                cards: snapshot,
                cardDatas: null,
                tagText: tag,
                tagColor: tagCol,
                multiToggle: target > 1,
                minSelect: target,
                maxSelect: target,
                confirmText: confirm,
                showSkip: false,
                onIndices: indices =>
                {
                    List<Card> picked = new();
                    foreach (int idx in indices)
                    {
                        if (idx >= 0 && idx < snapshot.Count) picked.Add(snapshot[idx]);
                    }
                    onResolved?.Invoke(picked);
                });
        }

        private void OpenModal(
            string title,
            string subtitle,
            List<Card> cards,
            List<CardData> cardDatas,
            string tagText,
            Color tagColor,
            bool multiToggle,
            int minSelect,
            int maxSelect,
            string confirmText,
            bool showSkip,
            Action<List<int>> onIndices)
        {
            BuildModalIfNeeded();
            if (_modalRoot == null)
            {
                onIndices?.Invoke(new List<int>());
                return;
            }

            ClearCells();
            _selectedIndices.Clear();
            _isMultiToggleMode = multiToggle;
            _requiredCount = minSelect;
            _maxCount = maxSelect;
            _onCompleteIndices = onIndices;

            _titleText.text = title;
            _subtitleText.text = subtitle;
            _confirmLabel.text = confirmText;
            _skipButton.gameObject.SetActive(showSkip);

            int total = cards != null ? cards.Count : (cardDatas != null ? cardDatas.Count : 0);
            int layer = _gridContent.gameObject.layer;
            TMP_FontAsset font = CombatStatusTooltipUI.Instance != null
                ? CombatStatusTooltipUI.Instance.Font
                : TMP_Settings.defaultFontAsset;
            Sprite solid = CombatStatusTooltipUI.GetSolidSprite();

            for (int i = 0; i < total; i++)
            {
                RectTransform cell = CreateRect(_gridContent, $"ChoiceCell_{i:00}", layer);
                cell.sizeDelta = new Vector2(226f, 304f);

                Image highlight = cell.gameObject.AddComponent<Image>();
                highlight.sprite = solid;
                highlight.color = new Color(tagColor.r, tagColor.g, tagColor.b, 0.0f);
                _cellHighlights.Add(highlight);

                RectTransform holder = CreateRect(cell, "CardHolder", layer);
                holder.anchorMin = Vector2.zero;
                holder.anchorMax = Vector2.one;
                holder.offsetMin = new Vector2(8f, 8f);
                holder.offsetMax = new Vector2(-8f, -8f);

                Card cardObj = cards != null ? cards[i] : new Card(cardDatas[i]);
                SpawnCardVisualInto(holder, cardObj, font, solid);

                // Selection Badge Tag
                GameObject tagGo = new("SelectionTag", typeof(RectTransform), typeof(Image));
                tagGo.layer = layer;
                RectTransform tagRt = (RectTransform)tagGo.transform;
                tagRt.SetParent(cell, false);
                tagRt.anchorMin = new Vector2(0.12f, 0.80f);
                tagRt.anchorMax = new Vector2(0.88f, 0.94f);
                tagRt.offsetMin = Vector2.zero;
                tagRt.offsetMax = Vector2.zero;
                Image tagImg = tagGo.GetComponent<Image>();
                tagImg.sprite = solid;
                tagImg.color = tagColor;
                tagImg.raycastTarget = false;

                TextMeshProUGUI tagLbl = CreateText(tagRt, "Label", font, 17f, FontStyles.Bold, Color.white);
                tagLbl.alignment = TextAlignmentOptions.Center;
                tagLbl.text = tagText;
                tagGo.SetActive(false);
                _cellTags.Add(tagGo);

                int captured = i;
                Button btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = highlight;
                btn.onClick.AddListener(() => OnCellClicked(captured, tagColor));

                _spawnedCells.Add(cell.gameObject);
            }

            RefreshConfirmButton();

            IsAnyOpen = true;
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            _windowRect.DOKill();
            _windowRect.localScale = Vector3.one * 0.88f;
            _windowRect.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack).SetUpdate(true);
            Tune.SFX(SfxID.ui_panel_open);
        }

        private void SpawnCardVisualInto(RectTransform holder, Card card, TMP_FontAsset font, Sprite solid)
        {
            if (CardViewCreator.Instance != null)
            {
                CardView cv = CardViewCreator.Instance.CreateCardView(card, Vector3.zero, Quaternion.identity);
                if (cv != null)
                {
                    cv.transform.SetParent(holder, false);
                    if (cv.TryGetComponent(out CardPointerInput pi)) pi.SetInteractable(false);
                    foreach (Graphic g in cv.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                    if (cv.transform is RectTransform rt)
                    {
                        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = Vector2.zero;
                        rt.localRotation = Quaternion.identity;
                        rt.localScale = Vector3.one * 0.72f;
                    }
                    return;
                }
            }

            // Fallback crisp card panel if CardViewCreator is absent
            Image bg = holder.gameObject.AddComponent<Image>();
            bg.sprite = solid;
            bg.color = new Color(0.14f, 0.12f, 0.18f, 0.96f);
            bg.raycastTarget = false;

            TextMeshProUGUI title = CreateText(holder, "Title", font, 17f, FontStyles.Bold, new Color(1f, 0.90f, 0.54f, 1f));
            title.rectTransform.anchorMin = new Vector2(0.06f, 0.74f);
            title.rectTransform.anchorMax = new Vector2(0.94f, 0.95f);
            title.alignment = TextAlignmentOptions.Center;
            title.text = $"{card.Title} ({card.CostLabel})";

            TextMeshProUGUI desc = CreateText(holder, "Desc", font, 14f, FontStyles.Normal, Color.white);
            desc.rectTransform.anchorMin = new Vector2(0.08f, 0.08f);
            desc.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
            desc.alignment = TextAlignmentOptions.Center;
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.text = card.Description;
        }

        private void OnCellClicked(int index, Color tagColor)
        {
            Tune.SFX(SfxID.hover);

            if (!_isMultiToggleMode && _maxCount == 1)
            {
                // Single-pick immediate toggle (click again or press Confirm; double-click/single-select feels snappy)
                if (_selectedIndices.Contains(index))
                {
                    ConfirmSelection();
                    return;
                }

                _selectedIndices.Clear();
                _selectedIndices.Add(index);
            }
            else
            {
                if (_selectedIndices.Contains(index))
                {
                    _selectedIndices.Remove(index);
                }
                else if (_selectedIndices.Count < _maxCount)
                {
                    _selectedIndices.Add(index);
                }
            }

            for (int i = 0; i < _cellHighlights.Count; i++)
            {
                bool sel = _selectedIndices.Contains(i);
                if (_cellHighlights[i] != null)
                {
                    _cellHighlights[i].color = sel
                        ? new Color(tagColor.r, tagColor.g, tagColor.b, 0.85f)
                        : new Color(tagColor.r, tagColor.g, tagColor.b, 0f);
                }
                if (i < _cellTags.Count && _cellTags[i] != null)
                {
                    _cellTags[i].SetActive(sel);
                }
            }

            RefreshConfirmButton();
        }

        private void RefreshConfirmButton()
        {
            if (_confirmButton == null) return;
            _confirmButton.interactable = _selectedIndices.Count >= _requiredCount;
        }

        private void ConfirmSelection()
        {
            if (_selectedIndices.Count < _requiredCount) return;
            List<int> result = new(_selectedIndices);
            result.Sort();
            CloseAndInvoke(result);
        }

        private void SkipSelection()
        {
            CloseAndInvoke(new List<int>());
        }

        private void CloseAndInvoke(List<int> result)
        {
            IsAnyOpen = false;
            if (_modalRoot != null) _modalRoot.SetActive(false);
            Tune.SFX(SfxID.ui_panel_close);
            Action<List<int>> cb = _onCompleteIndices;
            _onCompleteIndices = null;
            cb?.Invoke(result);
        }

        private void ClearCells()
        {
            for (int i = 0; i < _spawnedCells.Count; i++)
            {
                if (_spawnedCells[i] != null) Destroy(_spawnedCells[i]);
            }
            _spawnedCells.Clear();
            _cellHighlights.Clear();
            _cellTags.Clear();
        }

        private void BuildModalIfNeeded()
        {
            if (_modalRoot != null) return;
            CombatTopBarUI topBar = FindFirstObjectByType<CombatTopBarUI>();
            _canvas = topBar != null
                ? topBar.GetComponentInParent<Canvas>()?.rootCanvas
                : FindFirstObjectByType<Canvas>()?.rootCanvas;
            if (_canvas == null) return;

            int layer = _canvas.gameObject.layer;
            TMP_FontAsset font = CombatStatusTooltipUI.Instance != null
                ? CombatStatusTooltipUI.Instance.Font
                : TMP_Settings.defaultFontAsset;
            Sprite solid = CombatStatusTooltipUI.GetSolidSprite();
            Sprite panelSpr = FindProjectSprite("Panel");
            Sprite btnSpr = FindProjectSprite("button1 ") ?? FindProjectSprite("Tombol");

            _modalRoot = new GameObject("CombatCardPickerModal", typeof(RectTransform), typeof(Image));
            _modalRoot.layer = layer;
            RectTransform mRt = (RectTransform)_modalRoot.transform;
            mRt.SetParent(_canvas.transform, false);
            mRt.anchorMin = Vector2.zero;
            mRt.anchorMax = Vector2.one;
            mRt.offsetMin = Vector2.zero;
            mRt.offsetMax = Vector2.zero;
            Image dim = _modalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.03f, 0.03f, 0.06f, 0.82f);

            _windowRect = CreateRect(mRt, "Window", layer);
            _windowRect.anchorMin = _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowRect.sizeDelta = new Vector2(1180f, 620f);
            Image winBg = _windowRect.gameObject.AddComponent<Image>();
            winBg.sprite = panelSpr != null ? panelSpr : solid;
            winBg.color = panelSpr != null ? Color.white : new Color(0.11f, 0.09f, 0.14f, 0.98f);

            _titleText = CreateText(_windowRect, "Title", font, 30f, FontStyles.Bold, new Color(1f, 0.90f, 0.52f, 1f));
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.offsetMin = new Vector2(32f, -68f);
            _titleText.rectTransform.offsetMax = new Vector2(-32f, -20f);
            _titleText.alignment = TextAlignmentOptions.Center;

            _subtitleText = CreateText(_windowRect, "Subtitle", font, 18f, FontStyles.Normal, new Color(0.92f, 0.90f, 0.84f, 0.95f));
            _subtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitleText.rectTransform.offsetMin = new Vector2(40f, -106f);
            _subtitleText.rectTransform.offsetMax = new Vector2(-40f, -66f);
            _subtitleText.alignment = TextAlignmentOptions.Center;

            // Scrollable horizontal/grid container
            RectTransform vp = CreateRect(_windowRect, "Viewport", layer);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(40f, 96f);
            vp.offsetMax = new Vector2(-40f, -116f);
            vp.gameObject.AddComponent<RectMask2D>();
            ScrollRect sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.vertical = false;
            sr.horizontal = true;

            _gridContent = CreateRect(vp, "Content", layer);
            _gridContent.anchorMin = new Vector2(0.5f, 0.5f);
            _gridContent.anchorMax = new Vector2(0.5f, 0.5f);
            _gridContent.pivot = new Vector2(0.5f, 0.5f);
            _gridContent.sizeDelta = new Vector2(1060f, 360f);
            HorizontalLayoutGroup hlg = _gridContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            ContentSizeFitter csf = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = _gridContent;

            // Confirm Button
            RectTransform confirmRt = CreateRect(_windowRect, "ConfirmButton", layer);
            confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(0f, 26f);
            confirmRt.sizeDelta = new Vector2(250f, 54f);
            Image cfImg = confirmRt.gameObject.AddComponent<Image>();
            cfImg.sprite = btnSpr != null ? btnSpr : solid;
            cfImg.color = new Color(0.52f, 0.92f, 0.56f, 1f);
            _confirmButton = confirmRt.gameObject.AddComponent<Button>();
            _confirmButton.targetGraphic = cfImg;
            _confirmButton.onClick.AddListener(ConfirmSelection);
            _confirmLabel = CreateText(confirmRt, "Label", font, 21f, FontStyles.Bold, new Color(0.10f, 0.22f, 0.08f, 1f));
            _confirmLabel.alignment = TextAlignmentOptions.Center;

            // Skip Button
            RectTransform skipRt = CreateRect(_windowRect, "SkipButton", layer);
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(1f, 0f);
            skipRt.pivot = new Vector2(1f, 0f);
            skipRt.anchoredPosition = new Vector2(-42f, 26f);
            skipRt.sizeDelta = new Vector2(150f, 54f);
            Image skImg = skipRt.gameObject.AddComponent<Image>();
            skImg.sprite = btnSpr != null ? btnSpr : solid;
            skImg.color = new Color(0.92f, 0.82f, 0.64f, 1f);
            _skipButton = skipRt.gameObject.AddComponent<Button>();
            _skipButton.targetGraphic = skImg;
            _skipButton.onClick.AddListener(SkipSelection);
            _skipLabel = CreateText(skipRt, "Label", font, 19f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            _skipLabel.alignment = TextAlignmentOptions.Center;
            _skipLabel.text = "SKIP";

            _modalRoot.SetActive(false);
        }

        private static RectTransform CreateRect(Transform parent, string name, int layer)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Sprite FindProjectSprite(string exactName)
        {
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && string.Equals(sprites[i].name, exactName, StringComparison.OrdinalIgnoreCase))
                    return sprites[i];
            }
            return null;
        }
    }
}

