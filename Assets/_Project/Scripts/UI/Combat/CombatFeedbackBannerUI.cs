using DG.Tweening;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Displays Slay-the-Spire style Turn Phase Banners ("PLAYER TURN" / "ENEMY TURN")
    /// and crisp Play-Rejection / Hand-Full toast prompts during combat.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatFeedbackBannerUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CombatHandController _handController;
        [SerializeField] private TMP_FontAsset _font;

        [Header("Colors")]
        [SerializeField] private Color _playerTurnAccent = new(0.36f, 0.78f, 1f, 1f);
        [SerializeField] private Color _enemyTurnAccent = new(1f, 0.36f, 0.34f, 1f);
        [SerializeField] private Color _toastAccent = new(1f, 0.72f, 0.28f, 1f);

        private static Sprite _solidSprite;

        private RectTransform _bannerRoot;
        private CanvasGroup _bannerGroup;
        private Image _bannerTopLine;
        private Image _bannerBottomLine;
        private TextMeshProUGUI _bannerText;
        private Sequence _bannerSequence;

        private RectTransform _toastRoot;
        private CanvasGroup _toastGroup;
        private TextMeshProUGUI _toastText;
        private Sequence _toastSequence;

        private int _turnNumber = 1;
        private CardSystem _subscribedCardSystem;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_handController == null) _handController = FindFirstObjectByType<CombatHandController>();
            BuildUiIfNeeded();
        }

        private void OnEnable()
        {
            ActionSystem.SubscribeReaction<CombatStartedGA>(OnCombatStartedPost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);

            if (_handController != null)
            {
                _handController.PlayRejected += OnPlayRejected;
            }

            TrySubscribeCardSystem();
        }

        private void OnDisable()
        {
            ActionSystem.UnsubscribeReaction<CombatStartedGA>(OnCombatStartedPost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);

            if (_handController != null)
            {
                _handController.PlayRejected -= OnPlayRejected;
            }

            if (_subscribedCardSystem != null)
            {
                _subscribedCardSystem.HandFullRejected -= OnHandFullRejected;
                _subscribedCardSystem = null;
            }

            _bannerSequence?.Kill();
            _toastSequence?.Kill();
        }

        private void Update()
        {
            if (_subscribedCardSystem == null && CardSystem.Instance != null)
            {
                TrySubscribeCardSystem();
            }
        }

        private void TrySubscribeCardSystem()
        {
            if (CardSystem.Instance == null || _subscribedCardSystem == CardSystem.Instance) return;
            _subscribedCardSystem = CardSystem.Instance;
            _subscribedCardSystem.HandFullRejected += OnHandFullRejected;
        }

        private void OnCombatStartedPost(CombatStartedGA _)
        {
            _turnNumber = 1;
            ShowBanner($"PLAYER TURN  •  TURN {_turnNumber}", _playerTurnAccent);
        }

        private void OnEnemyTurnPre(EnemyTurnGA _)
        {
            ShowBanner("ENEMY TURN", _enemyTurnAccent);
        }

        private void OnEnemyTurnPost(EnemyTurnGA _)
        {
            if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null && HeroSystem.Instance.HeroView.IsStunned)
            {
                ShowToast("Hero is Stunned! Turn skipped.");
                return;
            }
            _turnNumber++;
            ShowBanner($"PLAYER TURN  •  TURN {_turnNumber}", _playerTurnAccent);
        }

        private void OnPlayRejected(CardView view, CardPlayFailure failure)
        {
            switch (failure)
            {
                case CardPlayFailure.NotEnoughMana:
                    ShowToast("Not enough Mana!");
                    break;
                case CardPlayFailure.InvalidTarget:
                    ShowToast("Select a valid target!");
                    break;
                case CardPlayFailure.Blocked:
                    if (view != null && view.Card != null && view.Card.HasKeyword(Data.CardKeywords.Unplayable))
                        ShowToast("This card is Unplayable.");
                    else
                        ShowToast("- Low Will (<25%): Attack cards are blocked!");
                    break;
                case CardPlayFailure.TutorialBlocked:
                    ShowToast("Follow the tutorial prompt first.");
                    break;
            }
        }

        private void OnHandFullRejected()
        {
            ShowToast("Hand is full (10 cards max)!");
        }

        public void ShowBanner(string message, Color accentColor)
        {
            BuildUiIfNeeded();
            if (_bannerRoot == null) return;

            _bannerSequence?.Kill();
            _bannerRoot.gameObject.SetActive(true);
            _bannerText.text = message;
            _bannerTopLine.color = accentColor;
            _bannerBottomLine.color = accentColor;
            _bannerGroup.alpha = 0f;
            _bannerRoot.localScale = new Vector3(1.06f, 0.85f, 1f);

            _bannerSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Append(_bannerGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad))
                .Join(_bannerRoot.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack))
                .AppendInterval(0.85f)
                .Append(_bannerGroup.DOFade(0f, 0.28f).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    if (_bannerRoot != null) _bannerRoot.gameObject.SetActive(false);
                });
        }

        public void ShowToast(string message)
        {
            BuildUiIfNeeded();
            if (_toastRoot == null || string.IsNullOrWhiteSpace(message)) return;

            _toastSequence?.Kill();
            _toastRoot.gameObject.SetActive(true);
            _toastText.text = message;
            _toastGroup.alpha = 0f;
            _toastRoot.anchoredPosition = new Vector2(0f, 340f);

            _toastSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Append(_toastGroup.DOFade(1f, 0.12f))
                .Join(_toastRoot.DOAnchorPosY(372f, 0.16f).SetEase(Ease.OutQuad))
                .AppendInterval(1.1f)
                .Append(_toastGroup.DOFade(0f, 0.25f))
                .OnComplete(() =>
                {
                    if (_toastRoot != null) _toastRoot.gameObject.SetActive(false);
                });
        }

        private void BuildUiIfNeeded()
        {
            if (_bannerRoot != null) return;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null) return;

            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;

            // Turn Phase Banner
            GameObject bannerGo = new("TurnPhaseBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            bannerGo.layer = _canvas.gameObject.layer;
            _bannerRoot = (RectTransform)bannerGo.transform;
            _bannerRoot.SetParent(_canvas.transform, false);
            _bannerRoot.anchorMin = new Vector2(0f, 0.5f);
            _bannerRoot.anchorMax = new Vector2(1f, 0.5f);
            _bannerRoot.pivot = new Vector2(0.5f, 0.5f);
            _bannerRoot.anchoredPosition = new Vector2(0f, 80f);
            _bannerRoot.sizeDelta = new Vector2(0f, 92f);

            _bannerGroup = bannerGo.GetComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            Image bannerBg = bannerGo.GetComponent<Image>();
            bannerBg.sprite = solid;
            bannerBg.color = new Color(0.05f, 0.06f, 0.10f, 0.84f);
            bannerBg.raycastTarget = false;

            _bannerTopLine = CreateLine(_bannerRoot, "TopLine", solid, _playerTurnAccent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 4f);
            _bannerBottomLine = CreateLine(_bannerRoot, "BottomLine", solid, _playerTurnAccent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 4f);

            GameObject bTextGo = new("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            bTextGo.layer = bannerGo.layer;
            RectTransform bTextRect = (RectTransform)bTextGo.transform;
            bTextRect.SetParent(_bannerRoot, false);
            bTextRect.anchorMin = Vector2.zero;
            bTextRect.anchorMax = Vector2.one;
            bTextRect.offsetMin = Vector2.zero;
            bTextRect.offsetMax = Vector2.zero;

            _bannerText = bTextGo.GetComponent<TextMeshProUGUI>();
            _bannerText.fontSize = 42f;
            _bannerText.fontStyle = FontStyles.Bold;
            _bannerText.alignment = TextAlignmentOptions.Center;
            _bannerText.color = Color.white;
            _bannerText.raycastTarget = false;
            if (font != null) _bannerText.font = font;

            bannerGo.SetActive(false);

            // Rejection Toast
            GameObject toastGo = new("CombatFeedbackToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            toastGo.layer = _canvas.gameObject.layer;
            _toastRoot = (RectTransform)toastGo.transform;
            _toastRoot.SetParent(_canvas.transform, false);
            _toastRoot.anchorMin = _toastRoot.anchorMax = new Vector2(0.5f, 0f);
            _toastRoot.pivot = new Vector2(0.5f, 0.5f);
            _toastRoot.anchoredPosition = new Vector2(0f, 360f);
            _toastRoot.sizeDelta = new Vector2(460f, 48f);

            _toastGroup = toastGo.GetComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;

            Image toastBg = toastGo.GetComponent<Image>();
            toastBg.sprite = solid;
            toastBg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
            toastBg.raycastTarget = false;

            CreateLine(_toastRoot, "ToastAccent", solid, _toastAccent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 3f);

            GameObject tTextGo = new("ToastText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tTextGo.layer = toastGo.layer;
            RectTransform tTextRect = (RectTransform)tTextGo.transform;
            tTextRect.SetParent(_toastRoot, false);
            tTextRect.anchorMin = Vector2.zero;
            tTextRect.anchorMax = Vector2.one;
            tTextRect.offsetMin = new Vector2(14f, 4f);
            tTextRect.offsetMax = new Vector2(-14f, -4f);

            _toastText = tTextGo.GetComponent<TextMeshProUGUI>();
            _toastText.fontSize = 22f;
            _toastText.fontStyle = FontStyles.Bold;
            _toastText.alignment = TextAlignmentOptions.Center;
            _toastText.color = new Color(1f, 0.92f, 0.72f, 1f);
            _toastText.raycastTarget = false;
            if (font != null) _toastText.font = font;

            toastGo.SetActive(false);
        }

        private static Image CreateLine(RectTransform parent, string name, Sprite sprite, Color color, Vector2 aMin, Vector2 aMax, Vector2 pivot, float height)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = aMin;
            rect.anchorMax = aMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
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

