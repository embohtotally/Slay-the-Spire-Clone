using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.Levels
{
    /// <summary>
    /// Two-stage Celestial Party Setup &amp; Character Detail UI matching the project's Figma mockups:
    /// - Compact Levels.unity Launcher Bar showing your 3 active party mini-portraits + "PARTY SETUP" button.
    /// - Stage 1 (Overview): Warm golden parchment backdrop, corner sunburst, terracotta waves, 3 diamond
    ///   slot-clear badges, and 3 ornate tarot party cards (Gold Sun / Crimson Star / Azure Moon).
    /// - Stage 2 (Character Detail &amp; Roster Selector): Dimmed scrim over the 3 tarot cards, oversized
    ///   left Character Splash Portrait with breathing aura, vertical white divider line, serif Character Name,
    ///   Ultimate block, Innate Talent + interactive Future Unlockable Talent slots, bottom row of square
    ///   roster icons with 1/2/3 party badges, and bottom-right live frame-animated character sprite!
    /// </summary>
    [DisallowMultipleComponent]
    public class PartySetupUI : MonoBehaviour
    {
        public const int PartySlotCount = 3;
        public const int MinimumRosterIconSlots = 5;

        [Serializable]
        public class LockedFutureHeroTeaser
        {
            public string DisplayName = "Unknown Companion";
            public string RoleTitle = "FUTURE CHARACTER";
            public string UltimateTitle = "Sealed Arcane Arts";
            [TextArea(2, 3)] public string UltimateDescription = "A sealed ultimate technique waiting to be discovered in future chapters.";
            public string InnateTitle = "Dormant Bond";
            [TextArea(2, 3)] public string InnateDescription = "Unlock this companion in a future chapter to add their passive talent to your party.";
            public string UnlockRequirement = "Unlocks in a Future Chapter";
            public Color TintColor = new(0.42f, 0.48f, 0.62f, 1f);
        }

        [Header("Core References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset serifFontAsset;
        [SerializeField] private TMP_FontAsset uiFontAsset;
        [SerializeField] private List<HeroData> playableRoster = new();
        [SerializeField] private List<LockedFutureHeroTeaser> futureLockedRoster = new();

        [Header("Existing Art Assets")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite nameplateSprite;

        [Header("Behavior")]
        [SerializeField] private bool openPartySetupOnStart = false;
        [SerializeField] private bool allowDuplicateHeroAcrossSlots = false;

        // Runtime Party State (3 slots; null means empty)
        private readonly HeroData[] _partySlots = new HeroData[PartySlotCount];
        private bool _built;
        private bool _isOverviewOpen;
        private bool _isDetailOpen;
        private int _editingSlotIndex;
        private int _inspectedRosterIndex; // 0..playableRoster.Count-1, or >= playableRoster.Count for locked teaser
        private int _inspectedTalentIndex;
        private int _hoveredTarotIndex = -1;

        // UI Roots
        private RectTransform _launcherDock;
        private readonly Image[] _dockMiniPortraits = new Image[PartySlotCount];
        private readonly TextMeshProUGUI[] _dockMiniEmptyLabels = new TextMeshProUGUI[PartySlotCount];
        private TextMeshProUGUI _dockPartySummaryText;

        private RectTransform _modalRoot;
        private CanvasGroup _modalCg;
        private RectTransform _overviewLayer;
        private RectTransform _fxLayer;

        // Stage 1 — Tarot Cards & Top-Right Diamond Badges
        private readonly TarotSlotWidgets[] _tarotCards = new TarotSlotWidgets[PartySlotCount];
        private readonly DiamondBadgeWidgets[] _diamondBadges = new DiamondBadgeWidgets[PartySlotCount];
        private GameObject _confirmPartyButtonRoot;
        private readonly List<FloatingMote> _motes = new();
        private readonly List<SparkleParticle> _sparkles = new();

        // Stage 2 — Character Detail & Roster Selector
        private RectTransform _detailOverlay;
        private CanvasGroup _detailCg;
        private RectTransform _splashHolder;
        private Image _splashImage;
        private Image _splashAura;
        private RectTransform _infoColumn;
        private CanvasGroup _infoColumnCg;
        private TextMeshProUGUI _detailNameText;
        private TextMeshProUGUI _detailSubtitleText;
        private TextMeshProUGUI _ultimateTitleText;
        private TextMeshProUGUI _ultimateCostText;
        private TextMeshProUGUI _ultimateDescText;
        private TextMeshProUGUI _innateTitleText;
        private TextMeshProUGUI _innateDescText;
        private RectTransform _talentNodesRow;
        private readonly List<TalentNodeWidgets> _talentNodes = new();
        private TextMeshProUGUI _talentDetailTitleText;
        private TextMeshProUGUI _talentDetailDescText;
        private Button _assignButton;
        private Image _assignButtonBg;
        private TextMeshProUGUI _assignButtonLabel;

        // Bottom Roster Strip & Bottom-Right Animated Chibi Sprite
        private RectTransform _rosterStrip;
        private readonly List<RosterIconWidgets> _rosterIcons = new();
        private RectTransform _spriteStage;
        private Image _animatedSpriteImage;
        private Image _spriteFloorShadow;
        private TextMeshProUGUI _spriteHintText;

        // Frame-by-frame Sprite Animator State
        private IReadOnlyList<Sprite> _idleFrames;
        private IReadOnlyList<Sprite> _actionFrames;
        private float _idleFps = 2.4f;
        private float _actionFps = 8.5f;
        private bool _playingActionAnim;
        private int _animFrameIdx;
        private float _animTimer;

        private Vector2 _splashBasePos;
        private Vector2 _spriteBasePos;

        public bool IsOverviewOpen => _isOverviewOpen;
        public bool IsDetailOpen => _isDetailOpen;
        public int EditingSlotIndex => _editingSlotIndex;
        public int InspectedRosterIndex => _inspectedRosterIndex;
        public IReadOnlyList<HeroData> PlayableRoster => playableRoster;

        public HeroData GetPartySlotHero(int slotIndex) =>
            slotIndex >= 0 && slotIndex < PartySlotCount ? _partySlots[slotIndex] : null;

        private sealed class TarotSlotWidgets
        {
            public RectTransform Root;
            public RectTransform TiltCard;
            public Image Glow;
            public Image Backdrop;
            public Image Portrait;
            public RectTransform PortraitRect;
            public Image Frame;
            public GameObject EmptyStateRoot;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI SlotTagText;
            public Vector2 BaseAnchoredPos;
            public float HoverBlend;
        }

        private sealed class DiamondBadgeWidgets
        {
            public RectTransform Root;
            public Image Bg;
            public TextMeshProUGUI SlotHint;
        }

        private sealed class RosterIconWidgets
        {
            public RectTransform Root;
            public Image Bg;
            public Image Portrait;
            public RectTransform PortraitRect;
            public Image SelectionOutline;
            public GameObject LockOverlay;
            public TextMeshProUGUI SlotBadgeText;
            public GameObject SlotBadgeRoot;
            public bool IsLockedFuture;
            public int RosterIndex;
        }

        private sealed class TalentNodeWidgets
        {
            public RectTransform Root;
            public Image Bg;
            public Image Outline;
            public Image LockIcon;
            public TextMeshProUGUI TierLabel;
            public GameObject ActiveDot;
        }

        private sealed class FloatingMote
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Phase;
            public float BaseAlpha;
        }

        private sealed class SparkleParticle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 StartPos;
            public Vector2 EndPos;
            public float StartScale;
            public float SpinSpeed;
            public float Age;
            public float Duration;
            public Color BaseColor;
        }

        private void Awake()
        {
            if (RunManager.Instance == null) new GameObject("Run Manager").AddComponent<RunManager>();
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
            HideLegacyCharacterSelectPanels();
            EnsureDefaultFutureTeasers();
            InitializePartyFromRunManagerOrDefaults();
            BuildUIIfNeeded();
        }

        private void HideLegacyCharacterSelectPanels()
        {
            if (targetCanvas == null) return;
            foreach (Transform child in targetCanvas.transform)
            {
                if (child != null && child.name == "CharacterSelectPanel")
                {
                    child.gameObject.SetActive(false);
                }
            }
            CharacterSelectUI[] legacy = FindObjectsByType<CharacterSelectUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] != null) legacy[i].enabled = false;
            }
        }

        private void Start()
        {
            HideLegacyCharacterSelectPanels();
            CommitPartyToRunManager();
            RefreshLauncherDock();
            RefreshTarotCards(animate: false);

            if (openPartySetupOnStart)
            {
                OpenPartySetup();
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Escape key navigates back cleanly (Detail -> Overview -> Closed)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_isDetailOpen) CloseDetailOverlay();
                else if (_isOverviewOpen) ClosePartySetup();
            }

            if (!_isOverviewOpen) return;

            UpdateFloatingMotes(dt);
            UpdateSparkleParticles(dt);
            UpdateTarotIdleAndHover(dt);

            if (_isDetailOpen)
            {
                UpdateSplashBreathing();
                UpdateAnimatedStandingSprite(dt);
            }
        }

        #region Public Actions

        public void OpenPartySetup()
        {
            BuildUIIfNeeded();
            if (_modalRoot == null) return;

            _isOverviewOpen = true;
            _isDetailOpen = false;
            _modalRoot.gameObject.SetActive(true);
            _modalRoot.SetAsLastSibling();
            if (_detailOverlay != null) _detailOverlay.gameObject.SetActive(false);
            if (_confirmPartyButtonRoot != null) _confirmPartyButtonRoot.SetActive(true);

            RefreshTarotCards(animate: false);
            RefreshDiamondBadges();

            // Juicy staggered tarot deal-in entrance
            _modalCg.DOKill();
            _modalCg.alpha = 0f;
            _modalCg.DOFade(1f, 0.24f).SetEase(Ease.OutQuad).SetUpdate(true);

            for (int i = 0; i < PartySlotCount; i++)
            {
                TarotSlotWidgets card = _tarotCards[i];
                if (card?.TiltCard == null) continue;
                card.TiltCard.DOKill();
                card.TiltCard.anchoredPosition = new Vector2(0f, -64f);
                card.TiltCard.localScale = Vector3.one * 0.86f;
                card.TiltCard.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * -4.5f);

                float delay = 0.05f + i * 0.085f;
                card.TiltCard.DOAnchorPos(Vector2.zero, 0.42f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true);
                card.TiltCard.DOScale(Vector3.one, 0.38f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true);
                card.TiltCard.DORotate(Vector3.zero, 0.38f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            Tune.SFX(SfxID.DrawCard);
        }

        public void ClosePartySetup()
        {
            if (!_isOverviewOpen) return;
            EnsureAtLeastOneHeroInParty();
            CommitPartyToRunManager();
            RefreshLauncherDock();

            _isDetailOpen = false;
            _isOverviewOpen = false;

            if (_modalCg != null)
            {
                _modalCg.DOKill();
                _modalCg.DOFade(0f, 0.18f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
                {
                    if (!_isOverviewOpen && _modalRoot != null) _modalRoot.gameObject.SetActive(false);
                });
            }
            else if (_modalRoot != null)
            {
                _modalRoot.gameObject.SetActive(false);
            }

            Tune.SFX(SfxID.PlayCard);
        }

        public void OpenSlotSelector(int slotIndex)
        {
            if (!_isOverviewOpen) OpenPartySetup();
            _editingSlotIndex = Mathf.Clamp(slotIndex, 0, PartySlotCount - 1);
            _isDetailOpen = true;
            if (_confirmPartyButtonRoot != null) _confirmPartyButtonRoot.SetActive(false);

            // Default to inspecting the hero already assigned to this slot, else first playable hero
            HeroData slotHero = _partySlots[_editingSlotIndex];
            int targetRoster = slotHero != null ? playableRoster.IndexOf(slotHero) : 0;
            if (targetRoster < 0) targetRoster = 0;

            if (_detailOverlay != null)
            {
                _detailOverlay.gameObject.SetActive(true);
                _detailOverlay.SetAsLastSibling();
                _detailCg.DOKill();
                _detailCg.alpha = 0f;
                _detailCg.DOFade(1f, 0.22f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            // Subtle tarot click punch + background settle
            if (_tarotCards[_editingSlotIndex]?.TiltCard != null)
            {
                _tarotCards[_editingSlotIndex].TiltCard.DOPunchScale(Vector3.one * 0.055f, 0.24f, 6, 0.5f).SetUpdate(true);
                SpawnSparkleBurst(_tarotCards[_editingSlotIndex].Root.anchoredPosition, PartySetupArtFactory.GetSlotAccentColor(_editingSlotIndex), 10);
            }

            SelectRosterIndex(targetRoster, playVoice: true, triggerActionAnim: true);
        }

        public void CloseDetailOverlay()
        {
            if (!_isDetailOpen) return;
            _isDetailOpen = false;
            if (_confirmPartyButtonRoot != null) _confirmPartyButtonRoot.SetActive(true);

            if (_detailCg != null)
            {
                _detailCg.DOKill();
                _detailCg.DOFade(0f, 0.16f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
                {
                    if (!_isDetailOpen && _detailOverlay != null) _detailOverlay.gameObject.SetActive(false);
                });
            }
            else if (_detailOverlay != null)
            {
                _detailOverlay.gameObject.SetActive(false);
            }

            RefreshTarotCards(animate: true);
            RefreshDiamondBadges();
            RefreshLauncherDock();
        }

        public void SelectRosterIndex(int rosterIndex, bool playVoice = true, bool triggerActionAnim = true)
        {
            int totalSlots = Mathf.Max(MinimumRosterIconSlots, playableRoster.Count + futureLockedRoster.Count);
            if (rosterIndex < 0 || rosterIndex >= totalSlots) return;

            _inspectedRosterIndex = rosterIndex;
            _inspectedTalentIndex = 0;

            bool isPlayable = rosterIndex < playableRoster.Count && playableRoster[rosterIndex] != null;

            if (isPlayable)
            {
                HeroData hero = playableRoster[rosterIndex];
                BindPlayableHeroDetail(hero, triggerActionAnim);

                if (playVoice)
                {
                    bool isEmber = IsEmberHero(hero);
                    Tune.SFX(isEmber ? SfxID.Ember_attack : SfxID.Aira_heal);
                }
            }
            else
            {
                int teaserIdx = Mathf.Clamp(rosterIndex - playableRoster.Count, 0, Mathf.Max(0, futureLockedRoster.Count - 1));
                LockedFutureHeroTeaser teaser = futureLockedRoster.Count > 0 ? futureLockedRoster[teaserIdx] : new LockedFutureHeroTeaser();
                BindLockedHeroDetail(teaser);
                if (playVoice) Tune.SFX(SfxID.DrawCard);
            }

            // Punch selected roster icon
            if (rosterIndex < _rosterIcons.Count && _rosterIcons[rosterIndex]?.Root != null)
            {
                _rosterIcons[rosterIndex].Root.DOKill();
                _rosterIcons[rosterIndex].Root.localScale = Vector3.one * 0.90f;
                _rosterIcons[rosterIndex].Root.DOScale(Vector3.one * 1.07f, 0.24f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            RefreshRosterIconVisuals();
        }

        public void AssignInspectedHeroToActiveSlot()
        {
            if (_inspectedRosterIndex < 0 || _inspectedRosterIndex >= playableRoster.Count) return;
            HeroData hero = playableRoster[_inspectedRosterIndex];
            if (hero == null) return;

            int existingSlot = FindPartySlotOfHero(hero);

            if (existingSlot == _editingSlotIndex)
            {
                // Hero is already in this slot -> unequip if at least one other slot is occupied
                if (CountAssignedHeroes() > 1)
                {
                    _partySlots[_editingSlotIndex] = null;
                    Tune.SFX(SfxID.DrawCard);
                }
                else
                {
                    // Gentle shake to signal party cannot be completely empty
                    _assignButton?.GetComponent<RectTransform>()?.DOShakeAnchorPos(0.24f, 8f, 18).SetUpdate(true);
                    return;
                }
            }
            else
            {
                if (!allowDuplicateHeroAcrossSlots && existingSlot >= 0)
                {
                    // Swap slots smoothly so the player never loses a hero unexpectedly
                    HeroData displaced = _partySlots[_editingSlotIndex];
                    _partySlots[_editingSlotIndex] = hero;
                    _partySlots[existingSlot] = displaced;
                }
                else
                {
                    _partySlots[_editingSlotIndex] = hero;
                }

                TriggerPreviewActionAnimation();
                Tune.SFX(IsEmberHero(hero) ? SfxID.Ember_fire : SfxID.Aira_cure);

                // Sparkle burst on the assigned roster icon!
                if (_inspectedRosterIndex < _rosterIcons.Count && _rosterIcons[_inspectedRosterIndex]?.Root != null)
                {
                    Vector2 burstPos = ConvertToFxLocalPos(_rosterIcons[_inspectedRosterIndex].Root);
                    SpawnSparkleBurst(burstPos, PartySetupArtFactory.GetSlotAccentColor(_editingSlotIndex), 14);
                }
            }

            CommitPartyToRunManager();
            RefreshTarotCards(animate: false);
            RefreshDiamondBadges();
            RefreshRosterIconVisuals();
            RefreshAssignButtonState();
            RefreshLauncherDock();
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= PartySlotCount) return;
            if (_partySlots[slotIndex] == null)
            {
                // Clicking an empty slot's diamond badge opens that slot to assign someone
                OpenSlotSelector(slotIndex);
                return;
            }

            if (CountAssignedHeroes() <= 1)
            {
                // Shake diamond badge — keep at least 1 hero in party
                _diamondBadges[slotIndex]?.Root?.DOShakeAnchorPos(0.24f, 7f, 16).SetUpdate(true);
                return;
            }

            _partySlots[slotIndex] = null;
            CommitPartyToRunManager();
            RefreshTarotCards(animate: true);
            RefreshDiamondBadges();
            RefreshRosterIconVisuals();
            RefreshLauncherDock();

            if (_tarotCards[slotIndex]?.Root != null)
                SpawnSparkleBurst(_tarotCards[slotIndex].Root.anchoredPosition, new Color(0.95f, 0.85f, 0.65f, 1f), 9);

            Tune.SFX(SfxID.DrawCard);
        }

        public void InspectTalent(int talentIndex)
        {
            bool isPlayable = _inspectedRosterIndex >= 0 && _inspectedRosterIndex < playableRoster.Count && playableRoster[_inspectedRosterIndex] != null;
            if (!isPlayable) return;

            HeroData hero = playableRoster[_inspectedRosterIndex];
            if (hero.Talents == null || hero.Talents.Count == 0) return;

            _inspectedTalentIndex = Mathf.Clamp(talentIndex, 0, hero.Talents.Count - 1);
            RefreshTalentSelectionUI(hero, animate: true);
        }

        public void TriggerPreviewActionAnimation()
        {
            if (_actionFrames == null || _actionFrames.Count == 0) return;
            _playingActionAnim = true;
            _animFrameIdx = 0;
            _animTimer = 0f;

            if (_animatedSpriteImage != null)
            {
                _animatedSpriteImage.sprite = _actionFrames[0];
                _animatedSpriteImage.rectTransform.DOKill();
                _animatedSpriteImage.rectTransform.DOPunchScale(new Vector3(0.11f, 0.11f, 0f), 0.30f, 6, 0.5f).SetUpdate(true);
                _animatedSpriteImage.rectTransform.DOAnchorPosY(_spriteBasePos.y + 14f, 0.14f).SetEase(Ease.OutQuad).SetUpdate(true)
                    .OnComplete(() => _animatedSpriteImage.rectTransform.DOAnchorPosY(_spriteBasePos.y, 0.22f).SetEase(Ease.OutBounce).SetUpdate(true));
            }

            if (_spriteStage != null)
            {
                SpawnSparkleBurst(ConvertToFxLocalPos(_spriteStage), new Color(1f, 0.90f, 0.55f, 1f), 8);
            }
        }

        #endregion

        #region Data & RunManager Sync

        private void EnsureDefaultFutureTeasers()
        {
            if (futureLockedRoster != null && futureLockedRoster.Count > 0) return;
            futureLockedRoster = new List<LockedFutureHeroTeaser>
            {
                new()
                {
                    DisplayName = "Lyra",
                    RoleTitle = "STARLIGHT ARCHER  •  LOCKED",
                    UltimateTitle = "Meteor Volley",
                    UltimateDescription = "Unleash a piercing barrage of astral arrows across all enemies, marking them Vulnerable.",
                    InnateTitle = "Keen Sight",
                    InnateDescription = "When Lyra is in the party, the first Attack card played each turn deals +3 bonus damage.",
                    UnlockRequirement = "Unlocks in Chapter 2",
                    TintColor = new Color(0.95f, 0.65f, 0.36f, 1f)
                },
                new()
                {
                    DisplayName = "Noctis",
                    RoleTitle = "SHADOW WEAVER  •  LOCKED",
                    UltimateTitle = "Eclipse Requiem",
                    UltimateDescription = "Convert enemy Will drain and debuffs into devastating shadow damage while restoring party Will.",
                    InnateTitle = "Velvet Shroud",
                    InnateDescription = "When Noctis is in the party, reduce incoming Will drain from enemies by 25%.",
                    UnlockRequirement = "Unlocks in Chapter 3",
                    TintColor = new Color(0.60f, 0.50f, 0.88f, 1f)
                },
                new()
                {
                    DisplayName = "Seraphina",
                    RoleTitle = "FROST ORACLE  •  LOCKED",
                    UltimateTitle = "Glacial Sanctuary",
                    UltimateDescription = "Encase the battlefield in crystal frost, freezing enemy intent and fortifying your entire party.",
                    InnateTitle = "Crystalline Calm",
                    InnateDescription = "When Seraphina is in the party, ending a turn with unspent Energy grants +4 Shield.",
                    UnlockRequirement = "Unlocks in Chapter 4",
                    TintColor = new Color(0.42f, 0.78f, 0.96f, 1f)
                }
            };
        }

        private void InitializePartyFromRunManagerOrDefaults()
        {
            Array.Clear(_partySlots, 0, PartySlotCount);

            if (RunManager.Instance != null && RunManager.Instance.HasSelectedParty)
            {
                int slot = 0;
                foreach (HeroData h in RunManager.Instance.SelectedParty)
                {
                    if (h != null && slot < PartySlotCount)
                    {
                        _partySlots[slot++] = h;
                    }
                }
            }

            if (CountAssignedHeroes() == 0 && playableRoster != null)
            {
                // Default starting party: Aura in Slot 1, Ember in Slot 2
                for (int i = 0; i < Mathf.Min(PartySlotCount, playableRoster.Count); i++)
                {
                    _partySlots[i] = playableRoster[i];
                }
            }
        }

        private void EnsureAtLeastOneHeroInParty()
        {
            if (CountAssignedHeroes() > 0) return;
            if (playableRoster != null && playableRoster.Count > 0)
            {
                _partySlots[0] = playableRoster[0];
            }
        }

        private void CommitPartyToRunManager()
        {
            List<HeroData> activeParty = new();
            List<RelicData> startingRelics = new();

            for (int i = 0; i < PartySlotCount; i++)
            {
                HeroData h = _partySlots[i];
                if (h == null) continue;
                activeParty.Add(h);
                if (h.StartingRelic != null && !startingRelics.Contains(h.StartingRelic))
                {
                    startingRelics.Add(h.StartingRelic);
                }
            }

            if (RunManager.Instance != null && activeParty.Count > 0)
            {
                RunManager.Instance.SetSelectedParty(activeParty);
            }

            if (startingRelics.Count > 0)
            {
                RunRelicManager.EnsureInstance().ResetRelics(startingRelics);
            }
        }

        private int FindPartySlotOfHero(HeroData hero)
        {
            if (hero == null) return -1;
            for (int i = 0; i < PartySlotCount; i++)
            {
                if (_partySlots[i] == hero) return i;
            }
            return -1;
        }

        private int CountAssignedHeroes()
        {
            int c = 0;
            for (int i = 0; i < PartySlotCount; i++)
                if (_partySlots[i] != null) c++;
            return c;
        }

        private static bool IsEmberHero(HeroData hero)
        {
            if (hero == null) return false;
            string n = $"{hero.name} {hero.EffectiveDisplayName} {hero.CharacterId}";
            return n.IndexOf("Ember", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region UI Binding & Refresh

        private void RefreshLauncherDock()
        {
            if (_launcherDock == null) return;
            List<string> names = new();
            for (int i = 0; i < PartySlotCount; i++)
            {
                HeroData hero = _partySlots[i];
                Image img = _dockMiniPortraits[i];
                TextMeshProUGUI empty = _dockMiniEmptyLabels[i];

                if (hero != null)
                {
                    names.Add(hero.EffectiveDisplayName);
                    if (img != null)
                    {
                        img.sprite = hero.EffectiveRosterIcon;
                        img.enabled = img.sprite != null;
                        ApplyPortraitCrop(img.rectTransform, hero.RosterIconOffset * 0.56f, hero.RosterIconScale);
                    }
                    if (empty != null) empty.gameObject.SetActive(false);
                }
                else
                {
                    if (img != null) img.enabled = false;
                    if (empty != null) empty.gameObject.SetActive(true);
                }
            }

            if (_dockPartySummaryText != null)
            {
                _dockPartySummaryText.text = names.Count > 0
                    ? $"ACTIVE PARTY ({names.Count}/{PartySlotCount}):  <color=#FFE69A>{string.Join("  •  ", names)}</color>"
                    : "CLICK TO ASSEMBLE YOUR PARTY";
            }
        }

        private void RefreshTarotCards(bool animate)
        {
            for (int i = 0; i < PartySlotCount; i++)
            {
                TarotSlotWidgets card = _tarotCards[i];
                if (card == null) continue;

                HeroData hero = _partySlots[i];
                bool occupied = hero != null;

                if (card.Portrait != null)
                {
                    if (occupied)
                    {
                        card.Portrait.sprite = hero.GetPortraitForWill(false, false, fallbackToWorldSprite: true);
                        card.Portrait.enabled = card.Portrait.sprite != null;
                        ApplyPortraitCrop(card.PortraitRect, hero.TarotPortraitOffset, hero.TarotPortraitScale);
                    }
                    else
                    {
                        card.Portrait.enabled = false;
                    }
                }

                if (card.EmptyStateRoot != null) card.EmptyStateRoot.SetActive(!occupied);
                if (card.NameText != null)
                {
                    card.NameText.text = occupied ? hero.EffectiveDisplayName.ToUpperInvariant() : "EMPTY SLOT";
                    card.NameText.color = occupied ? new Color(1f, 0.96f, 0.85f, 1f) : new Color(0.84f, 0.78f, 0.68f, 0.85f);
                }
                if (card.SlotTagText != null)
                {
                    card.SlotTagText.text = occupied ? $"SLOT {i + 1}  •  {hero.RoleTitle}" : $"SLOT {i + 1}  •  CLICK TO ASSIGN";
                }

                if (animate && card.TiltCard != null)
                {
                    card.TiltCard.DOKill();
                    card.TiltCard.DOPunchScale(Vector3.one * 0.04f, 0.25f, 6, 0.5f).SetUpdate(true);
                }
            }
        }

        private void RefreshDiamondBadges()
        {
            for (int i = 0; i < PartySlotCount; i++)
            {
                DiamondBadgeWidgets badge = _diamondBadges[i];
                if (badge?.Bg == null) continue;
                bool occupied = _partySlots[i] != null;
                Color slotColor = PartySetupArtFactory.GetSlotAccentColor(i);
                badge.Bg.color = occupied
                    ? Color.Lerp(Color.white, slotColor, 0.26f)
                    : new Color(0.75f, 0.73f, 0.70f, 0.72f);
                if (badge.SlotHint != null)
                {
                    badge.SlotHint.text = $"{i + 1}";
                    badge.SlotHint.color = occupied ? Color.white : new Color(0.85f, 0.85f, 0.85f, 0.65f);
                }
            }
        }

        private void BindPlayableHeroDetail(HeroData hero, bool triggerActionAnim)
        {
            // Oversized left splash portrait slide-in
            if (_splashImage != null)
            {
                _splashImage.sprite = hero.GetPortraitForWill(false, false, fallbackToWorldSprite: true);
                _splashImage.color = Color.white;
                _splashImage.enabled = _splashImage.sprite != null;

                _splashHolder.DOKill();
                _splashBasePos = new Vector2(-370f + hero.SplashPortraitOffset.x, hero.SplashPortraitOffset.y);
                _splashHolder.anchoredPosition = _splashBasePos + new Vector2(-90f, 0f);
                _splashHolder.localScale = Vector3.one * (hero.SplashPortraitScale * 0.94f);
                _splashHolder.DOAnchorPos(_splashBasePos, 0.34f).SetEase(Ease.OutCubic).SetUpdate(true);
                _splashHolder.DOScale(Vector3.one * hero.SplashPortraitScale, 0.36f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (_splashAura != null)
            {
                Color c = hero.ThemeColor;
                _splashAura.color = new Color(c.r, c.g, c.b, 0.28f);
            }

            // Info column smooth slide-punch
            if (_infoColumn != null && _infoColumnCg != null)
            {
                _infoColumn.DOKill();
                _infoColumnCg.DOKill();
                _infoColumnCg.alpha = 0f;
                _infoColumnCg.DOFade(1f, 0.24f).SetEase(Ease.OutQuad).SetUpdate(true);
                _infoColumn.anchoredPosition = new Vector2(28f, 0f);
                _infoColumn.DOAnchorPos(Vector2.zero, 0.28f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (_detailNameText != null) _detailNameText.text = hero.EffectiveDisplayName;
            if (_detailSubtitleText != null)
            {
                int deckCount = hero.Deck != null ? hero.Deck.Count : 0;
                _detailSubtitleText.text = $"{hero.RoleTitle}   •   HP {hero.Health}   •   WILL {hero.EffectiveStartingWill}/{hero.EffectiveMaxWill}   •   {deckCount} CARDS";
                _detailSubtitleText.color = Color.Lerp(hero.ThemeColor, Color.white, 0.45f);
            }

            // Ultimate section
            HeroUltimateDefinition ult = hero.Ultimate;
            if (_ultimateTitleText != null)
                _ultimateTitleText.text = ult != null && !string.IsNullOrWhiteSpace(ult.Title) ? ult.Title : "Signature Ultimate";
            if (_ultimateCostText != null)
                _ultimateCostText.text = $"{(ult != null ? ult.UltimatePointCost : 3)} UP";
            if (_ultimateDescText != null)
                _ultimateDescText.text = ult != null && !string.IsNullOrWhiteSpace(ult.Description)
                    ? ult.Description
                    : "Unleash this hero's signature combat ultimate.";

            // Innate Talent section
            HeroTalentDefinition innate = hero.GetInnateTalent();
            if (_innateTitleText != null)
                _innateTitleText.text = innate != null && !string.IsNullOrWhiteSpace(innate.Title) ? innate.Title : "Innate Talent";
            if (_innateDescText != null)
                _innateDescText.text = innate != null && !string.IsNullOrWhiteSpace(innate.Description)
                    ? innate.Description
                    : $"When {hero.EffectiveDisplayName} is in party, strengthens your opening turn.";

            // Unlockable Future Talents strip
            RebuildTalentNodes(hero);
            RefreshTalentSelectionUI(hero, animate: false);
            RefreshAssignButtonState();

            // Live frame-by-frame animated standing sprite (bottom-right)
            _idleFrames = hero.PreviewIdleFrames;
            _actionFrames = hero.PreviewActionFrames;
            _idleFps = hero.PreviewIdleFps;
            _actionFps = hero.PreviewActionFps;
            _animFrameIdx = 0;
            _animTimer = 0f;
            _playingActionAnim = false;

            if (_animatedSpriteImage != null)
            {
                Sprite firstFrame = (_idleFrames != null && _idleFrames.Count > 0)
                    ? _idleFrames[0]
                    : hero.GetPortraitForWill(false, false, fallbackToWorldSprite: true);
                _animatedSpriteImage.sprite = firstFrame;
                _animatedSpriteImage.color = Color.white;
                _animatedSpriteImage.enabled = firstFrame != null;
            }
            if (_spriteHintText != null) _spriteHintText.text = "CLICK TO PREVIEW ACTION";

            if (triggerActionAnim) TriggerPreviewActionAnimation();
        }

        private void BindLockedHeroDetail(LockedFutureHeroTeaser teaser)
        {
            if (_splashImage != null)
            {
                // Styled mysterious silhouette using fallback hero sprite darkened
                Sprite fallback = playableRoster.Count > 0 && playableRoster[0] != null ? playableRoster[0].Image : null;
                _splashImage.sprite = fallback;
                _splashImage.color = new Color(0.08f, 0.08f, 0.12f, 0.65f);
                _splashImage.enabled = fallback != null;
                _splashHolder.DOKill();
                _splashBasePos = new Vector2(-370f, -45f);
                _splashHolder.anchoredPosition = _splashBasePos;
                _splashHolder.localScale = Vector3.one * 1.02f;
            }
            if (_splashAura != null) _splashAura.color = new Color(teaser.TintColor.r, teaser.TintColor.g, teaser.TintColor.b, 0.24f);

            if (_detailNameText != null) _detailNameText.text = teaser.DisplayName;
            if (_detailSubtitleText != null)
            {
                _detailSubtitleText.text = $"{teaser.RoleTitle}   •   {teaser.UnlockRequirement.ToUpperInvariant()}";
                _detailSubtitleText.color = Color.Lerp(teaser.TintColor, Color.white, 0.35f);
            }

            if (_ultimateTitleText != null) _ultimateTitleText.text = teaser.UltimateTitle;
            if (_ultimateCostText != null) _ultimateCostText.text = "LOCKED";
            if (_ultimateDescText != null) _ultimateDescText.text = teaser.UltimateDescription;
            if (_innateTitleText != null) _innateTitleText.text = teaser.InnateTitle;
            if (_innateDescText != null) _innateDescText.text = teaser.InnateDescription;

            ClearTalentNodes();
            if (_talentDetailTitleText != null) _talentDetailTitleText.text = $"LOCKED COMPANION  •  {teaser.UnlockRequirement.ToUpperInvariant()}";
            if (_talentDetailDescText != null) _talentDetailDescText.text = "Progress through chapters to recruit this hero and unlock their Ultimate & Talent tree.";

            _idleFrames = null;
            _actionFrames = null;
            _playingActionAnim = false;
            if (_animatedSpriteImage != null)
            {
                _animatedSpriteImage.sprite = PartySetupArtFactory.GetPadlockIcon();
                _animatedSpriteImage.color = new Color(0.85f, 0.78f, 0.62f, 0.55f);
                _animatedSpriteImage.enabled = true;
            }
            if (_spriteHintText != null) _spriteHintText.text = "LOCKED HERO";

            RefreshAssignButtonState();
        }

        private void RefreshAssignButtonState()
        {
            if (_assignButton == null || _assignButtonLabel == null || _assignButtonBg == null) return;

            bool isPlayable = _inspectedRosterIndex >= 0 && _inspectedRosterIndex < playableRoster.Count && playableRoster[_inspectedRosterIndex] != null;
            if (!isPlayable)
            {
                _assignButton.interactable = false;
                _assignButtonLabel.text = "LOCKED  •  FUTURE CHAPTER";
                _assignButtonBg.color = new Color(0.32f, 0.30f, 0.34f, 0.90f);
                return;
            }

            _assignButton.interactable = true;
            HeroData hero = playableRoster[_inspectedRosterIndex];
            int currentSlot = FindPartySlotOfHero(hero);

            if (currentSlot == _editingSlotIndex)
            {
                bool canRemove = CountAssignedHeroes() > 1;
                _assignButtonLabel.text = canRemove ? $"REMOVE FROM SLOT {_editingSlotIndex + 1}" : $"ASSIGNED TO SLOT {_editingSlotIndex + 1}";
                _assignButtonBg.color = canRemove ? new Color(0.72f, 0.28f, 0.26f, 0.95f) : new Color(0.45f, 0.52f, 0.42f, 0.92f);
            }
            else if (currentSlot >= 0)
            {
                _assignButtonLabel.text = $"SWAP FROM SLOT {currentSlot + 1} -> SLOT {_editingSlotIndex + 1}";
                _assignButtonBg.color = new Color(0.88f, 0.66f, 0.22f, 0.98f);
            }
            else
            {
                _assignButtonLabel.text = $"ASSIGN TO SLOT {_editingSlotIndex + 1}";
                _assignButtonBg.color = new Color(0.28f, 0.68f, 0.42f, 0.98f);
            }
        }

        private void RefreshRosterIconVisuals()
        {
            for (int i = 0; i < _rosterIcons.Count; i++)
            {
                RosterIconWidgets w = _rosterIcons[i];
                if (w == null) continue;
                bool selected = i == _inspectedRosterIndex;

                if (w.SelectionOutline != null)
                {
                    w.SelectionOutline.enabled = selected;
                    w.SelectionOutline.color = selected ? new Color(1f, 0.88f, 0.42f, 1f) : Color.clear;
                }

                if (!w.IsLockedFuture && i < playableRoster.Count && playableRoster[i] != null)
                {
                    int assignedSlot = FindPartySlotOfHero(playableRoster[i]);
                    bool inParty = assignedSlot >= 0;
                    if (w.SlotBadgeRoot != null) w.SlotBadgeRoot.SetActive(inParty);
                    if (w.SlotBadgeText != null && inParty)
                    {
                        w.SlotBadgeText.text = (assignedSlot + 1).ToString();
                    }
                    if (w.Bg != null)
                        w.Bg.color = selected ? new Color(1f, 0.96f, 0.84f, 1f) : new Color(0.88f, 0.85f, 0.80f, 0.95f);
                }
                else
                {
                    if (w.SlotBadgeRoot != null) w.SlotBadgeRoot.SetActive(false);
                }

                if (w.Root != null && !selected)
                {
                    w.Root.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
            }
        }

        private void ClearTalentNodes()
        {
            for (int i = 0; i < _talentNodes.Count; i++)
                if (_talentNodes[i]?.Root != null) Destroy(_talentNodes[i].Root.gameObject);
            _talentNodes.Clear();
        }

        private void RebuildTalentNodes(HeroData hero)
        {
            ClearTalentNodes();
            if (_talentNodesRow == null || hero == null || hero.Talents == null) return;

            int layer = _talentNodesRow.gameObject.layer;
            TMP_FontAsset font = ResolveUiFont();
            Sprite roundRect = PartySetupArtFactory.GetRoundedRect();
            Sprite roundOutline = PartySetupArtFactory.GetRoundedOutline();
            Sprite padlock = PartySetupArtFactory.GetPadlockIcon();
            string[] numerals = { "I", "II", "III", "IV", "V" };

            for (int i = 0; i < hero.Talents.Count; i++)
            {
                HeroTalentDefinition t = hero.Talents[i];
                if (t == null) continue;

                RectTransform nodeRt = CreateRect(_talentNodesRow, $"TalentNode_{i}", layer);
                nodeRt.sizeDelta = new Vector2(104f, 58f);

                Image bg = nodeRt.gameObject.AddComponent<Image>();
                bg.sprite = roundRect;
                bg.type = Image.Type.Sliced;
                bg.color = t.IsUnlocked ? new Color(0.28f, 0.24f, 0.16f, 0.92f) : new Color(0.14f, 0.14f, 0.17f, 0.85f);

                RectTransform outRt = CreateRect(nodeRt, "Outline", layer);
                Stretch(outRt);
                Image outline = outRt.gameObject.AddComponent<Image>();
                outline.sprite = roundOutline;
                outline.type = Image.Type.Sliced;
                outline.raycastTarget = false;

                TextMeshProUGUI tier = MakeText(nodeRt, "Tier", font, 16f, FontStyles.Bold, t.IsUnlocked ? new Color(1f, 0.90f, 0.56f, 1f) : new Color(0.68f, 0.68f, 0.74f, 0.88f));
                tier.alignment = TextAlignmentOptions.Center;
                tier.text = t.IsInnate ? "INNATE" : $"TALENT {(i < numerals.Length ? numerals[i] : (i + 1).ToString())}";
                tier.rectTransform.offsetMin = new Vector2(4f, t.IsUnlocked ? 4f : 22f);
                tier.rectTransform.offsetMax = new Vector2(-4f, -4f);

                // Padlock icon for locked future talents
                RectTransform lockRt = CreateRect(nodeRt, "LockIcon", layer);
                lockRt.anchorMin = lockRt.anchorMax = new Vector2(0.5f, 0f);
                lockRt.pivot = new Vector2(0.5f, 0f);
                lockRt.anchoredPosition = new Vector2(0f, 5f);
                lockRt.sizeDelta = new Vector2(18f, 18f);
                Image lockImg = lockRt.gameObject.AddComponent<Image>();
                lockImg.sprite = padlock;
                lockImg.color = new Color(0.86f, 0.78f, 0.60f, 0.85f);
                lockImg.raycastTarget = false;
                lockImg.gameObject.SetActive(!t.IsUnlocked);

                // Golden active dot for unlocked talents
                RectTransform dotRt = CreateRect(nodeRt, "ActiveDot", layer);
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(1f, 1f);
                dotRt.pivot = new Vector2(1f, 1f);
                dotRt.anchoredPosition = new Vector2(-7f, -7f);
                dotRt.sizeDelta = new Vector2(10f, 10f);
                Image dotImg = dotRt.gameObject.AddComponent<Image>();
                dotImg.sprite = PartySetupArtFactory.GetSoftRadialGlow();
                dotImg.color = new Color(0.42f, 0.95f, 0.56f, 1f);
                dotImg.raycastTarget = false;
                dotRt.gameObject.SetActive(t.IsUnlocked);

                Button btn = nodeRt.gameObject.AddComponent<Button>();
                int capturedIdx = i;
                btn.onClick.AddListener(() => InspectTalent(capturedIdx));

                // Also allow hover preview!
                EventTrigger trigger = nodeRt.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry enter = new() { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => InspectTalent(capturedIdx));
                trigger.triggers.Add(enter);

                _talentNodes.Add(new TalentNodeWidgets
                {
                    Root = nodeRt,
                    Bg = bg,
                    Outline = outline,
                    LockIcon = lockImg,
                    TierLabel = tier,
                    ActiveDot = dotRt.gameObject
                });
            }
        }

        private void RefreshTalentSelectionUI(HeroData hero, bool animate)
        {
            if (hero == null || hero.Talents == null || hero.Talents.Count == 0) return;
            int idx = Mathf.Clamp(_inspectedTalentIndex, 0, hero.Talents.Count - 1);
            HeroTalentDefinition selected = hero.Talents[idx];

            for (int i = 0; i < _talentNodes.Count; i++)
            {
                TalentNodeWidgets node = _talentNodes[i];
                HeroTalentDefinition t = i < hero.Talents.Count ? hero.Talents[i] : null;
                bool active = i == idx;

                if (node.Outline != null)
                {
                    node.Outline.color = active
                        ? new Color(1f, 0.86f, 0.42f, 1f)
                        : (t != null && t.IsUnlocked ? new Color(0.68f, 0.56f, 0.30f, 0.65f) : new Color(0.40f, 0.40f, 0.46f, 0.45f));
                }

                if (animate && active && node.Root != null)
                {
                    node.Root.DOKill();
                    node.Root.DOPunchScale(Vector3.one * 0.08f, 0.18f, 5, 0.5f).SetUpdate(true);
                }
            }

            if (selected != null)
            {
                string statusTag = selected.IsUnlocked
                    ? "<color=#7DF29A>ACTIVE</color>"
                    : $"<color=#F2C46D>LOCKED • {selected.UnlockRequirement}</color>";

                if (_talentDetailTitleText != null)
                    _talentDetailTitleText.text = $"{selected.Title}   —   {statusTag}";
                if (_talentDetailDescText != null)
                    _talentDetailDescText.text = selected.Description;
            }
        }

        #endregion

        #region Procedural Animations (Motes, Tarot Hover, Splash Breath, Animated Standing Sprite)

        private void UpdateFloatingMotes(float dt)
        {
            for (int i = 0; i < _motes.Count; i++)
            {
                FloatingMote m = _motes[i];
                if (m?.Rect == null) continue;
                Vector2 p = m.Rect.anchoredPosition + m.Velocity * dt;
                if (p.y > 560f) p.y = -560f;
                if (p.x > 980f) p.x = -980f;
                if (p.x < -980f) p.x = 980f;
                m.Rect.anchoredPosition = p;

                float twinkle = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.2f + m.Phase);
                if (m.Image != null)
                {
                    Color c = m.Image.color;
                    c.a = m.BaseAlpha * twinkle;
                    m.Image.color = c;
                }
            }
        }

        private void UpdateTarotIdleAndHover(float dt)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < PartySlotCount; i++)
            {
                TarotSlotWidgets card = _tarotCards[i];
                if (card?.TiltCard == null) continue;

                bool hovered = !_isDetailOpen && _hoveredTarotIndex == i;
                card.HoverBlend = Mathf.MoveTowards(card.HoverBlend, hovered ? 1f : 0f, dt * 6.5f);

                float idleBob = Mathf.Sin(t * 1.55f + i * 1.45f) * 4.5f;
                float targetY = idleBob + card.HoverBlend * 18f;
                float targetScale = 1f + card.HoverBlend * 0.048f;
                float targetRot = hovered ? Mathf.Sin(t * 4.2f) * 1.15f : Mathf.Sin(t * 0.9f + i) * 0.45f;

                if (!DOTween.IsTweening(card.TiltCard))
                {
                    card.TiltCard.anchoredPosition = Vector2.Lerp(card.TiltCard.anchoredPosition, new Vector2(0f, targetY), dt * 11f);
                    card.TiltCard.localScale = Vector3.Lerp(card.TiltCard.localScale, Vector3.one * targetScale, dt * 11f);
                    card.TiltCard.localRotation = Quaternion.Lerp(card.TiltCard.localRotation, Quaternion.Euler(0f, 0f, targetRot), dt * 10f);
                }

                if (card.Glow != null)
                {
                    Color slotCol = PartySetupArtFactory.GetSlotAccentColor(i);
                    float pulse = 0.22f + 0.10f * Mathf.Sin(t * 2.4f + i);
                    card.Glow.color = new Color(slotCol.r, slotCol.g, slotCol.b, Mathf.Lerp(pulse * 0.45f, 0.72f, card.HoverBlend));
                }
            }
        }

        private void UpdateSplashBreathing()
        {
            if (_splashHolder == null || DOTween.IsTweening(_splashHolder)) return;
            float t = Time.unscaledTime;
            float breathY = Mathf.Sin(t * 1.8f) * 4.5f;
            _splashHolder.anchoredPosition = _splashBasePos + new Vector2(0f, breathY);

            if (_splashAura != null)
            {
                float s = 1f + 0.045f * Mathf.Sin(t * 2.1f);
                _splashAura.rectTransform.localScale = Vector3.one * s;
            }
        }

        private void UpdateAnimatedStandingSprite(float dt)
        {
            if (_animatedSpriteImage == null) return;

            // Gentle living breathing bounce on the chibi sprite
            if (!DOTween.IsTweening(_animatedSpriteImage.rectTransform))
            {
                float bob = Mathf.Sin(Time.unscaledTime * 3.1f) * 3.2f;
                _animatedSpriteImage.rectTransform.anchoredPosition = _spriteBasePos + new Vector2(0f, bob);
            }

            IReadOnlyList<Sprite> activeFrames = _playingActionAnim ? _actionFrames : _idleFrames;
            if (activeFrames == null || activeFrames.Count == 0) return;

            float fps = _playingActionAnim ? _actionFps : _idleFps;
            _animTimer += dt;
            float step = 1f / Mathf.Max(0.5f, fps);

            if (_animTimer >= step)
            {
                _animTimer -= step;
                _animFrameIdx++;

                if (_playingActionAnim && _animFrameIdx >= activeFrames.Count)
                {
                    _playingActionAnim = false;
                    _animFrameIdx = 0;
                    activeFrames = _idleFrames;
                }
                else if (activeFrames.Count > 0)
                {
                    _animFrameIdx %= activeFrames.Count;
                }

                if (activeFrames != null && activeFrames.Count > 0)
                {
                    _animatedSpriteImage.sprite = activeFrames[_animFrameIdx % activeFrames.Count];
                }
            }
        }

        private void SpawnSparkleBurst(Vector2 fxLocalPos, Color tint, int count)
        {
            if (_fxLayer == null) return;
            Sprite sparkle = PartySetupArtFactory.GetSparkle();
            int layer = _fxLayer.gameObject.layer;

            for (int i = 0; i < count; i++)
            {
                RectTransform rt = CreateRect(_fxLayer, "Sparkle", layer);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = fxLocalPos;
                float sz = UnityEngine.Random.Range(14f, 30f);
                rt.sizeDelta = new Vector2(sz, sz);

                Color baseCol = Color.Lerp(tint, Color.white, UnityEngine.Random.Range(0.25f, 0.75f));
                Image img = rt.gameObject.AddComponent<Image>();
                img.sprite = sparkle;
                img.color = baseCol;
                img.raycastTarget = false;

                float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.25f, 0.25f);
                float dist = UnityEngine.Random.Range(55f, 155f);
                Vector2 target = fxLocalPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                float dur = UnityEngine.Random.Range(0.32f, 0.56f);

                _sparkles.Add(new SparkleParticle
                {
                    Rect = rt,
                    Image = img,
                    StartPos = fxLocalPos,
                    EndPos = target,
                    StartScale = 1f,
                    SpinSpeed = UnityEngine.Random.Range(-280f, 280f),
                    Age = 0f,
                    Duration = dur,
                    BaseColor = baseCol
                });
            }
        }

        private void UpdateSparkleParticles(float dt)
        {
            for (int i = _sparkles.Count - 1; i >= 0; i--)
            {
                SparkleParticle s = _sparkles[i];
                s.Age += dt;
                float u = Mathf.Clamp01(s.Age / Mathf.Max(0.01f, s.Duration));
                if (u >= 1f || s.Rect == null)
                {
                    if (s.Rect != null) Destroy(s.Rect.gameObject);
                    _sparkles.RemoveAt(i);
                    continue;
                }

                float easeOut = 1f - Mathf.Pow(1f - u, 3f);
                s.Rect.anchoredPosition = Vector2.Lerp(s.StartPos, s.EndPos, easeOut);
                s.Rect.localScale = Vector3.one * (s.StartScale * (1f - u * u));
                s.Rect.Rotate(0f, 0f, s.SpinSpeed * dt);
                if (s.Image != null)
                {
                    Color c = s.BaseColor;
                    c.a = s.BaseColor.a * (1f - u * u);
                    s.Image.color = c;
                }
            }
        }

        private Vector2 ConvertToFxLocalPos(RectTransform target)
        {
            if (_fxLayer == null || target == null) return Vector2.zero;
            Vector3 worldCenter = target.TransformPoint(target.rect.center);
            Vector3 local = _fxLayer.InverseTransformPoint(worldCenter);
            return new Vector2(local.x, local.y);
        }

        #endregion

        #region UI Construction

        private void BuildUIIfNeeded()
        {
            if (_built || targetCanvas == null) return;
            _built = true;

            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset serifFont = ResolveSerifFont();
            TMP_FontAsset uiFont = ResolveUiFont();

            BuildLauncherDock(layer, serifFont, uiFont);
            BuildFullScreenPartySetupModal(layer, serifFont, uiFont);
        }

        private void BuildLauncherDock(int layer, TMP_FontAsset serifFont, TMP_FontAsset uiFont)
        {
            Sprite solid = PartySetupArtFactory.GetSolid();
            Sprite roundRect = PartySetupArtFactory.GetRoundedRect();
            Sprite roundOutline = PartySetupArtFactory.GetRoundedOutline();

            _launcherDock = CreateRect(targetCanvas.transform, "PartySetupLauncherDock", layer);
            _launcherDock.anchorMin = _launcherDock.anchorMax = new Vector2(0f, 0f);
            _launcherDock.pivot = new Vector2(0f, 0f);
            _launcherDock.anchoredPosition = new Vector2(28f, 24f);
            _launcherDock.sizeDelta = new Vector2(560f, 128f);

            Image dockBg = _launcherDock.gameObject.AddComponent<Image>();
            dockBg.sprite = panelSprite != null ? panelSprite : roundRect;
            dockBg.type = panelSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            dockBg.color = panelSprite != null ? Color.white : new Color(0.24f, 0.16f, 0.10f, 0.94f);

            // Active party summary bar along the top
            _dockPartySummaryText = MakeText(_launcherDock, "Summary", uiFont, 14.5f, FontStyles.Bold, new Color(0.95f, 0.89f, 0.76f, 0.96f));
            _dockPartySummaryText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _dockPartySummaryText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _dockPartySummaryText.rectTransform.offsetMin = new Vector2(22f, -34f);
            _dockPartySummaryText.rectTransform.offsetMax = new Vector2(-22f, -10f);
            _dockPartySummaryText.alignment = TextAlignmentOptions.MidlineLeft;

            // 3 compact party mini-slot portraits
            for (int i = 0; i < PartySlotCount; i++)
            {
                RectTransform slotBox = CreateRect(_launcherDock, $"DockSlot_{i + 1}", layer);
                slotBox.anchorMin = slotBox.anchorMax = new Vector2(0f, 0f);
                slotBox.pivot = new Vector2(0f, 0f);
                slotBox.anchoredPosition = new Vector2(22f + i * 86f, 16f);
                slotBox.sizeDelta = new Vector2(74f, 74f);

                Image sBg = slotBox.gameObject.AddComponent<Image>();
                sBg.sprite = roundRect;
                sBg.type = Image.Type.Sliced;
                sBg.color = new Color(0.16f, 0.12f, 0.09f, 0.96f);

                RectTransform maskRt = CreateRect(slotBox, "Mask", layer);
                Stretch(maskRt, 4f);
                maskRt.gameObject.AddComponent<RectMask2D>();

                RectTransform pRt = CreateRect(maskRt, "Portrait", layer);
                pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(66f, 66f);
                Image pImg = pRt.gameObject.AddComponent<Image>();
                pImg.preserveAspect = true;
                pImg.raycastTarget = false;
                _dockMiniPortraits[i] = pImg;

                TextMeshProUGUI emptyLbl = MakeText(slotBox, "Empty", uiFont, 22f, FontStyles.Bold, new Color(0.82f, 0.74f, 0.58f, 0.50f));
                emptyLbl.alignment = TextAlignmentOptions.Center;
                emptyLbl.text = "+";
                _dockMiniEmptyLabels[i] = emptyLbl;

                RectTransform borderRt = CreateRect(slotBox, "Border", layer);
                Stretch(borderRt);
                Image bImg = borderRt.gameObject.AddComponent<Image>();
                bImg.sprite = roundOutline;
                bImg.type = Image.Type.Sliced;
                bImg.color = PartySetupArtFactory.GetSlotAccentColor(i);
                bImg.raycastTarget = false;

                // Small 1/2/3 slot tag
                TextMeshProUGUI num = MakeText(slotBox, "Num", uiFont, 15f, FontStyles.Bold, Color.white);
                num.rectTransform.anchorMin = num.rectTransform.anchorMax = Vector2.zero;
                num.rectTransform.pivot = Vector2.zero;
                num.rectTransform.anchoredPosition = new Vector2(5f, 3f);
                num.rectTransform.sizeDelta = new Vector2(22f, 20f);
                num.alignment = TextAlignmentOptions.BottomLeft;
                num.outlineWidth = 0.35f;
                num.outlineColor = Color.black;
                num.text = (i + 1).ToString();

                Button slotBtn = slotBox.gameObject.AddComponent<Button>();
                int capturedSlot = i;
                slotBtn.onClick.AddListener(() => OpenSlotSelector(capturedSlot));
            }

            // Prominent "PARTY SETUP" button on the right half of the dock
            RectTransform openBtnRt = CreateRect(_launcherDock, "OpenPartySetupButton", layer);
            openBtnRt.anchorMin = openBtnRt.anchorMax = new Vector2(1f, 0f);
            openBtnRt.pivot = new Vector2(1f, 0f);
            openBtnRt.anchoredPosition = new Vector2(-22f, 20f);
            openBtnRt.sizeDelta = new Vector2(246f, 66f);

            Image openBg = openBtnRt.gameObject.AddComponent<Image>();
            openBg.sprite = buttonSprite != null ? buttonSprite : solid;
            openBg.color = new Color(0.98f, 0.86f, 0.48f, 1f);

            Button openBtn = openBtnRt.gameObject.AddComponent<Button>();
            openBtn.onClick.AddListener(OpenPartySetup);

            TextMeshProUGUI openTxt = MakeText(openBtnRt, "Label", serifFont, 24f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            openTxt.alignment = TextAlignmentOptions.Center;
            openTxt.text = "PARTY SETUP";
        }

        private void BuildFullScreenPartySetupModal(int layer, TMP_FontAsset serifFont, TMP_FontAsset uiFont)
        {
            _modalRoot = CreateRect(targetCanvas.transform, "PartySetupModal", layer);
            Stretch(_modalRoot);
            _modalCg = _modalRoot.gameObject.AddComponent<CanvasGroup>();

            // Click blocker so clicks never bleed through to Levels.unity behind it
            Image rootBlocker = _modalRoot.gameObject.AddComponent<Image>();
            rootBlocker.sprite = PartySetupArtFactory.GetParchmentBackground();
            rootBlocker.color = Color.white;

            BuildStage1Overview(_modalRoot, layer, serifFont, uiFont);
            BuildStage2DetailOverlay(_modalRoot, layer, serifFont, uiFont);

            // Top-most FX layer for golden sparkle bursts
            _fxLayer = CreateRect(_modalRoot, "FxLayer", layer);
            Stretch(_fxLayer);

            _modalRoot.gameObject.SetActive(false);
        }

        private void BuildStage1Overview(RectTransform parent, int layer, TMP_FontAsset serifFont, TMP_FontAsset uiFont)
        {
            _overviewLayer = CreateRect(parent, "Stage1_Overview", layer);
            Stretch(_overviewLayer);

            // Flowing golden constellation thread curves behind the 3 tarot cards
            RectTransform threadsRt = CreateRect(_overviewLayer, "GoldenThreads", layer);
            Stretch(threadsRt);
            Image threadsImg = threadsRt.gameObject.AddComponent<Image>();
            threadsImg.sprite = PartySetupArtFactory.GetGoldenThreads();
            threadsImg.raycastTarget = false;

            // Drifting subtle golden motes
            Sprite sparkle = PartySetupArtFactory.GetSparkle();
            for (int i = 0; i < 18; i++)
            {
                RectTransform mRt = CreateRect(_overviewLayer, $"Mote_{i}", layer);
                mRt.anchorMin = mRt.anchorMax = new Vector2(0.5f, 0.5f);
                mRt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-920f, 920f), UnityEngine.Random.Range(-500f, 500f));
                float sz = UnityEngine.Random.Range(9f, 20f);
                mRt.sizeDelta = new Vector2(sz, sz);
                Image mImg = mRt.gameObject.AddComponent<Image>();
                mImg.sprite = sparkle;
                float a = UnityEngine.Random.Range(0.22f, 0.50f);
                mImg.color = new Color(1f, 0.92f, 0.58f, a);
                mImg.raycastTarget = false;
                _motes.Add(new FloatingMote
                {
                    Rect = mRt,
                    Image = mImg,
                    Velocity = new Vector2(UnityEngine.Random.Range(-8f, 12f), UnityEngine.Random.Range(10f, 24f)),
                    Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    BaseAlpha = a
                });
            }

            // Top-Left Radiant Sunburst Emblem (exact match to Image 1!)
            RectTransform sunRt = CreateRect(_overviewLayer, "CornerSunburst", layer);
            sunRt.anchorMin = sunRt.anchorMax = new Vector2(0f, 1f);
            sunRt.pivot = new Vector2(0.5f, 0.5f);
            sunRt.anchoredPosition = new Vector2(36f, -34f);
            sunRt.sizeDelta = new Vector2(490f, 490f);
            Image sunImg = sunRt.gameObject.AddComponent<Image>();
            sunImg.sprite = PartySetupArtFactory.GetSunburstEmblem();
            sunImg.raycastTarget = false;

            // Top-Right Terracotta Wave Accent
            RectTransform trWave = CreateRect(_overviewLayer, "TopRightWave", layer);
            trWave.anchorMin = trWave.anchorMax = new Vector2(1f, 1f);
            trWave.pivot = new Vector2(1f, 1f);
            trWave.anchoredPosition = Vector2.zero;
            trWave.sizeDelta = new Vector2(1020f, 220f);
            Image trImg = trWave.gameObject.AddComponent<Image>();
            trImg.sprite = PartySetupArtFactory.GetTopRightWave();
            trImg.raycastTarget = false;

            // Bottom-Left Terracotta Wave Accent
            RectTransform blWave = CreateRect(_overviewLayer, "BottomLeftWave", layer);
            blWave.anchorMin = blWave.anchorMax = new Vector2(0f, 0f);
            blWave.pivot = new Vector2(0f, 0f);
            blWave.anchoredPosition = Vector2.zero;
            blWave.sizeDelta = new Vector2(1080f, 250f);
            Image blImg = blWave.gameObject.AddComponent<Image>();
            blImg.sprite = PartySetupArtFactory.GetBottomLeftWave();
            blImg.raycastTarget = false;

            // "PARTY SETUP" Header Title (Top-Left, serif with dark outline like Image 1!)
            TextMeshProUGUI title = MakeText(_overviewLayer, "PartySetupTitle", serifFont, 64f, FontStyles.Bold, new Color(0.99f, 0.95f, 0.84f, 1f));
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(225f, -38f);
            title.rectTransform.sizeDelta = new Vector2(680f, 92f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.characterSpacing = 6f;
            title.outlineWidth = 0.24f;
            title.outlineColor = new Color(0.18f, 0.11f, 0.05f, 0.96f);
            title.text = "PARTY SETUP";

            // Top-Right 3 Diamond Slot-Clear Badges (matching Image 1!)
            Sprite diamondSprite = PartySetupArtFactory.GetDiamondBadge();
            for (int i = 0; i < PartySlotCount; i++)
            {
                RectTransform dRt = CreateRect(_overviewLayer, $"SlotDiamond_{i + 1}", layer);
                dRt.anchorMin = dRt.anchorMax = new Vector2(1f, 1f);
                dRt.pivot = new Vector2(0.5f, 0.5f);
                dRt.anchoredPosition = new Vector2(-335f + i * 124f, -72f);
                dRt.sizeDelta = new Vector2(104f, 104f);

                Image dImg = dRt.gameObject.AddComponent<Image>();
                dImg.sprite = diamondSprite;

                TextMeshProUGUI hint = MakeText(dRt, "SlotNum", uiFont, 14f, FontStyles.Bold, Color.white);
                hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                hint.rectTransform.pivot = new Vector2(0.5f, 0f);
                hint.rectTransform.anchoredPosition = new Vector2(0f, 11f);
                hint.rectTransform.sizeDelta = new Vector2(36f, 20f);
                hint.alignment = TextAlignmentOptions.Center;
                hint.outlineWidth = 0.30f;
                hint.outlineColor = Color.black;
                hint.text = (i + 1).ToString();

                Button btn = dRt.gameObject.AddComponent<Button>();
                int capturedIdx = i;
                btn.onClick.AddListener(() => ClearSlot(capturedIdx));

                _diamondBadges[i] = new DiamondBadgeWidgets { Root = dRt, Bg = dImg, SlotHint = hint };
            }

            // Center — 3 Tall Ornate Tarot Party Cards (Sun / Star / Moon)
            float[] xPositions = { -535f, 0f, 535f };
            Sprite glowSprite = PartySetupArtFactory.GetSoftRadialGlow();
            Sprite solid = PartySetupArtFactory.GetSolid();

            for (int i = 0; i < PartySlotCount; i++)
            {
                RectTransform cardRoot = CreateRect(_overviewLayer, $"TarotPartyCard_{i + 1}", layer);
                cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
                cardRoot.anchoredPosition = new Vector2(xPositions[i], -22f);
                cardRoot.sizeDelta = new Vector2(372f, 658f);

                RectTransform tiltCard = CreateRect(cardRoot, "TiltPivot", layer);
                Stretch(tiltCard);

                // Luminous hover halo behind the tarot card
                RectTransform glowRt = CreateRect(tiltCard, "HoverGlow", layer);
                glowRt.anchorMin = Vector2.zero;
                glowRt.anchorMax = Vector2.one;
                glowRt.offsetMin = new Vector2(-42f, -42f);
                glowRt.offsetMax = new Vector2(42f, 42f);
                Image glowImg = glowRt.gameObject.AddComponent<Image>();
                glowImg.sprite = glowSprite;
                glowImg.color = new Color(1f, 0.85f, 0.40f, 0.16f);
                glowImg.raycastTarget = false;

                // Inner celestial tarot backdrop (Sun / Star / Moon)
                RectTransform bgRt = CreateRect(tiltCard, "CelestialBackdrop", layer);
                Stretch(bgRt);
                Image bgImg = bgRt.gameObject.AddComponent<Image>();
                bgImg.sprite = PartySetupArtFactory.GetTarotCardBackdrop(i);

                // Clipped portrait mask inside the card frame
                RectTransform maskRt = CreateRect(tiltCard, "PortraitMask", layer);
                Stretch(maskRt, 14f);
                maskRt.gameObject.AddComponent<RectMask2D>();

                RectTransform pRt = CreateRect(maskRt, "HeroPortrait", layer);
                pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(344f, 630f);
                Image pImg = pRt.gameObject.AddComponent<Image>();
                pImg.preserveAspect = true;
                pImg.raycastTarget = false;

                // Empty slot invitation overlay
                RectTransform emptyRt = CreateRect(maskRt, "EmptyPrompt", layer);
                Stretch(emptyRt);
                TextMeshProUGUI emptyPlus = MakeText(emptyRt, "Plus", serifFont, 88f, FontStyles.Bold, new Color(0.52f, 0.44f, 0.34f, 0.42f));
                emptyPlus.alignment = TextAlignmentOptions.Center;
                emptyPlus.text = "+";

                // Subtle bottom dark gradient bar inside card for legible Hero Name & Role
                RectTransform captionRt = CreateRect(maskRt, "CaptionBar", layer);
                captionRt.anchorMin = new Vector2(0f, 0f);
                captionRt.anchorMax = new Vector2(1f, 0f);
                captionRt.pivot = new Vector2(0.5f, 0f);
                captionRt.sizeDelta = new Vector2(0f, 84f);
                Image capBg = captionRt.gameObject.AddComponent<Image>();
                capBg.sprite = solid;
                capBg.color = new Color(0.09f, 0.07f, 0.06f, 0.72f);
                capBg.raycastTarget = false;

                TextMeshProUGUI heroName = MakeText(captionRt, "HeroName", serifFont, 30f, FontStyles.Bold, new Color(1f, 0.96f, 0.86f, 1f));
                heroName.rectTransform.offsetMin = new Vector2(14f, 28f);
                heroName.rectTransform.offsetMax = new Vector2(-14f, -6f);
                heroName.alignment = TextAlignmentOptions.Center;

                TextMeshProUGUI slotTag = MakeText(captionRt, "SlotTag", uiFont, 13.5f, FontStyles.Bold, PartySetupArtFactory.GetSlotAccentColor(i));
                slotTag.rectTransform.offsetMin = new Vector2(14f, 6f);
                slotTag.rectTransform.offsetMax = new Vector2(-14f, -52f);
                slotTag.alignment = TextAlignmentOptions.Center;

                // Ornate outer celestial double-border frame layered cleanly on top of the portrait!
                RectTransform frameRt = CreateRect(tiltCard, "OrnateFrame", layer);
                Stretch(frameRt);
                Image frameImg = frameRt.gameObject.AddComponent<Image>();
                frameImg.sprite = PartySetupArtFactory.GetTarotCardFrame(i);
                frameImg.raycastTarget = false;

                // Click & Hover interactions
                Button btn = bgRt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int capturedSlot = i;
                btn.onClick.AddListener(() => OpenSlotSelector(capturedSlot));

                EventTrigger trigger = bgRt.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry enter = new() { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => _hoveredTarotIndex = capturedSlot);
                EventTrigger.Entry exit = new() { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (_hoveredTarotIndex == capturedSlot) _hoveredTarotIndex = -1; });
                trigger.triggers.Add(enter);
                trigger.triggers.Add(exit);

                _tarotCards[i] = new TarotSlotWidgets
                {
                    Root = cardRoot,
                    TiltCard = tiltCard,
                    Glow = glowImg,
                    Backdrop = bgImg,
                    Portrait = pImg,
                    PortraitRect = pRt,
                    Frame = frameImg,
                    EmptyStateRoot = emptyRt.gameObject,
                    NameText = heroName,
                    SlotTagText = slotTag,
                    BaseAnchoredPos = cardRoot.anchoredPosition
                };
            }

            // Bottom-Right "CONFIRM PARTY" button (hidden automatically while Stage 2 Detail is open)
            RectTransform confirmRt = CreateRect(_overviewLayer, "ConfirmPartyButton", layer);
            _confirmPartyButtonRoot = confirmRt.gameObject;
            confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(1f, 0f);
            confirmRt.pivot = new Vector2(1f, 0f);
            confirmRt.anchoredPosition = new Vector2(-44f, 34f);
            confirmRt.sizeDelta = new Vector2(270f, 64f);

            Image confirmBg = confirmRt.gameObject.AddComponent<Image>();
            confirmBg.sprite = buttonSprite != null ? buttonSprite : PartySetupArtFactory.GetRoundedRect();
            confirmBg.type = buttonSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            confirmBg.color = new Color(0.99f, 0.87f, 0.48f, 1f);

            Button confirmBtn = confirmRt.gameObject.AddComponent<Button>();
            confirmBtn.onClick.AddListener(ClosePartySetup);

            TextMeshProUGUI confirmTxt = MakeText(confirmRt, "Label", serifFont, 24f, FontStyles.Bold, new Color(0.22f, 0.12f, 0.04f, 1f));
            confirmTxt.alignment = TextAlignmentOptions.Center;
            confirmTxt.text = "CONFIRM PARTY";
        }

        private void BuildStage2DetailOverlay(RectTransform parent, int layer, TMP_FontAsset serifFont, TMP_FontAsset uiFont)
        {
            Sprite solid = PartySetupArtFactory.GetSolid();
            Sprite roundRect = PartySetupArtFactory.GetRoundedRect();
            Sprite roundOutline = PartySetupArtFactory.GetRoundedOutline();

            _detailOverlay = CreateRect(parent, "Stage2_CharacterDetailOverlay", layer);
            Stretch(_detailOverlay);
            _detailCg = _detailOverlay.gameObject.AddComponent<CanvasGroup>();

            // Warm charcoal scrim dimming the 3 tarot cards in the background (exact match to Image 2!)
            Image scrim = _detailOverlay.gameObject.AddComponent<Image>();
            scrim.sprite = solid;
            scrim.color = new Color(0.08f, 0.07f, 0.07f, 0.76f);

            // Clicking open scrim area returns smoothly to Stage 1
            Button scrimBtn = _detailOverlay.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseDetailOverlay);

            // Soft theme-tinted halo behind the massive left splash art
            RectTransform auraRt = CreateRect(_detailOverlay, "SplashAura", layer);
            auraRt.anchorMin = auraRt.anchorMax = new Vector2(0.5f, 0.5f);
            auraRt.anchoredPosition = new Vector2(-380f, 10f);
            auraRt.sizeDelta = new Vector2(880f, 880f);
            _splashAura = auraRt.gameObject.AddComponent<Image>();
            _splashAura.sprite = PartySetupArtFactory.GetSoftRadialGlow();
            _splashAura.color = new Color(0.55f, 0.90f, 0.50f, 0.28f);
            _splashAura.raycastTarget = false;

            // Left Side — Oversized Dramatic Character Splash Portrait (Image 2!)
            _splashHolder = CreateRect(_detailOverlay, "SplashPortraitHolder", layer);
            _splashHolder.anchorMin = _splashHolder.anchorMax = new Vector2(0.5f, 0.5f);
            _splashHolder.anchoredPosition = new Vector2(-370f, -45f);
            _splashHolder.sizeDelta = new Vector2(860f, 1060f);

            _splashImage = _splashHolder.gameObject.AddComponent<Image>();
            _splashImage.preserveAspect = true;
            _splashImage.raycastTarget = false;

            // Center-Right Info Container
            RectTransform infoWrapper = CreateRect(_detailOverlay, "RightInfoWrapper", layer);
            infoWrapper.anchorMin = infoWrapper.anchorMax = new Vector2(0.5f, 0.5f);
            infoWrapper.anchoredPosition = new Vector2(385f, 130f);
            infoWrapper.sizeDelta = new Vector2(760f, 650f);

            // Stop backdrop click-through inside the info panel area
            Image infoCatch = infoWrapper.gameObject.AddComponent<Image>();
            infoCatch.color = new Color(0f, 0f, 0f, 0.001f);

            _infoColumn = CreateRect(infoWrapper, "InfoAnimatedGroup", layer);
            Stretch(_infoColumn);
            _infoColumnCg = _infoColumn.gameObject.AddComponent<CanvasGroup>();

            // Iconic Crisp Vertical White Divider Line (main segment + signature lower dash like Image 2!)
            RectTransform vLineMain = CreateRect(_infoColumn, "VerticalDividerMain", layer);
            vLineMain.anchorMin = vLineMain.anchorMax = new Vector2(0f, 1f);
            vLineMain.pivot = new Vector2(0f, 1f);
            vLineMain.anchoredPosition = new Vector2(10f, -12f);
            vLineMain.sizeDelta = new Vector2(4f, 472f);
            Image vMainImg = vLineMain.gameObject.AddComponent<Image>();
            vMainImg.sprite = solid;
            vMainImg.color = new Color(0.98f, 0.96f, 0.92f, 0.92f);
            vMainImg.raycastTarget = false;

            RectTransform vLineDash = CreateRect(_infoColumn, "VerticalDividerDash", layer);
            vLineDash.anchorMin = vLineDash.anchorMax = new Vector2(0f, 1f);
            vLineDash.pivot = new Vector2(0f, 1f);
            vLineDash.anchoredPosition = new Vector2(10f, -502f);
            vLineDash.sizeDelta = new Vector2(4f, 42f);
            Image vDashImg = vLineDash.gameObject.AddComponent<Image>();
            vDashImg.sprite = solid;
            vDashImg.color = new Color(0.98f, 0.96f, 0.92f, 0.85f);
            vDashImg.raycastTarget = false;

            // Character Name (Large Display Serif — "Aura", "Ember")
            _detailNameText = MakeText(_infoColumn, "HeroName", serifFont, 68f, FontStyles.Bold, Color.white);
            _detailNameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailNameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailNameText.rectTransform.offsetMin = new Vector2(36f, -86f);
            _detailNameText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            _detailNameText.alignment = TextAlignmentOptions.MidlineLeft;
            _detailNameText.outlineWidth = 0.14f;
            _detailNameText.outlineColor = new Color(0f, 0f, 0f, 0.55f);

            // Subtle Stats / Role line
            _detailSubtitleText = MakeText(_infoColumn, "HeroStatsLine", uiFont, 15.5f, FontStyles.Bold, new Color(0.75f, 0.95f, 0.78f, 0.95f));
            _detailSubtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailSubtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailSubtitleText.rectTransform.offsetMin = new Vector2(38f, -114f);
            _detailSubtitleText.rectTransform.offsetMax = new Vector2(-12f, -86f);
            _detailSubtitleText.alignment = TextAlignmentOptions.MidlineLeft;

            // "Ultimate" Section Header + Cost Pill + Title + Description
            TextMeshProUGUI ultHeader = MakeText(_infoColumn, "UltimateHeader", serifFont, 31f, FontStyles.Bold, new Color(1f, 0.96f, 0.86f, 1f));
            ultHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            ultHeader.rectTransform.anchorMax = new Vector2(0f, 1f);
            ultHeader.rectTransform.pivot = new Vector2(0f, 1f);
            ultHeader.rectTransform.anchoredPosition = new Vector2(36f, -134f);
            ultHeader.rectTransform.sizeDelta = new Vector2(148f, 40f);
            ultHeader.alignment = TextAlignmentOptions.MidlineLeft;
            ultHeader.text = "Ultimate";

            _ultimateTitleText = MakeText(_infoColumn, "UltimateTitle", serifFont, 26f, FontStyles.Bold, new Color(1f, 0.85f, 0.44f, 1f));
            _ultimateTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _ultimateTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _ultimateTitleText.rectTransform.offsetMin = new Vector2(188f, -174f);
            _ultimateTitleText.rectTransform.offsetMax = new Vector2(-96f, -134f);
            _ultimateTitleText.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform costPill = CreateRect(_infoColumn, "UltimateCostPill", layer);
            costPill.anchorMin = costPill.anchorMax = new Vector2(1f, 1f);
            costPill.pivot = new Vector2(1f, 1f);
            costPill.anchoredPosition = new Vector2(-12f, -139f);
            costPill.sizeDelta = new Vector2(78f, 30f);
            Image costBg = costPill.gameObject.AddComponent<Image>();
            costBg.sprite = roundRect;
            costBg.type = Image.Type.Sliced;
            costBg.color = new Color(0.86f, 0.64f, 0.18f, 0.92f);
            costBg.raycastTarget = false;
            _ultimateCostText = MakeText(costPill, "Cost", uiFont, 14f, FontStyles.Bold, new Color(0.14f, 0.08f, 0.02f, 1f));
            _ultimateCostText.alignment = TextAlignmentOptions.Center;

            _ultimateDescText = MakeText(_infoColumn, "UltimateDesc", serifFont, 22f, FontStyles.Normal, new Color(0.95f, 0.93f, 0.89f, 0.96f));
            _ultimateDescText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _ultimateDescText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _ultimateDescText.rectTransform.offsetMin = new Vector2(56f, -264f);
            _ultimateDescText.rectTransform.offsetMax = new Vector2(-16f, -176f);
            _ultimateDescText.alignment = TextAlignmentOptions.TopLeft;
            _ultimateDescText.textWrappingMode = TextWrappingModes.Normal;

            // "Innate Talent" Section Header + Title + Description
            TextMeshProUGUI innateHeader = MakeText(_infoColumn, "InnateHeader", serifFont, 31f, FontStyles.Bold, new Color(1f, 0.96f, 0.86f, 1f));
            innateHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            innateHeader.rectTransform.anchorMax = new Vector2(0f, 1f);
            innateHeader.rectTransform.pivot = new Vector2(0f, 1f);
            innateHeader.rectTransform.anchoredPosition = new Vector2(36f, -278f);
            innateHeader.rectTransform.sizeDelta = new Vector2(215f, 40f);
            innateHeader.alignment = TextAlignmentOptions.MidlineLeft;
            innateHeader.text = "Innate Talent";

            _innateTitleText = MakeText(_infoColumn, "InnateTitle", serifFont, 24f, FontStyles.Bold, new Color(0.58f, 0.95f, 0.66f, 1f));
            _innateTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _innateTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _innateTitleText.rectTransform.offsetMin = new Vector2(256f, -318f);
            _innateTitleText.rectTransform.offsetMax = new Vector2(-12f, -278f);
            _innateTitleText.alignment = TextAlignmentOptions.MidlineLeft;

            _innateDescText = MakeText(_infoColumn, "InnateDesc", serifFont, 22f, FontStyles.Normal, new Color(0.95f, 0.93f, 0.89f, 0.96f));
            _innateDescText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _innateDescText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _innateDescText.rectTransform.offsetMin = new Vector2(56f, -388f);
            _innateDescText.rectTransform.offsetMax = new Vector2(-16f, -320f);
            _innateDescText.alignment = TextAlignmentOptions.TopLeft;
            _innateDescText.textWrappingMode = TextWrappingModes.Normal;

            // Unlockable Future Talent Tree Strip (Innate + Future Locked Talents)
            TextMeshProUGUI treeLabel = MakeText(_infoColumn, "TalentTreeLabel", uiFont, 13.5f, FontStyles.Bold, new Color(0.88f, 0.78f, 0.56f, 0.92f));
            treeLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            treeLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            treeLabel.rectTransform.offsetMin = new Vector2(38f, -418f);
            treeLabel.rectTransform.offsetMax = new Vector2(-12f, -394f);
            treeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            treeLabel.text = "CHARACTER TALENTS  (CLICK OR HOVER TO INSPECT FUTURE UNLOCKS)";

            _talentNodesRow = CreateRect(_infoColumn, "TalentNodesRow", layer);
            _talentNodesRow.anchorMin = new Vector2(0f, 1f);
            _talentNodesRow.anchorMax = new Vector2(1f, 1f);
            _talentNodesRow.offsetMin = new Vector2(38f, -482f);
            _talentNodesRow.offsetMax = new Vector2(-16f, -422f);
            HorizontalLayoutGroup tHlg = _talentNodesRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tHlg.spacing = 12f;
            tHlg.childAlignment = TextAnchor.MiddleLeft;
            tHlg.childControlWidth = false;
            tHlg.childControlHeight = false;
            tHlg.childForceExpandWidth = false;
            tHlg.childForceExpandHeight = false;

            // Inspected Talent Preview Box
            RectTransform tPreview = CreateRect(_infoColumn, "TalentPreviewBox", layer);
            tPreview.anchorMin = new Vector2(0f, 1f);
            tPreview.anchorMax = new Vector2(1f, 1f);
            tPreview.offsetMin = new Vector2(38f, -578f);
            tPreview.offsetMax = new Vector2(-16f, -492f);
            Image tpBg = tPreview.gameObject.AddComponent<Image>();
            tpBg.sprite = roundRect;
            tpBg.type = Image.Type.Sliced;
            tpBg.color = new Color(0.11f, 0.10f, 0.12f, 0.80f);
            tpBg.raycastTarget = false;

            _talentDetailTitleText = MakeText(tPreview, "TalentTitle", uiFont, 15.5f, FontStyles.Bold, new Color(1f, 0.92f, 0.68f, 1f));
            _talentDetailTitleText.rectTransform.offsetMin = new Vector2(14f, 48f);
            _talentDetailTitleText.rectTransform.offsetMax = new Vector2(-14f, -8f);
            _talentDetailTitleText.alignment = TextAlignmentOptions.MidlineLeft;

            _talentDetailDescText = MakeText(tPreview, "TalentDesc", uiFont, 14.5f, FontStyles.Normal, new Color(0.90f, 0.88f, 0.84f, 0.94f));
            _talentDetailDescText.rectTransform.offsetMin = new Vector2(14f, 8f);
            _talentDetailDescText.rectTransform.offsetMax = new Vector2(-14f, -38f);
            _talentDetailDescText.alignment = TextAlignmentOptions.TopLeft;
            _talentDetailDescText.textWrappingMode = TextWrappingModes.Normal;

            // Top-Left "< BACK" pill (compact so it never touches PARTY SETUP) & Action button cleanly below Talent Preview
            RectTransform backBtnRt = CreateRect(_detailOverlay, "BackToPartyButton", layer);
            backBtnRt.anchorMin = backBtnRt.anchorMax = new Vector2(0f, 1f);
            backBtnRt.pivot = new Vector2(0f, 1f);
            backBtnRt.anchoredPosition = new Vector2(26f, -22f);
            backBtnRt.sizeDelta = new Vector2(174f, 46f);
            Image backBg = backBtnRt.gameObject.AddComponent<Image>();
            backBg.sprite = roundRect;
            backBg.type = Image.Type.Sliced;
            backBg.color = new Color(0.14f, 0.12f, 0.11f, 0.90f);
            Button backBtn = backBtnRt.gameObject.AddComponent<Button>();
            backBtn.onClick.AddListener(CloseDetailOverlay);
            TextMeshProUGUI backTxt = MakeText(backBtnRt, "Label", uiFont, 16f, FontStyles.Bold, new Color(0.98f, 0.93f, 0.82f, 1f));
            backTxt.alignment = TextAlignmentOptions.Center;
            backTxt.text = "<  PARTY";

            RectTransform assignRt = CreateRect(_infoColumn, "AssignSlotButton", layer);
            assignRt.anchorMin = assignRt.anchorMax = new Vector2(0f, 1f);
            assignRt.pivot = new Vector2(0f, 1f);
            assignRt.anchoredPosition = new Vector2(38f, -592f);
            assignRt.sizeDelta = new Vector2(336f, 52f);
            _assignButtonBg = assignRt.gameObject.AddComponent<Image>();
            _assignButtonBg.sprite = roundRect;
            _assignButtonBg.type = Image.Type.Sliced;
            _assignButtonBg.color = new Color(0.28f, 0.68f, 0.42f, 0.98f);
            _assignButton = assignRt.gameObject.AddComponent<Button>();
            _assignButton.onClick.AddListener(AssignInspectedHeroToActiveSlot);
            _assignButtonLabel = MakeText(assignRt, "Label", uiFont, 17f, FontStyles.Bold, Color.white);
            _assignButtonLabel.alignment = TextAlignmentOptions.Center;
            _assignButtonLabel.outlineWidth = 0.22f;
            _assignButtonLabel.outlineColor = new Color(0f, 0f, 0f, 0.65f);

            // Bottom-Center — Horizontal Row of Square Character Roster Icons with 1/2/3 Badges (Image 2!)
            _rosterStrip = CreateRect(_detailOverlay, "RosterIconStrip", layer);
            _rosterStrip.anchorMin = _rosterStrip.anchorMax = new Vector2(0.5f, 0f);
            _rosterStrip.pivot = new Vector2(0.5f, 0f);
            _rosterStrip.anchoredPosition = new Vector2(10f, 52f);
            _rosterStrip.sizeDelta = new Vector2(860f, 148f);

            HorizontalLayoutGroup rHlg = _rosterStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
            rHlg.spacing = 36f;
            rHlg.childAlignment = TextAnchor.MiddleCenter;
            rHlg.childControlWidth = false;
            rHlg.childControlHeight = false;
            rHlg.childForceExpandWidth = false;
            rHlg.childForceExpandHeight = false;

            BuildRosterIcons(layer, uiFont, roundRect, roundOutline);

            // Bottom-Right — Live Frame-Animated Character Sprite Showcase (exact match to Image 2!)
            _spriteStage = CreateRect(_detailOverlay, "AnimatedSpriteShowcase", layer);
            _spriteStage.anchorMin = _spriteStage.anchorMax = new Vector2(1f, 0f);
            _spriteStage.pivot = new Vector2(0.5f, 0f);
            _spriteStage.anchoredPosition = new Vector2(-180f, 34f);
            _spriteStage.sizeDelta = new Vector2(284f, 326f);

            // Soft ground shadow beneath standing chibi sprite
            RectTransform shadowRt = CreateRect(_spriteStage, "FloorShadow", layer);
            shadowRt.anchorMin = shadowRt.anchorMax = new Vector2(0.5f, 0f);
            shadowRt.pivot = new Vector2(0.5f, 0.5f);
            shadowRt.anchoredPosition = new Vector2(0f, 36f);
            shadowRt.sizeDelta = new Vector2(170f, 36f);
            _spriteFloorShadow = shadowRt.gameObject.AddComponent<Image>();
            _spriteFloorShadow.sprite = PartySetupArtFactory.GetSoftRadialGlow();
            _spriteFloorShadow.color = new Color(0f, 0f, 0f, 0.52f);
            _spriteFloorShadow.raycastTarget = false;

            RectTransform spriteRt = CreateRect(_spriteStage, "ChibiSprite", layer);
            spriteRt.anchorMin = spriteRt.anchorMax = new Vector2(0.5f, 0f);
            spriteRt.pivot = new Vector2(0.5f, 0f);
            _spriteBasePos = new Vector2(0f, 28f);
            spriteRt.anchoredPosition = _spriteBasePos;
            spriteRt.sizeDelta = new Vector2(276f, 290f);
            _animatedSpriteImage = spriteRt.gameObject.AddComponent<Image>();
            _animatedSpriteImage.preserveAspect = true;

            // Clicking the standing sprite plays its real Attack/Heal flourish!
            Button chibiBtn = _spriteStage.gameObject.AddComponent<Button>();
            chibiBtn.transition = Selectable.Transition.None;
            chibiBtn.onClick.AddListener(TriggerPreviewActionAnimation);

            _spriteHintText = MakeText(_spriteStage, "SpriteHint", uiFont, 12f, FontStyles.Bold, new Color(1f, 0.92f, 0.68f, 0.78f));
            _spriteHintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _spriteHintText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _spriteHintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _spriteHintText.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            _spriteHintText.rectTransform.sizeDelta = new Vector2(0f, 20f);
            _spriteHintText.alignment = TextAlignmentOptions.Center;
            _spriteHintText.text = "CLICK TO PREVIEW ACTION";

            _detailOverlay.gameObject.SetActive(false);
        }

        private void BuildRosterIcons(int layer, TMP_FontAsset uiFont, Sprite roundRect, Sprite roundOutline)
        {
            _rosterIcons.Clear();
            Sprite padlock = PartySetupArtFactory.GetPadlockIcon();
            int totalSlots = Mathf.Max(MinimumRosterIconSlots, playableRoster.Count + futureLockedRoster.Count);

            for (int i = 0; i < totalSlots; i++)
            {
                bool isPlayable = i < playableRoster.Count && playableRoster[i] != null;
                HeroData hero = isPlayable ? playableRoster[i] : null;
                int teaserIdx = Mathf.Clamp(i - playableRoster.Count, 0, Mathf.Max(0, futureLockedRoster.Count - 1));
                LockedFutureHeroTeaser teaser = !isPlayable && futureLockedRoster.Count > 0 ? futureLockedRoster[teaserIdx] : null;

                RectTransform iconRt = CreateRect(_rosterStrip, $"RosterIcon_{i}", layer);
                iconRt.sizeDelta = new Vector2(126f, 126f);

                Image bg = iconRt.gameObject.AddComponent<Image>();
                bg.sprite = roundRect;
                bg.type = Image.Type.Sliced;
                bg.color = isPlayable
                    ? new Color(0.95f, 0.92f, 0.86f, 1f)
                    : new Color(0.18f, 0.17f, 0.21f, 0.92f);

                RectTransform maskRt = CreateRect(iconRt, "Mask", layer);
                Stretch(maskRt, 5f);
                maskRt.gameObject.AddComponent<RectMask2D>();

                RectTransform pRt = CreateRect(maskRt, "Portrait", layer);
                pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(116f, 116f);
                Image pImg = pRt.gameObject.AddComponent<Image>();
                pImg.preserveAspect = true;
                pImg.raycastTarget = false;

                if (isPlayable)
                {
                    pImg.sprite = hero.EffectiveRosterIcon;
                    ApplyPortraitCrop(pRt, hero.RosterIconOffset, hero.RosterIconScale);
                }
                else
                {
                    pImg.sprite = PartySetupArtFactory.GetSoftRadialGlow();
                    Color tc = teaser != null ? teaser.TintColor : new Color(0.45f, 0.48f, 0.62f, 1f);
                    pImg.color = new Color(tc.r, tc.g, tc.b, 0.35f);
                }

                // Padlock badge for locked future heroes
                RectTransform lockOverlay = CreateRect(iconRt, "LockOverlay", layer);
                Stretch(lockOverlay);
                RectTransform lockIconRt = CreateRect(lockOverlay, "Padlock", layer);
                lockIconRt.anchorMin = lockIconRt.anchorMax = new Vector2(0.5f, 0.56f);
                lockIconRt.sizeDelta = new Vector2(44f, 44f);
                Image lockImg = lockIconRt.gameObject.AddComponent<Image>();
                lockImg.sprite = padlock;
                lockImg.color = new Color(0.88f, 0.82f, 0.68f, 0.78f);
                lockImg.raycastTarget = false;

                TextMeshProUGUI soonLbl = MakeText(lockOverlay, "Soon", uiFont, 12.5f, FontStyles.Bold, new Color(0.85f, 0.82f, 0.76f, 0.78f));
                soonLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
                soonLbl.rectTransform.anchorMax = new Vector2(1f, 0f);
                soonLbl.rectTransform.pivot = new Vector2(0.5f, 0f);
                soonLbl.rectTransform.anchoredPosition = new Vector2(0f, 10f);
                soonLbl.rectTransform.sizeDelta = new Vector2(0f, 22f);
                soonLbl.alignment = TextAlignmentOptions.Center;
                soonLbl.text = teaser != null ? teaser.DisplayName.ToUpperInvariant() : "LOCKED";
                lockOverlay.gameObject.SetActive(!isPlayable);

                // Selection Golden Highlight Frame
                RectTransform outRt = CreateRect(iconRt, "SelectionOutline", layer);
                Stretch(outRt, -3f);
                Image outImg = outRt.gameObject.AddComponent<Image>();
                outImg.sprite = roundOutline;
                outImg.type = Image.Type.Sliced;
                outImg.color = new Color(1f, 0.88f, 0.42f, 1f);
                outImg.raycastTarget = false;
                outImg.enabled = false;

                // Bold Outlined Party Slot Numeral ("1", "2", "3") tucked in bottom-left corner (exact match to Image 2!)
                RectTransform badgeRt = CreateRect(iconRt, "PartySlotBadge", layer);
                badgeRt.anchorMin = badgeRt.anchorMax = Vector2.zero;
                badgeRt.pivot = new Vector2(0.5f, 0.5f);
                badgeRt.anchoredPosition = new Vector2(8f, 12f);
                badgeRt.sizeDelta = new Vector2(46f, 46f);
                TextMeshProUGUI badgeTxt = MakeText(badgeRt, "Num", uiFont, 34f, FontStyles.Bold, Color.white);
                badgeTxt.alignment = TextAlignmentOptions.Center;
                badgeTxt.outlineWidth = 0.36f;
                badgeTxt.outlineColor = Color.black;
                badgeRt.gameObject.SetActive(false);

                Button btn = iconRt.gameObject.AddComponent<Button>();
                int capturedIdx = i;
                btn.onClick.AddListener(() => SelectRosterIndex(capturedIdx, playVoice: true, triggerActionAnim: true));

                _rosterIcons.Add(new RosterIconWidgets
                {
                    Root = iconRt,
                    Bg = bg,
                    Portrait = pImg,
                    PortraitRect = pRt,
                    SelectionOutline = outImg,
                    LockOverlay = lockOverlay.gameObject,
                    SlotBadgeText = badgeTxt,
                    SlotBadgeRoot = badgeRt.gameObject,
                    IsLockedFuture = !isPlayable,
                    RosterIndex = i
                });
            }
        }

        private static void ApplyPortraitCrop(RectTransform rt, Vector2 offset, float scale)
        {
            if (rt == null) return;
            rt.anchoredPosition = offset;
            rt.localScale = Vector3.one * Mathf.Max(0.25f, scale);
        }

        private TMP_FontAsset ResolveSerifFont() =>
            serifFontAsset != null ? serifFontAsset : (uiFontAsset != null ? uiFontAsset : TMP_Settings.defaultFontAsset);

        private TMP_FontAsset ResolveUiFont() =>
            uiFontAsset != null ? uiFontAsset : (serifFontAsset != null ? serifFontAsset : TMP_Settings.defaultFontAsset);

        private static RectTransform CreateRect(Transform p, string n, int l)
        {
            GameObject g = new(n, typeof(RectTransform));
            g.layer = l;
            RectTransform rt = (RectTransform)g.transform;
            rt.SetParent(p, false);
            return rt;
        }

        private static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private static TextMeshProUGUI MakeText(RectTransform p, string n, TMP_FontAsset f, float sz, FontStyles st, Color c)
        {
            RectTransform rt = CreateRect(p, n, p.gameObject.layer);
            Stretch(rt);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (f != null) t.font = f;
            t.fontSize = sz;
            t.fontStyle = st;
            t.color = c;
            t.raycastTarget = false;
            return t;
        }

        #endregion
    }
}

