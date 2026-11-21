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
    public sealed class RunEndSummarySnapshot
    {
        public bool IsVictory;
        public string HeroPartyNames;
        public int ChapterReached;
        public string ChapterName;
        public int TotalChapters;
        public int ChaptersCleared;
        public int NodesVisited;
        public int CombatsWon;
        public int ElitesDefeated;
        public int BossesDefeated;
        public int DeckSize;
        public List<string> RelicNames = new();
        public int GoldRemaining;
        public int TotalGoldEarned;
        public int CrystalsEarned;
        public string EndReason;
    }

    /// <summary>
    /// Full Run Summary UI for GameOver.unity supporting BOTH Campaign Victory and Defeat states
    /// using the existing Victory/Panel art assets (background, parchment panel, nameplate banner, buttons).
    /// Lists everything the player accomplished during the run (Party, Chapter Reached, Chapters Cleared,
    /// Nodes Explored, Combats/Elites/Bosses Won, Deck Size, Relics, Gold, and Crystals Earned).
    /// </summary>
    [DisallowMultipleComponent]
    public class RunEndSummaryUI : MonoBehaviour
    {
        public static bool PendingVictoryMode { get; set; }
        public static RunEndSummarySnapshot PendingSnapshot { get; private set; }

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
        private RunEndSummarySnapshot _activeSnapshot;
        private bool _isVictory;

        public bool IsVictoryMode => _isVictory;
        public RunEndSummarySnapshot ActiveSnapshot => _activeSnapshot;
        public string RenderedSummaryText => _statsText != null ? _statsText.text : string.Empty;

        /// <summary>
        /// Captures all run accomplishments BEFORE RunManager.AbandonRun() clears active run state.
        /// Call on both Campaign Victory (Chapter 4 Boss cleared) and Defeat (Hero HP = 0).
        /// </summary>
        public static RunEndSummarySnapshot CapturePendingSummary(bool isVictory, string endReason = null, int fallbackTotalChapters = 4)
        {
            RunManager rm = RunManager.Instance;
            LevelProgressionManager lpm = LevelProgressionManager.Instance;

            int totalChapters = (lpm != null && lpm.Levels != null && lpm.Levels.Count > 0)
                ? lpm.Levels.Count
                : Mathf.Max(1, fallbackTotalChapters);

            int chapterReached = rm != null ? Mathf.Clamp(rm.CurrentChapterNumber, 1, totalChapters) : 1;
            string chapterName = rm != null && !string.IsNullOrWhiteSpace(rm.CurrentChapterName)
                ? rm.CurrentChapterName
                : (lpm != null && lpm.CurrentLevel != null ? lpm.CurrentLevel.Description : string.Empty);

            int chaptersCleared = rm != null ? rm.ChaptersCleared : 0;
            if (isVictory)
            {
                chapterReached = totalChapters;
                chaptersCleared = Mathf.Max(chaptersCleared, totalChapters);
            }
            else
            {
                chaptersCleared = Mathf.Max(chaptersCleared, Mathf.Max(0, chapterReached - 1));
            }

            int nodesVisited = rm != null ? rm.NodesVisitedTotal : 0;
            if (nodesVisited == 0 && rm?.CurrentMap?.VisitedOrder != null)
            {
                nodesVisited = rm.CurrentMap.VisitedOrder.Count;
            }

            int combatsWon = rm != null ? rm.CombatsWon : 0;
            int elitesDefeated = rm != null ? rm.ElitesDefeated : 0;
            int bossesDefeated = rm != null ? rm.BossesDefeated : 0;
            if (isVictory && bossesDefeated < chaptersCleared)
            {
                bossesDefeated = chaptersCleared;
            }

            int goldRemaining = rm != null ? rm.Gold : 0;
            int totalGoldEarned = rm != null ? Mathf.Max(rm.TotalGoldEarned, goldRemaining) : goldRemaining;
            int deckSize = RunDeckManager.Instance != null ? RunDeckManager.Instance.CurrentDeck.Count : 0;

            List<string> relicNames = new();
            if (RunRelicManager.Instance != null && RunRelicManager.Instance.CurrentRelics != null)
            {
                foreach (RelicData r in RunRelicManager.Instance.CurrentRelics)
                {
                    if (r != null && !string.IsNullOrWhiteSpace(r.Title))
                    {
                        relicNames.Add(r.Title);
                    }
                }
            }

            string partyNames = ResolvePartyDisplayNames(rm);
            int crystalsEarned = isVictory
                ? (120 + chaptersCleared * 30 + elitesDefeated * 10)
                : (20 + chapterReached * 15 + combatsWon * 3 + elitesDefeated * 8);

            if (rm != null && crystalsEarned > 0)
            {
                rm.AddCrystals(crystalsEarned);
            }

            string resolvedReason = endReason;
            if (string.IsNullOrWhiteSpace(resolvedReason))
            {
                if (isVictory)
                {
                    resolvedReason = "You overcame Loneliness, Fear, Anxiety, and Anger!";
                }
                else if (rm != null && !string.IsNullOrWhiteSpace(rm.LastEncounterName))
                {
                    resolvedReason = $"Fallen against {rm.LastEncounterName} in Chapter {chapterReached}";
                }
                else
                {
                    resolvedReason = "Rest, regroup, and step back into the journey stronger than before.";
                }
            }

            PendingVictoryMode = isVictory;
            PendingSnapshot = new RunEndSummarySnapshot
            {
                IsVictory = isVictory,
                HeroPartyNames = partyNames,
                ChapterReached = chapterReached,
                ChapterName = chapterName,
                TotalChapters = totalChapters,
                ChaptersCleared = chaptersCleared,
                NodesVisited = nodesVisited,
                CombatsWon = combatsWon,
                ElitesDefeated = elitesDefeated,
                BossesDefeated = bossesDefeated,
                DeckSize = deckSize,
                RelicNames = relicNames,
                GoldRemaining = goldRemaining,
                TotalGoldEarned = totalGoldEarned,
                CrystalsEarned = crystalsEarned,
                EndReason = resolvedReason
            };

            return PendingSnapshot;
        }

        public static void ClearPendingSummary()
        {
            PendingVictoryMode = false;
            PendingSnapshot = null;
        }

        private void Awake()
        {
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();

            if (PendingSnapshot != null)
            {
                _activeSnapshot = PendingSnapshot;
                _isVictory = _activeSnapshot.IsVictory || PendingVictoryMode;
                _activeSnapshot.IsVictory = _isVictory;
            }
            else
            {
                _isVictory = PendingVictoryMode;
                _activeSnapshot = CapturePendingSummary(_isVictory);
            }

            PendingVictoryMode = false;
            PendingSnapshot = null;
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
            bg.color = _isVictory ? new Color(0.84f, 0.76f, 0.58f, 1f) : new Color(0.38f, 0.28f, 0.30f, 1f);
            bg.raycastTarget = false;

            RectTransform wash = CreateRect(root, "Wash", layer);
            Stretch(wash);
            Image wImg = wash.gameObject.AddComponent<Image>();
            wImg.sprite = solid;
            wImg.color = _isVictory ? new Color(0.10f, 0.08f, 0.04f, 0.48f) : new Color(0.08f, 0.04f, 0.05f, 0.66f);
            wImg.raycastTarget = false;

            // Main Summary Window using existing Victory/Panel art
            _panelRect = CreateRect(root, "SummaryWindow", layer);
            _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(1120f, 820f);
            Image panelImg = _panelRect.gameObject.AddComponent<Image>();
            panelImg.sprite = panelSprite != null ? panelSprite : solid;
            panelImg.color = _isVictory ? Color.white : new Color(0.95f, 0.88f, 0.88f, 1f);

            // Top Banner using existing Nameplate Victory art
            RectTransform banner = CreateRect(_panelRect, "HeadlineBanner", layer);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 0.52f);
            banner.sizeDelta = new Vector2(820f, 84f);
            Image bannerImg = banner.gameObject.AddComponent<Image>();
            bannerImg.sprite = nameplateSprite != null ? nameplateSprite : solid;
            bannerImg.color = _isVictory ? Color.white : new Color(0.94f, 0.84f, 0.84f, 1f);

            _headlineText = MakeText(banner, "Headline", font, 30f, FontStyles.Bold, _isVictory ? new Color(0.28f, 0.16f, 0.02f, 1f) : new Color(0.50f, 0.09f, 0.09f, 1f));
            _headlineText.alignment = TextAlignmentOptions.Center;
            _headlineText.text = _isVictory ? "VICTORY — JOURNEY COMPLETE!" : "DEFEAT — RUN SUMMARY";

            _sublineText = MakeText(_panelRect, "Subline", font, 21f, FontStyles.Italic, _isVictory ? new Color(1f, 0.90f, 0.50f, 1f) : new Color(0.98f, 0.80f, 0.76f, 0.98f));
            _sublineText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _sublineText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _sublineText.rectTransform.offsetMin = new Vector2(56f, -112f);
            _sublineText.rectTransform.offsetMax = new Vector2(-56f, -64f);
            _sublineText.alignment = TextAlignmentOptions.Center;
            _sublineText.text = _activeSnapshot.EndReason;

            // Accomplishments Card using existing Victory button/parchment frame art
            RectTransform statsCard = CreateRect(_panelRect, "StatsBox", layer);
            statsCard.anchorMin = new Vector2(0.07f, 0.24f);
            statsCard.anchorMax = new Vector2(0.93f, 0.82f);
            statsCard.offsetMin = Vector2.zero;
            statsCard.offsetMax = Vector2.zero;
            Image scBg = statsCard.gameObject.AddComponent<Image>();
            scBg.sprite = buttonSprite != null ? buttonSprite : solid;
            scBg.color = _isVictory ? new Color(0.97f, 0.92f, 0.80f, 0.96f) : new Color(0.94f, 0.88f, 0.82f, 0.96f);

            _statsText = MakeText(statsCard, "StatsText", font, 21f, FontStyles.Normal, new Color(0.22f, 0.12f, 0.04f, 1f));
            _statsText.enableAutoSizing = true;
            _statsText.fontSizeMin = 15f;
            _statsText.fontSizeMax = 22f;
            _statsText.rectTransform.offsetMin = new Vector2(44f, 24f);
            _statsText.rectTransform.offsetMax = new Vector2(-44f, -24f);
            _statsText.alignment = TextAlignmentOptions.MidlineLeft;
            _statsText.lineSpacing = 12f;

            string chapterSuffix = !string.IsNullOrWhiteSpace(_activeSnapshot.ChapterName)
                ? $" ({_activeSnapshot.ChapterName})"
                : string.Empty;
            string relicLine = _activeSnapshot.RelicNames.Count > 0
                ? $"{_activeSnapshot.RelicNames.Count}  ({string.Join(", ", _activeSnapshot.RelicNames)})"
                : "0  (None)";

            _statsText.text =
                $"<b>Party:</b>  {_activeSnapshot.HeroPartyNames}\n" +
                $"<b>Chapter Reached:</b>  Chapter {_activeSnapshot.ChapterReached} / {_activeSnapshot.TotalChapters}{chapterSuffix}   |   <b>Chapters Cleared:</b>  {_activeSnapshot.ChaptersCleared} / {_activeSnapshot.TotalChapters}\n" +
                $"<b>Nodes Explored:</b>  {_activeSnapshot.NodesVisited}   |   <b>Battles Won:</b>  {_activeSnapshot.CombatsWon}  (Elites: {_activeSnapshot.ElitesDefeated}, Bosses: {_activeSnapshot.BossesDefeated})\n" +
                $"<b>Final Deck Size:</b>  {_activeSnapshot.DeckSize} Cards\n" +
                $"<b>Relics Collected:</b>  {relicLine}\n" +
                $"<b>Gold:</b>  {_activeSnapshot.GoldRemaining}g remaining  ({_activeSnapshot.TotalGoldEarned}g total earned)\n" +
                $"<b>Crystals Earned:</b>  <color=#1E6B7B>+{_activeSnapshot.CrystalsEarned} Crystals</color>";

            // Action Buttons (Play Again + Main Menu)
            MakeButton(_panelRect, "PlayAgainBtn", "PLAY AGAIN", font, new Vector2(-185f, 58f), new Vector2(300f, 74f), new Color(0.56f, 0.90f, 0.58f, 1f), PlayAgain);
            MakeButton(_panelRect, "MainMenuBtn", "MAIN MENU", font, new Vector2(185f, 58f), new Vector2(300f, 74f), new Color(0.95f, 0.85f, 0.64f, 1f), ReturnToMainMenu);
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

        private static string ResolvePartyDisplayNames(RunManager rm)
        {
            if (rm != null && rm.SelectedParty != null && rm.SelectedParty.Count > 0)
            {
                List<string> names = new();
                for (int i = 0; i < rm.SelectedParty.Count; i++)
                {
                    HeroData h = rm.SelectedParty[i];
                    if (h != null && !string.IsNullOrWhiteSpace(h.EffectiveDisplayName))
                    {
                        names.Add(h.EffectiveDisplayName);
                    }
                }
                if (names.Count > 0) return string.Join(" & ", names);
            }

            return "Aira & Ember";
        }

        private static RectTransform CreateRect(Transform p, string n, int l) { GameObject g = new(n, typeof(RectTransform)); g.layer = l; RectTransform rt = (RectTransform)g.transform; rt.SetParent(p, false); return rt; }
        private static void Stretch(RectTransform rt, float pad = 0f) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad); }
        private static TextMeshProUGUI MakeText(RectTransform p, string n, TMP_FontAsset f, float sz, FontStyles st, Color c) { RectTransform rt = CreateRect(p, n, p.gameObject.layer); Stretch(rt); TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>(); if (f != null) t.font = f; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false; return t; }
        private static Sprite GetSolid() { if (s_solid != null) return s_solid; Texture2D t = Texture2D.whiteTexture; s_solid = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)); return s_solid; }
    }
}
