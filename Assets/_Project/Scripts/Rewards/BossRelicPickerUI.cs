using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlushieOdyssey.Rewards
{
    /// <summary>
    /// Slay-the-Spire style Boss 3-Relic Choice Chest modal.
    /// Presents 3 high-impact Boss Relics with trade-offs after Act Bosses, styled with existing project art
    /// (Panel.png, button1 .png, Merchant_Circle.png, Merchant_SoftGlow.png).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossRelicPickerUI : MonoBehaviour
    {
        [SerializeField] private List<RelicData> fallbackBossRelics = new();

        private Canvas _canvas;
        private GameObject _modalRoot;
        private RectTransform _windowRect;
        private RectTransform _cardsRow;
        private readonly List<GameObject> _spawnedCards = new();
        private Action<RelicData> _onComplete;

        public bool IsOpen => _modalRoot != null && _modalRoot.activeSelf;

        public static BossRelicPickerUI EnsureInstance(Canvas preferredCanvas = null)
        {
            BossRelicPickerUI existing = FindFirstObjectByType<BossRelicPickerUI>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Canvas c = preferredCanvas != null ? preferredCanvas : FindFirstObjectByType<Canvas>()?.rootCanvas;
            GameObject go = new("BossRelicPickerUI");
            if (c != null) go.transform.SetParent(c.transform, false);
            return go.AddComponent<BossRelicPickerUI>();
        }

        public void Open(RelicRewardPool pool, Action<RelicData> onCompleted, IReadOnlyList<RelicData> extraBossRelics = null)
        {
            List<RelicData> choices = GatherThreeBossRelics(pool, extraBossRelics);
            if (choices.Count == 0)
            {
                onCompleted?.Invoke(null);
                return;
            }

            _onComplete = onCompleted;
            BuildModalIfNeeded();
            if (_modalRoot == null)
            {
                onCompleted?.Invoke(null);
                return;
            }

            PopulateChoices(choices);
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            _windowRect.DOKill();
            _windowRect.localScale = Vector3.one * 0.86f;
            _windowRect.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
            Tune.SFX(SfxID.ui_panel_open);
        }

        private List<RelicData> GatherThreeBossRelics(RelicRewardPool pool, IReadOnlyList<RelicData> extraBossRelics)
        {
            RunRelicManager mgr = RunRelicManager.EnsureInstance();
            List<RelicData> candidates = new();
            HashSet<RelicData> seen = new();

            void TryAdd(RelicData r)
            {
                if (r == null || !seen.Add(r)) return;
                if (r.Unique && mgr.Contains(r)) return;
                candidates.Add(r);
            }

            if (extraBossRelics != null)
            {
                for (int i = 0; i < extraBossRelics.Count; i++)
                    if (extraBossRelics[i] != null && extraBossRelics[i].Rarity == RelicRarity.Boss) TryAdd(extraBossRelics[i]);
            }

            if (fallbackBossRelics != null)
            {
                foreach (RelicData r in fallbackBossRelics)
                    if (r != null && r.Rarity == RelicRarity.Boss) TryAdd(r);
            }

            if (pool != null)
            {
                foreach (RelicData r in pool.BuildCandidateRewards(mgr, RelicRarityMask.Boss))
                    TryAdd(r);
            }

            RelicData[] resLoaded = Resources.LoadAll<RelicData>(string.Empty);
            for (int i = 0; i < resLoaded.Length; i++)
            {
                if (resLoaded[i] != null && resLoaded[i].Rarity == RelicRarity.Boss)
                    TryAdd(resLoaded[i]);
            }

            RelicData[] allLoaded = Resources.FindObjectsOfTypeAll<RelicData>();
            for (int i = 0; i < allLoaded.Length; i++)
            {
                if (allLoaded[i] != null && allLoaded[i].Rarity == RelicRarity.Boss)
                    TryAdd(allLoaded[i]);
            }

#if UNITY_EDITOR
            if (candidates.Count < 3)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RelicData");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    RelicData r = UnityEditor.AssetDatabase.LoadAssetAtPath<RelicData>(path);
                    if (r != null && r.Rarity == RelicRarity.Boss) TryAdd(r);
                }
            }
#endif

            // Fallback to Rare/All relics if fewer than 3 Boss relics are available
            if (candidates.Count < 3 && pool != null)
            {
                foreach (RelicData r in pool.BuildCandidateRewards(mgr, RelicRarityMask.All))
                    TryAdd(r);
            }

            // Shuffle and take up to 3
            for (int i = 0; i < candidates.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            if (candidates.Count > 3)
                candidates.RemoveRange(3, candidates.Count - 3);

            return candidates;
        }

        private void PopulateChoices(List<RelicData> choices)
        {
            for (int i = 0; i < _spawnedCards.Count; i++)
                if (_spawnedCards[i] != null) Destroy(_spawnedCards[i]);
            _spawnedCards.Clear();

            int layer = _cardsRow.gameObject.layer;
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolidSprite();
            Sprite panelSpr = FindProjectSprite("Panel") ?? FindProjectSprite("button1 ") ?? solid;
            Sprite circleSpr = FindProjectSprite("Merchant_Circle") ?? solid;

            for (int i = 0; i < choices.Count; i++)
            {
                RelicData relic = choices[i];
                RectTransform card = CreateRect(_cardsRow, $"BossRelicCard_{i + 1}", layer);
                card.sizeDelta = new Vector2(340f, 410f);

                Image bg = card.gameObject.AddComponent<Image>();
                bg.sprite = panelSpr;
                bg.color = panelSpr != solid ? new Color(0.96f, 0.90f, 0.80f, 1f) : new Color(0.16f, 0.11f, 0.10f, 0.98f);

                Outline outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.68f, 0.30f, 0.92f);
                outline.effectDistance = new Vector2(2f, -2f);

                // Circular Boss Relic Emblem
                RectTransform ring = CreateRect(card, "Ring", layer);
                ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.77f);
                ring.sizeDelta = new Vector2(104f, 104f);
                Image ringImg = ring.gameObject.AddComponent<Image>();
                ringImg.sprite = circleSpr;
                ringImg.color = new Color(1f, 0.56f, 0.36f, 1f);
                ringImg.raycastTarget = false;

                RectTransform socket = CreateRect(ring, "Socket", layer);
                Stretch(socket, 6f);
                Image sockImg = socket.gameObject.AddComponent<Image>();
                sockImg.sprite = circleSpr;
                sockImg.color = new Color(0.14f, 0.09f, 0.07f, 1f);
                sockImg.raycastTarget = false;

                if (relic.Image != null)
                {
                    RectTransform ic = CreateRect(socket, "Icon", layer);
                    Stretch(ic, 14f);
                    Image icImg = ic.gameObject.AddComponent<Image>();
                    icImg.sprite = relic.Image;
                    icImg.preserveAspect = true;
                    icImg.raycastTarget = false;
                }
                else
                {
                    TextMeshProUGUI mono = CreateText(socket, "Mono", font, 32f, FontStyles.Bold, new Color(1f, 0.86f, 0.44f, 1f));
                    mono.alignment = TextAlignmentOptions.Center;
                    mono.text = TopBarRelicSlotsUI.BuildMonogram(relic.Title);
                }

                // Rarity Tag
                TextMeshProUGUI tag = CreateText(card, "Tag", font, 14f, FontStyles.Bold, new Color(1f, 0.62f, 0.36f, 1f));
                tag.rectTransform.anchorMin = new Vector2(0.08f, 0.56f);
                tag.rectTransform.anchorMax = new Vector2(0.92f, 0.63f);
                tag.alignment = TextAlignmentOptions.Center;
                tag.text = "BOSS RELIC";

                // Title
                TextMeshProUGUI title = CreateText(card, "Title", font, 22f, FontStyles.Bold, new Color(1f, 0.92f, 0.58f, 1f));
                title.rectTransform.anchorMin = new Vector2(0.06f, 0.45f);
                title.rectTransform.anchorMax = new Vector2(0.94f, 0.57f);
                title.alignment = TextAlignmentOptions.Center;
                title.textWrappingMode = TextWrappingModes.Normal;
                title.text = relic.Title;

                // Description (highlights "Trade-off:" in warm red)
                string desc = string.IsNullOrWhiteSpace(relic.Description) ? "Powerful Boss Relic." : relic.Description;
                desc = desc.Replace("Trade-off:", "<color=#FF766B><b>Trade-off:</b></color>");
                TextMeshProUGUI body = CreateText(card, "Desc", font, 16.5f, FontStyles.Normal, new Color(0.95f, 0.92f, 0.86f, 0.98f));
                body.rectTransform.anchorMin = new Vector2(0.08f, 0.14f);
                body.rectTransform.anchorMax = new Vector2(0.92f, 0.45f);
                body.alignment = TextAlignmentOptions.Top;
                body.textWrappingMode = TextWrappingModes.Normal;
                body.text = desc;

                // Claim Pill Hint
                TextMeshProUGUI claimHint = CreateText(card, "Hint", font, 15f, FontStyles.Bold, new Color(0.56f, 0.96f, 0.62f, 1f));
                claimHint.rectTransform.anchorMin = new Vector2(0.08f, 0.04f);
                claimHint.rectTransform.anchorMax = new Vector2(0.92f, 0.13f);
                claimHint.alignment = TextAlignmentOptions.Center;
                claimHint.text = "CLICK TO CLAIM";

                Button btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                RelicData captured = relic;
                btn.onClick.AddListener(() => ClaimBossRelic(captured));

                BossRelicHover hover = card.gameObject.AddComponent<BossRelicHover>();
                hover.Init(card);

                // Staggered pop-in
                card.localScale = Vector3.one * 0.78f;
                card.DOScale(Vector3.one, 0.30f).SetDelay(0.06f + i * 0.09f).SetEase(Ease.OutBack).SetUpdate(true);
                _spawnedCards.Add(card.gameObject);
            }
        }

        private void ClaimBossRelic(RelicData relic)
        {
            if (relic == null) return;
            RunRelicManager mgr = RunRelicManager.EnsureInstance();

            if (mgr.IsFull && FindFirstObjectByType<RelicSwapModalUI>() == null && _canvas != null)
            {
                GameObject swapGo = new("RelicSwapModalUI");
                swapGo.transform.SetParent(_canvas.transform, false);
                swapGo.AddComponent<RelicSwapModalUI>();
            }

            mgr.AddRelic(relic);
            Tune.SFX(SfxID.relic_claim);
            CloseAndNotify(relic);
        }

        public void SkipChest()
        {
            Tune.SFX(SfxID.ui_panel_close);
            CloseAndNotify(null);
        }

        private void CloseAndNotify(RelicData picked)
        {
            if (_modalRoot != null) _modalRoot.SetActive(false);
            Action<RelicData> cb = _onComplete;
            _onComplete = null;
            cb?.Invoke(picked);
        }

        private void BuildModalIfNeeded()
        {
            if (_modalRoot != null) return;
            _canvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>()?.rootCanvas;
            if (_canvas == null) return;

            int layer = _canvas.gameObject.layer;
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolidSprite();
            Sprite panelSpr = FindProjectSprite("Panel") ?? solid;
            Sprite btnSpr = FindProjectSprite("button1 ") ?? FindProjectSprite("Tombol") ?? solid;

            _modalRoot = new GameObject("BossRelicChestModal", typeof(RectTransform), typeof(Image));
            _modalRoot.layer = layer;
            RectTransform mRt = (RectTransform)_modalRoot.transform;
            mRt.SetParent(_canvas.transform, false);
            Stretch(mRt);
            Image dim = _modalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.04f, 0.02f, 0.03f, 0.86f);

            _windowRect = CreateRect(mRt, "Window", layer);
            _windowRect.anchorMin = _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowRect.sizeDelta = new Vector2(1240f, 680f);
            Image winBg = _windowRect.gameObject.AddComponent<Image>();
            winBg.sprite = panelSpr;
            winBg.color = panelSpr != solid ? Color.white : new Color(0.12f, 0.08f, 0.07f, 0.98f);

            TextMeshProUGUI title = CreateText(_windowRect, "Title", font, 34f, FontStyles.Bold, new Color(1f, 0.86f, 0.44f, 1f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(32f, -76f);
            title.rectTransform.offsetMax = new Vector2(-32f, -20f);
            title.alignment = TextAlignmentOptions.Center;
            title.text = "BOSS RELIC CHEST — CHOOSE 1 OF 3";

            TextMeshProUGUI sub = CreateText(_windowRect, "Subtitle", font, 18f, FontStyles.Normal, new Color(0.92f, 0.86f, 0.76f, 0.95f));
            sub.rectTransform.anchorMin = new Vector2(0f, 1f);
            sub.rectTransform.anchorMax = new Vector2(1f, 1f);
            sub.rectTransform.offsetMin = new Vector2(32f, -112f);
            sub.rectTransform.offsetMax = new Vector2(-32f, -74f);
            sub.alignment = TextAlignmentOptions.Center;
            sub.text = "High-impact relics with powerful bonuses and strategic trade-offs.";

            _cardsRow = CreateRect(_windowRect, "BossRelicCardsRow", layer);
            _cardsRow.anchorMin = _cardsRow.anchorMax = new Vector2(0.5f, 0.50f);
            _cardsRow.sizeDelta = new Vector2(1140f, 430f);
            HorizontalLayoutGroup hlg = _cardsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 32f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            RectTransform skipRt = CreateRect(_windowRect, "SkipButton", layer);
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(0.5f, 0f);
            skipRt.pivot = new Vector2(0.5f, 0f);
            skipRt.anchoredPosition = new Vector2(0f, 26f);
            skipRt.sizeDelta = new Vector2(210f, 52f);
            Image skImg = skipRt.gameObject.AddComponent<Image>();
            skImg.sprite = btnSpr;
            skImg.color = new Color(0.92f, 0.82f, 0.64f, 1f);
            Button skBtn = skipRt.gameObject.AddComponent<Button>();
            skBtn.targetGraphic = skImg;
            skBtn.onClick.AddListener(SkipChest);
            TextMeshProUGUI skTxt = CreateText(skipRt, "Label", font, 20f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            skTxt.alignment = TextAlignmentOptions.Center;
            skTxt.text = "SKIP CHEST";

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

        private static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Sprite s_solid;
        private static Sprite GetSolidSprite()
        {
            if (s_solid != null) return s_solid;
            Texture2D tex = Texture2D.whiteTexture;
            s_solid = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return s_solid;
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

    internal sealed class BossRelicHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _target;
        public void Init(RectTransform target) => _target = target;
        public void OnPointerEnter(PointerEventData _)
        {
            if (_target == null) return;
            Tune.SFX(SfxID.hover);
            _target.DOKill();
            _target.DOScale(1.06f, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        public void OnPointerExit(PointerEventData _)
        {
            if (_target == null) return;
            _target.DOKill();
            _target.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}

