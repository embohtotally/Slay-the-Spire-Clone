using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Collapsible Slay-the-Spire style Combat Log drawer anchored on the right edge of the combat canvas.
    /// Automatically records turn transitions, played cards, damage, shield, healing, buffs/debuffs,
    /// Power activations, and enemy passives. Utilizes existing project art (Panel.png, button1 .png, scroll.png).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatLogUI : MonoBehaviour
    {
        public static CombatLogUI Instance { get; private set; }

        private const int MaxEntries = 120;
        private const float DrawerWidth = 372f;
        private const float DrawerHeight = 420f;

        private readonly List<string> _entries = new();
        private readonly StringBuilder _sb = new();

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _drawerPanel;
        private TextMeshProUGUI _logText;
        private TextMeshProUGUI _toggleLabel;
        private ScrollRect _scrollRect;
        private bool _isOpen;
        private int _turnNumber = 1;
        private bool _built;

        public bool IsOpen => _isOpen;
        public int EntryCount => _entries.Count;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            BuildUIIfNeeded();
        }

        private void OnEnable()
        {
            Instance = this;
            ActionSystem.SubscribeReaction<CombatStartedGA>(OnCombatStarted, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCard, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamage, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<GainShieldGA>(OnGainShield, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<HealGA>(OnHeal, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<ApplyBuffGA>(OnApplyBuff, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<KillEnemyGA>(OnKillEnemy, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.UnsubscribeReaction<CombatStartedGA>(OnCombatStarted, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCard, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamage, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<GainShieldGA>(OnGainShield, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<HealGA>(OnHeal, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<ApplyBuffGA>(OnApplyBuff, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<KillEnemyGA>(OnKillEnemy, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
            if (Instance == this) Instance = null;
        }

        public static void Log(string richLine)
        {
            if (Instance != null) Instance.AppendEntry(richLine);
        }

        public void AppendEntry(string richLine)
        {
            if (string.IsNullOrWhiteSpace(richLine)) return;
            _entries.Add(richLine);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
            RefreshLogText();
        }

        public void Toggle()
        {
            SetOpen(!_isOpen);
        }

        public void SetOpen(bool open)
        {
            BuildUIIfNeeded();
            if (_drawerPanel == null) return;
            _isOpen = open;
            Tune.SFX(open ? SfxID.ui_panel_open : SfxID.ui_panel_close);

            _drawerPanel.DOKill();
            float targetX = open ? -16f : DrawerWidth + 24f;
            _drawerPanel.DOAnchorPosX(targetX, 0.24f).SetEase(open ? Ease.OutCubic : Ease.InCubic).SetUpdate(true);
            if (_toggleLabel != null)
            {
                _toggleLabel.text = open ? "LOG >" : "< LOG";
            }
            if (open)
            {
                RefreshLogText();
            }
        }

        private void OnCombatStarted(CombatStartedGA _)
        {
            _entries.Clear();
            _turnNumber = 1;
            AppendEntry("<color=#FFE066><b>--- COMBAT STARTED (TURN 1) ---</b></color>");
        }

        private void OnPlayCard(PlayCardGA ga)
        {
            if (ga?.Card == null) return;
            string costStr = ga.Card.IsXCost ? $"X={ga.ManaSpent}" : $"{ga.Card.CostLabel} Mana";
            AppendEntry($"<color=#7CE8FF><b>Played</b></color> <b>{ga.Card.Title}</b> <color=#AAB6C8>({costStr})</color>");
        }

        private void OnDealDamage(DealDamageGA ga)
        {
            if (ga == null || ga.Amount <= 0 || ga.Targets == null) return;
            string src = GetName(ga.Caster);
            for (int i = 0; i < ga.Targets.Count; i++)
            {
                if (ga.Targets[i] == null) continue;
                string dst = GetName(ga.Targets[i]);
                string tag = ga.IsThornsDamage ? " (Reflect/Effect)" : string.Empty;
                AppendEntry($"  {src} dealt <color=#FF6B6B><b>{ga.Amount}</b></color> dmg to {dst}{tag}.");
            }
        }

        private void OnGainShield(GainShieldGA ga)
        {
            if (ga == null || ga.Amount <= 0 || ga.Targets == null) return;
            for (int i = 0; i < ga.Targets.Count; i++)
            {
                if (ga.Targets[i] == null) continue;
                AppendEntry($"  {GetName(ga.Targets[i])} gained <color=#56D0FF><b>+{ga.Amount} Shield</b></color>.");
            }
        }

        private void OnHeal(HealGA ga)
        {
            if (ga == null || ga.Amount <= 0 || ga.Targets == null) return;
            for (int i = 0; i < ga.Targets.Count; i++)
            {
                if (ga.Targets[i] == null) continue;
                AppendEntry($"  {GetName(ga.Targets[i])} healed <color=#59E892><b>+{ga.Amount} HP</b></color>.");
            }
        }

        private void OnApplyBuff(ApplyBuffGA ga)
        {
            if (ga == null || ga.Targets == null) return;
            string hex = BuffSystem.IsDebuff(ga.BuffType) ? "#D18BFF" : "#FFD54F";
            for (int i = 0; i < ga.Targets.Count; i++)
            {
                if (ga.Targets[i] == null) continue;
                AppendEntry($"  {GetName(ga.Targets[i])} gained <color={hex}><b>{ga.BuffType} ({ga.Value})</b></color>.");
            }
        }

        private void OnKillEnemy(KillEnemyGA ga)
        {
            if (ga?.EnemyView == null) return;
            AppendEntry($"<color=#59E892><b>Defeated {GetName(ga.EnemyView)}!</b></color>");
        }

        private void OnEnemyTurnPre(EnemyTurnGA _)
        {
            AppendEntry("<color=#FF8A80><b>-- Enemy Turn --</b></color>");
        }

        private void OnEnemyTurnPost(EnemyTurnGA _)
        {
            _turnNumber++;
            AppendEntry($"<color=#FFE066><b>--- TURN {_turnNumber} ---</b></color>");
        }

        private static string GetName(CombatantView view)
        {
            if (view == null) return "Effect";
            if (view is HeroView) return "Hero";
            if (view is EnemyView ev && ev.Data != null && !string.IsNullOrWhiteSpace(ev.Data.name))
                return ev.Data.name;
            return view.name.Replace("(Clone)", string.Empty).Trim();
        }

        private void RefreshLogText()
        {
            if (_logText == null) return;
            _sb.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0) _sb.Append('\n');
                _sb.Append(_entries[i]);
            }
            _logText.text = _sb.ToString();
            if (_scrollRect != null && _isOpen)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void BuildUIIfNeeded()
        {
            if (_built) return;
            _canvas = FindCombatCanvas();
            if (_canvas == null) return;
            _built = true;

            int layer = _canvas.gameObject.layer;
            TMP_FontAsset font = CombatStatusTooltipUI.Instance != null
                ? CombatStatusTooltipUI.Instance.Font
                : TMP_Settings.defaultFontAsset;
            Sprite solid = CombatStatusTooltipUI.GetSolidSprite();
            Sprite panelSpr = FindProjectSprite("Panel");
            Sprite btnSpr = FindProjectSprite("button1 ") ?? FindProjectSprite("Tombol");

            GameObject rootGo = new("CombatLogDrawerRoot", typeof(RectTransform));
            rootGo.layer = layer;
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(_canvas.transform, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            // Toggle Tab Button tucked on right edge below top bar
            GameObject toggleGo = new("CombatLogToggleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleGo.layer = layer;
            RectTransform toggleRt = (RectTransform)toggleGo.transform;
            toggleRt.SetParent(_root, false);
            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(1f, 0.72f);
            toggleRt.pivot = new Vector2(1f, 0.5f);
            toggleRt.anchoredPosition = new Vector2(-10f, 0f);
            toggleRt.sizeDelta = new Vector2(88f, 38f);
            Image toggleBg = toggleGo.GetComponent<Image>();
            toggleBg.sprite = btnSpr != null ? btnSpr : solid;
            toggleBg.color = btnSpr != null ? new Color(0.92f, 0.84f, 0.66f, 0.96f) : new Color(0.14f, 0.16f, 0.24f, 0.92f);
            toggleGo.GetComponent<Button>().onClick.AddListener(Toggle);

            GameObject toggleLblGo = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            toggleLblGo.layer = layer;
            RectTransform toggleLblRt = (RectTransform)toggleLblGo.transform;
            toggleLblRt.SetParent(toggleRt, false);
            toggleLblRt.anchorMin = Vector2.zero;
            toggleLblRt.anchorMax = Vector2.one;
            toggleLblRt.offsetMin = Vector2.zero;
            toggleLblRt.offsetMax = Vector2.zero;
            _toggleLabel = toggleLblGo.GetComponent<TextMeshProUGUI>();
            if (font != null) _toggleLabel.font = font;
            _toggleLabel.fontSize = 15f;
            _toggleLabel.fontStyle = FontStyles.Bold;
            _toggleLabel.alignment = TextAlignmentOptions.Center;
            _toggleLabel.color = btnSpr != null ? new Color(0.22f, 0.12f, 0.04f, 1f) : new Color(0.96f, 0.90f, 0.68f, 1f);
            _toggleLabel.text = "< LOG";
            _toggleLabel.raycastTarget = false;

            // Collapsible Drawer Panel
            GameObject panelGo = new("CombatLogPanel", typeof(RectTransform), typeof(Image));
            panelGo.layer = layer;
            _drawerPanel = (RectTransform)panelGo.transform;
            _drawerPanel.SetParent(_root, false);
            _drawerPanel.anchorMin = _drawerPanel.anchorMax = new Vector2(1f, 0.52f);
            _drawerPanel.pivot = new Vector2(1f, 0.5f);
            _drawerPanel.sizeDelta = new Vector2(DrawerWidth, DrawerHeight);
            _drawerPanel.anchoredPosition = new Vector2(DrawerWidth + 24f, 0f);

            Image panelBg = panelGo.GetComponent<Image>();
            panelBg.sprite = panelSpr != null ? panelSpr : solid;
            panelBg.color = panelSpr != null ? new Color(0.22f, 0.17f, 0.13f, 0.95f) : new Color(0.06f, 0.08f, 0.13f, 0.94f);

            // Header
            GameObject hdrGo = new("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            hdrGo.layer = layer;
            RectTransform hdrRt = (RectTransform)hdrGo.transform;
            hdrRt.SetParent(_drawerPanel, false);
            hdrRt.anchorMin = new Vector2(0f, 1f);
            hdrRt.anchorMax = new Vector2(1f, 1f);
            hdrRt.pivot = new Vector2(0.5f, 1f);
            hdrRt.offsetMin = new Vector2(18f, -42f);
            hdrRt.offsetMax = new Vector2(-18f, -10f);
            TextMeshProUGUI hdrTmp = hdrGo.GetComponent<TextMeshProUGUI>();
            if (font != null) hdrTmp.font = font;
            hdrTmp.fontSize = 18f;
            hdrTmp.fontStyle = FontStyles.Bold;
            hdrTmp.color = new Color(1f, 0.88f, 0.48f, 1f);
            hdrTmp.alignment = TextAlignmentOptions.MidlineLeft;
            hdrTmp.text = "COMBAT LOG";
            hdrTmp.raycastTarget = false;

            // Scroll Viewport
            GameObject vpGo = new("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            vpGo.layer = layer;
            RectTransform vpRt = (RectTransform)vpGo.transform;
            vpRt.SetParent(_drawerPanel, false);
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(16f, 16f);
            vpRt.offsetMax = new Vector2(-16f, -46f);
            Image vpBg = vpGo.GetComponent<Image>();
            vpBg.sprite = solid;
            vpBg.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);

            GameObject contentGo = new("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            contentGo.layer = layer;
            RectTransform contentRt = (RectTransform)contentGo.transform;
            contentRt.SetParent(vpRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(10f, 0f);
            contentRt.offsetMax = new Vector2(-10f, 0f);
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _logText = contentGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _logText.font = font;
            _logText.fontSize = 13.5f;
            _logText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
            _logText.textWrappingMode = TextWrappingModes.Normal;
            _logText.alignment = TextAlignmentOptions.TopLeft;
            _logText.raycastTarget = false;

            _scrollRect = vpGo.GetComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.viewport = vpRt;
            _scrollRect.content = contentRt;
            _scrollRect.scrollSensitivity = 28f;

            RefreshLogText();
        }

        private static Canvas FindCombatCanvas()
        {
            CombatTopBarUI topBar = FindFirstObjectByType<CombatTopBarUI>();
            if (topBar != null)
            {
                Canvas c = topBar.GetComponentInParent<Canvas>()?.rootCanvas;
                if (c != null) return c;
            }
            return FindFirstObjectByType<Canvas>()?.rootCanvas;
        }

        private static Sprite FindProjectSprite(string exactName)
        {
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && string.Equals(sprites[i].name, exactName, System.StringComparison.OrdinalIgnoreCase))
                    return sprites[i];
            }
            return null;
        }
    }
}

