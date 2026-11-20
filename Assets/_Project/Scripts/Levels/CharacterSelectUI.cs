using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.Levels
{
    /// <summary>
    /// Modular Character Selection panel for Levels.unity (Milestone 3).
    /// Lets players switch between Aira and Ember (or any assigned HeroData roster), displaying
    /// animated portrait, HP, Will traits, starting Relic, and starter deck summary, and syncing
    /// selection into RunManager.SetSelectedParty + RunRelicManager before entering a chapter.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSelectUI : MonoBehaviour
    {
        [System.Serializable]
        public class PlayableHeroOption
        {
            public HeroData Hero;
            public string RoleTag = "ARCANE SUPPORT";
            [TextArea(2, 4)] public string Bio = "Balanced spellcaster with high Will scaling and draw tools.";
            public RelicData StartingRelic;
            public Color AccentColor = new(0.46f, 0.88f, 0.58f, 1f);
        }

        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private List<PlayableHeroOption> heroOptions = new();

        [Header("Existing Art Assets")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite nameplateSprite;

        private static Sprite s_solid;

        private RectTransform _dockPanel;
        private Image _portraitImg;
        private RectTransform _portraitRect;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _roleText;
        private TextMeshProUGUI _statsText;
        private readonly List<Image> _tabBgs = new();

        private int _selectedIndex;
        private bool _built;

        public int SelectedIndex => _selectedIndex;
        public HeroData SelectedHero => _selectedIndex >= 0 && _selectedIndex < heroOptions.Count ? heroOptions[_selectedIndex]?.Hero : null;

        private void Awake()
        {
            if (RunManager.Instance == null) new GameObject("Run Manager").AddComponent<RunManager>();
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
            BuildUIIfNeeded();
        }

        private void Start()
        {
            if (heroOptions.Count > 0)
            {
                SelectHero(0, playSfx: false);
            }
        }

        public void SelectHero(int index, bool playSfx = true)
        {
            if (heroOptions == null || index < 0 || index >= heroOptions.Count) return;
            PlayableHeroOption opt = heroOptions[index];
            if (opt == null || opt.Hero == null) return;

            _selectedIndex = index;

            // Commit to RunManager + RunRelicManager + RunDeckManager
            if (RunManager.Instance != null)
            {
                RunManager.Instance.SetSelectedParty(new[] { opt.Hero });
            }
            if (opt.StartingRelic != null)
            {
                RunRelicManager.EnsureInstance().ResetRelics(new[] { opt.StartingRelic });
            }

            if (playSfx)
            {
                bool isEmber = opt.Hero.EffectiveDisplayName.IndexOf("Ember", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Tune.SFX(isEmber ? SfxID.Ember_attack : SfxID.Aira_heal);
            }

            RefreshDisplay(opt, animate: playSfx);
        }

        private void RefreshDisplay(PlayableHeroOption opt, bool animate)
        {
            if (_portraitImg != null)
            {
                Sprite s = opt.Hero.GetPortraitForWill(false, false, fallbackToWorldSprite: true);
                _portraitImg.sprite = s;
                _portraitImg.enabled = s != null;
            }
            if (animate && _portraitRect != null)
            {
                _portraitRect.DOKill();
                _portraitRect.localScale = Vector3.one * 0.86f;
                _portraitRect.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (_nameText != null) _nameText.text = opt.Hero.EffectiveDisplayName.ToUpper();
            if (_roleText != null) { _roleText.text = opt.RoleTag.ToUpper(); _roleText.color = opt.AccentColor; }

            if (_statsText != null)
            {
                int deckCount = opt.Hero.Deck != null ? opt.Hero.Deck.Count : 0;
                string relicName = opt.StartingRelic != null ? opt.StartingRelic.Title : "None";
                _statsText.text =
                    $"<b>HP:</b> {opt.Hero.Health}   •   <b>Start Will:</b> {opt.Hero.EffectiveStartingWill}/{opt.Hero.EffectiveMaxWill}\n" +
                    $"<b>Relic:</b> {relicName}   •   <b>Deck:</b> {deckCount} Cards\n" +
                    $"<color=#E6D8BE>{opt.Bio}</color>";
            }

            for (int i = 0; i < _tabBgs.Count; i++)
            {
                if (_tabBgs[i] == null) continue;
                bool active = i == _selectedIndex;
                _tabBgs[i].color = active ? new Color(0.98f, 0.86f, 0.48f, 1f) : new Color(0.42f, 0.32f, 0.22f, 0.88f);
            }
        }

        private void BuildUIIfNeeded()
        {
            if (_built || targetCanvas == null || heroOptions.Count == 0) return;
            _built = true;

            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolid();

            // Clean compact Hero Selection card docked at bottom-left of Levels.unity
            _dockPanel = CreateRect(targetCanvas.transform, "CharacterSelectPanel", layer);
            _dockPanel.anchorMin = _dockPanel.anchorMax = new Vector2(0f, 0f);
            _dockPanel.pivot = new Vector2(0f, 0f);
            _dockPanel.anchoredPosition = new Vector2(28f, 24f);
            _dockPanel.sizeDelta = new Vector2(610f, 224f);
            _dockPanel.gameObject.AddComponent<Image>().sprite = panelSprite != null ? panelSprite : solid;

            // Header Ribbon
            RectTransform hdr = CreateRect(_dockPanel, "Header", layer);
            hdr.anchorMin = hdr.anchorMax = new Vector2(0.5f, 1f);
            hdr.pivot = new Vector2(0.5f, 0.5f);
            hdr.sizeDelta = new Vector2(310f, 44f);
            hdr.gameObject.AddComponent<Image>().sprite = nameplateSprite != null ? nameplateSprite : solid;
            TextMeshProUGUI hdrTxt = MakeText(hdr, "Label", font, 17f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            hdrTxt.alignment = TextAlignmentOptions.Center;
            hdrTxt.text = "SELECT CHARACTER";

            // Portrait Box (Left)
            RectTransform pBox = CreateRect(_dockPanel, "PortraitFrame", layer);
            pBox.anchorMin = pBox.anchorMax = new Vector2(0f, 0.5f);
            pBox.pivot = new Vector2(0f, 0.5f);
            pBox.anchoredPosition = new Vector2(24f, -6f);
            pBox.sizeDelta = new Vector2(146f, 162f);
            Image pbImg = pBox.gameObject.AddComponent<Image>();
            pbImg.sprite = buttonSprite != null ? buttonSprite : solid;
            pbImg.color = new Color(0.22f, 0.14f, 0.09f, 0.96f);

            _portraitRect = CreateRect(pBox, "Portrait", layer);
            Stretch(_portraitRect, 8f);
            _portraitImg = _portraitRect.gameObject.AddComponent<Image>();
            _portraitImg.preserveAspect = true;
            _portraitImg.raycastTarget = false;

            // Hero Selector Tabs (Top-Right inside card)
            RectTransform tabRow = CreateRect(_dockPanel, "HeroTabs", layer);
            tabRow.anchorMin = new Vector2(0f, 1f);
            tabRow.anchorMax = new Vector2(1f, 1f);
            tabRow.offsetMin = new Vector2(186f, -72f);
            tabRow.offsetMax = new Vector2(-24f, -30f);
            HorizontalLayoutGroup hlg = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            _tabBgs.Clear();
            for (int i = 0; i < heroOptions.Count; i++)
            {
                PlayableHeroOption opt = heroOptions[i];
                RectTransform tab = CreateRect(tabRow, $"Tab_{i}", layer);
                Image bg = tab.gameObject.AddComponent<Image>();
                bg.sprite = buttonSprite != null ? buttonSprite : solid;
                _tabBgs.Add(bg);

                Button btn = tab.gameObject.AddComponent<Button>();
                int captured = i;
                btn.onClick.AddListener(() => SelectHero(captured, playSfx: true));

                TextMeshProUGUI t = MakeText(tab, "Name", font, 17f, FontStyles.Bold, new Color(0.20f, 0.11f, 0.04f, 1f));
                t.alignment = TextAlignmentOptions.Center;
                t.text = opt.Hero != null ? opt.Hero.EffectiveDisplayName.ToUpper() : $"HERO {i + 1}";
            }

            _nameText = MakeText(_dockPanel, "HeroName", font, 21f, FontStyles.Bold, new Color(1f, 0.90f, 0.52f, 1f));
            _nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _nameText.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            _nameText.rectTransform.offsetMin = new Vector2(188f, -106f);
            _nameText.rectTransform.offsetMax = new Vector2(0f, -76f);
            _nameText.alignment = TextAlignmentOptions.MidlineLeft;

            _roleText = MakeText(_dockPanel, "HeroRole", font, 14f, FontStyles.Bold, Color.white);
            _roleText.rectTransform.anchorMin = new Vector2(0.62f, 1f);
            _roleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _roleText.rectTransform.offsetMin = new Vector2(0f, -106f);
            _roleText.rectTransform.offsetMax = new Vector2(-24f, -76f);
            _roleText.alignment = TextAlignmentOptions.MidlineRight;

            _statsText = MakeText(_dockPanel, "HeroStats", font, 15.5f, FontStyles.Normal, new Color(0.96f, 0.90f, 0.78f, 0.96f));
            _statsText.rectTransform.anchorMin = Vector2.zero;
            _statsText.rectTransform.anchorMax = Vector2.one;
            _statsText.rectTransform.offsetMin = new Vector2(188f, 18f);
            _statsText.rectTransform.offsetMax = new Vector2(-24f, -108f);
            _statsText.alignment = TextAlignmentOptions.TopLeft;
            _statsText.textWrappingMode = TextWrappingModes.Normal;
        }

        private static RectTransform CreateRect(Transform p, string n, int l) { GameObject g = new(n, typeof(RectTransform)); g.layer = l; RectTransform rt = (RectTransform)g.transform; rt.SetParent(p, false); return rt; }
        private static void Stretch(RectTransform rt, float pad = 0f) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad); }
        private static TextMeshProUGUI MakeText(RectTransform p, string n, TMP_FontAsset f, float sz, FontStyles st, Color c) { RectTransform rt = CreateRect(p, n, p.gameObject.layer); Stretch(rt); TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>(); if (f != null) t.font = f; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false; return t; }
        private static Sprite GetSolid() { if (s_solid != null) return s_solid; Texture2D t = Texture2D.whiteTexture; s_solid = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)); return s_solid; }
    }
}

