using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Levels;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Menus
{
    /// <summary>
    /// Full Run Summary UI for GameOver.unity supporting BOTH Defeat and Campaign Victory states.
    /// Displays Chapters Cleared, Hero, Gold, Crystals Earned, Equipped Relics, Deck Size,
    /// and animated Play Again / Main Menu actions using existing project art assets.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunEndSummaryUI : MonoBehaviour
    {
        public static bool PendingVictoryMode { get; set; }

        [Header("Scene & Assets")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string levelsSceneName = "Levels";
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite nameplateSprite;
        [SerializeField] private Sprite buttonSprite;

        private static Sprite s_solid;
        private RectTransform _panelRect;
        private TextMeshProUGUI _headlineText;
        private TextMeshProUGUI _sublineText;
        private TextMeshProUGUI _statsText;
        private bool _isVictory;

        public bool IsVictoryMode => _isVictory;

        private void Awake()
        {
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
            _isVictory = PendingVictoryMode;
            PendingVictoryMode = false;
            BuildUI();
        }

        private void Start()
        {
            if (_panelRect != null)
            {
                _panelRect.localScale = Vector3.one * 0.84f;
                _panelRect.DOScale(Vector3.one, 0.38f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            Tune.SFX(_isVictory ? SfxID.combat_won : SfxID.ui_panel_open);
        }

        public void PlayAgain()
        {
            Tune.SFX(SfxID.button_click);
            RunManager.Instance?.AbandonRun();
            SceneLoader.LoadScene(string.IsNullOrWhiteSpace(levelsSceneName) ? "Levels" : levelsSceneName);
        }

        public void ReturnToMainMenu()
        {
            Tune.SFX(SfxID.button_click);
            RunManager.Instance?.AbandonRun();
            SceneLoader.LoadScene(string.IsNullOrWhiteSpace(mainMenuSceneName) ? "MainMenu" : mainMenuSceneName);
        }

        private void BuildUI()
        {
            if (targetCanvas == null) return;
            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolid();

            RectTransform root = CreateRect(targetCanvas.transform, "RunEndSummaryRoot", layer);
            Stretch(root);

            RectTransform bgRt = CreateRect(root, "Backdrop", layer);
            Stretch(bgRt);
            bgRt.localScale = new Vector3(1.05f, 1.05f, 1f);
            Image bg = bgRt.gameObject.AddComponent<Image>();
            bg.sprite = backgroundSprite != null ? backgroundSprite : solid;
            bg.color = _isVictory ? new Color(0.75f, 0.68f, 0.52f, 1f) : new Color(0.28f, 0.20f, 0.22f, 1f);
            bg.raycastTarget = false;

            RectTransform wash = CreateRect(root, "Wash", layer);
            Stretch(wash);
            Image wImg = wash.gameObject.AddComponent<Image>();
            wImg.sprite = solid;
            wImg.color = _isVictory ? new Color(0.10f, 0.08f, 0.04f, 0.56f) : new Color(0.06f, 0.03f, 0.04f, 0.72f);
            wImg.raycastTarget = false;

            _panelRect = CreateRect(root, "SummaryWindow", layer);
            _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(1060f, 760f);
            _panelRect.gameObject.AddComponent<Image>().sprite = panelSprite != null ? panelSprite : solid;

            // Top Banner
            RectTransform banner = CreateRect(_panelRect, "HeadlineBanner", layer);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 0.52f);
            banner.sizeDelta = new Vector2(760f, 80f);
            banner.gameObject.AddComponent<Image>().sprite = nameplateSprite != null ? nameplateSprite : solid;
            _headlineText = MakeText(banner, "Headline", font, 29f, FontStyles.Bold, _isVictory ? new Color(0.28f, 0.16f, 0.02f, 1f) : new Color(0.45f, 0.10f, 0.10f, 1f));
            _headlineText.alignment = TextAlignmentOptions.Center;
            _headlineText.text = _isVictory ? "VICTORY — RUN COMPLETE!" : "RUN ENDED — GAME OVER";

            _sublineText = MakeText(_panelRect, "Subline", font, 21f, FontStyles.Italic, _isVictory ? new Color(1f, 0.90f, 0.50f, 1f) : new Color(0.95f, 0.74f, 0.72f, 0.96f));
            _sublineText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _sublineText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _sublineText.rectTransform.offsetMin = new Vector2(48f, -108f);
            _sublineText.rectTransform.offsetMax = new Vector2(-48f, -64f);
            _sublineText.alignment = TextAlignmentOptions.Center;
            _sublineText.text = _isVictory
                ? "You overcame Loneliness, Fear, Anxiety, and Anger!"
                : "Rest, regroup, and step back into the journey stronger than before.";

            // Stats Box
            RunManager rm = RunManager.Instance;
            int chapter = rm != null ? rm.CurrentChapterNumber : 1;
            int gold = rm != null ? rm.Gold : 0;
            int deckCount = RunDeckManager.Instance != null ? RunDeckManager.Instance.CurrentDeck.Count : 0;
            IReadOnlyList<RelicData> relics = RunRelicManager.Instance != null ? RunRelicManager.Instance.CurrentRelics : new List<RelicData>();
            int crystalsEarned = _isVictory ? (120 + chapter * 30) : (20 + chapter * 15);
            string heroName = rm != null && rm.SelectedParty != null && rm.SelectedParty.Count > 0 && rm.SelectedParty[0] != null
                ? rm.SelectedParty[0].EffectiveDisplayName
                : "Default Hero";

            RectTransform statsCard = CreateRect(_panelRect, "StatsBox", layer);
            statsCard.anchorMin = new Vector2(0.08f, 0.36f);
            statsCard.anchorMax = new Vector2(0.92f, 0.80f);
            statsCard.offsetMin = Vector2.zero;
            statsCard.offsetMax = Vector2.zero;
            Image scBg = statsCard.gameObject.AddComponent<Image>();
            scBg.sprite = buttonSprite != null ? buttonSprite : solid;
            scBg.color = new Color(0.95f, 0.89f, 0.76f, 0.96f);

            _statsText = MakeText(statsCard, "StatsText", font, 23f, FontStyles.Normal, new Color(0.22f, 0.12f, 0.04f, 1f));
            _statsText.rectTransform.offsetMin = new Vector2(40f, 24f);
            _statsText.rectTransform.offsetMax = new Vector2(-40f, -24f);
            _statsText.alignment = TextAlignmentOptions.MidlineLeft;
            _statsText.lineSpacing = 14f;
            _statsText.text =
                $"<b>Hero:</b>  {heroName}\n" +
                $"<b>Chapter Reached:</b>  Chapter {chapter} / 4\n" +
                $"<b>Final Deck Size:</b>  {deckCount} Cards\n" +
                $"<b>Relics Collected:</b>  {relics.Count}  {(relics.Count > 0 ? $"({JoinRelicNames(relics)})" : "(None)")}\n" +
                $"<b>Gold Remaining:</b>  {gold}g\n" +
                $"<b>Crystals Earned:</b>  <color=#1E6B7B>+{crystalsEarned} Crystals</color>";

            // Action Buttons (Play Again + Main Menu)
            MakeButton(_panelRect, "PlayAgainBtn", "PLAY AGAIN", font, new Vector2(-175f, 76f), new Vector2(290f, 72f), new Color(0.54f, 0.90f, 0.56f, 1f), PlayAgain);
            MakeButton(_panelRect, "MainMenuBtn", "MAIN MENU", font, new Vector2(175f, 76f), new Vector2(290f, 72f), new Color(0.95f, 0.85f, 0.64f, 1f), ReturnToMainMenu);
        }

        private void MakeButton(RectTransform parent, string name, string label, TMP_FontAsset font, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rt = CreateRect(parent, name, parent.gameObject.layer);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = buttonSprite != null ? buttonSprite : GetSolid();
            img.color = bgColor;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            TextMeshProUGUI txt = MakeText(rt, "Label", font, 24f, FontStyles.Bold, new Color(0.20f, 0.11f, 0.04f, 1f));
            txt.alignment = TextAlignmentOptions.Center;
            txt.text = label;
        }

        private static string JoinRelicNames(IReadOnlyList<RelicData> relics)
        {
            List<string> names = new();
            foreach (RelicData r in relics) if (r != null) names.Add(r.Title);
            return string.Join(", ", names);
        }

        private static RectTransform CreateRect(Transform p, string n, int l) { GameObject g = new(n, typeof(RectTransform)); g.layer = l; RectTransform rt = (RectTransform)g.transform; rt.SetParent(p, false); return rt; }
        private static void Stretch(RectTransform rt, float pad = 0f) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad); }
        private static TextMeshProUGUI MakeText(RectTransform p, string n, TMP_FontAsset f, float sz, FontStyles st, Color c) { RectTransform rt = CreateRect(p, n, p.gameObject.layer); Stretch(rt); TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>(); if (f != null) t.font = f; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false; return t; }
        private static Sprite GetSolid() { if (s_solid != null) return s_solid; Texture2D t = Texture2D.whiteTexture; s_solid = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)); return s_solid; }
    }
}

