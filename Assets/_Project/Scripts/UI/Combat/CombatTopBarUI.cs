using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Slay-the-Spire style Combat Top Bar matching the Figma layout:
    /// - Shared Party HP bar (with Shield & Low-Will penalty visualization)
    /// - 3 Character Portrait Cards with individual Will sliders (>=75% positive, <25% Fear)
    ///   and per-character Buff/Debuff icon grids directly below each portrait
    /// - Center Potions/Relics tray, Crystal & Gold counters, Map scroll button, and Menu button.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatTopBarUI : MonoBehaviour
    {
        [Serializable]
        public class CharacterWillSlot
        {
            public GameObject Root;
            public Image PortraitImage;
            public Image PortraitBorder;
            public Slider WillSlider;
            public Image WillFillImage;
            public TMP_Text WillStateBadge;
            public List<StatusCell> StatusCells = new();
        }

        [Serializable]
        public class StatusCell
        {
            public Image Background;
            public TMP_Text Label;
        }

        private readonly struct ActiveBadge
        {
            public readonly string ShortText;
            public readonly Color Color;

            public ActiveBadge(string shortText, Color color)
            {
                ShortText = shortText;
                Color = color;
            }
        }

        [Header("Core References")]
        [SerializeField] private HeroView _heroView;
        [SerializeField] private CombatFeedbackBannerUI _feedbackBanner;

        [Header("Top Bar HP & Currencies")]
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Slider _shieldSlider;
        [SerializeField] private Slider _blockedSlider;
        [SerializeField] private TMP_Text _crystalText;
        [SerializeField] private TMP_Text _goldText;

        [Header("3 Character Portrait / Will / Buff Slots")]
        [SerializeField] private List<CharacterWillSlot> _characterSlots = new();

        [Header("Will Slider Colors")]
        [SerializeField] private Color _highWillFillColor = new(0.14f, 0.68f, 1f, 1f);
        [SerializeField] private Color _neutralWillFillColor = new(0.15f, 0.45f, 0.92f, 1f);
        [SerializeField] private Color _lowWillFillColor = new(0.66f, 0.11f, 0.15f, 1f);
        [SerializeField] private Color _normalBorderColor = new(0.78f, 0.65f, 0.36f, 1f);
        [SerializeField] private Color _highWillBorderColor = new(0.32f, 0.86f, 1f, 1f);
        [SerializeField] private Color _lowWillBorderColor = new(1f, 0.28f, 0.32f, 1f);

        [Header("Status Cell Styling")]
        [SerializeField] private Color _emptyCellColor = new(0.84f, 0.86f, 0.88f, 0.45f);
        [SerializeField] private Color _buffCellColor = new(0.18f, 0.66f, 0.42f, 0.95f);
        [SerializeField] private Color _debuffCellColor = new(0.76f, 0.22f, 0.26f, 0.95f);
        [SerializeField] private Color _highWillCellColor = new(0.14f, 0.62f, 0.94f, 0.96f);

        private RunManager _subscribedRunManager;
        private BuffSystem _subscribedBuffSystem;
        private DamageOverTimeSystem _subscribedDotSystem;

        private void Awake()
        {
            ResolveHeroView();
        }

        private void OnEnable()
        {
            ResolveHeroView();
            SubscribeAll();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void Update()
        {
            if (_subscribedRunManager != RunManager.Instance ||
                _subscribedBuffSystem != BuffSystem.Instance ||
                _subscribedDotSystem != DamageOverTimeSystem.Instance)
            {
                UnsubscribeAll();
                SubscribeAll();
                RefreshAll();
            }
        }

        public void OnMapButtonClicked()
        {
            if (_feedbackBanner != null)
            {
                _feedbackBanner.ShowToast("Defeat enemies to advance on the Map.");
            }
        }

        public void RefreshAll()
        {
            RefreshHealth();
            RefreshCurrencies();
            RefreshCharacterWillSlots();
            RefreshStatusGrids();
        }

        private void RefreshHealth()
        {
            if (_heroView != null && _heroView.MaxHealth > 0)
            {
                int cur = _heroView.CurrentHealth;
                int max = _heroView.EffectiveMaxHealth;
                int shield = _heroView.CurrentShield;

                if (_hpText != null)
                {
                    _hpText.text = shield > 0
                        ? $"{cur}  /  {max}   <color=#7CE8FF>(+{shield})</color>"
                        : $"{cur}  /  {max}";
                }
                return;
            }

            if (RunManager.Instance != null && RunManager.Instance.HasHeroState)
            {
                if (_hpText != null)
                    _hpText.text = $"{RunManager.Instance.HeroCurrentHealth}  /  {RunManager.Instance.HeroMaxHealth}";
                if (_hpSlider != null)
                    _hpSlider.value = (float)RunManager.Instance.HeroCurrentHealth / Mathf.Max(1, RunManager.Instance.HeroMaxHealth);
            }
        }

        private void RefreshCurrencies()
        {
            int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 150;
            int crystals = RunManager.Instance != null ? RunManager.Instance.Crystals : 100;

            if (_goldText != null) _goldText.text = gold.ToString();
            if (_crystalText != null) _crystalText.text = crystals.ToString();
        }

        private void RefreshCharacterWillSlots()
        {
            if (_characterSlots == null || _characterSlots.Count == 0) return;

            IReadOnlyList<RunHeroStressState> states = _heroView != null ? _heroView.StressStates : null;

            for (int i = 0; i < _characterSlots.Count; i++)
            {
                CharacterWillSlot slot = _characterSlots[i];
                if (slot == null) continue;

                // Map per-character Will when multi-hero states exist; otherwise share the active Hero's Will cleanly across party portraits.
                RunHeroStressState state = null;
                if (states != null && states.Count > 0)
                {
                    state = i < states.Count ? states[i] : states[0];
                }

                float willNorm = state != null
                    ? state.WillNormalized
                    : (_heroView != null ? _heroView.WillNormalized : 0.5f);
                bool isHighWill = state != null ? state.IsHighWill : (_heroView != null && _heroView.IsHighWill);
                bool isLowWill = state != null ? state.IsLowWill : (_heroView != null && _heroView.IsLowWill);

                if (slot.WillSlider != null)
                {
                    slot.WillSlider.DOKill();
                    if (Application.isPlaying) slot.WillSlider.DOValue(willNorm, 0.22f).SetUpdate(true);
                    else slot.WillSlider.value = willNorm;
                }

                Color fillColor = isLowWill ? _lowWillFillColor : (isHighWill ? _highWillFillColor : _neutralWillFillColor);
                Color borderColor = isLowWill ? _lowWillBorderColor : (isHighWill ? _highWillBorderColor : _normalBorderColor);

                if (slot.WillFillImage != null) slot.WillFillImage.color = fillColor;
                if (slot.PortraitBorder != null) slot.PortraitBorder.color = borderColor;

                if (slot.WillStateBadge != null)
                {
                    int pct = Mathf.RoundToInt(willNorm * 100f);
                    if (isHighWill)
                        slot.WillStateBadge.text = $"<color=#64E8FF>{pct}% +</color>";
                    else if (isLowWill)
                        slot.WillStateBadge.text = $"<color=#FF6B6B>{pct}% !</color>";
                    else
                        slot.WillStateBadge.text = $"{pct}%";
                }
            }
        }

        private void RefreshStatusGrids()
        {
            if (_characterSlots == null || _characterSlots.Count == 0) return;

            List<ActiveBadge> sharedBadges = BuildHeroStatusBadges();

            for (int i = 0; i < _characterSlots.Count; i++)
            {
                CharacterWillSlot slot = _characterSlots[i];
                if (slot == null || slot.StatusCells == null) continue;

                // Show active party buffs/debuffs on the primary character slot (and Will state on all slots when multi-hero states diverge)
                List<ActiveBadge> slotBadges = i == 0 ? sharedBadges : BuildSecondarySlotBadges(i);

                for (int c = 0; c < slot.StatusCells.Count; c++)
                {
                    StatusCell cell = slot.StatusCells[c];
                    if (cell == null || cell.Background == null) continue;

                    if (c < slotBadges.Count)
                    {
                        cell.Background.color = slotBadges[c].Color;
                        if (cell.Label != null)
                        {
                            cell.Label.gameObject.SetActive(true);
                            cell.Label.text = slotBadges[c].ShortText;
                        }
                    }
                    else
                    {
                        cell.Background.color = _emptyCellColor;
                        if (cell.Label != null)
                        {
                            cell.Label.text = string.Empty;
                            cell.Label.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        private List<ActiveBadge> BuildHeroStatusBadges()
        {
            List<ActiveBadge> badges = new();
            if (_heroView == null) return badges;

            if (_heroView.IsHighWill)
                badges.Add(new ActiveBadge("W+", _highWillCellColor));
            else if (_heroView.IsLowWill)
                badges.Add(new ActiveBadge("F!", _debuffCellColor));

            if (_heroView.IsStunned)
                badges.Add(new ActiveBadge($"ST{_heroView.StunDuration}", _debuffCellColor));

            if (_heroView.IsTaunted)
                badges.Add(new ActiveBadge($"T{_heroView.TauntDuration}", _buffCellColor));

            if (BuffSystem.Instance != null)
            {
                Dictionary<BuffType, int> grouped = new();
                foreach (BuffData buff in BuffSystem.Instance.GetBuffsFor(_heroView))
                {
                    if (!grouped.ContainsKey(buff.Type)) grouped[buff.Type] = 0;
                    grouped[buff.Type] += Mathf.Max(1, buff.Value);
                }

                foreach (KeyValuePair<BuffType, int> kv in grouped)
                {
                    string code = GetBuffShortCode(kv.Key, kv.Value);
                    Color color = BuffSystem.IsDebuff(kv.Key) ? _debuffCellColor : _buffCellColor;
                    badges.Add(new ActiveBadge(code, color));
                }
            }

            if (DamageOverTimeSystem.Instance != null)
            {
                int dotDamage = 0;
                foreach (DamageOverTimeData dot in DamageOverTimeSystem.Instance.GetDoTsFor(_heroView))
                    dotDamage += dot.DamagePerTurn;
                if (dotDamage > 0)
                    badges.Add(new ActiveBadge($"D{dotDamage}", _debuffCellColor));
            }

            return badges;
        }

        private List<ActiveBadge> BuildSecondarySlotBadges(int slotIndex)
        {
            List<ActiveBadge> badges = new();
            if (_heroView == null || _heroView.StressStates == null || _heroView.StressStates.Count == 0)
                return badges;

            RunHeroStressState state = slotIndex < _heroView.StressStates.Count
                ? _heroView.StressStates[slotIndex]
                : _heroView.StressStates[0];

            if (state.IsHighWill) badges.Add(new ActiveBadge("W+", _highWillCellColor));
            else if (state.IsLowWill) badges.Add(new ActiveBadge("F!", _debuffCellColor));
            return badges;
        }

        private static string GetBuffShortCode(BuffType type, int value)
        {
            return type switch
            {
                BuffType.Strength => $"S{value}",
                BuffType.Dexterity => $"DX{value}",
                BuffType.Weak => $"WK",
                BuffType.Vulnerable => $"VN",
                BuffType.Frail => $"FR",
                BuffType.Poison => $"P{value}",
                BuffType.Thorns => $"TH{value}",
                BuffType.Artifact => $"A{value}",
                BuffType.Barricade => "BR",
                BuffType.Metallicize => $"M{value}",
                BuffType.PlatedArmor => $"PA{value}",
                BuffType.Intangible => "IN",
                BuffType.FearImmunity => "WI",
                BuffType.Redirect => "RD",
                BuffType.DamageReduction => $"DR{value}",
                BuffType.DamageReductionPercentage => $"DR%",
                _ => "?"
            };
        }

        private void ResolveHeroView()
        {
            if (_heroView == null && HeroSystem.Instance != null) _heroView = HeroSystem.Instance.HeroView;
            if (_heroView == null) _heroView = FindFirstObjectByType<HeroView>();
        }

        private void SubscribeAll()
        {
            if (_heroView != null)
            {
                _heroView.HealthChanged -= RefreshHealth;
                _heroView.HealthChanged += RefreshHealth;
                _heroView.StressStateChanged -= RefreshWillAndStatuses;
                _heroView.StressStateChanged += RefreshWillAndStatuses;
                _heroView.StatusEffectsChanged -= RefreshStatusGrids;
                _heroView.StatusEffectsChanged += RefreshStatusGrids;
            }

            _subscribedRunManager = RunManager.Instance;
            if (_subscribedRunManager != null)
            {
                _subscribedRunManager.RunStateChanged -= RefreshCurrencies;
                _subscribedRunManager.RunStateChanged += RefreshCurrencies;
            }

            _subscribedBuffSystem = BuffSystem.Instance;
            if (_subscribedBuffSystem != null)
            {
                _subscribedBuffSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedBuffSystem.StatusEffectsChanged += RefreshStatusGrids;
            }

            _subscribedDotSystem = DamageOverTimeSystem.Instance;
            if (_subscribedDotSystem != null)
            {
                _subscribedDotSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedDotSystem.StatusEffectsChanged += RefreshStatusGrids;
            }
        }

        private void UnsubscribeAll()
        {
            if (_heroView != null)
            {
                _heroView.HealthChanged -= RefreshHealth;
                _heroView.StressStateChanged -= RefreshWillAndStatuses;
                _heroView.StatusEffectsChanged -= RefreshStatusGrids;
            }
            if (_subscribedRunManager != null)
            {
                _subscribedRunManager.RunStateChanged -= RefreshCurrencies;
                _subscribedRunManager = null;
            }
            if (_subscribedBuffSystem != null)
            {
                _subscribedBuffSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedBuffSystem = null;
            }
            if (_subscribedDotSystem != null)
            {
                _subscribedDotSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedDotSystem = null;
            }
        }

        private void RefreshWillAndStatuses()
        {
            RefreshCharacterWillSlots();
            RefreshStatusGrids();
        }
    }
}

