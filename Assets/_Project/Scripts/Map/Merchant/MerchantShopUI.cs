using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Rewards;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI;
using PlushieOdyssey.UI.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlushieOdyssey.Map.Merchant
{
    [Serializable]
    public sealed class MerchantRelicOffer
    {
        public RelicData Relic { get; }
        public int Price { get; }
        public bool IsSold { get; private set; }

        public MerchantRelicOffer(RelicData relic, int price)
        {
            Relic = relic;
            Price = Mathf.Max(0, price);
        }

        public void MarkSold()
        {
            IsSold = true;
        }
    }

    /// <summary>
    /// Slay-the-Spire / Crazy-Dave inspired Merchant Shop UI controller.
    /// Left side: Map exit + Gold + Equipped Relics, Item Description inspector,
    /// animated talking Vendor character, and native-styled typewriter dialogue box.
    /// Right side: Framed stall (Panel.png) with 2x4 card grid, 3 relic pedestals beside
    /// an escalating-cost circular RESET medallion with procedural restock choreography.
    /// </summary>
    [DisallowMultipleComponent]
    public class MerchantShopUI : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        [Header("Scene & Data References")]
        [SerializeField] private Canvas targetCanvas;
        [Tooltip("Baked editable MerchantShopRoot inside the scene. When assigned (or found under targetCanvas), Play Mode binds directly to your edited scene hierarchy instead of rebuilding from code.")]
        [SerializeField] private RectTransform bakedShopRoot;
        [SerializeField] private MerchantCardPool cardPool;
        [SerializeField] private RelicRewardPool relicPool;
        [SerializeField] private RunDeckCardView cardViewPrefab;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private string mapSceneName = "Map";

        [Header("Stock & Pricing")]
        [SerializeField, Min(1)] private int cardOfferCount = 8;
        [SerializeField, Min(1)] private int relicOfferCount = 3;
        [SerializeField] private bool includeUpgrades = false;
        [SerializeField] private bool allowDuplicateOffers = false;
        [SerializeField, Min(0)] private int fallbackRelicPrice = 150;
        [SerializeField, Min(0)] private int standaloneTestGold = 250;

        [Header("Reset Button Pricing (Escalating)")]
        [SerializeField, Min(0)] private int baseResetCost = 10;
        [SerializeField, Min(0)] private int resetCostIncrement = 10;
        [SerializeField, Min(0)] private int resetCostAcceleration = 0;

        [Header("Card Removal Service")]
        [SerializeField, Min(0)] private int baseRemovalCost = 75;
        [SerializeField, Min(0)] private int removalCostIncrement = 25;

        [Header("Existing Art Assets")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite stallPanelSprite;
        [SerializeField] private Sprite dialogueBoxSprite;
        [SerializeField] private Sprite nameplateSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite pillButtonSprite;
        [SerializeField] private Sprite merchantIconSprite;
        [SerializeField] private Sprite vendorSprite;

        [Header("Persistent Baked UI Sprites (Editable Assets)")]
        [SerializeField] private Sprite solidSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite circleRingSprite;
        [SerializeField] private Sprite softGlowSprite;
        [SerializeField] private Sprite stampBorderSprite;
        [SerializeField] private Sprite refreshIconSprite;

        [Header("Vendor Identity & Dialogue")]
        [SerializeField] private string vendorName = "MERCHANT";
        [SerializeField] private string shopBannerTitle = "MERCHANT'S WARES";
        [SerializeField, Range(15f, 90f)] private float typewriterCharsPerSecond = 46f;
        [SerializeField] private float hoverQuipCooldown = 3.2f;

        [SerializeField] private string[] greetingLines =
        {
            "Welcome, traveler! Fresh cards, shiny relics—everything's priced to move!",
            "Step right up! Finest wares in the land, guaranteed mostly uncursed!",
            "Gold burning a hole in your pocket? Browse my cards, relics, or spin RESET for fresh stock!"
        };

        [SerializeField] private string[] hoverCardLines =
        {
            "Ah, '{0}'! A crowd favorite for just {1}g.",
            "Got your eye on '{0}'? That one's saved plenty of runs!",
            "'{0}' for {1}g—an absolute steal if you ask me!"
        };

        [SerializeField] private string[] hoverRelicLines =
        {
            "Ooh, '{0}'! A genuine relic—its blessing lasts the whole run!",
            "Careful with '{0}'—it's humming with magic! Only {1}g.",
            "'{0}'? Collectors travel miles for a trinket like that!"
        };

        [SerializeField] private string[] buyCardLines =
        {
            "Sold! '{0}' is all yours—put it to good use!",
            "Pleasure doing business! '{0}' slides right into your deck!",
            "Smart pick! May '{0}' turn the tide of battle!"
        };

        [SerializeField] private string[] buyRelicLines =
        {
            "A true collector's eye! '{0}' looks great on you!",
            "Magnificent choice! '{0}' will serve you well!",
            "Sold! Handle '{0}' with pride, adventurer!"
        };

        [SerializeField] private string[] cannotAffordLines =
        {
            "Whoa there, light pockets! You need {0}g for that one.",
            "No credit, friend! Bring back {0}g and it's yours.",
            "Short on coin! You'll need {0}g to seal that deal."
        };

        [SerializeField] private string[] relicFullLines =
        {
            "Your relic belt is full! Swap one out to claim '{0}'.",
            "All 5 relic slots packed! Make room in your belt for '{0}'."
        };

        [SerializeField] private string[] resetLines =
        {
            "Out with the old, in with the NEW! Next restock will run you {0}g!",
            "Presto—brand new stock! Careful, the restock fee just climbed to {0}g!",
            "Fresh wares from the back room! Next spin of the wheel is {0}g!"
        };

        [SerializeField] private string[] vendorPokeLines =
        {
            "Hey, careful—I'm ticklish! Buy a card instead!",
            "Not happy with the shelf? Hit RESET for {0}g and see what rolls in!",
            "Everything's hand-picked fresh every morning. Well, mostly!"
        };

        // ------------------------------------------------------------------
        // Runtime UI Models
        // ------------------------------------------------------------------
        private sealed class CardSlotUI
        {
            public int Index;
            public RectTransform Root;
            public RectTransform ContentRoot;
            public CanvasGroup ContentGroup;
            public Image HoverGlow;
            public Image ShimmerOverlay;
            public RectTransform CardContainer;
            public RunDeckCardView CardView;
            public TextMeshProUGUI PriceText;
            public Image PricePill;
            public RectTransform SoldStampRoot;
            public CanvasGroup SoldStampGroup;
            public MerchantOffer Offer;
            public Vector2 RestContentPos;
            public bool IsHovered;
        }

        private sealed class RelicSlotUI
        {
            public int Index;
            public RectTransform Root;
            public RectTransform ContentRoot;
            public CanvasGroup ContentGroup;
            public Image FrameBg;
            public Image HoverGlow;
            public Image ShimmerOverlay;
            public Image IconRing;
            public Image IconImage;
            public TextMeshProUGUI MonogramText;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI PriceText;
            public Image PricePill;
            public RectTransform SoldStampRoot;
            public CanvasGroup SoldStampGroup;
            public MerchantRelicOffer Offer;
            public Vector2 RestContentPos;
            public bool IsHovered;
        }

        private sealed class EquippedMiniSlotUI
        {
            public Image Border;
            public Image Bg;
            public Image Icon;
            public TextMeshProUGUI Monogram;
            public RectTransform Root;
        }

        private sealed class FloatingMote
        {
            public RectTransform Rect;
            public Image Image;
            public float BaseX;
            public float SpeedY;
            public float SwayAmp;
            public float SwayFreq;
            public float Phase;
            public float BaseAlpha;
        }

        // Static procedural sprites (cached across reloads/instances)
        private static Sprite s_solidSprite;
        private static Sprite s_circleSprite;
        private static Sprite s_circleRingSprite;
        private static Sprite s_softGlowSprite;
        private static Sprite s_refreshIconSprite;
        private static Sprite s_stampBorderSprite;

        // State
        private readonly List<MerchantOffer> _activeCardOffers = new();
        private readonly List<MerchantRelicOffer> _activeRelicOffers = new();
        private readonly List<CardSlotUI> _cardSlots = new();
        private readonly List<RelicSlotUI> _relicSlots = new();
        private readonly List<EquippedMiniSlotUI> _equippedMiniSlots = new();
        private readonly List<FloatingMote> _motes = new();

        private RectTransform _shopRoot;
        private RectTransform _stallRoot;
        private RectTransform _leftColumnRoot;
        private RectTransform _floatingPopupRoot;

        // Top-left bar
        private Button _leaveButton;
        private RectTransform _goldPillRect;
        private TextMeshProUGUI _goldValueText;
        private TextMeshProUGUI _deckBtnLabel;
        private TextMeshProUGUI _removeBtnLabel;
        private RectTransform _removeBtnRect;
        private GameObject _deckModalRoot;
        private Transform _deckModalGrid;
        private TextMeshProUGUI _deckModalTitle;
        private TextMeshProUGUI _deckModalEmpty;
        private readonly List<GameObject> _deckModalCards = new();
        private bool _deckModalIsRemovalMode;
        private int _removalCount;

        // Item desc inspector
        private RectTransform _inspectorRoot;
        private Image _inspectorBorderGlow;
        private TextMeshProUGUI _inspectorTitleText;
        private TextMeshProUGUI _inspectorMetaText;
        private TextMeshProUGUI _inspectorPriceText;
        private TextMeshProUGUI _inspectorBodyText;

        // Vendor & Dialogue
        private RectTransform _vendorFloatRoot;
        private RectTransform _vendorAnimRoot;
        private Vector2 _vendorFloatBasePos;
        private Vector2 _vendorAnimBasePos;
        private Image _vendorImage;
        private Image _vendorSpotlight;
        private RectTransform _dialogueBoxRoot;
        private TextMeshProUGUI _dialogueText;
        private RectTransform _dialogueDoneDiamond;
        private string _fullDialogueLine = string.Empty;
        private float _dialogueCharProgress;
        private bool _isTypingDialogue;
        private float _nextHoverQuipAllowedTime;
        private float _vendorIdleTime;
        private float _talkBounceTimer;

        // Reset Button
        private RectTransform _resetButtonRoot;
        private RectTransform _resetMedallionSpinRect;
        private Image _resetMedallionBg;
        private Image _resetHoverGlow;
        private Image _resetIconImage;
        private Image _resetPulseRing;
        private RectTransform _resetPricePillRect;
        private Image _resetPricePillBg;
        private TextMeshProUGUI _resetPriceText;
        private Button _resetButton;

        // Pending 5-slot full relic swap tracking
        private RelicSwapModalUI _relicSwapModal;
        private RelicSlotUI _pendingRelicSlot;
        private MerchantRelicOffer _pendingRelicOffer;
        private bool _awaitingRelicSwapResolution;

        // Counters & flags
        private int _resetCount;
        private bool _isRestocking;
        private bool _isBuilt;
        private RunManager _subscribedRunManager;
        private RunRelicManager _subscribedRelicManager;
        private Sequence _activeResetSequence;
        private Sequence _activeEntranceSequence;
        private Sequence _vendorActionSequence;

        // Public properties for inspection & verification
        public int ResetCount => _resetCount;
        public int CurrentResetCost => ComputeResetCost(_resetCount);
        public int CurrentRemovalCost => Mathf.Max(0, baseRemovalCost + _removalCount * Mathf.Max(0, removalCostIncrement));
        public bool IsRestocking => _isRestocking;
        public IReadOnlyList<MerchantOffer> ActiveCardOffers => _activeCardOffers;
        public IReadOnlyList<MerchantRelicOffer> ActiveRelicOffers => _activeRelicOffers;
        public string CurrentVendorLine => _fullDialogueLine;
        public string CurrentInspectorTitle => _inspectorTitleText != null ? _inspectorTitleText.text : string.Empty;
        public string CurrentInspectorBody => _inspectorBodyText != null ? _inspectorBodyText.text : string.Empty;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------
        private void Awake()
        {
            EnsureRuntimeManagers();
            EnsureCanvas();
            EnsureRelicSwapModal();
            BuildShopUIIfNeeded();
        }

        private void OnEnable()
        {
            SubscribeManagers();
        }

        private void Start()
        {
            SubscribeManagers();
            Canvas.ForceUpdateCanvases();
            PopulateStock(avoidPreviousStock: false);
            RefreshGoldDisplay(animate: false);
            RefreshEquippedRelicStrip(highlightIndex: -1);
            RefreshResetButtonVisuals(animatePrice: false);
            ClearInspector();
            PlayShopEntranceAnimation();
            SayVendorLine(PickRandom(greetingLines), playTalkAnim: true);
            Tune.SFX(SfxID.shop_open);
        }

        private void OnDisable()
        {
            UnsubscribeManagers();
            KillActiveTweens();
        }

        private void OnDestroy()
        {
            UnsubscribeManagers();
            KillActiveTweens();
        }

        private void Update()
        {
            if (_subscribedRunManager != RunManager.Instance || _subscribedRelicManager != RunRelicManager.Instance)
            {
                UnsubscribeManagers();
                SubscribeManagers();
                RefreshGoldDisplay(animate: false);
                RefreshEquippedRelicStrip(highlightIndex: -1);
            }

            UpdateTypewriterDialogue(Time.unscaledDeltaTime);
            UpdateVendorProceduralMotion(Time.unscaledDeltaTime);
            UpdateAmbientMotes(Time.unscaledDeltaTime);
            CheckPendingRelicSwapCompletion();
        }

        // ------------------------------------------------------------------
        // Public Shop Operations
        // ------------------------------------------------------------------
        public int ComputeResetCost(int resetNumber)
        {
            int n = Mathf.Max(0, resetNumber);
            int linear = n * Mathf.Max(0, resetCostIncrement);
            int accel = (n * (n - 1) / 2) * Mathf.Max(0, resetCostAcceleration);
            return Mathf.Max(0, baseResetCost + linear + accel);
        }

        public bool TryBuyCardAt(int slotIndex)
        {
            if (_isRestocking || slotIndex < 0 || slotIndex >= _cardSlots.Count) return false;
            CardSlotUI slot = _cardSlots[slotIndex];
            MerchantOffer offer = slot.Offer;
            if (offer == null || offer.IsSold) return false;

            EnsureRuntimeManagers();
            if (!CanAfford(offer.Price))
            {
                HandleCannotAfford(slot.ContentRoot, slot.PriceText, offer.Price);
                return false;
            }

            bool applied = ApplyCardOfferToDeck(offer);
            if (!applied) return false;

            if (!SpendGold(offer.Price, slot.Root)) return false;

            offer.MarkSold();
            Tune.SFX(SfxID.shop_buy);
            PlaySoldStampAnimation(slot.SoldStampRoot, slot.SoldStampGroup, slot.ContentRoot, slot.ContentGroup);
            PlayVendorHappyJump();
            SayVendorLine(string.Format(PickRandom(buyCardLines), offer.GetTitle()), playTalkAnim: true);
            RefreshAllAffordability();
            InspectCardAt(slotIndex);
            return true;
        }

        public bool TryBuyRelicAt(int slotIndex)
        {
            if (_isRestocking || slotIndex < 0 || slotIndex >= _relicSlots.Count) return false;
            RelicSlotUI slot = _relicSlots[slotIndex];
            MerchantRelicOffer offer = slot.Offer;
            if (offer == null || offer.Relic == null || offer.IsSold) return false;

            EnsureRuntimeManagers();
            RunRelicManager relicManager = RunRelicManager.EnsureInstance();

            if (offer.Relic.Unique && relicManager.Contains(offer.Relic))
            {
                ShakeRect(slot.ContentRoot);
                PlayVendorHeadShake();
                Tune.SFX(SfxID.cannot_afford);
                SayVendorLine($"You're already carrying '{offer.Relic.Title}'—one is plenty!", playTalkAnim: true);
                return false;
            }

            if (!CanAfford(offer.Price))
            {
                HandleCannotAfford(slot.ContentRoot, slot.PriceText, offer.Price);
                return false;
            }

            if (relicManager.IsFull)
            {
                // Hold gold until the player confirms swapping in RelicSwapModalUI!
                EnsureRelicSwapModal();
                _pendingRelicSlot = slot;
                _pendingRelicOffer = offer;
                _awaitingRelicSwapResolution = true;
                relicManager.AddRelic(offer.Relic);
                PlayVendorTalkAccent();
                SayVendorLine(string.Format(PickRandom(relicFullLines), offer.Relic.Title), playTalkAnim: true);
                return false;
            }

            bool added = relicManager.AddRelic(offer.Relic);
            if (!added) return false;

            return FinalizeRelicPurchase(slot, offer);
        }

        public bool TryResetShop()
        {
            if (_isRestocking) return false;

            EnsureRuntimeManagers();
            int cost = CurrentResetCost;
            if (!CanAfford(cost))
            {
                HandleCannotAfford(_resetButtonRoot, _resetPriceText, cost);
                return false;
            }

            if (!SpendGold(cost, _resetButtonRoot)) return false;

            _resetCount++;
            int nextCost = CurrentResetCost;
            RefreshResetButtonVisuals(animatePrice: true);
            PlayResetChoreography(nextCost);
            return true;
        }

        public void InspectCardAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _cardSlots.Count) return;
            MerchantOffer offer = _cardSlots[slotIndex].Offer;
            if (offer == null || offer.PreviewCard == null) return;

            CardData card = offer.PreviewCard;
            string statusTag = offer.IsSold ? "SOLD OUT" : $"{card.Type.ToString().ToUpper()} • {card.Tier.ToString().ToUpper()}";
            string priceStr = offer.IsSold ? "SOLD" : $"{offer.Price}g";
            Color priceColor = offer.IsSold
                ? new Color(0.76f, 0.20f, 0.18f, 1f)
                : (CanAfford(offer.Price) ? new Color(0.55f, 0.35f, 0.04f, 1f) : new Color(0.76f, 0.18f, 0.16f, 1f));

            string keywordLine = card.Keywords != CardKeywords.None ? $"\n<color=#6A3E14>Keywords: {card.Keywords}</color>" : string.Empty;
            string manaLabel = card.IsXCost
                ? (card.XBonus > 0 ? $"X (All Remaining, +{card.XBonus} Bonus)" : "X (All Remaining)")
                : card.Mana.ToString();
            string manaLine = $"<color=#1E4F7A><b>Mana Cost: {manaLabel}</b></color>";
            string desc = string.IsNullOrWhiteSpace(card.Description) ? "No description." : card.Description;

            SetInspectorContent(card.Title, statusTag, priceStr, priceColor, $"{manaLine}\n{desc}{keywordLine}");
        }

        public void InspectRelicAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _relicSlots.Count) return;
            MerchantRelicOffer offer = _relicSlots[slotIndex].Offer;
            if (offer == null || offer.Relic == null) return;

            RelicData relic = offer.Relic;
            string statusTag = offer.IsSold ? "SOLD OUT" : $"RELIC • {relic.Rarity.ToString().ToUpper()}";
            string priceStr = offer.IsSold ? "SOLD" : $"{offer.Price}g";
            Color priceColor = offer.IsSold
                ? new Color(0.76f, 0.20f, 0.18f, 1f)
                : (CanAfford(offer.Price) ? new Color(0.55f, 0.35f, 0.04f, 1f) : new Color(0.76f, 0.18f, 0.16f, 1f));

            string desc = string.IsNullOrWhiteSpace(relic.Description) ? "Passive run relic." : relic.Description;
            string uniqueNote = relic.Unique ? "\n<color=#5A4226><i>Unique Relic • Active all run</i></color>" : string.Empty;
            SetInspectorContent(relic.Title, statusTag, priceStr, priceColor, $"{desc}{uniqueNote}");
        }

        public void InspectResetButton()
        {
            int cost = CurrentResetCost;
            int nextCost = ComputeResetCost(_resetCount + 1);
            Color priceColor = CanAfford(cost) ? new Color(0.55f, 0.35f, 0.04f, 1f) : new Color(0.76f, 0.18f, 0.16f, 1f);
            SetInspectorContent(
                "RESTOCK SHOP",
                "MERCHANT SERVICE",
                $"{cost}g",
                priceColor,
                $"Refresh all {cardOfferCount} cards and {relicOfferCount} relics with fresh inventory.\n<color=#6A3E14>Next restock after this: {nextCost}g</color>");
        }

        public void ClearInspector()
        {
            SetInspectorContent(
                "ITEM DETAILS",
                "HOVER TO INSPECT",
                string.Empty,
                new Color(0.45f, 0.31f, 0.14f, 1f),
                "Hover over any card, relic, or the RESET wheel to inspect its effect and gold cost.",
                animate: false);
        }

        public void PokeVendor()
        {
            if (_isRestocking) return;
            PlayVendorHappyJump();
            Tune.SFX(SfxID.dialogue_advance);
            SayVendorLine(string.Format(PickRandom(vendorPokeLines), CurrentResetCost), playTalkAnim: true);
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
        // Inventory Generation
        // ------------------------------------------------------------------
        private void PopulateStock(bool avoidPreviousStock)
        {
            HashSet<CardData> prevCards = new();
            HashSet<RelicData> prevRelics = new();

            if (avoidPreviousStock)
            {
                foreach (MerchantOffer o in _activeCardOffers)
                {
                    if (o?.PreviewCard != null) prevCards.Add(o.PreviewCard);
                }
                foreach (MerchantRelicOffer r in _activeRelicOffers)
                {
                    if (r?.Relic != null) prevRelics.Add(r.Relic);
                }
            }

            PopulateCardOffers(prevCards);
            PopulateRelicOffers(prevRelics);
            BindAllSlotsToActiveOffers();
        }

        private void PopulateCardOffers(HashSet<CardData> preferredAvoid)
        {
            _activeCardOffers.Clear();

            List<MerchantOffer> allCandidates = cardPool != null
                ? cardPool.BuildCandidateOffers(RunDeckManager.Instance, includeNewCards: true, includeUpgrades: includeUpgrades)
                : new List<MerchantOffer>();

            if (allCandidates.Count == 0) return;

            // Partition into fresh (not previously shown) and previously shown so Reset brings new wares first!
            List<MerchantOffer> fresh = new();
            List<MerchantOffer> repeats = new();
            foreach (MerchantOffer candidate in allCandidates)
            {
                if (preferredAvoid != null && candidate.PreviewCard != null && preferredAvoid.Contains(candidate.PreviewCard))
                    repeats.Add(candidate);
                else
                    fresh.Add(candidate);
            }

            ShuffleList(fresh);
            ShuffleList(repeats);

            List<MerchantOffer> ordered = new(fresh.Count + repeats.Count);
            ordered.AddRange(fresh);
            ordered.AddRange(repeats);

            int target = Mathf.Max(1, cardOfferCount);
            for (int i = 0; i < target; i++)
            {
                if (ordered.Count == 0)
                {
                    if (!allowDuplicateOffers || allCandidates.Count == 0) break;
                    MerchantOffer src = allCandidates[UnityEngine.Random.Range(0, allCandidates.Count)];
                    _activeCardOffers.Add(CloneOffer(src));
                    continue;
                }

                MerchantOffer picked = ordered[0];
                ordered.RemoveAt(0);
                _activeCardOffers.Add(picked);
            }
        }

        private void PopulateRelicOffers(HashSet<RelicData> preferredAvoid)
        {
            _activeRelicOffers.Clear();

            RunRelicManager relicManager = RunRelicManager.EnsureInstance();
            List<RelicData> candidates = relicPool != null
                ? relicPool.BuildCandidateRewards(relicManager, RelicRarityMask.All)
                : new List<RelicData>();

            if (candidates.Count == 0) return;

            List<RelicData> fresh = new();
            List<RelicData> repeats = new();
            foreach (RelicData relic in candidates)
            {
                if (relic == null) continue;
                if (preferredAvoid != null && preferredAvoid.Contains(relic))
                    repeats.Add(relic);
                else
                    fresh.Add(relic);
            }

            ShuffleList(fresh);
            ShuffleList(repeats);

            List<RelicData> ordered = new(fresh.Count + repeats.Count);
            ordered.AddRange(fresh);
            ordered.AddRange(repeats);

            int target = Mathf.Max(1, relicOfferCount);
            for (int i = 0; i < target && i < ordered.Count; i++)
            {
                RelicData relic = ordered[i];
                int price = relic.GoldValue > 0 ? relic.GoldValue : fallbackRelicPrice;
                _activeRelicOffers.Add(new MerchantRelicOffer(relic, price));
            }
        }

        private static MerchantOffer CloneOffer(MerchantOffer src)
        {
            if (src == null) return null;
            return src.Type == MerchantOfferType.UpgradeCard
                ? new MerchantOffer(src.UpgradeRecipe, src.Price)
                : new MerchantOffer(src.Card, src.Price);
        }

        private void BindAllSlotsToActiveOffers()
        {
            for (int i = 0; i < _cardSlots.Count; i++)
            {
                CardSlotUI slot = _cardSlots[i];
                if (i < _activeCardOffers.Count)
                {
                    slot.Offer = _activeCardOffers[i];
                    slot.Root.gameObject.SetActive(true);
                    if (slot.CardView != null)
                    {
                        slot.CardView.gameObject.SetActive(true);
                        slot.CardView.Setup(slot.Offer.PreviewCard, 1, i);
                        FitCardViewIntoContainer(slot.CardView, slot.CardContainer);
                    }
                    ResetSlotVisualState(slot.SoldStampRoot, slot.SoldStampGroup, slot.ContentRoot, slot.ContentGroup, slot.RestContentPos);
                }
                else
                {
                    slot.Offer = null;
                    slot.Root.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < _relicSlots.Count; i++)
            {
                RelicSlotUI slot = _relicSlots[i];
                if (i < _activeRelicOffers.Count && _activeRelicOffers[i].Relic != null)
                {
                    MerchantRelicOffer offer = _activeRelicOffers[i];
                    RelicData relic = offer.Relic;
                    slot.Offer = offer;
                    slot.Root.gameObject.SetActive(true);

                    Color rarityColor = TopBarRelicSlotsUI.GetRarityBorderColor(relic.Rarity, new Color(0.92f, 0.76f, 0.36f, 1f));
                    slot.IconRing.color = rarityColor;

                    bool hasImg = relic.Image != null;
                    slot.IconImage.enabled = hasImg;
                    if (hasImg)
                    {
                        slot.IconImage.sprite = relic.Image;
                        slot.IconImage.color = GetRelicIconTint(relic);
                    }
                    slot.MonogramText.gameObject.SetActive(!hasImg);
                    if (!hasImg)
                    {
                        slot.MonogramText.text = TopBarRelicSlotsUI.BuildMonogram(relic.Title);
                        slot.MonogramText.color = rarityColor;
                    }

                    slot.NameText.text = relic.Title;
                    ResetSlotVisualState(slot.SoldStampRoot, slot.SoldStampGroup, slot.ContentRoot, slot.ContentGroup, slot.RestContentPos);
                }
                else
                {
                    slot.Offer = null;
                    slot.Root.gameObject.SetActive(false);
                }
            }

            RefreshAllAffordability();
        }

        private static Color GetRelicIconTint(RelicData relic)
        {
            if (relic == null || relic.Image == null) return Color.white;
            string n = relic.Image.name;
            // Full-color existing icons keep natural white tint; crisp monochrome Status/Intent glyphs get rich gem tints!
            if (n.IndexOf("scroll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Icon_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Perk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.white;
            }

            return relic.Rarity switch
            {
                RelicRarity.Uncommon => new Color(0.54f, 0.94f, 0.72f, 1f),
                RelicRarity.Rare => new Color(1.00f, 0.84f, 0.38f, 1f),
                RelicRarity.Boss => new Color(1.00f, 0.56f, 0.44f, 1f),
                RelicRarity.Special => new Color(0.78f, 0.64f, 1.00f, 1f),
                _ => new Color(0.96f, 0.88f, 0.66f, 1f)
            };
        }

        private static void ResetSlotVisualState(RectTransform stampRoot, CanvasGroup stampGroup, RectTransform contentRoot, CanvasGroup contentGroup, Vector2 restPos)
        {
            if (stampRoot != null)
            {
                stampRoot.DOKill();
                stampRoot.gameObject.SetActive(false);
                stampRoot.localScale = Vector3.one;
            }
            if (stampGroup != null)
            {
                stampGroup.DOKill();
                stampGroup.alpha = 0f;
            }
            if (contentRoot != null)
            {
                contentRoot.anchoredPosition = restPos;
                contentRoot.localRotation = Quaternion.identity;
            }
            if (contentGroup != null)
            {
                contentGroup.DOKill();
                contentGroup.alpha = 1f;
            }
        }

        // ------------------------------------------------------------------
        // Purchase & Gold Helpers
        // ------------------------------------------------------------------
        private bool FinalizeRelicPurchase(RelicSlotUI slot, MerchantRelicOffer offer)
        {
            if (!SpendGold(offer.Price, slot.Root)) return false;

            offer.MarkSold();
            Tune.SFX(SfxID.relic_claim);
            PlaySoldStampAnimation(slot.SoldStampRoot, slot.SoldStampGroup, slot.ContentRoot, slot.ContentGroup);
            PlayVendorHappyJump();
            SayVendorLine(string.Format(PickRandom(buyRelicLines), offer.Relic.Title), playTalkAnim: true);

            int equippedIdx = FindEquippedSlotIndex(offer.Relic);
            RefreshEquippedRelicStrip(equippedIdx);
            RefreshAllAffordability();
            InspectRelicAt(slot.Index);
            return true;
        }

        private void CheckPendingRelicSwapCompletion()
        {
            if (!_awaitingRelicSwapResolution) return;
            if (_relicSwapModal != null && _relicSwapModal.IsOpen) return;

            // Modal closed — check if the player equipped the pending relic!
            _awaitingRelicSwapResolution = false;
            RelicSlotUI slot = _pendingRelicSlot;
            MerchantRelicOffer offer = _pendingRelicOffer;
            _pendingRelicSlot = null;
            _pendingRelicOffer = null;

            if (slot == null || offer == null || offer.Relic == null || offer.IsSold) return;

            RunRelicManager mgr = RunRelicManager.Instance;
            if (mgr != null && mgr.Contains(offer.Relic))
            {
                FinalizeRelicPurchase(slot, offer);
            }
            else
            {
                SayVendorLine("Changed your mind? No problem—that relic's staying right here!", playTalkAnim: true);
            }
        }

        private bool ApplyCardOfferToDeck(MerchantOffer offer)
        {
            if (RunDeckManager.Instance == null) return false;
            if (offer.Type == MerchantOfferType.NewCard && offer.Card != null)
            {
                RunDeckManager.Instance.AddCard(offer.Card);
                return true;
            }

            if (offer.Type == MerchantOfferType.UpgradeCard && offer.UpgradeRecipe != null)
            {
                return RunDeckManager.Instance.ReplaceFirst(offer.UpgradeRecipe.BaseCard, offer.UpgradeRecipe.UpgradedCard);
            }

            return false;
        }

        private bool CanAfford(int price)
        {
            if (price <= 0) return true;
            return RunManager.Instance != null && RunManager.Instance.Gold >= price;
        }

        private bool SpendGold(int amount, RectTransform sourceRect)
        {
            if (amount <= 0) return true;
            if (RunManager.Instance == null || !RunManager.Instance.SpendGold(amount)) return false;

            RefreshGoldDisplay(animate: true);
            SpawnFloatingCostPopup(_goldPillRect, $"-{amount}g", new Color(1f, 0.84f, 0.34f, 1f));
            if (sourceRect != null && sourceRect != _goldPillRect)
            {
                SpawnFloatingCostPopup(sourceRect, $"-{amount}g", new Color(1f, 0.86f, 0.38f, 1f));
            }
            return true;
        }

        private void HandleCannotAfford(RectTransform targetRect, TextMeshProUGUI priceLabel, int neededPrice)
        {
            Tune.SFX(SfxID.cannot_afford);
            ShakeRect(targetRect);
            ShakeRect(_goldPillRect);
            PlayVendorHeadShake();

            if (priceLabel != null)
            {
                priceLabel.DOKill();
                Color orig = priceLabel.color;
                priceLabel.color = new Color(1f, 0.25f, 0.22f, 1f);
                priceLabel.DOColor(orig, 0.45f).SetUpdate(true);
            }

            SayVendorLine(string.Format(PickRandom(cannotAffordLines), neededPrice), playTalkAnim: true);
        }

        private void RefreshAllAffordability()
        {
            Color affordableGold = new(1f, 0.89f, 0.46f, 1f);
            Color unaffordableRed = new(1f, 0.45f, 0.42f, 1f);
            Color soldMuted = new(0.68f, 0.56f, 0.46f, 0.72f);

            foreach (CardSlotUI slot in _cardSlots)
            {
                if (slot.Offer == null || slot.PriceText == null) continue;
                slot.PriceText.DOKill();
                if (slot.Offer.IsSold)
                {
                    slot.PriceText.text = "SOLD";
                    slot.PriceText.color = soldMuted;
                }
                else
                {
                    slot.PriceText.text = $"{slot.Offer.Price}g";
                    slot.PriceText.color = CanAfford(slot.Offer.Price) ? affordableGold : unaffordableRed;
                }
            }

            foreach (RelicSlotUI slot in _relicSlots)
            {
                if (slot.Offer == null || slot.PriceText == null) continue;
                slot.PriceText.DOKill();
                if (slot.Offer.IsSold)
                {
                    slot.PriceText.text = "SOLD";
                    slot.PriceText.color = soldMuted;
                }
                else
                {
                    slot.PriceText.text = $"{slot.Offer.Price}g";
                    slot.PriceText.color = CanAfford(slot.Offer.Price) ? affordableGold : unaffordableRed;
                }
            }

            RefreshResetButtonVisuals(animatePrice: false);
            RefreshDeckAndRemovalButtons();
        }

        private void RefreshDeckAndRemovalButtons()
        {
            int deckCount = RunDeckManager.Instance != null ? RunDeckManager.Instance.CurrentDeck.Count : 0;
            if (_deckBtnLabel != null) _deckBtnLabel.text = $"DECK ({deckCount})";
            if (_removeBtnLabel != null)
            {
                int remCost = CurrentRemovalCost;
                _removeBtnLabel.text = $"REMOVE {remCost}g";
                _removeBtnLabel.color = CanAfford(remCost)
                    ? new Color(0.24f, 0.13f, 0.04f, 1f)
                    : new Color(0.62f, 0.14f, 0.12f, 1f);
            }
        }

        public void OpenDeckModal(bool removalMode)
        {
            if (_isRestocking) return;
            EnsureRuntimeManagers();
            if (removalMode && !CanAfford(CurrentRemovalCost))
            {
                HandleCannotAfford(_removeBtnRect, _removeBtnLabel, CurrentRemovalCost);
                return;
            }

            _deckModalIsRemovalMode = removalMode;
            BuildDeckModalIfNeeded();
            PopulateDeckModal();
            _deckModalRoot.SetActive(true);
            _deckModalRoot.transform.SetAsLastSibling();
            Tune.SFX(SfxID.ui_panel_open);
        }

        public void CloseDeckModal()
        {
            if (_deckModalRoot != null) _deckModalRoot.SetActive(false);
            Tune.SFX(SfxID.ui_panel_close);
        }

        public bool TryRemoveDeckCardAt(int deckIndex)
        {
            if (!_deckModalIsRemovalMode || RunDeckManager.Instance == null) return false;
            int cost = CurrentRemovalCost;
            if (!CanAfford(cost))
            {
                HandleCannotAfford(_removeBtnRect, _removeBtnLabel, cost);
                return false;
            }
            IReadOnlyList<CardData> deck = RunDeckManager.Instance.CurrentDeck;
            if (deckIndex < 0 || deckIndex >= deck.Count) return false;

            string removedTitle = deck[deckIndex] != null ? deck[deckIndex].Title : "Card";
            if (!RunDeckManager.Instance.RemoveAt(deckIndex)) return false;
            if (!SpendGold(cost, _removeBtnRect)) return false;

            _removalCount++;
            CloseDeckModal();
            Tune.SFX(SfxID.card_remove);
            PlayVendorHappyJump();
            SayVendorLine($"Poof! '{removedTitle}' is gone from your deck! Next purge costs {CurrentRemovalCost}g.", playTalkAnim: true);
            RefreshAllAffordability();
            return true;
        }

        private void BuildDeckModalIfNeeded()
        {
            if (_deckModalRoot != null || _shopRoot == null) return;
            int layer = _shopRoot.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolidSprite();

            _deckModalRoot = new GameObject("MerchantDeckModal", typeof(RectTransform), typeof(Image));
            _deckModalRoot.layer = layer;
            RectTransform mRt = (RectTransform)_deckModalRoot.transform;
            mRt.SetParent(_shopRoot, false);
            StretchFull(mRt);
            Image dim = _deckModalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.04f, 0.02f, 0.01f, 0.84f);

            RectTransform win = CreateRect(mRt, "Window", layer);
            win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
            win.sizeDelta = new Vector2(1380f, 820f);
            win.gameObject.AddComponent<Image>().sprite = stallPanelSprite != null ? stallPanelSprite : solid;

            _deckModalTitle = CreateLabel(win, "Title", font, 28f, FontStyles.Bold, new Color(1f, 0.88f, 0.48f, 1f));
            _deckModalTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _deckModalTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _deckModalTitle.rectTransform.offsetMin = new Vector2(36f, -78f);
            _deckModalTitle.rectTransform.offsetMax = new Vector2(-36f, -20f);
            _deckModalTitle.alignment = TextAlignmentOptions.Center;

            RectTransform vp = CreateRect(win, "Viewport", layer);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(44f, 92f);
            vp.offsetMax = new Vector2(-44f, -88f);
            vp.gameObject.AddComponent<RectMask2D>();
            ScrollRect sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;

            RectTransform content = CreateRect(vp, "Content", layer);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            GridLayoutGroup glg = content.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(204f, 276f);
            glg.spacing = new Vector2(20f, 20f);
            glg.padding = new RectOffset(20, 20, 14, 14);
            glg.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            _deckModalGrid = content;

            _deckModalEmpty = CreateLabel(win, "EmptyText", font, 22f, FontStyles.Italic, new Color(0.92f, 0.84f, 0.70f, 0.9f));
            _deckModalEmpty.alignment = TextAlignmentOptions.Center;
            _deckModalEmpty.text = "Your run deck is currently empty.";
            _deckModalEmpty.gameObject.SetActive(false);

            RectTransform closeRt = CreateRect(win, "CloseButton", layer);
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 24f);
            closeRt.sizeDelta = new Vector2(220f, 52f);
            Image cImg = closeRt.gameObject.AddComponent<Image>();
            cImg.sprite = buttonSprite != null ? buttonSprite : solid;
            cImg.color = new Color(0.95f, 0.86f, 0.66f, 1f);
            closeRt.gameObject.AddComponent<Button>().onClick.AddListener(CloseDeckModal);
            TextMeshProUGUI cTxt = CreateLabel(closeRt, "Label", font, 21f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            cTxt.alignment = TextAlignmentOptions.Center;
            cTxt.text = "CLOSE";

            _deckModalRoot.SetActive(false);
        }

        private void PopulateDeckModal()
        {
            foreach (GameObject g in _deckModalCards) if (g != null) Destroy(g);
            _deckModalCards.Clear();

            IReadOnlyList<CardData> deck = RunDeckManager.Instance != null ? RunDeckManager.Instance.CurrentDeck : new List<CardData>();
            _deckModalTitle.text = _deckModalIsRemovalMode
                ? $"CARD REMOVAL SERVICE ({CurrentRemovalCost}g) — CLICK A CARD TO REMOVE"
                : $"YOUR RUN DECK ({deck.Count} CARDS)";
            _deckModalEmpty.gameObject.SetActive(deck.Count == 0);

            int layer = _deckModalGrid.gameObject.layer;
            for (int i = 0; i < deck.Count; i++)
            {
                CardData card = deck[i];
                if (card == null) continue;
                RectTransform cell = CreateRect(_deckModalGrid, $"DeckCard_{i:00}", layer);
                cell.sizeDelta = new Vector2(204f, 276f);
                Image hit = cell.gameObject.AddComponent<Image>();
                hit.sprite = GetSolidSprite();
                hit.color = new Color(1f, 1f, 1f, 0.001f);

                if (cardViewPrefab != null)
                {
                    RunDeckCardView view = Instantiate(cardViewPrefab, cell);
                    view.Setup(card, 1, i);
                    FitCardViewIntoContainer(view, cell);
                }

                if (_deckModalIsRemovalMode)
                {
                    int captured = i;
                    Button btn = cell.gameObject.AddComponent<Button>();
                    btn.onClick.AddListener(() => TryRemoveDeckCardAt(captured));
                }
                _deckModalCards.Add(cell.gameObject);
            }
        }

        private void RefreshGoldDisplay(bool animate)
        {
            if (_goldValueText == null) return;
            int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 0;
            _goldValueText.text = $"{gold}g";

            if (animate && _goldPillRect != null)
            {
                _goldPillRect.DOKill();
                _goldPillRect.localScale = Vector3.one;
                _goldPillRect.DOPunchScale(new Vector3(0.18f, 0.18f, 0f), 0.28f, 6, 0.6f).SetUpdate(true);
            }
        }

        private void RefreshResetButtonVisuals(bool animatePrice)
        {
            int cost = CurrentResetCost;
            bool affordable = CanAfford(cost);

            if (_resetPriceText != null)
            {
                _resetPriceText.DOKill();
                _resetPriceText.text = $"{cost}g";
                _resetPriceText.color = affordable
                    ? new Color(1f, 0.90f, 0.48f, 1f)
                    : new Color(1f, 0.46f, 0.44f, 1f);
            }

            if (_resetMedallionBg != null)
            {
                _resetMedallionBg.color = affordable
                    ? new Color(0.95f, 0.84f, 0.56f, 1f)
                    : new Color(0.72f, 0.65f, 0.56f, 0.92f);
            }

            if (animatePrice && _resetPricePillRect != null)
            {
                _resetPricePillRect.DOKill();
                _resetPricePillRect.localScale = Vector3.one;
                _resetPricePillRect.DOPunchScale(new Vector3(0.32f, 0.32f, 0f), 0.36f, 6, 0.6f).SetUpdate(true);
            }
        }

        private void RefreshEquippedRelicStrip(int highlightIndex)
        {
            RunRelicManager mgr = RunRelicManager.Instance;
            IReadOnlyList<RelicData> equipped = mgr != null ? mgr.EquippedSlots : null;

            for (int i = 0; i < _equippedMiniSlots.Count; i++)
            {
                EquippedMiniSlotUI mini = _equippedMiniSlots[i];
                RelicData relic = equipped != null && i < equipped.Count ? equipped[i] : null;
                bool hasRelic = relic != null;

                mini.Bg.color = hasRelic
                    ? new Color(0.16f, 0.12f, 0.09f, 0.95f)
                    : new Color(0.24f, 0.17f, 0.11f, 0.55f);
                mini.Border.color = hasRelic
                    ? TopBarRelicSlotsUI.GetRarityBorderColor(relic.Rarity, new Color(0.94f, 0.78f, 0.38f, 1f))
                    : new Color(0.68f, 0.56f, 0.36f, 0.55f);

                bool hasImg = hasRelic && relic.Image != null;
                mini.Icon.enabled = hasImg;
                if (hasImg)
                {
                    mini.Icon.sprite = relic.Image;
                    mini.Icon.color = GetRelicIconTint(relic);
                }

                mini.Monogram.gameObject.SetActive(hasRelic && !hasImg);
                if (hasRelic && !hasImg)
                {
                    mini.Monogram.text = TopBarRelicSlotsUI.BuildMonogram(relic.Title);
                }

                if (i == highlightIndex && mini.Root != null)
                {
                    mini.Root.DOKill();
                    mini.Root.localScale = Vector3.one;
                    mini.Root.DOPunchScale(new Vector3(0.36f, 0.36f, 0f), 0.38f, 6, 0.6f).SetUpdate(true);
                }
            }
        }

        private static int FindEquippedSlotIndex(RelicData relic)
        {
            if (relic == null || RunRelicManager.Instance == null) return -1;
            IReadOnlyList<RelicData> slots = RunRelicManager.Instance.EquippedSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == relic) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------------
        // Procedural Animations (Entrance, Hover, Purchase, Reset, Vendor)
        // ------------------------------------------------------------------
        private void PlayShopEntranceAnimation()
        {
            _activeEntranceSequence?.Kill();
            _activeEntranceSequence = DOTween.Sequence().SetUpdate(true);

            if (_stallRoot != null)
            {
                _stallRoot.localScale = new Vector3(0.94f, 0.94f, 1f);
                _activeEntranceSequence.Join(_stallRoot.DOScale(Vector3.one, 0.34f).SetEase(Ease.OutBack));
            }

            if (_vendorAnimRoot != null)
            {
                _vendorAnimRoot.anchoredPosition = _vendorAnimBasePos + new Vector2(0f, -60f);
                _vendorAnimRoot.localScale = new Vector3(0.82f, 0.82f, 1f);
                _activeEntranceSequence.Join(_vendorAnimRoot.DOAnchorPos(_vendorAnimBasePos, 0.42f).SetEase(Ease.OutBack));
                _activeEntranceSequence.Join(_vendorAnimRoot.DOScale(Vector3.one, 0.42f).SetEase(Ease.OutBack));
            }

            if (_dialogueBoxRoot != null)
            {
                Vector2 targetPos = _dialogueBoxRoot.anchoredPosition;
                _dialogueBoxRoot.anchoredPosition = targetPos + new Vector2(0f, -40f);
                _activeEntranceSequence.Join(_dialogueBoxRoot.DOAnchorPos(targetPos, 0.36f).SetEase(Ease.OutCubic));
            }

            for (int i = 0; i < _cardSlots.Count; i++)
            {
                CardSlotUI slot = _cardSlots[i];
                if (!slot.Root.gameObject.activeSelf) continue;
                slot.ContentRoot.localScale = new Vector3(0f, 0.78f, 1f);
                slot.ContentRoot.anchoredPosition = slot.RestContentPos + new Vector2(0f, 16f);
                float delay = 0.08f + i * 0.035f;
                _activeEntranceSequence.Insert(delay, slot.ContentRoot.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutBack));
                _activeEntranceSequence.Insert(delay, slot.ContentRoot.DOAnchorPos(slot.RestContentPos, 0.30f).SetEase(Ease.OutCubic));
            }

            for (int i = 0; i < _relicSlots.Count; i++)
            {
                RelicSlotUI slot = _relicSlots[i];
                if (!slot.Root.gameObject.activeSelf) continue;
                slot.ContentRoot.localScale = new Vector3(0f, 0.78f, 1f);
                float delay = 0.30f + i * 0.05f;
                _activeEntranceSequence.Insert(delay, slot.ContentRoot.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutBack));
            }

            if (_resetButtonRoot != null)
            {
                _resetButtonRoot.localScale = Vector3.zero;
                _activeEntranceSequence.Insert(0.40f, _resetButtonRoot.DOScale(Vector3.one, 0.36f).SetEase(Ease.OutBack));
                if (_resetMedallionSpinRect != null)
                {
                    _resetMedallionSpinRect.localEulerAngles = new Vector3(0f, 0f, 180f);
                    _activeEntranceSequence.Insert(0.40f, _resetMedallionSpinRect.DORotate(Vector3.zero, 0.44f, RotateMode.FastBeyond360).SetEase(Ease.OutBack));
                }
            }
        }

        private void PlayResetChoreography(int nextResetCost)
        {
            _isRestocking = true;
            _activeResetSequence?.Kill();

            Tune.SFX(SfxID.Shuffle);
            PlayVendorResetSpinHop();
            SayVendorLine(string.Format(PickRandom(resetLines), nextResetCost), playTalkAnim: true);

            // Reset button spin + golden pulse ring burst!
            if (_resetMedallionSpinRect != null)
            {
                _resetMedallionSpinRect.DOKill();
                _resetMedallionSpinRect.localEulerAngles = Vector3.zero;
                _resetMedallionSpinRect.DORotate(new Vector3(0f, 0f, -360f), 0.58f, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
            if (_resetButtonRoot != null)
            {
                _resetButtonRoot.DOKill();
                _resetButtonRoot.localScale = Vector3.one;
                _resetButtonRoot.DOPunchScale(new Vector3(0.22f, 0.22f, 0f), 0.38f, 6, 0.6f).SetUpdate(true);
            }
            if (_resetPulseRing != null)
            {
                _resetPulseRing.DOKill();
                _resetPulseRing.rectTransform.DOKill();
                _resetPulseRing.gameObject.SetActive(true);
                _resetPulseRing.rectTransform.localScale = Vector3.one * 0.85f;
                _resetPulseRing.color = new Color(1f, 0.88f, 0.44f, 0.92f);
                _resetPulseRing.rectTransform.DOScale(1.85f, 0.50f).SetEase(Ease.OutCubic).SetUpdate(true);
                _resetPulseRing.DOFade(0f, 0.50f).SetEase(Ease.OutQuad).SetUpdate(true)
                    .OnComplete(() => { if (_resetPulseRing != null) _resetPulseRing.gameObject.SetActive(false); });
            }

            _activeResetSequence = DOTween.Sequence().SetUpdate(true);

            // Phase 1: Flip & drop old cards + relics staggered left-to-right
            for (int i = 0; i < _cardSlots.Count; i++)
            {
                CardSlotUI slot = _cardSlots[i];
                float delay = i * 0.018f;
                slot.ContentRoot.DOKill();
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOScaleX(0f, 0.16f).SetEase(Ease.InBack));
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOAnchorPosY(slot.RestContentPos.y - 14f, 0.16f).SetEase(Ease.InQuad));
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOLocalRotate(new Vector3(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 8f), 0.16f));
            }
            for (int i = 0; i < _relicSlots.Count; i++)
            {
                RelicSlotUI slot = _relicSlots[i];
                float delay = 0.06f + i * 0.025f;
                slot.ContentRoot.DOKill();
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOScaleX(0f, 0.16f).SetEase(Ease.InBack));
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOAnchorPosY(slot.RestContentPos.y - 12f, 0.16f).SetEase(Ease.InQuad));
            }

            // Phase 2: Swap in fresh cards & relics at midpoint
            const float swapAt = 0.28f;
            _activeResetSequence.InsertCallback(swapAt, () =>
            {
                PopulateStock(avoidPreviousStock: true);
                for (int i = 0; i < _cardSlots.Count; i++)
                {
                    CardSlotUI s = _cardSlots[i];
                    s.ContentRoot.localScale = new Vector3(0f, 0.78f, 1f);
                    s.ContentRoot.anchoredPosition = s.RestContentPos + new Vector2(0f, 18f);
                    s.ContentRoot.localEulerAngles = new Vector3(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 6f);
                }
                for (int i = 0; i < _relicSlots.Count; i++)
                {
                    RelicSlotUI s = _relicSlots[i];
                    s.ContentRoot.localScale = new Vector3(0f, 0.78f, 1f);
                    s.ContentRoot.anchoredPosition = s.RestContentPos + new Vector2(0f, 16f);
                }
                Tune.SFX(SfxID.DrawCard);
            });

            // Phase 3: Pop & flip fresh wares in with golden shimmer wave!
            for (int i = 0; i < _cardSlots.Count; i++)
            {
                CardSlotUI slot = _cardSlots[i];
                float delay = swapAt + 0.02f + i * 0.032f;
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOScale(Vector3.one, 0.30f).SetEase(Ease.OutBack));
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOAnchorPos(slot.RestContentPos, 0.28f).SetEase(Ease.OutCubic));
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOLocalRotate(Vector3.zero, 0.28f).SetEase(Ease.OutBack));
                if (slot.ShimmerOverlay != null)
                {
                    Image shimmer = slot.ShimmerOverlay;
                    _activeResetSequence.InsertCallback(delay, () => FlashOverlay(shimmer, new Color(1f, 0.92f, 0.56f, 0.55f), 0.32f));
                }
            }

            for (int i = 0; i < _relicSlots.Count; i++)
            {
                RelicSlotUI slot = _relicSlots[i];
                float delay = swapAt + 0.14f + i * 0.045f;
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutBack));
                _activeResetSequence.Insert(delay, slot.ContentRoot.DOAnchorPos(slot.RestContentPos, 0.28f).SetEase(Ease.OutCubic));
                if (slot.ShimmerOverlay != null)
                {
                    Image shimmer = slot.ShimmerOverlay;
                    _activeResetSequence.InsertCallback(delay, () => FlashOverlay(shimmer, new Color(1f, 0.90f, 0.50f, 0.52f), 0.34f));
                }
            }

            _activeResetSequence.OnComplete(() =>
            {
                _isRestocking = false;
                InspectResetButton();
            });
        }

        private static void PlaySoldStampAnimation(RectTransform stampRoot, CanvasGroup stampGroup, RectTransform contentRoot, CanvasGroup contentGroup)
        {
            if (contentGroup != null)
            {
                contentGroup.DOKill();
                contentGroup.DOFade(0.54f, 0.24f).SetUpdate(true);
            }

            if (contentRoot != null)
            {
                contentRoot.DOKill();
                contentRoot.DOPunchScale(new Vector3(0.08f, -0.06f, 0f), 0.26f, 6, 0.6f).SetUpdate(true);
            }

            if (stampRoot != null)
            {
                stampRoot.DOKill();
                stampRoot.gameObject.SetActive(true);
                stampRoot.SetAsLastSibling();
                stampRoot.localScale = Vector3.one * 2.35f;
                stampRoot.localEulerAngles = new Vector3(0f, 0f, -26f + UnityEngine.Random.Range(-5f, 5f));
                stampRoot.DOScale(Vector3.one, 0.26f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (stampGroup != null)
            {
                stampGroup.DOKill();
                stampGroup.alpha = 0f;
                stampGroup.DOFade(1f, 0.14f).SetUpdate(true);
            }
        }

        private static void FlashOverlay(Image overlay, Color peakColor, float duration)
        {
            if (overlay == null) return;
            overlay.DOKill();
            overlay.gameObject.SetActive(true);
            overlay.color = peakColor;
            overlay.DOFade(0f, duration).SetEase(Ease.OutQuad).SetUpdate(true)
                .OnComplete(() => { if (overlay != null) overlay.gameObject.SetActive(false); });
        }

        private static void ShakeRect(RectTransform rect)
        {
            if (rect == null) return;
            rect.DOKill(complete: true);
            rect.DOShakeAnchorPos(0.32f, new Vector2(12f, 2f), 18, 0f, false, true).SetUpdate(true);
        }

        private void SpawnFloatingCostPopup(RectTransform anchorTarget, string text, Color color)
        {
            if (_floatingPopupRoot == null || anchorTarget == null) return;

            GameObject go = new("CostPopup", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = _floatingPopupRoot.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(_floatingPopupRoot, false);
            rt.sizeDelta = new Vector2(160f, 44f);
            rt.position = anchorTarget.position;
            Vector2 startLocal = rt.anchoredPosition + new Vector2(0f, 26f);
            rt.anchoredPosition = startLocal;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.text = text;
            tmp.fontSize = 26f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            AddShadow(go, new Color(0.12f, 0.06f, 0.02f, 0.9f), new Vector2(2f, -2f));

            rt.localScale = Vector3.one * 0.7f;
            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(rt.DOScale(1.1f, 0.18f).SetEase(Ease.OutBack));
            seq.Join(rt.DOAnchorPosY(startLocal.y + 56f, 0.75f).SetEase(Ease.OutCubic));
            seq.Insert(0.35f, tmp.DOFade(0f, 0.40f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (go != null) Destroy(go); });
        }

        // ------------------------------------------------------------------
        // Vendor Procedural Animation & Dialogue
        // ------------------------------------------------------------------
        private void SayVendorLine(string line, bool playTalkAnim)
        {
            _fullDialogueLine = line ?? string.Empty;
            _dialogueCharProgress = 0f;
            _isTypingDialogue = !string.IsNullOrEmpty(_fullDialogueLine);

            if (_dialogueText != null)
            {
                _dialogueText.text = _fullDialogueLine;
                _dialogueText.maxVisibleCharacters = 0;
            }
            if (_dialogueDoneDiamond != null)
            {
                _dialogueDoneDiamond.gameObject.SetActive(false);
            }
            if (playTalkAnim)
            {
                _talkBounceTimer = 0f;
            }
        }

        private void CompleteDialogueImmediately()
        {
            if (!_isTypingDialogue)
            {
                PokeVendor();
                return;
            }

            _isTypingDialogue = false;
            if (_dialogueText != null)
            {
                _dialogueText.maxVisibleCharacters = _fullDialogueLine.Length;
            }
            if (_dialogueDoneDiamond != null)
            {
                _dialogueDoneDiamond.gameObject.SetActive(true);
            }
        }

        private void UpdateTypewriterDialogue(float dt)
        {
            if (_dialogueText == null) return;

            if (_isTypingDialogue)
            {
                _dialogueCharProgress += dt * Mathf.Max(10f, typewriterCharsPerSecond);
                int visible = Mathf.Clamp(Mathf.FloorToInt(_dialogueCharProgress), 0, _fullDialogueLine.Length);
                _dialogueText.maxVisibleCharacters = visible;

                if (visible >= _fullDialogueLine.Length)
                {
                    _isTypingDialogue = false;
                    if (_dialogueDoneDiamond != null) _dialogueDoneDiamond.gameObject.SetActive(true);
                }
            }
            else if (_dialogueDoneDiamond != null && _dialogueDoneDiamond.gameObject.activeSelf)
            {
                float pulse = 0.88f + 0.16f * Mathf.Sin(Time.unscaledTime * 5.2f);
                _dialogueDoneDiamond.localScale = Vector3.one * pulse;
            }
        }

        private void UpdateVendorProceduralMotion(float dt)
        {
            if (_vendorFloatRoot == null) return;

            _vendorIdleTime += dt;
            float idleY = Mathf.Sin(_vendorIdleTime * 2.2f) * 8.5f;
            float idleTilt = Mathf.Sin(_vendorIdleTime * 1.45f) * 1.9f;
            float breathScaleX = 1f + Mathf.Sin(_vendorIdleTime * 2.2f + 0.4f) * 0.022f;
            float breathScaleY = 1f - Mathf.Sin(_vendorIdleTime * 2.2f + 0.4f) * 0.022f;

            // Layer bouncy talking squash-and-stretch on top while dialogue is typing!
            if (_isTypingDialogue)
            {
                _talkBounceTimer += dt * 15.5f;
                float talkWave = Mathf.Abs(Mathf.Sin(_talkBounceTimer));
                idleY += talkWave * 11f;
                breathScaleY += talkWave * 0.055f;
                breathScaleX -= talkWave * 0.038f;
                idleTilt += Mathf.Sin(_talkBounceTimer * 0.5f) * 2.8f;
            }

            _vendorFloatRoot.anchoredPosition = _vendorFloatBasePos + new Vector2(0f, idleY);
            _vendorFloatRoot.localEulerAngles = new Vector3(0f, 0f, idleTilt);
            _vendorFloatRoot.localScale = new Vector3(breathScaleX, breathScaleY, 1f);

            if (_vendorSpotlight != null)
            {
                float glowAlpha = 0.24f + 0.05f * Mathf.Sin(_vendorIdleTime * 1.8f);
                Color c = _vendorSpotlight.color;
                c.a = glowAlpha;
                _vendorSpotlight.color = c;
            }
        }

        private void PlayVendorHappyJump()
        {
            if (_vendorAnimRoot == null) return;
            _vendorActionSequence?.Kill(complete: true);
            _vendorAnimRoot.anchoredPosition = _vendorAnimBasePos;
            _vendorAnimRoot.localScale = Vector3.one;
            _vendorAnimRoot.localEulerAngles = Vector3.zero;

            _vendorActionSequence = DOTween.Sequence().SetUpdate(true);
            _vendorActionSequence.Append(_vendorAnimRoot.DOScale(new Vector3(1.12f, 0.86f, 1f), 0.07f).SetEase(Ease.OutQuad));
            _vendorActionSequence.Append(_vendorAnimRoot.DOAnchorPosY(_vendorAnimBasePos.y + 34f, 0.16f).SetEase(Ease.OutQuad));
            _vendorActionSequence.Join(_vendorAnimRoot.DOScale(new Vector3(0.92f, 1.12f, 1f), 0.16f).SetEase(Ease.OutQuad));
            _vendorActionSequence.Join(_vendorAnimRoot.DOLocalRotate(new Vector3(0f, 0f, -7f), 0.16f));
            _vendorActionSequence.Append(_vendorAnimRoot.DOAnchorPosY(_vendorAnimBasePos.y, 0.20f).SetEase(Ease.InQuad));
            _vendorActionSequence.Join(_vendorAnimRoot.DOScale(new Vector3(1.08f, 0.92f, 1f), 0.16f));
            _vendorActionSequence.Join(_vendorAnimRoot.DOLocalRotate(new Vector3(0f, 0f, 4f), 0.16f));
            _vendorActionSequence.Append(_vendorAnimRoot.DOScale(Vector3.one, 0.14f).SetEase(Ease.OutBack));
            _vendorActionSequence.Join(_vendorAnimRoot.DOLocalRotate(Vector3.zero, 0.14f));
        }

        private void PlayVendorHeadShake()
        {
            if (_vendorAnimRoot == null) return;
            _vendorActionSequence?.Kill(complete: true);
            _vendorAnimRoot.anchoredPosition = _vendorAnimBasePos;
            _vendorAnimRoot.localScale = Vector3.one;
            _vendorAnimRoot.localEulerAngles = Vector3.zero;

            _vendorActionSequence = DOTween.Sequence().SetUpdate(true);
            _vendorActionSequence.Join(_vendorAnimRoot.DOShakeAnchorPos(0.32f, new Vector2(16f, 2f), 18, 0f, false, true));
            _vendorActionSequence.Join(_vendorAnimRoot.DOScale(new Vector3(1.06f, 0.92f, 1f), 0.10f).SetLoops(2, LoopType.Yoyo));
        }

        private void PlayVendorResetSpinHop()
        {
            if (_vendorAnimRoot == null) return;
            _vendorActionSequence?.Kill(complete: true);
            _vendorAnimRoot.anchoredPosition = _vendorAnimBasePos;
            _vendorAnimRoot.localScale = Vector3.one;
            _vendorAnimRoot.localEulerAngles = Vector3.zero;

            _vendorActionSequence = DOTween.Sequence().SetUpdate(true);
            _vendorActionSequence.Append(_vendorAnimRoot.DOScale(new Vector3(1.16f, 0.80f, 1f), 0.10f).SetEase(Ease.InQuad));
            _vendorActionSequence.Append(_vendorAnimRoot.DOAnchorPosY(_vendorAnimBasePos.y + 38f, 0.18f).SetEase(Ease.OutQuad));
            _vendorActionSequence.Join(_vendorAnimRoot.DOScale(new Vector3(0.88f, 1.14f, 1f), 0.18f));
            _vendorActionSequence.Join(_vendorAnimRoot.DOLocalRotate(new Vector3(0f, 0f, 12f), 0.12f));
            _vendorActionSequence.Append(_vendorAnimRoot.DOLocalRotate(new Vector3(0f, 0f, -10f), 0.14f));
            _vendorActionSequence.Join(_vendorAnimRoot.DOAnchorPosY(_vendorAnimBasePos.y, 0.22f).SetEase(Ease.InOutQuad));
            _vendorActionSequence.Append(_vendorAnimRoot.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBack));
            _vendorActionSequence.Join(_vendorAnimRoot.DOLocalRotate(Vector3.zero, 0.16f));
        }

        private void PlayVendorTalkAccent()
        {
            if (_vendorAnimRoot == null) return;
            _vendorAnimRoot.DOKill(complete: true);
            _vendorAnimRoot.DOPunchScale(new Vector3(-0.05f, 0.08f, 0f), 0.24f, 4, 0.5f).SetUpdate(true);
        }

        // ------------------------------------------------------------------
        // Pointer Hover & Click Callbacks (from lightweight slot handlers)
        // ------------------------------------------------------------------
        private void OnCardSlotHoverEnter(CardSlotUI slot)
        {
            if (_isRestocking || slot == null || slot.Offer == null) return;
            slot.IsHovered = true;
            InspectCardAt(slot.Index);

            if (!slot.Offer.IsSold)
            {
                Tune.SFX(SfxID.hover);
                slot.ContentRoot.DOKill();
                slot.ContentRoot.DOAnchorPos(slot.RestContentPos + new Vector2(0f, 12f), 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
                slot.ContentRoot.DOScale(1.065f, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);

                if (slot.HoverGlow != null)
                {
                    slot.HoverGlow.DOKill();
                    slot.HoverGlow.DOFade(0.90f, 0.14f).SetUpdate(true);
                }

                if (Time.unscaledTime >= _nextHoverQuipAllowedTime && !_isTypingDialogue)
                {
                    _nextHoverQuipAllowedTime = Time.unscaledTime + hoverQuipCooldown;
                    SayVendorLine(string.Format(PickRandom(hoverCardLines), slot.Offer.GetTitle(), slot.Offer.Price), playTalkAnim: true);
                }
            }
        }

        private void OnCardSlotHoverExit(CardSlotUI slot)
        {
            if (slot == null) return;
            slot.IsHovered = false;
            if (!_isRestocking)
            {
                slot.ContentRoot.DOKill();
                slot.ContentRoot.DOAnchorPos(slot.RestContentPos, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
                slot.ContentRoot.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            if (slot.HoverGlow != null)
            {
                slot.HoverGlow.DOKill();
                slot.HoverGlow.DOFade(0f, 0.16f).SetUpdate(true);
            }
        }

        private void OnRelicSlotHoverEnter(RelicSlotUI slot)
        {
            if (_isRestocking || slot == null || slot.Offer == null || slot.Offer.Relic == null) return;
            slot.IsHovered = true;
            InspectRelicAt(slot.Index);

            if (!slot.Offer.IsSold)
            {
                Tune.SFX(SfxID.hover);
                slot.ContentRoot.DOKill();
                slot.ContentRoot.DOAnchorPos(slot.RestContentPos + new Vector2(0f, 9f), 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
                slot.ContentRoot.DOScale(1.06f, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);

                if (slot.HoverGlow != null)
                {
                    slot.HoverGlow.DOKill();
                    slot.HoverGlow.DOFade(0.92f, 0.14f).SetUpdate(true);
                }

                if (Time.unscaledTime >= _nextHoverQuipAllowedTime && !_isTypingDialogue)
                {
                    _nextHoverQuipAllowedTime = Time.unscaledTime + hoverQuipCooldown;
                    SayVendorLine(string.Format(PickRandom(hoverRelicLines), slot.Offer.Relic.Title, slot.Offer.Price), playTalkAnim: true);
                }
            }
        }

        private void OnRelicSlotHoverExit(RelicSlotUI slot)
        {
            if (slot == null) return;
            slot.IsHovered = false;
            if (!_isRestocking)
            {
                slot.ContentRoot.DOKill();
                slot.ContentRoot.DOAnchorPos(slot.RestContentPos, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
                slot.ContentRoot.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            if (slot.HoverGlow != null)
            {
                slot.HoverGlow.DOKill();
                slot.HoverGlow.DOFade(0f, 0.16f).SetUpdate(true);
            }
        }

        private void OnResetButtonHoverEnter()
        {
            InspectResetButton();
            if (_isRestocking) return;

            Tune.SFX(SfxID.hover);
            if (_resetButtonRoot != null)
            {
                _resetButtonRoot.DOKill();
                _resetButtonRoot.DOScale(1.09f, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            if (_resetMedallionSpinRect != null)
            {
                _resetMedallionSpinRect.DOKill();
                _resetMedallionSpinRect.DOLocalRotate(new Vector3(0f, 0f, -24f), 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            if (_resetHoverGlow != null)
            {
                _resetHoverGlow.DOKill();
                _resetHoverGlow.DOFade(0.90f, 0.16f).SetUpdate(true);
            }
        }

        private void OnResetButtonHoverExit()
        {
            if (_isRestocking) return;
            if (_resetButtonRoot != null)
            {
                _resetButtonRoot.DOKill();
                _resetButtonRoot.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            if (_resetMedallionSpinRect != null)
            {
                _resetMedallionSpinRect.DOKill();
                _resetMedallionSpinRect.DOLocalRotate(Vector3.zero, 0.20f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            if (_resetHoverGlow != null)
            {
                _resetHoverGlow.DOKill();
                _resetHoverGlow.DOFade(0f, 0.16f).SetUpdate(true);
            }
        }

        private void SetInspectorContent(string title, string meta, string price, Color priceColor, string body, bool animate = true)
        {
            if (_inspectorTitleText != null) _inspectorTitleText.text = title;
            if (_inspectorMetaText != null) _inspectorMetaText.text = meta;
            if (_inspectorPriceText != null)
            {
                _inspectorPriceText.text = price;
                _inspectorPriceText.color = priceColor;
                _inspectorPriceText.gameObject.SetActive(!string.IsNullOrEmpty(price));
            }
            if (_inspectorBodyText != null) _inspectorBodyText.text = body;

            if (animate && _inspectorRoot != null)
            {
                _inspectorRoot.DOKill();
                _inspectorRoot.localScale = new Vector3(0.975f, 0.975f, 1f);
                _inspectorRoot.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------------
        // Ambient Floating Shop Dust Motes
        // ------------------------------------------------------------------
        private void UpdateAmbientMotes(float dt)
        {
            for (int i = 0; i < _motes.Count; i++)
            {
                FloatingMote m = _motes[i];
                if (m.Rect == null) continue;
                Vector2 pos = m.Rect.anchoredPosition;
                pos.y += m.SpeedY * dt;
                if (pos.y > ReferenceHeight * 0.54f)
                {
                    pos.y = -ReferenceHeight * 0.54f;
                }
                pos.x = m.BaseX + Mathf.Sin(Time.unscaledTime * m.SwayFreq + m.Phase) * m.SwayAmp;
                m.Rect.anchoredPosition = pos;

                Color c = m.Image.color;
                c.a = m.BaseAlpha * (0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.1f + m.Phase));
                m.Image.color = c;
            }
        }

        // ------------------------------------------------------------------
        // Procedural / Editable Scene Hierarchy Construction & Binding
        // ------------------------------------------------------------------
        private void BuildShopUIIfNeeded()
        {
            if (_isBuilt || targetCanvas == null) return;
            _isBuilt = true;

            RectTransform existing = bakedShopRoot != null
                ? bakedShopRoot
                : targetCanvas.transform.Find("MerchantShopRoot") as RectTransform;

            if (existing != null)
            {
                BindExistingShopUI(existing);
                return;
            }

            BuildFreshHierarchyUnderCanvas();
        }

        /// <summary>
        /// Editor baker entry point: builds a clean, persistent MerchantShopRoot under targetCanvas
        /// using baked Sprite assets and populates Edit-Mode preview cards/relics so designers can
        /// inspect and edit every panel, slot, button, and character directly in the Unity Editor.
        /// </summary>
        public RectTransform BuildBakedHierarchyForEditor()
        {
            EnsureCanvas();
            if (targetCanvas == null) return null;

            // Clean any previous baked root before rebuilding
            for (int i = targetCanvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = targetCanvas.transform.GetChild(i);
                if (child == bakedShopRoot || child.name == "MerchantShopRoot")
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            _isBuilt = false;
            _deckModalRoot = null;
            BuildFreshHierarchyUnderCanvas();
            BuildDeckModalIfNeeded();
            if (_deckModalRoot != null) _deckModalRoot.SetActive(false);

            bakedShopRoot = _shopRoot;
            PopulateEditorPreviewVisuals();
            _isBuilt = false; // Allow Play Mode Awake() to cleanly bind listeners onto the baked scene hierarchy
            return _shopRoot;
        }

        private void BuildFreshHierarchyUnderCanvas()
        {
            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolidSprite();

            _shopRoot = CreateRect(targetCanvas.transform, "MerchantShopRoot", layer);
            StretchFull(_shopRoot);

            BuildBackdrop(_shopRoot, layer, solid);
            BuildAmbientMotes(_shopRoot, layer);
            BuildLeftColumn(_shopRoot, layer, font, solid);
            BuildRightShopStall(_shopRoot, layer, font, solid);

            _floatingPopupRoot = CreateRect(_shopRoot, "FloatingPopups", layer);
            StretchFull(_floatingPopupRoot);
        }

        private void BindExistingShopUI(RectTransform root)
        {
            _shopRoot = root;
            bakedShopRoot = root;
            root.gameObject.SetActive(true);

            _leftColumnRoot = FindRect(root, "LeftColumn");
            _stallRoot = FindRect(root, "ShopStallPanel");
            _floatingPopupRoot = FindRect(root, "FloatingPopups");
            if (_floatingPopupRoot == null)
            {
                _floatingPopupRoot = CreateRect(root, "FloatingPopups", root.gameObject.layer);
                StretchFull(_floatingPopupRoot);
            }

            // Ambient motes
            _motes.Clear();
            Transform moteLayer = root.Find("AmbientMotes");
            if (moteLayer != null)
            {
                for (int i = 0; i < moteLayer.childCount; i++)
                {
                    RectTransform mRt = moteLayer.GetChild(i) as RectTransform;
                    Image mImg = mRt != null ? mRt.GetComponent<Image>() : null;
                    if (mRt == null || mImg == null) continue;
                    _motes.Add(new FloatingMote
                    {
                        Rect = mRt,
                        Image = mImg,
                        BaseX = mRt.anchoredPosition.x,
                        SpeedY = UnityEngine.Random.Range(14f, 34f),
                        SwayAmp = UnityEngine.Random.Range(12f, 32f),
                        SwayFreq = UnityEngine.Random.Range(0.5f, 1.2f),
                        Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                        BaseAlpha = mImg.color.a > 0.02f ? mImg.color.a : 0.22f
                    });
                }
            }

            // Left column controls
            if (_leftColumnRoot != null)
            {
                RectTransform leaveRt = FindRect(_leftColumnRoot, "LeaveToMapButton");
                if (leaveRt != null)
                {
                    _leaveButton = leaveRt.GetComponent<Button>();
                    if (_leaveButton != null)
                    {
                        _leaveButton.onClick.RemoveAllListeners();
                        _leaveButton.onClick.AddListener(ReturnToMap);
                    }
                    AttachOrRebindHoverScale(leaveRt, 1.06f);
                }

                _goldPillRect = FindRect(_leftColumnRoot, "GoldCounterPill");
                _goldValueText = FindTMP(_goldPillRect, "GoldValue");

                // Equipped Relics mini-row
                _equippedMiniSlots.Clear();
                Transform beltRow = _leftColumnRoot.Find("EquippedRelicsRow");
                if (beltRow != null)
                {
                    for (int i = 0; i < beltRow.childCount; i++)
                    {
                        RectTransform mSlot = beltRow.GetChild(i) as RectTransform;
                        if (mSlot == null || !mSlot.name.StartsWith("MiniRelic_", StringComparison.Ordinal)) continue;
                        Image border = mSlot.GetComponent<Image>();
                        Image bg = mSlot.Find("Inner")?.GetComponent<Image>();
                        Image icon = mSlot.Find("Inner/Icon")?.GetComponent<Image>();
                        TextMeshProUGUI mono = mSlot.Find("Inner/Mono")?.GetComponent<TextMeshProUGUI>();
                        Button btn = mSlot.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() =>
                            {
                                EnsureRelicSwapModal();
                                _relicSwapModal?.OpenManageMode();
                            });
                        }
                        _equippedMiniSlots.Add(new EquippedMiniSlotUI
                        {
                            Root = mSlot,
                            Border = border,
                            Bg = bg,
                            Icon = icon,
                            Monogram = mono
                        });
                    }
                }

                RectTransform deckBtnRt = FindRect(_leftColumnRoot, "ShopDeckButton");
                if (deckBtnRt != null)
                {
                    Button deckBtn = deckBtnRt.GetComponent<Button>();
                    if (deckBtn != null)
                    {
                        deckBtn.onClick.RemoveAllListeners();
                        deckBtn.onClick.AddListener(() => OpenDeckModal(removalMode: false));
                    }
                    AttachOrRebindHoverScale(deckBtnRt, 1.06f);
                    _deckBtnLabel = FindTMP(deckBtnRt, "Label");
                }

                _removeBtnRect = FindRect(_leftColumnRoot, "CardRemovalButton");
                if (_removeBtnRect != null)
                {
                    Button remBtn = _removeBtnRect.GetComponent<Button>();
                    if (remBtn != null)
                    {
                        remBtn.onClick.RemoveAllListeners();
                        remBtn.onClick.AddListener(() => OpenDeckModal(removalMode: true));
                    }
                    AttachOrRebindHoverScale(_removeBtnRect, 1.06f);
                    _removeBtnLabel = FindTMP(_removeBtnRect, "Label");
                }

                // Item Description Inspector
                _inspectorRoot = FindRect(_leftColumnRoot, "ItemDescPanel");
                if (_inspectorRoot != null)
                {
                    _inspectorBorderGlow = _inspectorRoot.Find("InnerInset")?.GetComponent<Image>();
                    _inspectorTitleText = FindTMP(_inspectorRoot, "InnerInset/ItemTitle");
                    _inspectorPriceText = FindTMP(_inspectorRoot, "InnerInset/ItemPrice");
                    _inspectorMetaText = FindTMP(_inspectorRoot, "InnerInset/ItemMeta");
                    _inspectorBodyText = FindTMP(_inspectorRoot, "InnerInset/ItemBody");
                }

                // Vendor Stage
                Transform vendorStage = _leftColumnRoot.Find("VendorStage");
                if (vendorStage != null)
                {
                    _vendorSpotlight = vendorStage.Find("VendorSpotlight")?.GetComponent<Image>();
                    _vendorFloatRoot = vendorStage.Find("VendorFloatRoot") as RectTransform;
                    _vendorAnimRoot = vendorStage.Find("VendorFloatRoot/VendorAnimRoot") as RectTransform;
                    _vendorFloatBasePos = _vendorFloatRoot != null ? _vendorFloatRoot.anchoredPosition : Vector2.zero;
                    _vendorAnimBasePos = _vendorAnimRoot != null ? _vendorAnimRoot.anchoredPosition : Vector2.zero;

                    Transform visualTr = vendorStage.Find("VendorFloatRoot/VendorAnimRoot/VendorVisual");
                    if (visualTr != null)
                    {
                        _vendorImage = visualTr.GetComponent<Image>();
                        Button pokeBtn = visualTr.GetComponent<Button>();
                        if (pokeBtn != null)
                        {
                            pokeBtn.onClick.RemoveAllListeners();
                            pokeBtn.onClick.AddListener(PokeVendor);
                        }
                    }
                }

                // Vendor Dialogue Box
                _dialogueBoxRoot = FindRect(_leftColumnRoot, "VendorDialogueBox");
                if (_dialogueBoxRoot != null)
                {
                    Button dlgBtn = _dialogueBoxRoot.GetComponent<Button>();
                    if (dlgBtn != null)
                    {
                        dlgBtn.onClick.RemoveAllListeners();
                        dlgBtn.onClick.AddListener(CompleteDialogueImmediately);
                    }
                    _dialogueText = FindTMP(_dialogueBoxRoot, "VendorText");
                    _dialogueDoneDiamond = FindRect(_dialogueBoxRoot, "DonePrompt");
                }
            }

            // Right Shop Stall: Card Slots, Relic Shelf, Reset Medallion
            _cardSlots.Clear();
            _relicSlots.Clear();
            if (_stallRoot != null)
            {
                Transform cardsGrid = _stallRoot.Find("CardOffersGrid");
                if (cardsGrid != null)
                {
                    for (int i = 0; i < cardsGrid.childCount; i++)
                    {
                        RectTransform slotRoot = cardsGrid.GetChild(i) as RectTransform;
                        if (slotRoot == null) continue;
                        RectTransform contentRoot = slotRoot.Find("Content") as RectTransform;
                        if (contentRoot == null) continue;

                        RectTransform cardHolder = contentRoot.Find("CardHolder") as RectTransform;
                        RunDeckCardView cardView = cardHolder != null ? cardHolder.GetComponentInChildren<RunDeckCardView>(true) : null;
                        if (cardView == null && cardViewPrefab != null && cardHolder != null)
                        {
                            cardView = Instantiate(cardViewPrefab, cardHolder);
                        }
                        if (cardView != null)
                        {
                            CanvasGroup cg = cardView.GetComponent<CanvasGroup>();
                            if (cg != null) { cg.interactable = false; cg.blocksRaycasts = false; }
                        }

                        RectTransform soldRoot = slotRoot.Find("SoldStamp") as RectTransform;
                        CardSlotUI slot = new()
                        {
                            Index = _cardSlots.Count,
                            Root = slotRoot,
                            ContentRoot = contentRoot,
                            ContentGroup = contentRoot.GetComponent<CanvasGroup>() ?? contentRoot.gameObject.AddComponent<CanvasGroup>(),
                            HoverGlow = contentRoot.Find("HoverGlow")?.GetComponent<Image>(),
                            ShimmerOverlay = contentRoot.Find("ShimmerOverlay")?.GetComponent<Image>(),
                            CardContainer = cardHolder,
                            CardView = cardView,
                            PriceText = FindTMP(contentRoot, "PricePill/PriceText"),
                            PricePill = contentRoot.Find("PricePill")?.GetComponent<Image>(),
                            SoldStampRoot = soldRoot,
                            SoldStampGroup = soldRoot != null ? soldRoot.GetComponent<CanvasGroup>() : null,
                            RestContentPos = contentRoot.anchoredPosition
                        };
                        int capturedIdx = slot.Index;
                        _cardSlots.Add(slot);

                        MerchantPointerRelay relay = slotRoot.GetComponent<MerchantPointerRelay>() ?? slotRoot.gameObject.AddComponent<MerchantPointerRelay>();
                        relay.Init(
                            onEnter: () => OnCardSlotHoverEnter(slot),
                            onExit: () => OnCardSlotHoverExit(slot),
                            onClick: () => TryBuyCardAt(capturedIdx));
                    }
                }

                Transform relicsRow = _stallRoot.Find("BottomRelicsAndResetShelf/RelicsZone/RelicsRow");
                if (relicsRow != null)
                {
                    for (int i = 0; i < relicsRow.childCount; i++)
                    {
                        RectTransform slotRoot = relicsRow.GetChild(i) as RectTransform;
                        if (slotRoot == null) continue;
                        RectTransform contentRoot = slotRoot.Find("Content") as RectTransform;
                        if (contentRoot == null) continue;

                        RectTransform soldRoot = slotRoot.Find("SoldStamp") as RectTransform;
                        RelicSlotUI slot = new()
                        {
                            Index = _relicSlots.Count,
                            Root = slotRoot,
                            ContentRoot = contentRoot,
                            ContentGroup = contentRoot.GetComponent<CanvasGroup>() ?? contentRoot.gameObject.AddComponent<CanvasGroup>(),
                            FrameBg = contentRoot.Find("Pedestal")?.GetComponent<Image>(),
                            HoverGlow = contentRoot.Find("HoverGlow")?.GetComponent<Image>(),
                            ShimmerOverlay = contentRoot.Find("ShimmerOverlay")?.GetComponent<Image>(),
                            IconRing = contentRoot.Find("Pedestal/IconRing")?.GetComponent<Image>(),
                            IconImage = contentRoot.Find("Pedestal/IconRing/Socket/Icon")?.GetComponent<Image>(),
                            MonogramText = FindTMP(contentRoot, "Pedestal/IconRing/Socket/Monogram"),
                            NameText = FindTMP(contentRoot, "Pedestal/RelicName"),
                            PriceText = FindTMP(contentRoot, "PricePill/PriceText"),
                            PricePill = contentRoot.Find("PricePill")?.GetComponent<Image>(),
                            SoldStampRoot = soldRoot,
                            SoldStampGroup = soldRoot != null ? soldRoot.GetComponent<CanvasGroup>() : null,
                            RestContentPos = contentRoot.anchoredPosition
                        };
                        int capturedIdx = slot.Index;
                        _relicSlots.Add(slot);

                        MerchantPointerRelay relay = slotRoot.GetComponent<MerchantPointerRelay>() ?? slotRoot.gameObject.AddComponent<MerchantPointerRelay>();
                        relay.Init(
                            onEnter: () => OnRelicSlotHoverEnter(slot),
                            onExit: () => OnRelicSlotHoverExit(slot),
                            onClick: () => TryBuyRelicAt(capturedIdx));
                    }
                }

                // Reset Medallion Button
                _resetButtonRoot = FindRect(_stallRoot, "BottomRelicsAndResetShelf/ResetZone/ResetButton");
                if (_resetButtonRoot != null)
                {
                    _resetPulseRing = _resetButtonRoot.Find("PulseRing")?.GetComponent<Image>();
                    _resetHoverGlow = _resetButtonRoot.Find("HoverGlow")?.GetComponent<Image>();
                    _resetMedallionSpinRect = _resetButtonRoot.Find("SpinningMedallion") as RectTransform;
                    _resetMedallionBg = _resetButtonRoot.Find("SpinningMedallion/InnerDisc")?.GetComponent<Image>();
                    _resetIconImage = _resetButtonRoot.Find("SpinningMedallion/InnerDisc/RefreshArrowsIcon")?.GetComponent<Image>();
                    _resetPricePillRect = _resetButtonRoot.Find("ResetPricePill") as RectTransform;
                    _resetPricePillBg = _resetPricePillRect != null ? _resetPricePillRect.GetComponent<Image>() : null;
                    _resetPriceText = FindTMP(_resetPricePillRect, "ResetPriceText");
                    _resetButton = _resetButtonRoot.GetComponent<Button>();

                    MerchantPointerRelay relay = _resetButtonRoot.GetComponent<MerchantPointerRelay>() ?? _resetButtonRoot.gameObject.AddComponent<MerchantPointerRelay>();
                    relay.Init(
                        onEnter: OnResetButtonHoverEnter,
                        onExit: OnResetButtonHoverExit,
                        onClick: () => TryResetShop());
                }
            }

            // Optional baked Deck Viewer / Card Removal Modal
            Transform existingModal = root.Find("MerchantDeckModal");
            if (existingModal != null)
            {
                _deckModalRoot = existingModal.gameObject;
                _deckModalTitle = FindTMP(existingModal, "Window/Title");
                _deckModalGrid = existingModal.Find("Window/Viewport/Content");
                _deckModalEmpty = FindTMP(existingModal, "Window/EmptyText");
                Button closeBtn = existingModal.Find("Window/CloseButton")?.GetComponent<Button>();
                if (closeBtn != null)
                {
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(CloseDeckModal);
                }
                _deckModalRoot.SetActive(false);
            }
        }

        private void PopulateEditorPreviewVisuals()
        {
            Canvas.ForceUpdateCanvases();

            // Preview cards directly in Edit Mode so designers see the real 2x4 grid immediately!
            if (cardPool != null)
            {
                List<MerchantOffer> previewCards = cardPool.BuildCandidateOffers(null, includeNewCards: true, includeUpgrades: includeUpgrades);
                for (int i = 0; i < _cardSlots.Count; i++)
                {
                    CardSlotUI slot = _cardSlots[i];
                    if (i < previewCards.Count && previewCards[i] != null)
                    {
                        if (slot.CardView != null)
                        {
                            slot.CardView.Setup(previewCards[i].PreviewCard, 1, i);
                            FitCardViewIntoContainer(slot.CardView, slot.CardContainer);
                        }
                        if (slot.PriceText != null) slot.PriceText.text = $"{previewCards[i].Price}g";
                    }
                }
            }

            // Preview relics directly in Edit Mode!
            if (relicPool != null)
            {
                List<RelicData> previewRelics = relicPool.BuildCandidateRewards(null, RelicRarityMask.All);
                for (int i = 0; i < _relicSlots.Count; i++)
                {
                    RelicSlotUI slot = _relicSlots[i];
                    if (i < previewRelics.Count && previewRelics[i] != null)
                    {
                        RelicData relic = previewRelics[i];
                        Color rarityColor = TopBarRelicSlotsUI.GetRarityBorderColor(relic.Rarity, new Color(0.92f, 0.76f, 0.36f, 1f));
                        if (slot.IconRing != null) slot.IconRing.color = rarityColor;
                        bool hasImg = relic.Image != null;
                        if (slot.IconImage != null)
                        {
                            slot.IconImage.enabled = hasImg;
                            if (hasImg)
                            {
                                slot.IconImage.sprite = relic.Image;
                                slot.IconImage.color = GetRelicIconTint(relic);
                            }
                        }
                        if (slot.MonogramText != null)
                        {
                            slot.MonogramText.gameObject.SetActive(!hasImg);
                            if (!hasImg) slot.MonogramText.text = TopBarRelicSlotsUI.BuildMonogram(relic.Title);
                        }
                        if (slot.NameText != null) slot.NameText.text = relic.Title;
                        int price = relic.GoldValue > 0 ? relic.GoldValue : fallbackRelicPrice;
                        if (slot.PriceText != null) slot.PriceText.text = $"{price}g";
                    }
                }
            }

            if (_goldValueText != null) _goldValueText.text = $"{standaloneTestGold}g";
            if (_resetPriceText != null) _resetPriceText.text = $"{baseResetCost}g";
            if (_dialogueText != null && greetingLines != null && greetingLines.Length > 0)
            {
                _dialogueText.text = greetingLines[0];
                _dialogueText.maxVisibleCharacters = greetingLines[0].Length;
            }
            ClearInspector();
        }

        private static RectTransform FindRect(Transform parent, string path)
        {
            if (parent == null) return null;
            Transform t = parent.Find(path);
            return t as RectTransform;
        }

        private static TextMeshProUGUI FindTMP(Transform parent, string path)
        {
            if (parent == null) return null;
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private static void AttachOrRebindHoverScale(RectTransform target, float hoverScale)
        {
            MerchantPointerRelay relay = target.GetComponent<MerchantPointerRelay>() ?? target.gameObject.AddComponent<MerchantPointerRelay>();
            relay.Init(
                onEnter: () =>
                {
                    Tune.SFX(SfxID.hover);
                    target.DOKill();
                    target.DOScale(hoverScale, 0.14f).SetEase(Ease.OutBack).SetUpdate(true);
                },
                onExit: () =>
                {
                    target.DOKill();
                    target.DOScale(Vector3.one, 0.14f).SetEase(Ease.OutCubic).SetUpdate(true);
                },
                onClick: null);
        }

        private void BuildBackdrop(RectTransform parent, int layer, Sprite solid)
        {
            RectTransform bgRt = CreateRect(parent, "ShopBackdrop", layer);
            StretchFull(bgRt);
            bgRt.localScale = new Vector3(1.06f, 1.06f, 1f);
            Image bgImg = bgRt.gameObject.AddComponent<Image>();
            if (backgroundSprite != null)
            {
                bgImg.sprite = backgroundSprite;
                bgImg.preserveAspect = false;
                bgImg.color = new Color(0.84f, 0.74f, 0.60f, 1f);
            }
            else
            {
                bgImg.sprite = solid;
                bgImg.color = new Color(0.16f, 0.12f, 0.09f, 1f);
            }
            bgImg.raycastTarget = false;

            // Cozy lantern vignette wash so parchment dialogue & cards pop cleanly
            RectTransform washRt = CreateRect(parent, "WarmVignetteWash", layer);
            StretchFull(washRt);
            Image washImg = washRt.gameObject.AddComponent<Image>();
            washImg.sprite = solid;
            washImg.color = new Color(0.12f, 0.08f, 0.05f, 0.48f);
            washImg.raycastTarget = false;
        }

        private void BuildAmbientMotes(RectTransform parent, int layer)
        {
            RectTransform moteLayer = CreateRect(parent, "AmbientMotes", layer);
            StretchFull(moteLayer);
            Sprite glow = GetSoftGlowSprite();

            _motes.Clear();
            for (int i = 0; i < 14; i++)
            {
                RectTransform mRt = CreateRect(moteLayer, $"Mote_{i:00}", layer);
                mRt.anchorMin = mRt.anchorMax = new Vector2(0.5f, 0.5f);
                mRt.pivot = new Vector2(0.5f, 0.5f);
                float size = UnityEngine.Random.Range(10f, 26f);
                mRt.sizeDelta = new Vector2(size, size);
                float bx = UnityEngine.Random.Range(-ReferenceWidth * 0.46f, ReferenceWidth * 0.46f);
                float by = UnityEngine.Random.Range(-ReferenceHeight * 0.48f, ReferenceHeight * 0.48f);
                mRt.anchoredPosition = new Vector2(bx, by);

                Image img = mRt.gameObject.AddComponent<Image>();
                img.sprite = glow;
                float alpha = UnityEngine.Random.Range(0.14f, 0.34f);
                img.color = new Color(1f, 0.90f, 0.58f, alpha);
                img.raycastTarget = false;

                _motes.Add(new FloatingMote
                {
                    Rect = mRt,
                    Image = img,
                    BaseX = bx,
                    SpeedY = UnityEngine.Random.Range(14f, 34f),
                    SwayAmp = UnityEngine.Random.Range(12f, 32f),
                    SwayFreq = UnityEngine.Random.Range(0.5f, 1.2f),
                    Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    BaseAlpha = alpha
                });
            }
        }

        private void BuildLeftColumn(RectTransform parent, int layer, TMP_FontAsset font, Sprite solid)
        {
            _leftColumnRoot = CreateRect(parent, "LeftColumn", layer);
            _leftColumnRoot.anchorMin = new Vector2(0f, 0f);
            _leftColumnRoot.anchorMax = new Vector2(0.49f, 1f);
            _leftColumnRoot.offsetMin = Vector2.zero;
            _leftColumnRoot.offsetMax = Vector2.zero;

            // --- 1. Top-Left Utility Controls: Back to Map + Gold Pill + Equipped Relics Strip ---
            RectTransform leaveRt = CreateRect(_leftColumnRoot, "LeaveToMapButton", layer);
            leaveRt.anchorMin = leaveRt.anchorMax = new Vector2(0f, 1f);
            leaveRt.pivot = new Vector2(0f, 1f);
            leaveRt.anchoredPosition = new Vector2(28f, -24f);
            leaveRt.sizeDelta = new Vector2(158f, 54f);
            Image leaveBg = leaveRt.gameObject.AddComponent<Image>();
            leaveBg.sprite = buttonSprite != null ? buttonSprite : solid;
            leaveBg.type = buttonSprite != null ? Image.Type.Simple : Image.Type.Simple;
            leaveBg.color = new Color(0.95f, 0.87f, 0.72f, 1f);
            _leaveButton = leaveRt.gameObject.AddComponent<Button>();
            _leaveButton.targetGraphic = leaveBg;
            _leaveButton.onClick.AddListener(ReturnToMap);
            AttachSimpleHoverScale(leaveRt, 1.06f);
            TextMeshProUGUI leaveTxt = CreateLabel(leaveRt, "Label", font, 21f, FontStyles.Bold, new Color(0.24f, 0.14f, 0.06f, 1f));
            leaveTxt.text = "<  MAP";
            leaveTxt.alignment = TextAlignmentOptions.Center;

            // Gold Pill right beside Leave button
            _goldPillRect = CreateRect(_leftColumnRoot, "GoldCounterPill", layer);
            _goldPillRect.anchorMin = _goldPillRect.anchorMax = new Vector2(0f, 1f);
            _goldPillRect.pivot = new Vector2(0f, 1f);
            _goldPillRect.anchoredPosition = new Vector2(198f, -24f);
            _goldPillRect.sizeDelta = new Vector2(168f, 54f);
            Image goldBg = _goldPillRect.gameObject.AddComponent<Image>();
            goldBg.sprite = buttonSprite != null ? buttonSprite : solid;
            goldBg.color = new Color(0.24f, 0.15f, 0.08f, 0.96f);

            RectTransform coinRt = CreateRect(_goldPillRect, "CoinDisc", layer);
            coinRt.anchorMin = coinRt.anchorMax = new Vector2(0f, 0.5f);
            coinRt.pivot = new Vector2(0f, 0.5f);
            coinRt.anchoredPosition = new Vector2(16f, 0f);
            coinRt.sizeDelta = new Vector2(28f, 28f);
            Image coinImg = coinRt.gameObject.AddComponent<Image>();
            coinImg.sprite = GetCircleSprite();
            coinImg.color = new Color(1f, 0.82f, 0.24f, 1f);
            coinImg.raycastTarget = false;
            TextMeshProUGUI coinG = CreateLabel(coinRt, "CoinG", font, 16f, FontStyles.Bold, new Color(0.36f, 0.20f, 0.02f, 1f));
            coinG.text = "G";
            coinG.alignment = TextAlignmentOptions.Center;

            _goldValueText = CreateLabel(_goldPillRect, "GoldValue", font, 25f, FontStyles.Bold, new Color(1f, 0.90f, 0.48f, 1f));
            _goldValueText.rectTransform.offsetMin = new Vector2(52f, 0f);
            _goldValueText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            _goldValueText.alignment = TextAlignmentOptions.MidlineLeft;
            _goldValueText.text = "150g";

            // Equipped Relics Mini-Row (5 slots below Map + Gold)
            RectTransform miniHeaderRt = CreateRect(_leftColumnRoot, "EquippedRelicsRow", layer);
            miniHeaderRt.anchorMin = miniHeaderRt.anchorMax = new Vector2(0f, 1f);
            miniHeaderRt.pivot = new Vector2(0f, 1f);
            miniHeaderRt.anchoredPosition = new Vector2(28f, -92f);
            miniHeaderRt.sizeDelta = new Vector2(338f, 70f);

            TextMeshProUGUI beltLabel = CreateLabel(miniHeaderRt, "BeltLabel", font, 13.5f, FontStyles.Bold, new Color(0.95f, 0.84f, 0.58f, 0.92f));
            beltLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            beltLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            beltLabel.rectTransform.pivot = new Vector2(0f, 1f);
            beltLabel.rectTransform.anchoredPosition = Vector2.zero;
            beltLabel.rectTransform.sizeDelta = new Vector2(300f, 20f);
            beltLabel.alignment = TextAlignmentOptions.MidlineLeft;
            beltLabel.text = "EQUIPPED RELICS (5 MAX)";

            _equippedMiniSlots.Clear();
            int maxRelics = RunRelicManager.Instance != null ? RunRelicManager.Instance.MaxEquippedRelics : RunRelicManager.DefaultMaxEquippedRelics;
            for (int i = 0; i < maxRelics; i++)
            {
                RectTransform mSlot = CreateRect(miniHeaderRt, $"MiniRelic_{i + 1}", layer);
                mSlot.anchorMin = mSlot.anchorMax = new Vector2(0f, 0f);
                mSlot.pivot = new Vector2(0f, 0f);
                mSlot.anchoredPosition = new Vector2(i * 54f, 0f);
                mSlot.sizeDelta = new Vector2(46f, 46f);

                Image border = mSlot.gameObject.AddComponent<Image>();
                border.sprite = solid;
                border.color = new Color(0.68f, 0.56f, 0.36f, 0.65f);

                RectTransform inner = CreateRect(mSlot, "Inner", layer);
                StretchFull(inner, 3f);
                Image bg = inner.gameObject.AddComponent<Image>();
                bg.sprite = solid;
                bg.color = new Color(0.18f, 0.13f, 0.09f, 0.8f);
                bg.raycastTarget = false;

                RectTransform iconRt = CreateRect(inner, "Icon", layer);
                StretchFull(iconRt, 5f);
                Image icon = iconRt.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;

                TextMeshProUGUI mono = CreateLabel(inner, "Mono", font, 16f, FontStyles.Bold, new Color(1f, 0.88f, 0.52f, 1f));
                mono.alignment = TextAlignmentOptions.Center;
                mono.gameObject.SetActive(false);

                Button btn = mSlot.gameObject.AddComponent<Button>();
                btn.targetGraphic = border;
                btn.onClick.AddListener(() =>
                {
                    EnsureRelicSwapModal();
                    _relicSwapModal?.OpenManageMode();
                });

                _equippedMiniSlots.Add(new EquippedMiniSlotUI
                {
                    Root = mSlot,
                    Border = border,
                    Bg = bg,
                    Icon = icon,
                    Monogram = mono
                });
            }

            // Deck Viewer & Card Removal Buttons right below Equipped Relics strip
            RectTransform deckBtnRt = CreateRect(_leftColumnRoot, "ShopDeckButton", layer);
            deckBtnRt.anchorMin = deckBtnRt.anchorMax = new Vector2(0f, 1f);
            deckBtnRt.pivot = new Vector2(0f, 1f);
            deckBtnRt.anchoredPosition = new Vector2(28f, -174f);
            deckBtnRt.sizeDelta = new Vector2(142f, 46f);
            Image deckBg = deckBtnRt.gameObject.AddComponent<Image>();
            deckBg.sprite = buttonSprite != null ? buttonSprite : solid;
            deckBg.color = new Color(0.92f, 0.84f, 0.68f, 1f);
            deckBtnRt.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenDeckModal(removalMode: false));
            AttachSimpleHoverScale(deckBtnRt, 1.06f);
            _deckBtnLabel = CreateLabel(deckBtnRt, "Label", font, 17f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.05f, 1f));
            _deckBtnLabel.alignment = TextAlignmentOptions.Center;
            _deckBtnLabel.text = "DECK (0)";

            _removeBtnRect = CreateRect(_leftColumnRoot, "CardRemovalButton", layer);
            _removeBtnRect.anchorMin = _removeBtnRect.anchorMax = new Vector2(0f, 1f);
            _removeBtnRect.pivot = new Vector2(0f, 1f);
            _removeBtnRect.anchoredPosition = new Vector2(180f, -174f);
            _removeBtnRect.sizeDelta = new Vector2(186f, 46f);
            Image remBg = _removeBtnRect.gameObject.AddComponent<Image>();
            remBg.sprite = buttonSprite != null ? buttonSprite : solid;
            remBg.color = new Color(0.96f, 0.78f, 0.66f, 1f);
            _removeBtnRect.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenDeckModal(removalMode: true));
            AttachSimpleHoverScale(_removeBtnRect, 1.06f);
            _removeBtnLabel = CreateLabel(_removeBtnRect, "Label", font, 16.5f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            _removeBtnLabel.alignment = TextAlignmentOptions.Center;
            _removeBtnLabel.text = $"REMOVE {baseRemovalCost}g";

            // --- 2. Top-Center-Left: Item Desc Inspector Box (Matches Figma top-center-left box!) ---
            _inspectorRoot = CreateRect(_leftColumnRoot, "ItemDescPanel", layer);
            _inspectorRoot.anchorMin = _inspectorRoot.anchorMax = new Vector2(1f, 1f);
            _inspectorRoot.pivot = new Vector2(1f, 1f);
            _inspectorRoot.anchoredPosition = new Vector2(-18f, -24f);
            _inspectorRoot.sizeDelta = new Vector2(532f, 242f);

            Image inspBg = _inspectorRoot.gameObject.AddComponent<Image>();
            inspBg.sprite = buttonSprite != null ? buttonSprite : solid;
            inspBg.color = new Color(0.96f, 0.90f, 0.78f, 0.98f);
            inspBg.raycastTarget = false;
            AddShadow(_inspectorRoot.gameObject, new Color(0.06f, 0.03f, 0.01f, 0.55f), new Vector2(0f, -6f));

            RectTransform inspInner = CreateRect(_inspectorRoot, "InnerInset", layer);
            StretchFull(inspInner, 14f);
            _inspectorBorderGlow = inspInner.gameObject.AddComponent<Image>();
            _inspectorBorderGlow.sprite = solid;
            _inspectorBorderGlow.color = new Color(0.95f, 0.89f, 0.76f, 0.35f);
            _inspectorBorderGlow.raycastTarget = false;

            _inspectorTitleText = CreateLabel(inspInner, "ItemTitle", font, 25f, FontStyles.Bold, new Color(0.22f, 0.12f, 0.05f, 1f));
            _inspectorTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _inspectorTitleText.rectTransform.anchorMax = new Vector2(0.76f, 1f);
            _inspectorTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            _inspectorTitleText.rectTransform.offsetMin = new Vector2(12f, -42f);
            _inspectorTitleText.rectTransform.offsetMax = new Vector2(0f, -6f);
            _inspectorTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            _inspectorTitleText.enableAutoSizing = true;
            _inspectorTitleText.fontSizeMin = 17f;
            _inspectorTitleText.fontSizeMax = 25f;

            _inspectorPriceText = CreateLabel(inspInner, "ItemPrice", font, 24f, FontStyles.Bold, new Color(0.55f, 0.35f, 0.04f, 1f));
            _inspectorPriceText.rectTransform.anchorMin = new Vector2(0.74f, 1f);
            _inspectorPriceText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _inspectorPriceText.rectTransform.pivot = new Vector2(1f, 1f);
            _inspectorPriceText.rectTransform.offsetMin = new Vector2(0f, -42f);
            _inspectorPriceText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            _inspectorPriceText.alignment = TextAlignmentOptions.MidlineRight;

            _inspectorMetaText = CreateLabel(inspInner, "ItemMeta", font, 14f, FontStyles.Bold, new Color(0.49f, 0.32f, 0.14f, 0.95f));
            _inspectorMetaText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _inspectorMetaText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _inspectorMetaText.rectTransform.pivot = new Vector2(0f, 1f);
            _inspectorMetaText.rectTransform.offsetMin = new Vector2(12f, -66f);
            _inspectorMetaText.rectTransform.offsetMax = new Vector2(-12f, -42f);
            _inspectorMetaText.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform inspDiv = CreateRect(inspInner, "Divider", layer);
            inspDiv.anchorMin = new Vector2(0f, 1f);
            inspDiv.anchorMax = new Vector2(1f, 1f);
            inspDiv.pivot = new Vector2(0.5f, 1f);
            inspDiv.offsetMin = new Vector2(12f, -72f);
            inspDiv.offsetMax = new Vector2(-12f, -70f);
            Image divImg = inspDiv.gameObject.AddComponent<Image>();
            divImg.sprite = solid;
            divImg.color = new Color(0.46f, 0.30f, 0.14f, 0.45f);
            divImg.raycastTarget = false;

            _inspectorBodyText = CreateLabel(inspInner, "ItemBody", font, 19f, FontStyles.Normal, new Color(0.24f, 0.15f, 0.07f, 1f));
            _inspectorBodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _inspectorBodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _inspectorBodyText.rectTransform.offsetMin = new Vector2(12f, 8f);
            _inspectorBodyText.rectTransform.offsetMax = new Vector2(-12f, -80f);
            _inspectorBodyText.alignment = TextAlignmentOptions.TopLeft;
            _inspectorBodyText.textWrappingMode = TextWrappingModes.Normal;
            _inspectorBodyText.enableAutoSizing = true;
            _inspectorBodyText.fontSizeMin = 14f;
            _inspectorBodyText.fontSizeMax = 19.5f;

            // --- 3. Center-Left: Talking Shop Vendor Character (Matches Figma green cat & Crazy Dave!) ---
            RectTransform vendorStage = CreateRect(_leftColumnRoot, "VendorStage", layer);
            vendorStage.anchorMin = vendorStage.anchorMax = new Vector2(0.42f, 0.48f);
            vendorStage.pivot = new Vector2(0.5f, 0.5f);
            vendorStage.anchoredPosition = new Vector2(-36f, -14f);
            vendorStage.sizeDelta = new Vector2(440f, 440f);

            // Warm lantern spotlight halo behind the shopkeeper
            RectTransform spotRt = CreateRect(vendorStage, "VendorSpotlight", layer);
            spotRt.anchorMin = spotRt.anchorMax = new Vector2(0.5f, 0.52f);
            spotRt.pivot = new Vector2(0.5f, 0.5f);
            spotRt.sizeDelta = new Vector2(450f, 450f);
            _vendorSpotlight = spotRt.gameObject.AddComponent<Image>();
            _vendorSpotlight.sprite = GetSoftGlowSprite();
            _vendorSpotlight.color = new Color(1f, 0.84f, 0.46f, 0.26f);
            _vendorSpotlight.raycastTarget = false;

            // Ground shadow oval
            RectTransform shadowRt = CreateRect(vendorStage, "VendorShadow", layer);
            shadowRt.anchorMin = shadowRt.anchorMax = new Vector2(0.5f, 0.12f);
            shadowRt.pivot = new Vector2(0.5f, 0.5f);
            shadowRt.sizeDelta = new Vector2(270f, 42f);
            Image shadowImg = shadowRt.gameObject.AddComponent<Image>();
            shadowImg.sprite = GetSoftGlowSprite();
            shadowImg.color = new Color(0.05f, 0.03f, 0.01f, 0.55f);
            shadowImg.raycastTarget = false;

            // Layered transforms: FloatRoot (procedural breathing/talk) -> AnimRoot (action jumps/shakes)
            _vendorFloatRoot = CreateRect(vendorStage, "VendorFloatRoot", layer);
            StretchFull(_vendorFloatRoot);

            _vendorAnimRoot = CreateRect(_vendorFloatRoot, "VendorAnimRoot", layer);
            StretchFull(_vendorAnimRoot);

            RectTransform charImgRt = CreateRect(_vendorAnimRoot, "VendorVisual", layer);
            charImgRt.anchorMin = charImgRt.anchorMax = new Vector2(0.5f, 0.5f);
            charImgRt.pivot = new Vector2(0.5f, 0.5f);
            charImgRt.sizeDelta = new Vector2(356f, 356f);
            _vendorImage = charImgRt.gameObject.AddComponent<Image>();
            _vendorImage.sprite = vendorSprite != null ? vendorSprite : GetCircleSprite();
            _vendorImage.preserveAspect = true;
            _vendorImage.color = Color.white;

            Button vendorPokeBtn = charImgRt.gameObject.AddComponent<Button>();
            vendorPokeBtn.transition = Selectable.Transition.None;
            vendorPokeBtn.onClick.AddListener(PokeVendor);

            // --- 4. Bottom-Left: Native-Art Vendor Dialogue Box (Panel Box Text_0 + Nameplate!) ---
            _dialogueBoxRoot = CreateRect(_leftColumnRoot, "VendorDialogueBox", layer);
            _dialogueBoxRoot.anchorMin = new Vector2(0f, 0f);
            _dialogueBoxRoot.anchorMax = new Vector2(1f, 0f);
            _dialogueBoxRoot.pivot = new Vector2(0.5f, 0f);
            _dialogueBoxRoot.offsetMin = new Vector2(24f, 24f);
            _dialogueBoxRoot.offsetMax = new Vector2(-16f, 232f);

            Image boxImg = _dialogueBoxRoot.gameObject.AddComponent<Image>();
            boxImg.sprite = dialogueBoxSprite != null ? dialogueBoxSprite : (buttonSprite != null ? buttonSprite : solid);
            boxImg.color = Color.white;
            AddShadow(_dialogueBoxRoot.gameObject, new Color(0.05f, 0.02f, 0.01f, 0.58f), new Vector2(0f, -6f));

            Button dlgClickBtn = _dialogueBoxRoot.gameObject.AddComponent<Button>();
            dlgClickBtn.transition = Selectable.Transition.None;
            dlgClickBtn.onClick.AddListener(CompleteDialogueImmediately);

            // Ornate Nameplate overlapping top-left of dialogue box (just like CONTOH !.png!)
            RectTransform nameRt = CreateRect(_dialogueBoxRoot, "VendorNameplate", layer);
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0f, 1f);
            nameRt.pivot = new Vector2(0f, 0.42f);
            nameRt.anchoredPosition = new Vector2(18f, 4f);
            nameRt.sizeDelta = new Vector2(276f, 58f);
            Image nameImg = nameRt.gameObject.AddComponent<Image>();
            nameImg.sprite = nameplateSprite != null ? nameplateSprite : solid;
            nameImg.color = Color.white;
            nameImg.raycastTarget = false;

            TextMeshProUGUI nameTxt = CreateLabel(nameRt, "NameText", font, 22f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            nameTxt.text = vendorName;
            nameTxt.alignment = TextAlignmentOptions.Center;

            _dialogueText = CreateLabel(_dialogueBoxRoot, "VendorText", font, 24f, FontStyles.Normal, new Color(0.21f, 0.12f, 0.05f, 1f));
            _dialogueText.rectTransform.anchorMin = Vector2.zero;
            _dialogueText.rectTransform.anchorMax = Vector2.one;
            _dialogueText.rectTransform.offsetMin = new Vector2(46f, 36f);
            _dialogueText.rectTransform.offsetMax = new Vector2(-86f, -34f);
            _dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            _dialogueText.textWrappingMode = TextWrappingModes.Normal;
            _dialogueText.enableAutoSizing = true;
            _dialogueText.fontSizeMin = 17f;
            _dialogueText.fontSizeMax = 25f;

            _dialogueDoneDiamond = CreateRect(_dialogueBoxRoot, "DonePrompt", layer);
            _dialogueDoneDiamond.anchorMin = _dialogueDoneDiamond.anchorMax = new Vector2(1f, 0f);
            _dialogueDoneDiamond.pivot = new Vector2(0.5f, 0.5f);
            _dialogueDoneDiamond.anchoredPosition = new Vector2(-64f, 36f);
            _dialogueDoneDiamond.sizeDelta = new Vector2(14f, 14f);
            _dialogueDoneDiamond.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image diaImg = _dialogueDoneDiamond.gameObject.AddComponent<Image>();
            diaImg.sprite = solid;
            diaImg.color = new Color(0.54f, 0.31f, 0.10f, 0.92f);
            diaImg.raycastTarget = false;
            _dialogueDoneDiamond.gameObject.SetActive(false);
        }

        private void BuildRightShopStall(RectTransform parent, int layer, TMP_FontAsset font, Sprite solid)
        {
            _stallRoot = CreateRect(parent, "ShopStallPanel", layer);
            _stallRoot.anchorMin = new Vector2(0.495f, 0f);
            _stallRoot.anchorMax = new Vector2(1f, 1f);
            _stallRoot.pivot = new Vector2(0.5f, 0.5f);
            _stallRoot.offsetMin = new Vector2(6f, 24f);
            _stallRoot.offsetMax = new Vector2(-24f, -26f);

            Image stallBg = _stallRoot.gameObject.AddComponent<Image>();
            stallBg.sprite = stallPanelSprite != null ? stallPanelSprite : solid;
            stallBg.color = stallPanelSprite != null ? Color.white : new Color(0.20f, 0.13f, 0.08f, 0.96f);
            AddShadow(_stallRoot.gameObject, new Color(0.04f, 0.02f, 0.01f, 0.62f), new Vector2(0f, -8f));

            // Top Ornate Stall Header Banner
            RectTransform bannerRt = CreateRect(_stallRoot, "StallHeaderBanner", layer);
            bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 0.55f);
            bannerRt.anchoredPosition = new Vector2(0f, -2f);
            bannerRt.sizeDelta = new Vector2(440f, 58f);
            Image bannerImg = bannerRt.gameObject.AddComponent<Image>();
            bannerImg.sprite = nameplateSprite != null ? nameplateSprite : (buttonSprite != null ? buttonSprite : solid);
            bannerImg.color = Color.white;
            bannerImg.raycastTarget = false;

            if (merchantIconSprite != null)
            {
                RectTransform iconRt = CreateRect(bannerRt, "MerchantEmblem", layer);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(58f, 0f);
                iconRt.sizeDelta = new Vector2(40f, 40f);
                Image mIcon = iconRt.gameObject.AddComponent<Image>();
                mIcon.sprite = merchantIconSprite;
                mIcon.preserveAspect = true;
                mIcon.raycastTarget = false;
            }

            TextMeshProUGUI bannerTxt = CreateLabel(bannerRt, "Title", font, 23f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            bannerTxt.rectTransform.offsetMin = new Vector2(merchantIconSprite != null ? 44f : 0f, 0f);
            bannerTxt.text = shopBannerTitle;
            bannerTxt.alignment = TextAlignmentOptions.Center;

            // --- Upper Zone: 2 Rows x 4 Columns = 8 Card Slots ---
            RectTransform cardsGridArea = CreateRect(_stallRoot, "CardOffersGrid", layer);
            cardsGridArea.anchorMin = new Vector2(0f, 0.27f);
            cardsGridArea.anchorMax = new Vector2(1f, 1f);
            cardsGridArea.offsetMin = new Vector2(36f, 10f);
            cardsGridArea.offsetMax = new Vector2(-36f, -52f);

            BuildCardSlotsGrid(cardsGridArea, layer, font, solid);

            // --- Decorative Gold Divider ---
            RectTransform divider = CreateRect(_stallRoot, "ShelfDivider", layer);
            divider.anchorMin = new Vector2(0f, 0.265f);
            divider.anchorMax = new Vector2(1f, 0.265f);
            divider.pivot = new Vector2(0.5f, 0.5f);
            divider.offsetMin = new Vector2(42f, -1.5f);
            divider.offsetMax = new Vector2(-42f, 1.5f);
            Image divImg = divider.gameObject.AddComponent<Image>();
            divImg.sprite = solid;
            divImg.color = new Color(0.86f, 0.68f, 0.34f, 0.55f);
            divImg.raycastTarget = false;

            // --- Bottom Shelf: Relics (Left/Center) BESIDE Circular RESET Button (Bottom-Right) ---
            RectTransform bottomShelf = CreateRect(_stallRoot, "BottomRelicsAndResetShelf", layer);
            bottomShelf.anchorMin = new Vector2(0f, 0f);
            bottomShelf.anchorMax = new Vector2(1f, 0.265f);
            bottomShelf.offsetMin = new Vector2(36f, 26f);
            bottomShelf.offsetMax = new Vector2(-32f, -10f);

            BuildRelicShelf(bottomShelf, layer, font, solid);
            BuildResetButton(bottomShelf, layer, font, solid);
        }

        private void BuildCardSlotsGrid(RectTransform parent, int layer, TMP_FontAsset font, Sprite solid)
        {
            _cardSlots.Clear();

            const int cols = 4;
            int rows = Mathf.CeilToInt(Mathf.Max(1, cardOfferCount) / (float)cols);
            float colStep = 1f / cols;
            float rowStep = 1f / rows;

            for (int i = 0; i < cardOfferCount; i++)
            {
                int col = i % cols;
                int row = i / cols;

                RectTransform slotRoot = CreateRect(parent, $"CardOfferSlot_{i + 1:00}", layer);
                slotRoot.anchorMin = new Vector2(col * colStep, 1f - (row + 1) * rowStep);
                slotRoot.anchorMax = new Vector2((col + 1) * colStep, 1f - row * rowStep);
                slotRoot.offsetMin = new Vector2(6f, 4f);
                slotRoot.offsetMax = new Vector2(-6f, -4f);

                // Raycast catcher on SlotRoot so hover never jitters when card lifts!
                Image hitArea = slotRoot.gameObject.AddComponent<Image>();
                hitArea.sprite = solid;
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);

                RectTransform contentRoot = CreateRect(slotRoot, "Content", layer);
                StretchFull(contentRoot);
                CanvasGroup contentGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
                contentGroup.blocksRaycasts = false;

                // Subtle golden hover glow frame behind card
                RectTransform glowRt = CreateRect(contentRoot, "HoverGlow", layer);
                glowRt.anchorMin = new Vector2(0.05f, 0.14f);
                glowRt.anchorMax = new Vector2(0.95f, 0.99f);
                glowRt.offsetMin = new Vector2(-6f, -6f);
                glowRt.offsetMax = new Vector2(6f, 6f);
                Image glowImg = glowRt.gameObject.AddComponent<Image>();
                glowImg.sprite = GetStampBorderSprite();
                glowImg.type = Image.Type.Sliced;
                glowImg.color = new Color(1f, 0.86f, 0.36f, 0f);
                glowImg.raycastTarget = false;

                // Card Display Container (top 84% of cell)
                RectTransform cardHolder = CreateRect(contentRoot, "CardHolder", layer);
                cardHolder.anchorMin = new Vector2(0.04f, 0.15f);
                cardHolder.anchorMax = new Vector2(0.96f, 0.99f);
                cardHolder.offsetMin = Vector2.zero;
                cardHolder.offsetMax = Vector2.zero;

                RunDeckCardView cardView = null;
                if (cardViewPrefab != null)
                {
                    cardView = Instantiate(cardViewPrefab, cardHolder);
                    RectTransform cvRt = cardView.GetComponent<RectTransform>();
                    if (cvRt != null)
                    {
                        cvRt.anchorMin = cvRt.anchorMax = new Vector2(0.5f, 0.5f);
                        cvRt.pivot = new Vector2(0.5f, 0.5f);
                        cvRt.anchoredPosition = Vector2.zero;
                        cvRt.localRotation = Quaternion.identity;
                    }
                    // Prevent card prefab internal raycasts from blocking slot click/hover
                    CanvasGroup cg = cardView.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }
                }

                // Shimmer flash overlay for procedural reset wave!
                RectTransform shimmerRt = CreateRect(contentRoot, "ShimmerOverlay", layer);
                shimmerRt.anchorMin = new Vector2(0.08f, 0.16f);
                shimmerRt.anchorMax = new Vector2(0.92f, 0.98f);
                shimmerRt.offsetMin = Vector2.zero;
                shimmerRt.offsetMax = Vector2.zero;
                Image shimmerImg = shimmerRt.gameObject.AddComponent<Image>();
                shimmerImg.sprite = solid;
                shimmerImg.color = new Color(1f, 0.92f, 0.56f, 0f);
                shimmerImg.raycastTarget = false;
                shimmerRt.gameObject.SetActive(false);

                // Price Tag directly below card (matches Figma "100g / 120g" italic-gold styling!)
                RectTransform pricePillRt = CreateRect(contentRoot, "PricePill", layer);
                pricePillRt.anchorMin = new Vector2(0.18f, 0.01f);
                pricePillRt.anchorMax = new Vector2(0.82f, 0.145f);
                pricePillRt.offsetMin = Vector2.zero;
                pricePillRt.offsetMax = Vector2.zero;
                Image pricePillImg = pricePillRt.gameObject.AddComponent<Image>();
                pricePillImg.sprite = solid;
                pricePillImg.color = new Color(0.11f, 0.06f, 0.03f, 0.72f);
                pricePillImg.raycastTarget = false;

                TextMeshProUGUI priceTxt = CreateLabel(pricePillRt, "PriceText", font, 22f, FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.89f, 0.46f, 1f));
                priceTxt.alignment = TextAlignmentOptions.Center;
                priceTxt.text = "100g";

                // Angled crimson SOLD stamp (matches Figma diagonal red SOLD!)
                BuildSoldStamp(slotRoot, layer, font, out RectTransform soldRoot, out CanvasGroup soldGroup, new Vector2(158f, 52f), 32f);

                CardSlotUI slot = new()
                {
                    Index = i,
                    Root = slotRoot,
                    ContentRoot = contentRoot,
                    ContentGroup = contentGroup,
                    HoverGlow = glowImg,
                    ShimmerOverlay = shimmerImg,
                    CardContainer = cardHolder,
                    CardView = cardView,
                    PriceText = priceTxt,
                    PricePill = pricePillImg,
                    SoldStampRoot = soldRoot,
                    SoldStampGroup = soldGroup,
                    RestContentPos = Vector2.zero
                };
                _cardSlots.Add(slot);

                MerchantPointerRelay relay = slotRoot.gameObject.AddComponent<MerchantPointerRelay>();
                int captured = i;
                relay.Init(
                    onEnter: () => OnCardSlotHoverEnter(slot),
                    onExit: () => OnCardSlotHoverExit(slot),
                    onClick: () => TryBuyCardAt(captured));
            }
        }

        private void BuildRelicShelf(RectTransform bottomShelf, int layer, TMP_FontAsset font, Sprite solid)
        {
            _relicSlots.Clear();

            // Left/center 74% of bottom shelf holds the 3 Relics right beside the Reset button!
            RectTransform relicsZone = CreateRect(bottomShelf, "RelicsZone", layer);
            relicsZone.anchorMin = new Vector2(0f, 0f);
            relicsZone.anchorMax = new Vector2(0.755f, 1f);
            relicsZone.offsetMin = Vector2.zero;
            relicsZone.offsetMax = new Vector2(-12f, 0f);

            TextMeshProUGUI sectionLabel = CreateLabel(relicsZone, "RelicsTitle", font, 16f, FontStyles.Bold, new Color(0.98f, 0.85f, 0.52f, 0.95f));
            sectionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            sectionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            sectionLabel.rectTransform.pivot = new Vector2(0f, 1f);
            sectionLabel.rectTransform.offsetMin = new Vector2(4f, -24f);
            sectionLabel.rectTransform.offsetMax = new Vector2(0f, 0f);
            sectionLabel.alignment = TextAlignmentOptions.MidlineLeft;
            sectionLabel.text = "RELICS";

            RectTransform rowContainer = CreateRect(relicsZone, "RelicsRow", layer);
            rowContainer.anchorMin = new Vector2(0f, 0f);
            rowContainer.anchorMax = new Vector2(1f, 1f);
            rowContainer.offsetMin = new Vector2(0f, 0f);
            rowContainer.offsetMax = new Vector2(0f, -26f);

            int count = Mathf.Max(1, relicOfferCount);
            float step = 1f / count;

            for (int i = 0; i < count; i++)
            {
                RectTransform slotRoot = CreateRect(rowContainer, $"RelicOfferSlot_{i + 1:00}", layer);
                slotRoot.anchorMin = new Vector2(i * step, 0f);
                slotRoot.anchorMax = new Vector2((i + 1) * step, 1f);
                slotRoot.offsetMin = new Vector2(6f, 2f);
                slotRoot.offsetMax = new Vector2(-6f, -2f);

                Image hitArea = slotRoot.gameObject.AddComponent<Image>();
                hitArea.sprite = solid;
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);

                RectTransform contentRoot = CreateRect(slotRoot, "Content", layer);
                StretchFull(contentRoot);
                CanvasGroup contentGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
                contentGroup.blocksRaycasts = false;

                // Golden hover highlight
                RectTransform glowRt = CreateRect(contentRoot, "HoverGlow", layer);
                StretchFull(glowRt, -4f);
                Image glowImg = glowRt.gameObject.AddComponent<Image>();
                glowImg.sprite = GetStampBorderSprite();
                glowImg.type = Image.Type.Sliced;
                glowImg.color = new Color(1f, 0.86f, 0.36f, 0f);
                glowImg.raycastTarget = false;

                // Pedestal Frame using existing Tombol.png beveled panel
                RectTransform pedestalRt = CreateRect(contentRoot, "Pedestal", layer);
                pedestalRt.anchorMin = new Vector2(0f, 0.22f);
                pedestalRt.anchorMax = new Vector2(1f, 1f);
                pedestalRt.offsetMin = Vector2.zero;
                pedestalRt.offsetMax = Vector2.zero;
                Image frameBg = pedestalRt.gameObject.AddComponent<Image>();
                frameBg.sprite = buttonSprite != null ? buttonSprite : solid;
                frameBg.color = new Color(0.32f, 0.21f, 0.12f, 0.96f);
                frameBg.raycastTarget = false;

                // Circular rarity ring + icon socket
                RectTransform ringRt = CreateRect(pedestalRt, "IconRing", layer);
                ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.64f);
                ringRt.pivot = new Vector2(0.5f, 0.5f);
                ringRt.sizeDelta = new Vector2(72f, 72f);
                Image ringImg = ringRt.gameObject.AddComponent<Image>();
                ringImg.sprite = GetCircleSprite();
                ringImg.color = new Color(0.92f, 0.76f, 0.36f, 1f);
                ringImg.raycastTarget = false;

                RectTransform socketRt = CreateRect(ringRt, "Socket", layer);
                StretchFull(socketRt, 4f);
                Image socketImg = socketRt.gameObject.AddComponent<Image>();
                socketImg.sprite = GetCircleSprite();
                socketImg.color = new Color(0.14f, 0.10f, 0.07f, 1f);
                socketImg.raycastTarget = false;

                RectTransform iconRt = CreateRect(socketRt, "Icon", layer);
                StretchFull(iconRt, 10f);
                Image iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.enabled = false;

                TextMeshProUGUI monoTxt = CreateLabel(socketRt, "Monogram", font, 24f, FontStyles.Bold, new Color(1f, 0.88f, 0.48f, 1f));
                monoTxt.alignment = TextAlignmentOptions.Center;

                // Relic Name at bottom of pedestal
                TextMeshProUGUI nameTxt = CreateLabel(pedestalRt, "RelicName", font, 15.5f, FontStyles.Bold, new Color(0.98f, 0.92f, 0.78f, 1f));
                nameTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
                nameTxt.rectTransform.anchorMax = new Vector2(1f, 0.30f);
                nameTxt.rectTransform.offsetMin = new Vector2(10f, 4f);
                nameTxt.rectTransform.offsetMax = new Vector2(-10f, 0f);
                nameTxt.alignment = TextAlignmentOptions.Center;
                nameTxt.enableAutoSizing = true;
                nameTxt.fontSizeMin = 12f;
                nameTxt.fontSizeMax = 15.5f;

                // Shimmer flash overlay
                RectTransform shimmerRt = CreateRect(contentRoot, "ShimmerOverlay", layer);
                StretchFull(shimmerRt, 2f);
                Image shimmerImg = shimmerRt.gameObject.AddComponent<Image>();
                shimmerImg.sprite = solid;
                shimmerImg.color = new Color(1f, 0.90f, 0.50f, 0f);
                shimmerImg.raycastTarget = false;
                shimmerRt.gameObject.SetActive(false);

                // Price Pill below pedestal
                RectTransform pricePillRt = CreateRect(contentRoot, "PricePill", layer);
                pricePillRt.anchorMin = new Vector2(0.14f, 0f);
                pricePillRt.anchorMax = new Vector2(0.86f, 0.20f);
                pricePillRt.offsetMin = Vector2.zero;
                pricePillRt.offsetMax = Vector2.zero;
                Image pricePillImg = pricePillRt.gameObject.AddComponent<Image>();
                pricePillImg.sprite = solid;
                pricePillImg.color = new Color(0.11f, 0.06f, 0.03f, 0.78f);
                pricePillImg.raycastTarget = false;

                TextMeshProUGUI priceTxt = CreateLabel(pricePillRt, "PriceText", font, 20f, FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.89f, 0.46f, 1f));
                priceTxt.alignment = TextAlignmentOptions.Center;
                priceTxt.text = "150g";

                // Angled crimson SOLD stamp (centered cleanly over the relic icon)
                BuildSoldStamp(slotRoot, layer, font, out RectTransform soldRoot, out CanvasGroup soldGroup, new Vector2(138f, 44f), 27f);
                soldRoot.anchorMin = soldRoot.anchorMax = new Vector2(0.5f, 0.65f);

                RelicSlotUI slot = new()
                {
                    Index = i,
                    Root = slotRoot,
                    ContentRoot = contentRoot,
                    ContentGroup = contentGroup,
                    FrameBg = frameBg,
                    HoverGlow = glowImg,
                    ShimmerOverlay = shimmerImg,
                    IconRing = ringImg,
                    IconImage = iconImg,
                    MonogramText = monoTxt,
                    NameText = nameTxt,
                    PriceText = priceTxt,
                    PricePill = pricePillImg,
                    SoldStampRoot = soldRoot,
                    SoldStampGroup = soldGroup,
                    RestContentPos = Vector2.zero
                };
                _relicSlots.Add(slot);

                MerchantPointerRelay relay = slotRoot.gameObject.AddComponent<MerchantPointerRelay>();
                int captured = i;
                relay.Init(
                    onEnter: () => OnRelicSlotHoverEnter(slot),
                    onExit: () => OnRelicSlotHoverExit(slot),
                    onClick: () => TryBuyRelicAt(captured));
            }
        }

        private void BuildResetButton(RectTransform bottomShelf, int layer, TMP_FontAsset font, Sprite solid)
        {
            // Bottom-Right corner (right beside the 3 Relics!) — circular RESET medallion matching Figma!
            RectTransform resetZone = CreateRect(bottomShelf, "ResetZone", layer);
            resetZone.anchorMin = new Vector2(0.76f, 0f);
            resetZone.anchorMax = new Vector2(1f, 1f);
            resetZone.offsetMin = Vector2.zero;
            resetZone.offsetMax = Vector2.zero;

            _resetButtonRoot = CreateRect(resetZone, "ResetButton", layer);
            _resetButtonRoot.anchorMin = _resetButtonRoot.anchorMax = new Vector2(0.54f, 0.54f);
            _resetButtonRoot.pivot = new Vector2(0.5f, 0.5f);
            _resetButtonRoot.sizeDelta = new Vector2(154f, 154f);

            // Expanding golden burst ring for Reset click animation
            RectTransform pulseRt = CreateRect(_resetButtonRoot, "PulseRing", layer);
            pulseRt.anchorMin = pulseRt.anchorMax = new Vector2(0.5f, 0.5f);
            pulseRt.pivot = new Vector2(0.5f, 0.5f);
            pulseRt.sizeDelta = new Vector2(160f, 160f);
            _resetPulseRing = pulseRt.gameObject.AddComponent<Image>();
            _resetPulseRing.sprite = GetCircleRingSprite();
            _resetPulseRing.color = new Color(1f, 0.88f, 0.44f, 0f);
            _resetPulseRing.raycastTarget = false;
            pulseRt.gameObject.SetActive(false);

            // Hover golden halo
            RectTransform glowRt = CreateRect(_resetButtonRoot, "HoverGlow", layer);
            glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = new Vector2(174f, 174f);
            _resetHoverGlow = glowRt.gameObject.AddComponent<Image>();
            _resetHoverGlow.sprite = GetSoftGlowSprite();
            _resetHoverGlow.color = new Color(1f, 0.84f, 0.36f, 0f);
            _resetHoverGlow.raycastTarget = false;

            // Spinning medallion body (outer espresso rim + inner warm parchment gold disc + curved arrows)
            _resetMedallionSpinRect = CreateRect(_resetButtonRoot, "SpinningMedallion", layer);
            StretchFull(_resetMedallionSpinRect);

            Image outerRim = _resetMedallionSpinRect.gameObject.AddComponent<Image>();
            outerRim.sprite = GetCircleSprite();
            outerRim.color = new Color(0.28f, 0.16f, 0.06f, 1f);
            AddShadow(_resetMedallionSpinRect.gameObject, new Color(0.04f, 0.02f, 0.01f, 0.6f), new Vector2(0f, -5f));

            RectTransform innerDisc = CreateRect(_resetMedallionSpinRect, "InnerDisc", layer);
            StretchFull(innerDisc, 7f);
            _resetMedallionBg = innerDisc.gameObject.AddComponent<Image>();
            _resetMedallionBg.sprite = GetCircleSprite();
            _resetMedallionBg.color = new Color(0.95f, 0.84f, 0.56f, 1f);
            _resetMedallionBg.raycastTarget = false;

            RectTransform innerRing = CreateRect(innerDisc, "InnerTrimRing", layer);
            StretchFull(innerRing, 6f);
            Image trimImg = innerRing.gameObject.AddComponent<Image>();
            trimImg.sprite = GetCircleRingSprite();
            trimImg.color = new Color(0.52f, 0.33f, 0.12f, 0.55f);
            trimImg.raycastTarget = false;

            // Crisp procedural curved dual-arrow refresh icon inside the medallion
            RectTransform iconRt = CreateRect(innerDisc, "RefreshArrowsIcon", layer);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(114f, 114f);
            _resetIconImage = iconRt.gameObject.AddComponent<Image>();
            _resetIconImage.sprite = GetRefreshIconSprite();
            _resetIconImage.color = new Color(0.38f, 0.21f, 0.06f, 0.46f);
            _resetIconImage.raycastTarget = false;

            // Stationary crisp "RESET" title on top of medallion so text stays upright while arrows spin!
            TextMeshProUGUI resetTitle = CreateLabel(_resetButtonRoot, "ResetTitle", font, 24f, FontStyles.Bold | FontStyles.Italic, new Color(0.22f, 0.12f, 0.04f, 1f));
            resetTitle.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            resetTitle.rectTransform.anchorMax = new Vector2(1f, 0.78f);
            resetTitle.alignment = TextAlignmentOptions.Center;
            resetTitle.text = "RESET";

            // Escalating cost pill tucked neatly at the lower rim of the circular RESET button ("10g", "20g", ...)
            _resetPricePillRect = CreateRect(_resetButtonRoot, "ResetPricePill", layer);
            _resetPricePillRect.anchorMin = _resetPricePillRect.anchorMax = new Vector2(0.5f, 0f);
            _resetPricePillRect.pivot = new Vector2(0.5f, 0.32f);
            _resetPricePillRect.anchoredPosition = new Vector2(0f, 4f);
            _resetPricePillRect.sizeDelta = new Vector2(96f, 36f);
            _resetPricePillBg = _resetPricePillRect.gameObject.AddComponent<Image>();
            _resetPricePillBg.sprite = buttonSprite != null ? buttonSprite : solid;
            _resetPricePillBg.color = new Color(0.20f, 0.12f, 0.06f, 0.98f);
            _resetPricePillBg.raycastTarget = false;

            _resetPriceText = CreateLabel(_resetPricePillRect, "ResetPriceText", font, 21f, FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.90f, 0.48f, 1f));
            _resetPriceText.alignment = TextAlignmentOptions.Center;
            _resetPriceText.text = $"{baseResetCost}g";

            _resetButton = _resetButtonRoot.gameObject.AddComponent<Button>();
            _resetButton.targetGraphic = outerRim;
            _resetButton.transition = Selectable.Transition.None;

            MerchantPointerRelay relay = _resetButtonRoot.gameObject.AddComponent<MerchantPointerRelay>();
            relay.Init(
                onEnter: OnResetButtonHoverEnter,
                onExit: OnResetButtonHoverExit,
                onClick: () => TryResetShop());
        }

        private void BuildSoldStamp(RectTransform parent, int layer, TMP_FontAsset font, out RectTransform stampRoot, out CanvasGroup stampGroup, Vector2 size, float fontSize)
        {
            stampRoot = CreateRect(parent, "SoldStamp", layer);
            stampRoot.anchorMin = stampRoot.anchorMax = new Vector2(0.5f, 0.56f);
            stampRoot.pivot = new Vector2(0.5f, 0.5f);
            stampRoot.sizeDelta = size;
            stampRoot.localEulerAngles = new Vector3(0f, 0f, -26f);

            stampGroup = stampRoot.gameObject.AddComponent<CanvasGroup>();
            stampGroup.alpha = 0f;
            stampGroup.blocksRaycasts = false;
            stampGroup.interactable = false;

            Image border = stampRoot.gameObject.AddComponent<Image>();
            border.sprite = GetStampBorderSprite();
            border.type = Image.Type.Sliced;
            border.color = new Color(0.86f, 0.12f, 0.12f, 0.96f);
            border.raycastTarget = false;

            RectTransform fillRt = CreateRect(stampRoot, "StampFill", layer);
            StretchFull(fillRt, 4f);
            Image fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = GetSolidSprite();
            fillImg.color = new Color(0.16f, 0.02f, 0.02f, 0.42f);
            fillImg.raycastTarget = false;

            TextMeshProUGUI txt = CreateLabel(stampRoot, "SoldText", font, fontSize, FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.22f, 0.20f, 1f));
            txt.alignment = TextAlignmentOptions.Center;
            txt.text = "SOLD";
            txt.characterSpacing = 6f;
            AddShadow(txt.gameObject, new Color(0.18f, 0.02f, 0.02f, 0.92f), new Vector2(2f, -2f));

            stampRoot.gameObject.SetActive(false);
        }

        private static void FitCardViewIntoContainer(RunDeckCardView cardView, RectTransform container)
        {
            if (cardView == null || container == null) return;
            RectTransform cvRt = cardView.GetComponent<RectTransform>();
            if (cvRt == null) return;

            cvRt.localScale = Vector3.one;
            cvRt.anchoredPosition = Vector2.zero;

            // Measure the actual visual card bounds (C_Run Deck Card View renders its art inside child Graphic rects)
            float rawW = 0f;
            float rawH = 0f;
            Vector2 centerOffset = Vector2.zero;

            Image bgImage = null;
            Image[] childImages = cardView.GetComponentsInChildren<Image>(true);
            float maxArea = 0f;
            foreach (Image img in childImages)
            {
                if (img == null || !img.gameObject.activeInHierarchy) continue;
                Rect r = img.rectTransform.rect;
                float area = Mathf.Abs(r.width * r.height);
                if (area > maxArea && r.width > 20f && r.height > 20f)
                {
                    maxArea = area;
                    bgImage = img;
                }
            }

            if (bgImage != null)
            {
                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(cvRt, bgImage.rectTransform);
                rawW = b.size.x;
                rawH = b.size.y;
                centerOffset = new Vector2(b.center.x, b.center.y);
            }

            if (rawW < 20f || rawH < 20f)
            {
                rawW = cvRt.rect.width > 20f ? cvRt.rect.width : 120f;
                rawH = cvRt.rect.height > 20f ? cvRt.rect.height : 165f;
            }

            // Fill the 2x4 stall cell crisply (~184 x 250 in 1920x1080 space)
            float targetW = container.rect.width > 40f ? container.rect.width - 10f : 186f;
            float targetH = container.rect.height > 40f ? container.rect.height - 8f : 254f;

            float scale = Mathf.Min(targetW / rawW, targetH / rawH);
            scale = Mathf.Clamp(scale, 0.45f, 2.25f);
            cvRt.localScale = new Vector3(scale, scale, 1f);
            cvRt.anchoredPosition = -centerOffset * scale;
        }

        // ------------------------------------------------------------------
        // Infrastructure & Manager Hooks
        // ------------------------------------------------------------------
        private void EnsureRuntimeManagers()
        {
            if (RunManager.Instance == null)
            {
                GameObject rmGo = new("Run Manager");
                RunManager rm = rmGo.AddComponent<RunManager>();
                rm.SetGold(standaloneTestGold);
            }

            if (RunDeckManager.Instance == null)
            {
                GameObject dmGo = new("Run Deck Manager");
                dmGo.AddComponent<RunDeckManager>();
            }

            RunRelicManager.EnsureInstance();
        }

        private void EnsureCanvas()
        {
            if (targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }
        }

        private void EnsureRelicSwapModal()
        {
            if (_relicSwapModal != null) return;
            _relicSwapModal = FindFirstObjectByType<RelicSwapModalUI>();
            if (_relicSwapModal == null && targetCanvas != null)
            {
                GameObject modalGo = new("RelicSwapModalController");
                modalGo.transform.SetParent(targetCanvas.transform, false);
                _relicSwapModal = modalGo.AddComponent<RelicSwapModalUI>();
            }
        }

        private void SubscribeManagers()
        {
            if (RunManager.Instance != null && _subscribedRunManager != RunManager.Instance)
            {
                _subscribedRunManager = RunManager.Instance;
                _subscribedRunManager.RunStateChanged += OnRunStateChanged;
            }

            if (RunRelicManager.Instance != null && _subscribedRelicManager != RunRelicManager.Instance)
            {
                _subscribedRelicManager = RunRelicManager.Instance;
                _subscribedRelicManager.RelicsChanged += OnRelicsChanged;
            }
        }

        private void UnsubscribeManagers()
        {
            if (_subscribedRunManager != null)
            {
                _subscribedRunManager.RunStateChanged -= OnRunStateChanged;
                _subscribedRunManager = null;
            }

            if (_subscribedRelicManager != null)
            {
                _subscribedRelicManager.RelicsChanged -= OnRelicsChanged;
                _subscribedRelicManager = null;
            }
        }

        private void OnRunStateChanged()
        {
            RefreshGoldDisplay(animate: false);
            RefreshAllAffordability();
        }

        private void OnRelicsChanged(IReadOnlyList<RelicData> _)
        {
            RefreshEquippedRelicStrip(highlightIndex: -1);
        }

        private void KillActiveTweens()
        {
            _activeResetSequence?.Kill();
            _activeEntranceSequence?.Kill();
            _vendorActionSequence?.Kill();
            _activeResetSequence = null;
            _activeEntranceSequence = null;
            _vendorActionSequence = null;
        }

        // ------------------------------------------------------------------
        // Static UI & Procedural Sprite Factories
        // ------------------------------------------------------------------
        private static void AttachSimpleHoverScale(RectTransform target, float hoverScale)
        {
            MerchantPointerRelay relay = target.gameObject.AddComponent<MerchantPointerRelay>();
            relay.Init(
                onEnter: () =>
                {
                    Tune.SFX(SfxID.hover);
                    target.DOKill();
                    target.DOScale(hoverScale, 0.14f).SetEase(Ease.OutBack).SetUpdate(true);
                },
                onExit: () =>
                {
                    target.DOKill();
                    target.DOScale(Vector3.one, 0.14f).SetEase(Ease.OutCubic).SetUpdate(true);
                },
                onClick: null);
        }

        private static RectTransform CreateRect(Transform parent, string name, int layer)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void StretchFull(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            RectTransform rt = CreateRect(parent, name, parent.gameObject.layer);
            StretchFull(rt);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private static void AddShadow(GameObject go, Color color, Vector2 distance)
        {
            Shadow s = go.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = distance;
        }

        private static string PickRandom(string[] pool)
        {
            if (pool == null || pool.Length == 0) return string.Empty;
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private Sprite GetSolidSprite()
        {
            if (solidSprite != null) return solidSprite;
            if (s_solidSprite != null) return s_solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            s_solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return s_solidSprite;
        }

        private Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            if (s_circleSprite != null) return s_circleSprite;
            const int size = 128;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            float radius = half - 1.5f;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    float alpha = Mathf.Clamp01(radius - d + 0.8f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            s_circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_circleSprite;
        }

        private Sprite GetCircleRingSprite()
        {
            if (circleRingSprite != null) return circleRingSprite;
            if (s_circleRingSprite != null) return s_circleRingSprite;
            const int size = 128;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            float outerR = half - 2f;
            float innerR = half - 7f;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    float aOut = Mathf.Clamp01(outerR - d + 0.8f);
                    float aIn = Mathf.Clamp01(d - innerR + 0.8f);
                    float alpha = aOut * aIn;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            s_circleRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_circleRingSprite;
        }

        private Sprite GetSoftGlowSprite()
        {
            if (softGlowSprite != null) return softGlowSprite;
            if (s_softGlowSprite != null) return s_softGlowSprite;
            const int size = 96;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float norm = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half);
                    float alpha = Mathf.Pow(1f - norm, 2.2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            s_softGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_softGlowSprite;
        }

        private Sprite GetStampBorderSprite()
        {
            if (stampBorderSprite != null) return stampBorderSprite;
            if (s_stampBorderSprite != null) return s_stampBorderSprite;
            const int size = 32;
            const int border = 4;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < border || x >= size - border || y < border || y >= size - border;
                    pixels[y * size + x] = isBorder ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            s_stampBorderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            return s_stampBorderSprite;
        }

        private Sprite GetRefreshIconSprite()
        {
            if (refreshIconSprite != null) return refreshIconSprite;
            if (s_refreshIconSprite != null) return s_refreshIconSprite;
            const int size = 160;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            float ringR = size * 0.36f;
            float strokeHalf = size * 0.048f;
            Color32[] pixels = new Color32[size * size];

            // Draw two curved opposing arcs + crisp triangular arrowheads
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (deg < 0f) deg += 360f;

                    bool onTopArc = deg >= 28f && deg <= 156f;
                    bool onBotArc = deg >= 208f && deg <= 336f;
                    float alpha = 0f;

                    if (onTopArc || onBotArc)
                    {
                        float rd = Mathf.Abs(dist - ringR);
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(strokeHalf - rd + 1.0f));
                    }

                    // Arrowhead 1 at ~28 deg (clockwise tip pointing down-right)
                    alpha = Mathf.Max(alpha, SampleArrowhead(dx, dy, ringR, 26f * Mathf.Deg2Rad, clockwise: true, size * 0.11f));
                    // Arrowhead 2 at ~208 deg (clockwise tip pointing up-left)
                    alpha = Mathf.Max(alpha, SampleArrowhead(dx, dy, ringR, 206f * Mathf.Deg2Rad, clockwise: true, size * 0.11f));

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            s_refreshIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_refreshIconSprite;
        }

        private static float SampleArrowhead(float px, float py, float ringR, float angleRad, bool clockwise, float arrowSize)
        {
            Vector2 radial = new(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector2 tangent = clockwise ? new Vector2(radial.y, -radial.x) : new Vector2(-radial.y, radial.x);
            Vector2 anchor = radial * ringR;

            Vector2 tip = anchor + tangent * (arrowSize * 0.75f);
            Vector2 left = anchor - tangent * (arrowSize * 0.45f) + radial * (arrowSize * 0.72f);
            Vector2 right = anchor - tangent * (arrowSize * 0.45f) - radial * (arrowSize * 0.72f);

            float d = SignedDistanceToTriangle(new Vector2(px, py), tip, left, right);
            return Mathf.Clamp01(1f - d);
        }

        private static float SignedDistanceToTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Mathf.Max(0.0001f, Vector2.Dot(e0, e0)));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Mathf.Max(0.0001f, Vector2.Dot(e1, e1)));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Mathf.Max(0.0001f, Vector2.Dot(e2, e2)));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(
                Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }
    }

    internal sealed class MerchantPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Action _onEnter;
        private Action _onExit;
        private Action _onClick;

        public void Init(Action onEnter, Action onExit, Action onClick)
        {
            _onEnter = onEnter;
            _onExit = onExit;
            _onClick = onClick;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => _onExit?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            _onClick?.Invoke();
        }
    }
}

