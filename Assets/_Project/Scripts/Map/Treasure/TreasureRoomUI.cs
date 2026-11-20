using System;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Rewards;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.Map.Treasure
{
    /// <summary>
    /// Interactive procedural Treasure Room UI for Treasure.unity.
    /// Click the rattling gilded chest to burst it open and claim Gold + a real Relic
    /// (with full 5-slot RelicSwapModalUI integration) before continuing to the Map.
    /// </summary>
    [DisallowMultipleComponent]
    public class TreasureRoomUI : MonoBehaviour
    {
        [Header("Scene & Pools")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private RelicRewardPool relicPool;
        [SerializeField] private string mapSceneName = "Map";

        [Header("Rewards")]
        [SerializeField, Min(10)] private int minGold = 55;
        [SerializeField, Min(10)] private int maxGold = 85;

        [Header("Existing Art Assets")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite nameplateSprite;
        [SerializeField] private Sprite chestIconSprite;

        private static Sprite s_solid;
        private static Sprite s_glow;
        private static Sprite s_circle;

        private RectTransform _root;
        private RectTransform _chestRoot;
        private Image _chestGlow;
        private TextMeshProUGUI _chestPromptText;
        private RectTransform _lootPanel;
        private TextMeshProUGUI _goldHeaderText;
        private Button _goldClaimBtn;
        private TextMeshProUGUI _goldClaimLabel;
        private Button _relicClaimBtn;
        private TextMeshProUGUI _relicClaimLabel;
        private TextMeshProUGUI _relicTitleText;
        private TextMeshProUGUI _relicDescText;
        private Image _relicIconImg;
        private TextMeshProUGUI _relicMonoText;
        private RectTransform _continueBtnRect;

        private RelicSwapModalUI _swapModal;
        private int _rolledGold;
        private RelicData _rolledRelic;
        private bool _chestOpened;
        private bool _goldClaimed;
        private bool _relicClaimed;
        private bool _awaitingSwap;
        private bool _built;

        public bool IsChestOpened => _chestOpened;
        public bool GoldClaimed => _goldClaimed;
        public bool RelicClaimed => _relicClaimed;
        public int RolledGold => _rolledGold;
        public RelicData RolledRelic => _rolledRelic;

        private void Awake()
        {
            if (RunManager.Instance == null) new GameObject("Run Manager").AddComponent<RunManager>();
            RunRelicManager.EnsureInstance();
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
            EnsureSwapModal();
            BuildUIIfNeeded();
        }

        private void Start()
        {
            _rolledGold = UnityEngine.Random.Range(minGold, Mathf.Max(minGold, maxGold) + 1);
            if (relicPool != null)
            {
                relicPool.TryGetRandomRelic(RunRelicManager.EnsureInstance(), out _rolledRelic, RelicRarityMask.All);
            }
            RefreshGoldHeader();
            Tune.SFX(SfxID.treasure_open);
        }

        private void Update()
        {
            if (!_chestOpened && _chestRoot != null)
            {
                float t = Time.unscaledTime;
                float bob = Mathf.Sin(t * 2.8f) * 6f;
                float wiggle = Mathf.Sin(t * 6.5f) * (Mathf.Sin(t * 1.4f) > 0.65f ? 4.5f : 0.6f);
                _chestRoot.anchoredPosition = new Vector2(0f, 16f + bob);
                _chestRoot.localEulerAngles = new Vector3(0f, 0f, wiggle);
                if (_chestGlow != null)
                {
                    _chestGlow.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 3.2f));
                }
            }

            if (_awaitingSwap && (_swapModal == null || !_swapModal.IsOpen))
            {
                _awaitingSwap = false;
                if (_rolledRelic != null && RunRelicManager.Instance != null && RunRelicManager.Instance.Contains(_rolledRelic))
                {
                    MarkRelicClaimed();
                }
            }
        }

        public bool OpenChest()
        {
            if (_chestOpened) return false;
            _chestOpened = true;
            Tune.SFX(SfxID.treasure_claim);

            if (_chestRoot != null)
            {
                Sequence seq = DOTween.Sequence().SetUpdate(true);
                seq.Append(_chestRoot.DOShakeAnchorPos(0.24f, new Vector2(14f, 6f), 20, 0f, false, true));
                seq.Append(_chestRoot.DOScale(new Vector3(1.24f, 0.80f, 1f), 0.10f));
                seq.Append(_chestRoot.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack));
                seq.OnComplete(() =>
                {
                    if (_chestRoot != null) _chestRoot.gameObject.SetActive(false);
                    RevealLootPanel();
                });
            }
            else
            {
                RevealLootPanel();
            }

            return true;
        }

        public bool ClaimGold()
        {
            if (!_chestOpened || _goldClaimed) return false;
            _goldClaimed = true;
            RunManager.Instance?.AddGold(_rolledGold);
            Tune.SFX(SfxID.gold_claim);
            if (_goldClaimBtn != null) _goldClaimBtn.interactable = false;
            if (_goldClaimLabel != null) _goldClaimLabel.text = "CLAIMED!";
            RefreshGoldHeader();
            return true;
        }

        public bool ClaimRelic()
        {
            if (!_chestOpened || _relicClaimed || _rolledRelic == null) return false;
            RunRelicManager mgr = RunRelicManager.EnsureInstance();
            if (mgr.IsFull)
            {
                EnsureSwapModal();
                _awaitingSwap = true;
                mgr.AddRelic(_rolledRelic);
                return false;
            }
            if (mgr.AddRelic(_rolledRelic))
            {
                MarkRelicClaimed();
                return true;
            }
            return false;
        }

        public void ReturnToMap()
        {
            Tune.SFX(SfxID.return_to_map);
            if (!string.IsNullOrWhiteSpace(mapSceneName)) SceneLoader.LoadScene(mapSceneName);
        }

        private void MarkRelicClaimed()
        {
            _relicClaimed = true;
            Tune.SFX(SfxID.relic_claim);
            if (_relicClaimBtn != null) _relicClaimBtn.interactable = false;
            if (_relicClaimLabel != null) _relicClaimLabel.text = "EQUIPPED!";
            if (_continueBtnRect != null) _continueBtnRect.DOPunchScale(Vector3.one * 0.14f, 0.34f, 6, 0.6f).SetUpdate(true);
        }

        private void RevealLootPanel()
        {
            if (_lootPanel == null) return;
            _lootPanel.gameObject.SetActive(true);
            _lootPanel.localScale = Vector3.one * 0.78f;
            _lootPanel.DOScale(Vector3.one, 0.36f).SetEase(Ease.OutBack).SetUpdate(true);

            if (_goldClaimLabel != null) _goldClaimLabel.text = $"CLAIM  +{_rolledGold}g";
            if (_rolledRelic != null)
            {
                _relicTitleText.text = _rolledRelic.Title;
                _relicDescText.text = string.IsNullOrWhiteSpace(_rolledRelic.Description) ? "Passive run relic." : _rolledRelic.Description;
                bool hasImg = _rolledRelic.Image != null;
                _relicIconImg.enabled = hasImg;
                if (hasImg) _relicIconImg.sprite = _rolledRelic.Image;
                _relicMonoText.gameObject.SetActive(!hasImg);
                if (!hasImg) _relicMonoText.text = TopBarRelicSlotsUI.BuildMonogram(_rolledRelic.Title);
                _relicClaimLabel.text = "CLAIM RELIC";
                _relicClaimBtn.interactable = true;
            }
            else
            {
                _relicTitleText.text = "All Unique Relics Owned";
                _relicDescText.text = "You already possess every relic from this vault!";
                _relicClaimLabel.text = "NONE LEFT";
                _relicClaimBtn.interactable = false;
            }
        }

        private void RefreshGoldHeader()
        {
            if (_goldHeaderText != null && RunManager.Instance != null)
            {
                _goldHeaderText.text = $"GOLD: {RunManager.Instance.Gold}g";
            }
        }

        private void EnsureSwapModal()
        {
            if (_swapModal != null) return;
            _swapModal = FindFirstObjectByType<RelicSwapModalUI>();
            if (_swapModal == null && targetCanvas != null)
            {
                GameObject go = new("RelicSwapModalUI");
                go.transform.SetParent(targetCanvas.transform, false);
                _swapModal = go.AddComponent<RelicSwapModalUI>();
            }
        }

        private void BuildUIIfNeeded()
        {
            if (_built || targetCanvas == null) return;
            _built = true;

            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolid();

            _root = CreateRect(targetCanvas.transform, "TreasureRoomRoot", layer);
            Stretch(_root);

            RectTransform bgRt = CreateRect(_root, "Backdrop", layer);
            Stretch(bgRt);
            bgRt.localScale = new Vector3(1.05f, 1.05f, 1f);
            Image bg = bgRt.gameObject.AddComponent<Image>();
            bg.sprite = backgroundSprite != null ? backgroundSprite : solid;
            bg.color = new Color(0.52f, 0.42f, 0.30f, 1f);
            bg.raycastTarget = false;

            RectTransform wash = CreateRect(_root, "Wash", layer);
            Stretch(wash);
            Image wImg = wash.gameObject.AddComponent<Image>();
            wImg.sprite = solid;
            wImg.color = new Color(0.10f, 0.06f, 0.03f, 0.56f);
            wImg.raycastTarget = false;

            // Top Banner
            RectTransform banner = CreateRect(_root, "Banner", layer);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -28f);
            banner.sizeDelta = new Vector2(520f, 68f);
            banner.gameObject.AddComponent<Image>().sprite = nameplateSprite != null ? nameplateSprite : solid;
            TextMeshProUGUI title = MakeText(banner, "Title", font, 28f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            title.alignment = TextAlignmentOptions.Center;
            title.text = "TREASURE VAULT";

            RectTransform goldPill = CreateRect(_root, "GoldHeader", layer);
            goldPill.anchorMin = goldPill.anchorMax = new Vector2(0f, 1f);
            goldPill.pivot = new Vector2(0f, 1f);
            goldPill.anchoredPosition = new Vector2(32f, -30f);
            goldPill.sizeDelta = new Vector2(210f, 54f);
            Image gBg = goldPill.gameObject.AddComponent<Image>();
            gBg.sprite = buttonSprite != null ? buttonSprite : solid;
            gBg.color = new Color(0.22f, 0.14f, 0.08f, 0.96f);
            _goldHeaderText = MakeText(goldPill, "Label", font, 22f, FontStyles.Bold, new Color(1f, 0.88f, 0.44f, 1f));
            _goldHeaderText.alignment = TextAlignmentOptions.Center;

            // Interactive Gilded Chest Stage
            _chestRoot = CreateRect(_root, "TreasureChestButton", layer);
            _chestRoot.anchorMin = _chestRoot.anchorMax = new Vector2(0.5f, 0.52f);
            _chestRoot.sizeDelta = new Vector2(340f, 340f);

            RectTransform glowRt = CreateRect(_chestRoot, "Glow", layer);
            glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = new Vector2(460f, 460f);
            _chestGlow = glowRt.gameObject.AddComponent<Image>();
            _chestGlow.sprite = GetGlow();
            _chestGlow.color = new Color(1f, 0.84f, 0.34f, 0.44f);
            _chestGlow.raycastTarget = false;

            RectTransform chestBox = CreateRect(_chestRoot, "ChestFrame", layer);
            Stretch(chestBox, 26f);
            Image chestFrameImg = chestBox.gameObject.AddComponent<Image>();
            chestFrameImg.sprite = panelSprite != null ? panelSprite : solid;
            chestFrameImg.color = Color.white;

            RectTransform iconRt = CreateRect(chestBox, "ChestEmblem", layer);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.58f);
            iconRt.sizeDelta = new Vector2(154f, 154f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.sprite = chestIconSprite != null ? chestIconSprite : GetCircle();
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;

            _chestPromptText = MakeText(chestBox, "Prompt", font, 24f, FontStyles.Bold, new Color(1f, 0.90f, 0.48f, 1f));
            _chestPromptText.rectTransform.anchorMin = new Vector2(0f, 0.08f);
            _chestPromptText.rectTransform.anchorMax = new Vector2(1f, 0.28f);
            _chestPromptText.alignment = TextAlignmentOptions.Center;
            _chestPromptText.text = "CLICK TO OPEN CHEST";

            Button chestBtn = _chestRoot.gameObject.AddComponent<Button>();
            chestBtn.targetGraphic = chestFrameImg;
            chestBtn.onClick.AddListener(() => OpenChest());

            // Loot Showcase Panel (hidden until chest opens!)
            _lootPanel = CreateRect(_root, "LootShowcasePanel", layer);
            _lootPanel.anchorMin = _lootPanel.anchorMax = new Vector2(0.5f, 0.52f);
            _lootPanel.sizeDelta = new Vector2(1020f, 480f);
            Image lpBg = _lootPanel.gameObject.AddComponent<Image>();
            lpBg.sprite = panelSprite != null ? panelSprite : solid;
            lpBg.color = Color.white;

            // Left Half: Gold Pile Reward
            RectTransform goldBox = CreateRect(_lootPanel, "GoldRewardCard", layer);
            goldBox.anchorMin = new Vector2(0.05f, 0.10f);
            goldBox.anchorMax = new Vector2(0.47f, 0.90f);
            goldBox.offsetMin = Vector2.zero;
            goldBox.offsetMax = Vector2.zero;
            Image gbImg = goldBox.gameObject.AddComponent<Image>();
            gbImg.sprite = buttonSprite != null ? buttonSprite : solid;
            gbImg.color = new Color(0.28f, 0.19f, 0.11f, 0.95f);

            RectTransform coinCircle = CreateRect(goldBox, "CoinDisc", layer);
            coinCircle.anchorMin = coinCircle.anchorMax = new Vector2(0.5f, 0.68f);
            coinCircle.sizeDelta = new Vector2(108f, 108f);
            Image cc = coinCircle.gameObject.AddComponent<Image>();
            cc.sprite = GetCircle();
            cc.color = new Color(1f, 0.82f, 0.24f, 1f);
            TextMeshProUGUI cgTxt = MakeText(coinCircle, "G", font, 46f, FontStyles.Bold, new Color(0.32f, 0.18f, 0.02f, 1f));
            cgTxt.alignment = TextAlignmentOptions.Center;
            cgTxt.text = "G";

            TextMeshProUGUI goldTitle = MakeText(goldBox, "Title", font, 26f, FontStyles.Bold, new Color(1f, 0.90f, 0.48f, 1f));
            goldTitle.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            goldTitle.rectTransform.anchorMax = new Vector2(1f, 0.50f);
            goldTitle.alignment = TextAlignmentOptions.Center;
            goldTitle.text = "TREASURE GOLD";

            RectTransform gBtnRt = CreateRect(goldBox, "ClaimGoldBtn", layer);
            gBtnRt.anchorMin = gBtnRt.anchorMax = new Vector2(0.5f, 0.16f);
            gBtnRt.sizeDelta = new Vector2(260f, 62f);
            Image gBtnImg = gBtnRt.gameObject.AddComponent<Image>();
            gBtnImg.sprite = buttonSprite != null ? buttonSprite : solid;
            gBtnImg.color = new Color(0.96f, 0.86f, 0.56f, 1f);
            _goldClaimBtn = gBtnRt.gameObject.AddComponent<Button>();
            _goldClaimBtn.onClick.AddListener(() => ClaimGold());
            _goldClaimLabel = MakeText(gBtnRt, "Label", font, 22f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            _goldClaimLabel.alignment = TextAlignmentOptions.Center;

            // Right Half: Relic Showcase
            RectTransform relicBox = CreateRect(_lootPanel, "RelicRewardCard", layer);
            relicBox.anchorMin = new Vector2(0.53f, 0.10f);
            relicBox.anchorMax = new Vector2(0.95f, 0.90f);
            relicBox.offsetMin = Vector2.zero;
            relicBox.offsetMax = Vector2.zero;
            Image rbImg = relicBox.gameObject.AddComponent<Image>();
            rbImg.sprite = buttonSprite != null ? buttonSprite : solid;
            rbImg.color = new Color(0.28f, 0.19f, 0.11f, 0.95f);

            RectTransform rRing = CreateRect(relicBox, "RelicRing", layer);
            rRing.anchorMin = rRing.anchorMax = new Vector2(0.5f, 0.72f);
            rRing.sizeDelta = new Vector2(98f, 98f);
            Image rRingImg = rRing.gameObject.AddComponent<Image>();
            rRingImg.sprite = GetCircle();
            rRingImg.color = new Color(0.96f, 0.80f, 0.36f, 1f);
            RectTransform rSock = CreateRect(rRing, "Sock", layer);
            Stretch(rSock, 5f);
            Image rSockImg = rSock.gameObject.AddComponent<Image>();
            rSockImg.sprite = GetCircle();
            rSockImg.color = new Color(0.14f, 0.10f, 0.07f, 1f);
            RectTransform rIconRt = CreateRect(rSock, "Icon", layer);
            Stretch(rIconRt, 14f);
            _relicIconImg = rIconRt.gameObject.AddComponent<Image>();
            _relicIconImg.preserveAspect = true;
            _relicIconImg.color = new Color(1f, 0.88f, 0.50f, 1f);
            _relicMonoText = MakeText(rSock, "Mono", font, 28f, FontStyles.Bold, new Color(1f, 0.88f, 0.50f, 1f));
            _relicMonoText.alignment = TextAlignmentOptions.Center;

            _relicTitleText = MakeText(relicBox, "RelicTitle", font, 24f, FontStyles.Bold, new Color(1f, 0.90f, 0.54f, 1f));
            _relicTitleText.rectTransform.anchorMin = new Vector2(0f, 0.44f);
            _relicTitleText.rectTransform.anchorMax = new Vector2(1f, 0.56f);
            _relicTitleText.alignment = TextAlignmentOptions.Center;

            _relicDescText = MakeText(relicBox, "RelicDesc", font, 17f, FontStyles.Normal, new Color(0.94f, 0.88f, 0.76f, 0.96f));
            _relicDescText.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            _relicDescText.rectTransform.anchorMax = new Vector2(1f, 0.44f);
            _relicDescText.rectTransform.offsetMin = new Vector2(24f, 0f);
            _relicDescText.rectTransform.offsetMax = new Vector2(-24f, 0f);
            _relicDescText.alignment = TextAlignmentOptions.Center;
            _relicDescText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform rBtnRt = CreateRect(relicBox, "ClaimRelicBtn", layer);
            rBtnRt.anchorMin = rBtnRt.anchorMax = new Vector2(0.5f, 0.16f);
            rBtnRt.sizeDelta = new Vector2(260f, 62f);
            Image rBtnImg = rBtnRt.gameObject.AddComponent<Image>();
            rBtnImg.sprite = buttonSprite != null ? buttonSprite : solid;
            rBtnImg.color = new Color(0.52f, 0.88f, 0.62f, 1f);
            _relicClaimBtn = rBtnRt.gameObject.AddComponent<Button>();
            _relicClaimBtn.onClick.AddListener(() => ClaimRelic());
            _relicClaimLabel = MakeText(rBtnRt, "Label", font, 22f, FontStyles.Bold, new Color(0.10f, 0.24f, 0.10f, 1f));
            _relicClaimLabel.alignment = TextAlignmentOptions.Center;

            _lootPanel.gameObject.SetActive(false);

            // Continue Button
            _continueBtnRect = CreateRect(_root, "ContinueToMapButton", layer);
            _continueBtnRect.anchorMin = _continueBtnRect.anchorMax = new Vector2(0.5f, 0f);
            _continueBtnRect.pivot = new Vector2(0.5f, 0f);
            _continueBtnRect.anchoredPosition = new Vector2(0f, 44f);
            _continueBtnRect.sizeDelta = new Vector2(300f, 74f);
            Image cImg = _continueBtnRect.gameObject.AddComponent<Image>();
            cImg.sprite = buttonSprite != null ? buttonSprite : solid;
            cImg.color = new Color(0.95f, 0.85f, 0.64f, 1f);
            _continueBtnRect.gameObject.AddComponent<Button>().onClick.AddListener(ReturnToMap);
            TextMeshProUGUI cTxt = MakeText(_continueBtnRect, "Label", font, 24f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            cTxt.alignment = TextAlignmentOptions.Center;
            cTxt.text = "CONTINUE TO MAP  >";
        }

        private static RectTransform CreateRect(Transform p, string n, int l)
        {
            GameObject g = new(n, typeof(RectTransform));
            g.layer = l;
            RectTransform rt = (RectTransform)g.transform;
            rt.SetParent(p, false);
            return rt;
        }
        private static void Stretch(RectTransform rt, float pad = 0f) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad); }
        private static TextMeshProUGUI MakeText(RectTransform p, string n, TMP_FontAsset f, float sz, FontStyles st, Color c)
        {
            RectTransform rt = CreateRect(p, n, p.gameObject.layer); Stretch(rt);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (f != null) t.font = f; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false;
            return t;
        }
        private static Sprite GetSolid() { if (s_solid != null) return s_solid; Texture2D t = Texture2D.whiteTexture; s_solid = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)); return s_solid; }
        private static Sprite GetCircle()
        {
            if (s_circle != null) return s_circle; const int sz = 96; Texture2D tex = new(sz, sz, TextureFormat.RGBA32, false); float h = (sz - 1) * 0.5f; Color32[] px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++) for (int x = 0; x < sz; x++) px[y * sz + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(h - 1.5f - Vector2.Distance(new Vector2(x, y), new Vector2(h, h)) + 0.8f) * 255f));
            tex.SetPixels32(px); tex.Apply(); s_circle = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f)); return s_circle;
        }
        private static Sprite GetGlow()
        {
            if (s_glow != null) return s_glow; const int sz = 96; Texture2D tex = new(sz, sz, TextureFormat.RGBA32, false); float h = (sz - 1) * 0.5f; Color32[] px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++) for (int x = 0; x < sz; x++) px[y * sz + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Pow(1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(h, h)) / h), 2.1f) * 255f));
            tex.SetPixels32(px); tex.Apply(); s_glow = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f)); return s_glow;
        }
    }
}

