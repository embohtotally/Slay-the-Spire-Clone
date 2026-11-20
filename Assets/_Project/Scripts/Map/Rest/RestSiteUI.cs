using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Map.Merchant;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlushieOdyssey.Map.Rest
{
    /// <summary>
    /// Full interactive Slay-the-Spire Campfire Rest Site UI built with existing project art assets.
    /// Supports Rest (Heal 30%), Calm Mind (Restore Will / Reduce Stress), and Smith (Interactive
    /// Deck-Grid Card Upgrade picker with side-by-side Card -> Card+ preview), plus procedural
    /// flickering campfire glow and rising ember sparks.
    /// </summary>
    [DisallowMultipleComponent]
    public class RestSiteUI : MonoBehaviour
    {
        [Header("Scene & Data")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private RunDeckCardView cardViewPrefab;
        [SerializeField] private MerchantCardPool upgradeRecipeSource;
        [SerializeField] private List<CardUpgradeRecipe> extraUpgradeRecipes = new();
        [SerializeField] private string mapSceneName = "Map";

        [Header("Rest Values")]
        [SerializeField, Range(0.05f, 1f)] private float healPercent = 0.30f;
        [SerializeField, Min(5)] private int stressReduction = 30;

        [Header("Existing Art Assets")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite nameplateSprite;
        [SerializeField] private Sprite dialogueBoxSprite;
        [SerializeField] private Sprite restIconSprite;
        [SerializeField] private Sprite healIconSprite;
        [SerializeField] private Sprite calmIconSprite;
        [SerializeField] private Sprite smithIconSprite;

        private sealed class EmberMote
        {
            public RectTransform Rect;
            public Image Image;
            public float BaseX;
            public float SpeedY;
            public float SwayAmp;
            public float Phase;
            public float BaseAlpha;
        }

        private static Sprite s_solid;
        private static Sprite s_glow;
        private static Sprite s_circle;

        private readonly List<EmberMote> _embers = new();
        private readonly List<Button> _actionButtons = new();
        private readonly List<RectTransform> _actionCards = new();
        private readonly List<GameObject> _pickerSpawned = new();

        private RectTransform _root;
        private Image _fireGlowOuter;
        private Image _fireGlowInner;
        private TextMeshProUGUI _statusHpText;
        private TextMeshProUGUI _statusWillText;
        private TextMeshProUGUI _statusGoldText;
        private TextMeshProUGUI _narrativeText;
        private TextMeshProUGUI _restDescText;
        private TextMeshProUGUI _calmDescText;
        private TextMeshProUGUI _smithDescText;
        private Button _continueButton;
        private RectTransform _continueBtnRect;

        // Smith Picker & Preview Modal
        private GameObject _smithModalRoot;
        private Transform _smithGridContent;
        private TextMeshProUGUI _smithEmptyText;
        private GameObject _previewOverlay;
        private RectTransform _previewBeforeHolder;
        private RectTransform _previewAfterHolder;
        private TextMeshProUGUI _previewTitle;
        private RunDeckCardView _previewBeforeView;
        private RunDeckCardView _previewAfterView;
        private CardUpgradeRecipe _selectedRecipe;

        private bool _actionTaken;
        private bool _built;

        public bool ActionTaken => _actionTaken;
        public string LastNarrative => _narrativeText != null ? _narrativeText.text : string.Empty;

        private void Awake()
        {
            EnsureRuntimeManagers();
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
            BuildUIIfNeeded();
        }

        private void Start()
        {
            RefreshStatusReadout();
            PlayEntrance();
            Tune.SFX(SfxID.scene_enter);
        }

        private void Update()
        {
            UpdateCampfireAnimation(Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------------
        // Public Campfire Actions
        // ------------------------------------------------------------------
        public bool TryRestHeal()
        {
            if (_actionTaken) return false;
            EnsureRuntimeManagers();
            RunManager rm = RunManager.Instance;
            int maxHp = Mathf.Max(1, rm.HeroMaxHealth);
            int healAmt = Mathf.Max(1, Mathf.CeilToInt(maxHp * healPercent));
            int before = rm.HeroCurrentHealth;
            rm.HealHero(healAmt);
            int gained = rm.HeroCurrentHealth - before;

            CompleteCampfireAction($"You rest beside the crackling flames and recover +{gained} HP! ({rm.HeroCurrentHealth}/{maxHp} HP)");
            Tune.SFX(SfxID.rest_heal);
            FlashFire(new Color(0.45f, 0.96f, 0.62f, 0.65f));
            return true;
        }

        public bool TryCalmMind()
        {
            if (_actionTaken) return false;
            EnsureRuntimeManagers();
            RunManager rm = RunManager.Instance;
            int beforeWill = rm.HeroCurrentWill;
            rm.ReduceStress(stressReduction);
            int gainedWill = rm.HeroCurrentWill - beforeWill;

            CompleteCampfireAction($"Warm lantern light steadies your resolve. Will restored by +{gainedWill}% ({rm.HeroCurrentWill}/{rm.HeroMaxWill})!");
            Tune.SFX(SfxID.stress_reduce);
            FlashFire(new Color(0.46f, 0.88f, 1f, 0.65f));
            return true;
        }

        public void OpenSmithPicker()
        {
            if (_actionTaken) return;
            EnsureRuntimeManagers();
            BuildSmithModalIfNeeded();
            PopulateSmithGrid();
            _smithModalRoot.SetActive(true);
            _smithModalRoot.transform.SetAsLastSibling();
            if (_previewOverlay != null) _previewOverlay.SetActive(false);
            Tune.SFX(SfxID.ui_panel_open);
        }

        public void CloseSmithPicker()
        {
            if (_smithModalRoot != null) _smithModalRoot.SetActive(false);
            if (_previewOverlay != null) _previewOverlay.SetActive(false);
            Tune.SFX(SfxID.ui_panel_close);
        }

        public bool ConfirmSmithUpgrade()
        {
            if (_actionTaken || _selectedRecipe == null || _selectedRecipe.BaseCard == null || _selectedRecipe.UpgradedCard == null)
                return false;

            EnsureRuntimeManagers();
            if (!RunDeckManager.Instance.ReplaceFirst(_selectedRecipe.BaseCard, _selectedRecipe.UpgradedCard))
                return false;

            string fromName = _selectedRecipe.BaseCard.Title;
            string toName = _selectedRecipe.UpgradedCard.Title;
            _selectedRecipe = null;
            if (_smithModalRoot != null) _smithModalRoot.SetActive(false);
            if (_previewOverlay != null) _previewOverlay.SetActive(false);

            CompleteCampfireAction($"Sparks fly off the anvil! Upgraded '{fromName}' -> '{toName}'!");
            Tune.SFX(SfxID.card_upgrade);
            FlashFire(new Color(1f, 0.84f, 0.34f, 0.72f));
            return true;
        }

        public List<CardUpgradeRecipe> GetAvailableUpgradesForCurrentDeck()
        {
            List<CardUpgradeRecipe> allRecipes = GatherUpgradeRecipes();
            List<CardUpgradeRecipe> result = new();
            RunDeckManager deck = RunDeckManager.Instance;
            if (deck == null) return result;

            HashSet<CardData> addedBase = new();
            foreach (CardData card in deck.CurrentDeck)
            {
                if (card == null || !addedBase.Add(card)) continue;
                CardUpgradeRecipe match = allRecipes.FirstOrDefault(r => r != null && r.BaseCard == card && r.UpgradedCard != null);
                if (match != null) result.Add(match);
            }
            return result;
        }

        public void ReturnToMap()
        {
            Tune.SFX(SfxID.return_to_map);
            if (!string.IsNullOrWhiteSpace(mapSceneName))
            {
                SceneLoader.LoadScene(mapSceneName);
            }
        }

        // ------------------------------------------------------------------
        // Internal Helpers
        // ------------------------------------------------------------------
        private void CompleteCampfireAction(string message)
        {
            _actionTaken = true;
            if (_narrativeText != null) _narrativeText.text = message;
            foreach (Button btn in _actionButtons)
            {
                if (btn != null) btn.interactable = false;
            }
            foreach (RectTransform card in _actionCards)
            {
                CanvasGroup cg = card.GetComponent<CanvasGroup>();
                if (cg != null) cg.DOFade(0.48f, 0.25f).SetUpdate(true);
            }
            RefreshStatusReadout();

            if (_continueBtnRect != null)
            {
                _continueBtnRect.DOKill();
                _continueBtnRect.DOPunchScale(Vector3.one * 0.14f, 0.38f, 6, 0.6f).SetUpdate(true);
            }
        }

        private List<CardUpgradeRecipe> GatherUpgradeRecipes()
        {
            List<CardUpgradeRecipe> list = new();
            if (extraUpgradeRecipes != null)
            {
                foreach (CardUpgradeRecipe r in extraUpgradeRecipes)
                {
                    if (r != null && r.BaseCard != null && r.UpgradedCard != null) list.Add(r);
                }
            }

            if (upgradeRecipeSource != null && RunDeckManager.Instance != null)
            {
                List<MerchantOffer> offers = upgradeRecipeSource.BuildCandidateOffers(RunDeckManager.Instance, includeNewCards: false, includeUpgrades: true);
                foreach (MerchantOffer o in offers)
                {
                    if (o?.UpgradeRecipe != null && !list.Any(x => x.BaseCard == o.UpgradeRecipe.BaseCard))
                    {
                        list.Add(o.UpgradeRecipe);
                    }
                }
            }
            return list;
        }

        private void RefreshStatusReadout()
        {
            RunManager rm = RunManager.Instance;
            int hp = rm != null ? rm.HeroCurrentHealth : 44;
            int maxHp = rm != null ? Mathf.Max(1, rm.HeroMaxHealth) : 44;
            int will = rm != null ? rm.HeroCurrentWill : 50;
            int maxWill = rm != null ? rm.HeroMaxWill : 100;
            int gold = rm != null ? rm.Gold : 150;

            if (_statusHpText != null) _statusHpText.text = $"HP  {hp}/{maxHp}";
            if (_statusWillText != null) _statusWillText.text = $"WILL  {will}/{maxWill}";
            if (_statusGoldText != null) _statusGoldText.text = $"{gold}g";

            int healAmt = Mathf.Max(1, Mathf.CeilToInt(maxHp * healPercent));
            if (_restDescText != null) _restDescText.text = $"Restore {Mathf.RoundToInt(healPercent * 100f)}% Max HP (+{healAmt} HP).";
            if (_calmDescText != null) _calmDescText.text = $"Ease Fear and restore +{stressReduction} Will.";

            int upgradableCount = GetAvailableUpgradesForCurrentDeck().Count;
            if (_smithDescText != null)
            {
                _smithDescText.text = upgradableCount > 0
                    ? $"Choose 1 of {upgradableCount} upgradable deck cards to forge into its + version."
                    : "Inspect your deck to forge an upgraded card (no base upgrade recipes currently in deck).";
            }
        }

        private void PlayEntrance()
        {
            for (int i = 0; i < _actionCards.Count; i++)
            {
                RectTransform card = _actionCards[i];
                card.localScale = Vector3.one * 0.82f;
                CanvasGroup cg = card.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                float d = 0.08f + i * 0.09f;
                card.DOScale(Vector3.one, 0.34f).SetDelay(d).SetEase(Ease.OutBack).SetUpdate(true);
                if (cg != null) cg.DOFade(1f, 0.28f).SetDelay(d).SetUpdate(true);
            }
        }

        private void UpdateCampfireAnimation(float dt)
        {
            float t = Time.unscaledTime;
            if (_fireGlowOuter != null)
            {
                float s = 1f + 0.06f * Mathf.Sin(t * 4.3f) + 0.03f * Mathf.Sin(t * 9.1f);
                _fireGlowOuter.rectTransform.localScale = Vector3.one * s;
            }
            if (_fireGlowInner != null)
            {
                float s = 1f + 0.09f * Mathf.Sin(t * 6.7f + 0.8f);
                _fireGlowInner.rectTransform.localScale = Vector3.one * s;
            }

            for (int i = 0; i < _embers.Count; i++)
            {
                EmberMote m = _embers[i];
                if (m.Rect == null) continue;
                Vector2 pos = m.Rect.anchoredPosition;
                pos.y += m.SpeedY * dt;
                if (pos.y > 460f) pos.y = -420f;
                pos.x = m.BaseX + Mathf.Sin(t * 1.6f + m.Phase) * m.SwayAmp;
                m.Rect.anchoredPosition = pos;
                Color c = m.Image.color;
                c.a = m.BaseAlpha * (0.55f + 0.45f * Mathf.Sin(t * 3.4f + m.Phase));
                m.Image.color = c;
            }
        }

        private void FlashFire(Color color)
        {
            if (_fireGlowOuter == null) return;
            Color orig = new(1f, 0.56f, 0.18f, 0.30f);
            _fireGlowOuter.DOKill();
            _fireGlowOuter.color = color;
            _fireGlowOuter.DOColor(orig, 0.65f).SetUpdate(true);
        }

        // ------------------------------------------------------------------
        // UI Construction
        // ------------------------------------------------------------------
        private void BuildUIIfNeeded()
        {
            if (_built || targetCanvas == null) return;
            _built = true;

            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolid();

            _root = CreateRect(targetCanvas.transform, "RestSiteRoot", layer);
            Stretch(_root);

            // Cozy dusk campfire backdrop
            RectTransform bgRt = CreateRect(_root, "Backdrop", layer);
            Stretch(bgRt);
            bgRt.localScale = new Vector3(1.05f, 1.05f, 1f);
            Image bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = backgroundSprite != null ? backgroundSprite : solid;
            bgImg.color = new Color(0.32f, 0.24f, 0.28f, 1f);
            bgImg.raycastTarget = false;

            RectTransform washRt = CreateRect(_root, "NightWash", layer);
            Stretch(washRt);
            Image wash = washRt.gameObject.AddComponent<Image>();
            wash.sprite = solid;
            wash.color = new Color(0.06f, 0.04f, 0.08f, 0.62f);
            wash.raycastTarget = false;

            // Flickering Campfire Glow Centerpiece
            RectTransform fireOuterRt = CreateRect(_root, "CampfireGlowOuter", layer);
            fireOuterRt.anchorMin = fireOuterRt.anchorMax = new Vector2(0.5f, 0.26f);
            fireOuterRt.sizeDelta = new Vector2(780f, 520f);
            _fireGlowOuter = fireOuterRt.gameObject.AddComponent<Image>();
            _fireGlowOuter.sprite = GetGlow();
            _fireGlowOuter.color = new Color(1f, 0.56f, 0.18f, 0.30f);
            _fireGlowOuter.raycastTarget = false;

            RectTransform fireInnerRt = CreateRect(_root, "CampfireGlowInner", layer);
            fireInnerRt.anchorMin = fireInnerRt.anchorMax = new Vector2(0.5f, 0.22f);
            fireInnerRt.sizeDelta = new Vector2(360f, 280f);
            _fireGlowInner = fireInnerRt.gameObject.AddComponent<Image>();
            _fireGlowInner.sprite = GetGlow();
            _fireGlowInner.color = new Color(1f, 0.84f, 0.38f, 0.34f);
            _fireGlowInner.raycastTarget = false;

            // Rising procedural embers
            RectTransform emberLayer = CreateRect(_root, "EmberSparks", layer);
            Stretch(emberLayer);
            for (int i = 0; i < 18; i++)
            {
                RectTransform eRt = CreateRect(emberLayer, $"Ember_{i:00}", layer);
                eRt.anchorMin = eRt.anchorMax = new Vector2(0.5f, 0.5f);
                float sz = UnityEngine.Random.Range(8f, 18f);
                eRt.sizeDelta = new Vector2(sz, sz);
                float bx = UnityEngine.Random.Range(-740f, 740f);
                eRt.anchoredPosition = new Vector2(bx, UnityEngine.Random.Range(-420f, 440f));
                Image eImg = eRt.gameObject.AddComponent<Image>();
                eImg.sprite = GetGlow();
                float a = UnityEngine.Random.Range(0.24f, 0.55f);
                eImg.color = new Color(1f, UnityEngine.Random.Range(0.55f, 0.85f), 0.22f, a);
                eImg.raycastTarget = false;
                _embers.Add(new EmberMote { Rect = eRt, Image = eImg, BaseX = bx, SpeedY = UnityEngine.Random.Range(24f, 56f), SwayAmp = UnityEngine.Random.Range(14f, 38f), Phase = UnityEngine.Random.Range(0f, 6.28f), BaseAlpha = a });
            }

            // Top Header & Run Status Bar
            RectTransform headerBanner = CreateRect(_root, "CampfireBanner", layer);
            headerBanner.anchorMin = headerBanner.anchorMax = new Vector2(0.5f, 1f);
            headerBanner.pivot = new Vector2(0.5f, 1f);
            headerBanner.anchoredPosition = new Vector2(0f, -24f);
            headerBanner.sizeDelta = new Vector2(520f, 68f);
            Image bImg = headerBanner.gameObject.AddComponent<Image>();
            bImg.sprite = nameplateSprite != null ? nameplateSprite : solid;
            bImg.color = Color.white;
            TextMeshProUGUI titleTxt = MakeText(headerBanner, "Title", font, 28f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.text = "CAMPFIRE REST SITE";

            // Top-left status pills (HP, Will, Gold)
            RectTransform statusBar = CreateRect(_root, "StatusBar", layer);
            statusBar.anchorMin = statusBar.anchorMax = new Vector2(0f, 1f);
            statusBar.pivot = new Vector2(0f, 1f);
            statusBar.anchoredPosition = new Vector2(28f, -26f);
            statusBar.sizeDelta = new Vector2(540f, 52f);
            HorizontalLayoutGroup shlg = statusBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            shlg.spacing = 12f;
            shlg.childControlWidth = false;
            shlg.childControlHeight = false;

            _statusHpText = MakeStatPill(statusBar, "HpPill", font, new Color(1f, 0.56f, 0.54f, 1f), 175f);
            _statusWillText = MakeStatPill(statusBar, "WillPill", font, new Color(0.56f, 0.88f, 1f, 1f), 185f);
            _statusGoldText = MakeStatPill(statusBar, "GoldPill", font, new Color(1f, 0.88f, 0.44f, 1f), 130f);

            // Center 3 Campfire Action Cards
            RectTransform actionsRow = CreateRect(_root, "CampfireActionsRow", layer);
            actionsRow.anchorMin = actionsRow.anchorMax = new Vector2(0.5f, 0.56f);
            actionsRow.pivot = new Vector2(0.5f, 0.5f);
            actionsRow.sizeDelta = new Vector2(1260f, 390f);
            HorizontalLayoutGroup ahlg = actionsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ahlg.spacing = 36f;
            ahlg.childAlignment = TextAnchor.MiddleCenter;
            ahlg.childControlWidth = false;
            ahlg.childControlHeight = false;

            BuildCampfireActionCard(actionsRow, "REST", "Recover HP", healIconSprite, new Color(0.36f, 0.86f, 0.54f, 1f), font, out _restDescText, () => TryRestHeal());
            BuildCampfireActionCard(actionsRow, "CALM MIND", "Restore Will", calmIconSprite, new Color(0.40f, 0.84f, 1f, 1f), font, out _calmDescText, () => TryCalmMind());
            BuildCampfireActionCard(actionsRow, "SMITH", "Upgrade a Card", smithIconSprite != null ? smithIconSprite : restIconSprite, new Color(1f, 0.78f, 0.34f, 1f), font, out _smithDescText, OpenSmithPicker);

            // Bottom Narrative Bar (Panel Box Text_0.png) + Continue to Map Button
            RectTransform dlgRt = CreateRect(_root, "NarrativeBox", layer);
            dlgRt.anchorMin = dlgRt.anchorMax = new Vector2(0.5f, 0f);
            dlgRt.pivot = new Vector2(0.5f, 0f);
            dlgRt.anchoredPosition = new Vector2(-110f, 28f);
            dlgRt.sizeDelta = new Vector2(1020f, 152f);
            Image dlgImg = dlgRt.gameObject.AddComponent<Image>();
            dlgImg.sprite = dialogueBoxSprite != null ? dialogueBoxSprite : solid;
            dlgImg.color = Color.white;
            _narrativeText = MakeText(dlgRt, "Text", font, 23f, FontStyles.Normal, new Color(0.22f, 0.12f, 0.05f, 1f));
            _narrativeText.rectTransform.offsetMin = new Vector2(46f, 24f);
            _narrativeText.rectTransform.offsetMax = new Vector2(-74f, -24f);
            _narrativeText.alignment = TextAlignmentOptions.MidlineLeft;
            _narrativeText.textWrappingMode = TextWrappingModes.Normal;
            _narrativeText.text = "The campfire crackles warmly. Choose one action before continuing your journey.";

            _continueBtnRect = CreateRect(_root, "ContinueToMapButton", layer);
            _continueBtnRect.anchorMin = _continueBtnRect.anchorMax = new Vector2(1f, 0f);
            _continueBtnRect.pivot = new Vector2(1f, 0f);
            _continueBtnRect.anchoredPosition = new Vector2(-36f, 54f);
            _continueBtnRect.sizeDelta = new Vector2(280f, 82f);
            Image cImg = _continueBtnRect.gameObject.AddComponent<Image>();
            cImg.sprite = buttonSprite != null ? buttonSprite : solid;
            cImg.color = new Color(0.96f, 0.86f, 0.64f, 1f);
            _continueButton = _continueBtnRect.gameObject.AddComponent<Button>();
            _continueButton.targetGraphic = cImg;
            _continueButton.onClick.AddListener(ReturnToMap);
            TextMeshProUGUI cTxt = MakeText(_continueBtnRect, "Label", font, 24f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.05f, 1f));
            cTxt.alignment = TextAlignmentOptions.Center;
            cTxt.text = "CONTINUE  >";
        }

        private void BuildCampfireActionCard(RectTransform parent, string title, string subtitle, Sprite icon, Color accent, TMP_FontAsset font, out TextMeshProUGUI descOut, Action onClick)
        {
            int layer = parent.gameObject.layer;
            RectTransform card = CreateRect(parent, $"Action_{title}", layer);
            card.sizeDelta = new Vector2(380f, 360f);
            card.gameObject.AddComponent<CanvasGroup>();
            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = panelSprite != null ? panelSprite : GetSolid();
            bg.color = Color.white;

            Button btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick?.Invoke());
            _actionButtons.Add(btn);
            _actionCards.Add(card);

            // Hover lift
            RestHoverRelay relay = card.gameObject.AddComponent<RestHoverRelay>();
            relay.Init(
                () => { if (!btn.interactable) return; Tune.SFX(SfxID.hover); card.DOKill(); card.DOScale(1.06f, 0.16f).SetEase(Ease.OutBack).SetUpdate(true); },
                () => { card.DOKill(); card.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true); });

            // Circular icon emblem
            RectTransform ring = CreateRect(card, "Ring", layer);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.72f);
            ring.sizeDelta = new Vector2(106f, 106f);
            Image ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = GetCircle();
            ringImg.color = accent;
            ringImg.raycastTarget = false;

            RectTransform sock = CreateRect(ring, "Socket", layer);
            Stretch(sock, 6f);
            Image sockImg = sock.gameObject.AddComponent<Image>();
            sockImg.sprite = GetCircle();
            sockImg.color = new Color(0.14f, 0.09f, 0.06f, 1f);
            sockImg.raycastTarget = false;

            RectTransform icRt = CreateRect(sock, "Icon", layer);
            Stretch(icRt, 16f);
            Image icImg = icRt.gameObject.AddComponent<Image>();
            icImg.sprite = icon != null ? icon : GetCircle();
            icImg.color = accent;
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;

            TextMeshProUGUI t = MakeText(card, "Title", font, 28f, FontStyles.Bold, new Color(1f, 0.90f, 0.56f, 1f));
            t.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            t.rectTransform.anchorMax = new Vector2(1f, 0.55f);
            t.alignment = TextAlignmentOptions.Center;
            t.text = title;

            TextMeshProUGUI sub = MakeText(card, "Sub", font, 16f, FontStyles.Bold, accent);
            sub.rectTransform.anchorMin = new Vector2(0f, 0.34f);
            sub.rectTransform.anchorMax = new Vector2(1f, 0.43f);
            sub.alignment = TextAlignmentOptions.Center;
            sub.text = subtitle.ToUpper();

            descOut = MakeText(card, "Desc", font, 18.5f, FontStyles.Normal, new Color(0.94f, 0.88f, 0.76f, 0.96f));
            descOut.rectTransform.anchorMin = new Vector2(0f, 0.08f);
            descOut.rectTransform.anchorMax = new Vector2(1f, 0.33f);
            descOut.rectTransform.offsetMin = new Vector2(28f, 0f);
            descOut.rectTransform.offsetMax = new Vector2(-28f, 0f);
            descOut.alignment = TextAlignmentOptions.Top;
            descOut.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildSmithModalIfNeeded()
        {
            if (_smithModalRoot != null || _root == null) return;
            int layer = _root.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolid();

            _smithModalRoot = new GameObject("SmithUpgradeModal", typeof(RectTransform), typeof(Image));
            _smithModalRoot.layer = layer;
            RectTransform mRt = (RectTransform)_smithModalRoot.transform;
            mRt.SetParent(_root, false);
            Stretch(mRt);
            Image dim = _smithModalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.04f, 0.02f, 0.02f, 0.84f);

            RectTransform win = CreateRect(mRt, "Window", layer);
            win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
            win.sizeDelta = new Vector2(1380f, 820f);
            Image winBg = win.gameObject.AddComponent<Image>();
            winBg.sprite = panelSprite != null ? panelSprite : solid;
            winBg.color = Color.white;

            TextMeshProUGUI hdr = MakeText(win, "Header", font, 30f, FontStyles.Bold, new Color(1f, 0.88f, 0.48f, 1f));
            hdr.rectTransform.anchorMin = new Vector2(0f, 1f);
            hdr.rectTransform.anchorMax = new Vector2(1f, 1f);
            hdr.rectTransform.offsetMin = new Vector2(36f, -84f);
            hdr.rectTransform.offsetMax = new Vector2(-36f, -24f);
            hdr.alignment = TextAlignmentOptions.Center;
            hdr.text = "SMITH — SELECT A CARD TO UPGRADE";

            // Scrollable grid
            RectTransform vp = CreateRect(win, "Viewport", layer);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(44f, 96f);
            vp.offsetMax = new Vector2(-44f, -96f);
            vp.gameObject.AddComponent<RectMask2D>();
            ScrollRect sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;

            RectTransform content = CreateRect(vp, "Content", layer);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            GridLayoutGroup glg = content.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(220f, 300f);
            glg.spacing = new Vector2(24f, 24f);
            glg.padding = new RectOffset(24, 24, 16, 16);
            glg.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            _smithGridContent = content;

            _smithEmptyText = MakeText(win, "EmptyMsg", font, 22f, FontStyles.Italic, new Color(0.92f, 0.84f, 0.70f, 0.9f));
            _smithEmptyText.alignment = TextAlignmentOptions.Center;
            _smithEmptyText.text = "No cards in your current run deck have an upgrade recipe available.";
            _smithEmptyText.gameObject.SetActive(false);

            // Cancel button
            RectTransform closeRt = CreateRect(win, "CloseButton", layer);
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 28f);
            closeRt.sizeDelta = new Vector2(220f, 54f);
            Image clImg = closeRt.gameObject.AddComponent<Image>();
            clImg.sprite = buttonSprite != null ? buttonSprite : solid;
            clImg.color = new Color(0.92f, 0.82f, 0.64f, 1f);
            Button clBtn = closeRt.gameObject.AddComponent<Button>();
            clBtn.onClick.AddListener(CloseSmithPicker);
            TextMeshProUGUI clTxt = MakeText(closeRt, "Label", font, 21f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.05f, 1f));
            clTxt.alignment = TextAlignmentOptions.Center;
            clTxt.text = "CANCEL";

            // Side-by-Side Card -> Card+ Preview Confirmation Overlay
            _previewOverlay = new GameObject("UpgradePreviewOverlay", typeof(RectTransform), typeof(Image));
            _previewOverlay.layer = layer;
            RectTransform pRt = (RectTransform)_previewOverlay.transform;
            pRt.SetParent(mRt, false);
            Stretch(pRt);
            Image pDim = _previewOverlay.GetComponent<Image>();
            pDim.sprite = solid;
            pDim.color = new Color(0.03f, 0.02f, 0.02f, 0.88f);

            RectTransform pBox = CreateRect(pRt, "PreviewBox", layer);
            pBox.anchorMin = pBox.anchorMax = new Vector2(0.5f, 0.5f);
            pBox.sizeDelta = new Vector2(900f, 560f);
            Image pbImg = pBox.gameObject.AddComponent<Image>();
            pbImg.sprite = panelSprite != null ? panelSprite : solid;
            pbImg.color = Color.white;

            _previewTitle = MakeText(pBox, "PreviewTitle", font, 28f, FontStyles.Bold, new Color(1f, 0.88f, 0.48f, 1f));
            _previewTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _previewTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _previewTitle.rectTransform.offsetMin = new Vector2(24f, -74f);
            _previewTitle.rectTransform.offsetMax = new Vector2(-24f, -20f);
            _previewTitle.alignment = TextAlignmentOptions.Center;

            _previewBeforeHolder = CreateRect(pBox, "BeforeHolder", layer);
            _previewBeforeHolder.anchorMin = _previewBeforeHolder.anchorMax = new Vector2(0.28f, 0.53f);
            _previewBeforeHolder.sizeDelta = new Vector2(220f, 300f);

            TextMeshProUGUI arrow = MakeText(pBox, "Arrow", font, 48f, FontStyles.Bold, new Color(1f, 0.84f, 0.34f, 1f));
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.53f);
            arrow.rectTransform.sizeDelta = new Vector2(100f, 80f);
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.text = "->";

            _previewAfterHolder = CreateRect(pBox, "AfterHolder", layer);
            _previewAfterHolder.anchorMin = _previewAfterHolder.anchorMax = new Vector2(0.72f, 0.53f);
            _previewAfterHolder.sizeDelta = new Vector2(220f, 300f);

            RectTransform confirmRt = CreateRect(pBox, "ConfirmUpgradeBtn", layer);
            confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.36f, 0f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(0f, 34f);
            confirmRt.sizeDelta = new Vector2(230f, 58f);
            Image cfImg = confirmRt.gameObject.AddComponent<Image>();
            cfImg.sprite = buttonSprite != null ? buttonSprite : solid;
            cfImg.color = new Color(0.52f, 0.90f, 0.54f, 1f);
            confirmRt.gameObject.AddComponent<Button>().onClick.AddListener(() => ConfirmSmithUpgrade());
            TextMeshProUGUI cfTxt = MakeText(confirmRt, "Label", font, 22f, FontStyles.Bold, new Color(0.10f, 0.24f, 0.08f, 1f));
            cfTxt.alignment = TextAlignmentOptions.Center;
            cfTxt.text = "FORGE UPGRADE";

            RectTransform backRt = CreateRect(pBox, "BackBtn", layer);
            backRt.anchorMin = backRt.anchorMax = new Vector2(0.64f, 0f);
            backRt.pivot = new Vector2(0.5f, 0f);
            backRt.anchoredPosition = new Vector2(0f, 34f);
            backRt.sizeDelta = new Vector2(180f, 58f);
            Image bkImg = backRt.gameObject.AddComponent<Image>();
            bkImg.sprite = buttonSprite != null ? buttonSprite : solid;
            bkImg.color = new Color(0.92f, 0.82f, 0.64f, 1f);
            backRt.gameObject.AddComponent<Button>().onClick.AddListener(() => _previewOverlay.SetActive(false));
            TextMeshProUGUI bkTxt = MakeText(backRt, "Label", font, 21f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.05f, 1f));
            bkTxt.alignment = TextAlignmentOptions.Center;
            bkTxt.text = "BACK";

            _previewOverlay.SetActive(false);
            _smithModalRoot.SetActive(false);
        }

        private void PopulateSmithGrid()
        {
            foreach (GameObject g in _pickerSpawned) if (g != null) Destroy(g);
            _pickerSpawned.Clear();

            List<CardUpgradeRecipe> available = GetAvailableUpgradesForCurrentDeck();
            _smithEmptyText.gameObject.SetActive(available.Count == 0);

            int layer = _smithGridContent.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;

            for (int i = 0; i < available.Count; i++)
            {
                CardUpgradeRecipe recipe = available[i];
                RectTransform cell = CreateRect(_smithGridContent, $"UpgradeCell_{i:00}", layer);
                cell.sizeDelta = new Vector2(220f, 300f);
                Image hit = cell.gameObject.AddComponent<Image>();
                hit.sprite = GetSolid();
                hit.color = new Color(1f, 1f, 1f, 0.001f);

                if (cardViewPrefab != null)
                {
                    RunDeckCardView view = Instantiate(cardViewPrefab, cell);
                    view.Setup(recipe.BaseCard, 1, i);
                    FitCard(view, 188f, 248f);
                }

                TextMeshProUGUI lbl = MakeText(cell, "UpgradeLabel", font, 16f, FontStyles.Bold, new Color(1f, 0.88f, 0.44f, 1f));
                lbl.rectTransform.anchorMin = new Vector2(0f, 0f);
                lbl.rectTransform.anchorMax = new Vector2(1f, 0.14f);
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.text = $"-> {recipe.UpgradedCard.Title}";

                Button btn = cell.gameObject.AddComponent<Button>();
                CardUpgradeRecipe captured = recipe;
                btn.onClick.AddListener(() => ShowUpgradePreview(captured));
                _pickerSpawned.Add(cell.gameObject);
            }
        }

        private void ShowUpgradePreview(CardUpgradeRecipe recipe)
        {
            _selectedRecipe = recipe;
            if (_previewOverlay == null || recipe == null) return;
            _previewTitle.text = $"{recipe.BaseCard.Title}   ->   {recipe.UpgradedCard.Title}";

            if (_previewBeforeView == null && cardViewPrefab != null)
            {
                _previewBeforeView = Instantiate(cardViewPrefab, _previewBeforeHolder);
            }
            if (_previewAfterView == null && cardViewPrefab != null)
            {
                _previewAfterView = Instantiate(cardViewPrefab, _previewAfterHolder);
            }

            if (_previewBeforeView != null) { _previewBeforeView.Setup(recipe.BaseCard, 1, 0); FitCard(_previewBeforeView, 198f, 266f); }
            if (_previewAfterView != null) { _previewAfterView.Setup(recipe.UpgradedCard, 1, 1); FitCard(_previewAfterView, 208f, 278f); }

            _previewOverlay.SetActive(true);
            _previewOverlay.transform.SetAsLastSibling();
            Tune.SFX(SfxID.hover);
        }

        private TextMeshProUGUI MakeStatPill(RectTransform parent, string name, TMP_FontAsset font, Color textColor, float width)
        {
            RectTransform rt = CreateRect(parent, name, parent.gameObject.layer);
            rt.sizeDelta = new Vector2(width, 48f);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = buttonSprite != null ? buttonSprite : GetSolid();
            bg.color = new Color(0.22f, 0.14f, 0.08f, 0.95f);
            TextMeshProUGUI txt = MakeText(rt, "Label", font, 20f, FontStyles.Bold, textColor);
            txt.alignment = TextAlignmentOptions.Center;
            return txt;
        }

        private static void FitCard(RunDeckCardView view, float targetW, float targetH)
        {
            if (view == null) return;
            RectTransform rt = view.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.54f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = Vector2.zero;

            CanvasGroup cg = view.GetComponent<CanvasGroup>();
            if (cg != null) { cg.interactable = false; cg.blocksRaycasts = false; }

            float rawW = 120f, rawH = 165f;
            Image largest = null;
            float maxArea = 0f;
            foreach (Image img in view.GetComponentsInChildren<Image>(true))
            {
                Rect r = img.rectTransform.rect;
                float a = Mathf.Abs(r.width * r.height);
                if (a > maxArea && r.width > 20f && r.height > 20f) { maxArea = a; largest = img; }
            }
            if (largest != null)
            {
                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(rt, largest.rectTransform);
                if (b.size.x > 20f && b.size.y > 20f) { rawW = b.size.x; rawH = b.size.y; }
            }
            float s = Mathf.Clamp(Mathf.Min(targetW / rawW, targetH / rawH), 0.5f, 2.2f);
            rt.localScale = new Vector3(s, s, 1f);
        }

        private static void EnsureRuntimeManagers()
        {
            if (RunManager.Instance == null)
            {
                var go = new GameObject("Run Manager");
                go.AddComponent<RunManager>();
            }
            if (RunManager.Instance != null && !RunManager.Instance.HasHeroState)
            {
                RunManager.Instance.InitializeHeroState(50, 100);
                RunManager.Instance.SetHeroHealth(32);
                RunManager.Instance.AddStress(45);
            }
            if (RunDeckManager.Instance == null)
            {
                new GameObject("Run Deck Manager").AddComponent<RunDeckManager>();
            }
            RunRelicManager.EnsureInstance();
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

        private static TextMeshProUGUI MakeText(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            RectTransform rt = CreateRect(parent, name, parent.gameObject.layer);
            Stretch(rt);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private static Sprite GetSolid()
        {
            if (s_solid != null) return s_solid;
            Texture2D tex = Texture2D.whiteTexture;
            s_solid = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return s_solid;
        }

        private static Sprite GetCircle()
        {
            if (s_circle != null) return s_circle;
            const int sz = 96;
            Texture2D tex = new(sz, sz, TextureFormat.RGBA32, false);
            float h = (sz - 1) * 0.5f;
            Color32[] px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(h - 1.5f - Vector2.Distance(new Vector2(x, y), new Vector2(h, h)) + 0.8f) * 255f));
            tex.SetPixels32(px);
            tex.Apply();
            s_circle = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return s_circle;
        }

        private static Sprite GetGlow()
        {
            if (s_glow != null) return s_glow;
            const int sz = 96;
            Texture2D tex = new(sz, sz, TextureFormat.RGBA32, false);
            float h = (sz - 1) * 0.5f;
            Color32[] px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Pow(1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(h, h)) / h), 2.1f) * 255f));
            tex.SetPixels32(px);
            tex.Apply();
            s_glow = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return s_glow;
        }
    }

    internal sealed class RestHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action _enter;
        private Action _exit;
        public void Init(Action enter, Action exit) { _enter = enter; _exit = exit; }
        public void OnPointerEnter(PointerEventData _) => _enter?.Invoke();
        public void OnPointerExit(PointerEventData _) => _exit?.Invoke();
    }
}

