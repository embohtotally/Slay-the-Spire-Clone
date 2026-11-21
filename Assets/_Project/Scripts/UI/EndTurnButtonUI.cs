using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Tutorials;
using PlushieOdyssey.UI.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI
{
    public class EndTurnButtonUI : MonoBehaviour
    {
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID endTurnSfx = global::PlushieOdyssey.Audio.SfxID.EndTurn;
        [Header("Smart Glow (Pulses when no playable cards remain)")]
        [SerializeField] private bool enableSmartGlow = true;
        [SerializeField] private Color smartGlowColor = new(0.36f, 0.92f, 1f, 0.88f);
        [SerializeField] private Color buttonHighlightTint = new(1f, 0.95f, 0.76f, 1f);

        private Image _buttonImage;
        private Color _baseButtonColor = Color.white;
        private bool _capturedBaseColor;
        private Image _glowHalo;
        private bool _isGlowing;
        private Vector3 _baseScale = Vector3.one;
        private CardSystem _subCardSystem;
        private ManaSystem _subManaSystem;
        private HeroView _subHero;
        private ActionSystem _subActionSystem;

        public bool IsSmartGlowActive => _isGlowing;

        private void Awake()
        {
            _buttonImage = GetComponent<Image>();
            if (_buttonImage != null)
            {
                _baseButtonColor = _buttonImage.color;
                _capturedBaseColor = true;
            }
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            SubscribeAll();
            RefreshSmartGlow();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
            SetGlowState(false);
        }

        private void Update()
        {
            if (_subCardSystem != CardSystem.Instance ||
                _subManaSystem != ManaSystem.Instance ||
                _subActionSystem != ActionSystem.Instance ||
                (_subHero == null && HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null))
            {
                UnsubscribeAll();
                SubscribeAll();
                RefreshSmartGlow();
            }

            if (_isGlowing && _glowHalo != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f);
                Color c = smartGlowColor;
                c.a = Mathf.Lerp(0.32f, 0.88f, pulse);
                _glowHalo.color = c;
                float s = Mathf.Lerp(1.03f, 1.13f, pulse);
                _glowHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        public void OnClick()
        {
            if (CombatCardPickerUI.IsAnyOpen) return;
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanEndTurn()) return;
            if (ActionSystem.Instance == null) return;
            if (ActionSystem.Instance.IsEnemyTurnQueuedOrActive) return;
            if (EnemySystem.Instance != null && EnemySystem.Instance.IsEnemyTurnActive) return;

            if (endTurnSfx != global::PlushieOdyssey.Audio.SfxID.None)
                global::PlushieOdyssey.Audio.Tune.SFX(endTurnSfx);

            SetGlowState(false);

            // Queue FIFO behind any in-flight card actions so End Turn is NEVER ignored!
            ActionSystem.Instance.Enqueue(new EnemyTurnGA());
        }

        public void RefreshSmartGlow()
        {
            if (!enableSmartGlow)
            {
                SetGlowState(false);
                return;
            }

            bool locked =
                CombatCardPickerUI.IsAnyOpen ||
                (ActionSystem.Instance != null && ActionSystem.Instance.IsEnemyTurnQueuedOrActive) ||
                (EnemySystem.Instance != null && EnemySystem.Instance.IsEnemyTurnActive);

            bool shouldGlow = !locked &&
                              CardSystem.Instance != null &&
                              (CardSystem.Instance.Hand.Count > 0 || (ManaSystem.Instance != null && ManaSystem.Instance.AvailableMana <= 0)) &&
                              !CardSystem.Instance.HasPlayableCards();

            SetGlowState(shouldGlow);
        }

        private void SetGlowState(bool active)
        {
            if (_isGlowing == active) return;
            _isGlowing = active;

            if (active)
            {
                EnsureGlowHalo();
                if (_glowHalo != null) _glowHalo.gameObject.SetActive(true);
                if (_buttonImage != null && _capturedBaseColor)
                {
                    _buttonImage.DOKill();
                    _buttonImage.DOColor(Color.Lerp(_baseButtonColor, buttonHighlightTint, 0.65f), 0.2f).SetUpdate(true);
                }
                transform.DOKill();
                transform.DOScale(_baseScale * 1.045f, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            else
            {
                if (_glowHalo != null) _glowHalo.gameObject.SetActive(false);
                if (_buttonImage != null && _capturedBaseColor)
                {
                    _buttonImage.DOKill();
                    _buttonImage.color = _baseButtonColor;
                }
                transform.DOKill();
                transform.localScale = _baseScale;
            }
        }

        private void EnsureGlowHalo()
        {
            if (_glowHalo != null) return;
            Transform existing = transform.Find("SmartGlowHalo");
            if (existing != null)
            {
                _glowHalo = existing.GetComponent<Image>();
                return;
            }

            GameObject go = new("SmartGlowHalo", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-14f, -14f);
            rt.offsetMax = new Vector2(14f, 14f);

            _glowHalo = go.GetComponent<Image>();
            _glowHalo.sprite = FindGlowSprite();
            _glowHalo.color = smartGlowColor;
            _glowHalo.raycastTarget = false;
        }

        private static Sprite s_fallbackGlow;
        private static Sprite FindGlowSprite()
        {
            Sprite[] loaded = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null && loaded[i].name == "Merchant_SoftGlow") return loaded[i];
            }
            if (s_fallbackGlow != null) return s_fallbackGlow;
            const int size = 64;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
            }
            tex.Apply();
            s_fallbackGlow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_fallbackGlow;
        }

        private void SubscribeAll()
        {
            _subCardSystem = CardSystem.Instance;
            _subManaSystem = ManaSystem.Instance;
            _subActionSystem = ActionSystem.Instance;
            _subHero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;

            if (_subCardSystem != null) _subCardSystem.PilesChanged += RefreshSmartGlow;
            if (_subManaSystem != null) _subManaSystem.ManaChanged += OnManaChanged;
            if (_subActionSystem != null)
            {
                _subActionSystem.TurnStateChanged += RefreshSmartGlow;
                _subActionSystem.BusyChanged += OnBusyChanged;
            }
            if (_subHero != null) _subHero.StressStateChanged += RefreshSmartGlow;
        }

        private void UnsubscribeAll()
        {
            if (_subCardSystem != null) _subCardSystem.PilesChanged -= RefreshSmartGlow;
            if (_subManaSystem != null) _subManaSystem.ManaChanged -= OnManaChanged;
            if (_subActionSystem != null)
            {
                _subActionSystem.TurnStateChanged -= RefreshSmartGlow;
                _subActionSystem.BusyChanged -= OnBusyChanged;
            }
            if (_subHero != null) _subHero.StressStateChanged -= RefreshSmartGlow;
            _subCardSystem = null;
            _subManaSystem = null;
            _subActionSystem = null;
            _subHero = null;
        }

        private void OnManaChanged(int _, int __) => RefreshSmartGlow();
        private void OnBusyChanged(bool _) => RefreshSmartGlow();
    }
}
