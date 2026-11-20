using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Map.Merchant;
using PlushieOdyssey.Rewards;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.Map.Event
{
    /// <summary>
    /// Data-driven Slay-the-Spire branching narrative Event scene controller for Event.unity.
    /// Styled with existing project art (Panel.png, Panel Box Text_0.png, Panel Box NAMA CHARACTER clip.png,
    /// Tombol.png) with typewriter story reveal, requirement-locked choices, outcome feedback, and relic/card rewards.
    /// </summary>
    [DisallowMultipleComponent]
    public class NarrativeEventUI : MonoBehaviour
    {
        [Header("Scene & Data")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private List<NarrativeEventData> eventPool = new();
        [SerializeField] private RelicRewardPool relicPool;
        [SerializeField] private MerchantCardPool cardPool;
        [SerializeField] private string mapSceneName = "Map";

        [Header("Existing Art Assets")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite dialogueBoxSprite;
        [SerializeField] private Sprite nameplateSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite fallbackIllustration;

        private static Sprite s_solid;

        private RectTransform _root;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private Image _illustrationImg;
        private TextMeshProUGUI _storyText;
        private TextMeshProUGUI _statusBarText;
        private Transform _choicesContainer;
        private Button _continueBtn;
        private GameObject _continueBtnGo;
        private readonly List<GameObject> _choiceObjects = new();

        private NarrativeEventData _activeEvent;
        private bool _choiceResolved;
        private bool _built;

        public NarrativeEventData ActiveEvent => _activeEvent;
        public bool ChoiceResolved => _choiceResolved;
        public string CurrentStoryText => _storyText != null ? _storyText.text : string.Empty;

        private void Awake()
        {
            if (RunManager.Instance == null) new GameObject("Run Manager").AddComponent<RunManager>();
            if (RunManager.Instance != null && !RunManager.Instance.HasHeroState)
            {
                RunManager.Instance.InitializeHeroState(50, 100);
            }
            if (RunDeckManager.Instance == null) new GameObject("Run Deck Manager").AddComponent<RunDeckManager>();
            RunRelicManager.EnsureInstance();
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
            EnsureRelicSwapModal();
            BuildUIIfNeeded();
        }

        private void Start()
        {
            PickAndDisplayEvent();
            Tune.SFX(SfxID.event_open);
        }

        public bool SelectChoice(int index)
        {
            if (_choiceResolved || _activeEvent == null || index < 0 || index >= _activeEvent.Choices.Count)
                return false;

            NarrativeEventChoice choice = _activeEvent.Choices[index];
            if (!MeetsRequirements(choice)) return false;

            _choiceResolved = true;
            Tune.SFX(SfxID.event_choice_select);

            List<string> summaryBits = new();
            RunManager rm = RunManager.Instance;

            if (choice.GoldDelta > 0) { rm.AddGold(choice.GoldDelta); summaryBits.Add($"+{choice.GoldDelta}g"); }
            else if (choice.GoldDelta < 0) { rm.SpendGold(-choice.GoldDelta); summaryBits.Add($"{choice.GoldDelta}g"); }

            if (choice.MaxHealthDelta != 0)
            {
                rm.ChangeHeroMaxHealth(choice.MaxHealthDelta, choice.MaxHealthDelta > 0);
                summaryBits.Add(choice.MaxHealthDelta > 0 ? $"+{choice.MaxHealthDelta} Max HP" : $"{choice.MaxHealthDelta} Max HP");
            }

            if (choice.HealthDelta > 0) { rm.HealHero(choice.HealthDelta); summaryBits.Add($"+{choice.HealthDelta} HP"); }
            else if (choice.HealthDelta < 0) { rm.DamageHero(-choice.HealthDelta); summaryBits.Add($"{choice.HealthDelta} HP"); }

            if (choice.StressDelta > 0) { rm.AddStress(choice.StressDelta); summaryBits.Add($"-{choice.StressDelta} Will"); }
            else if (choice.StressDelta < 0) { rm.ReduceStress(-choice.StressDelta); summaryBits.Add($"+{-choice.StressDelta} Will"); }

            if (choice.GrantRandomRelic && relicPool != null &&
                relicPool.TryGetRandomRelic(RunRelicManager.EnsureInstance(), out RelicData relic, RelicRarityMask.All))
            {
                RunRelicManager.EnsureInstance().AddRelic(relic);
                summaryBits.Add($"Relic: {relic.Title}");
                Tune.SFX(SfxID.relic_claim);
            }

            if (choice.GrantRandomCard && cardPool != null)
            {
                List<MerchantOffer> cards = cardPool.BuildCandidateOffers(RunDeckManager.Instance, true, false);
                if (cards.Count > 0)
                {
                    CardData gained = cards[Random.Range(0, cards.Count)].Card;
                    if (gained != null)
                    {
                        RunDeckManager.Instance.AddCard(gained);
                        summaryBits.Add($"Card: {gained.Title}");
                    }
                }
            }

            string deltaLine = summaryBits.Count > 0 ? $"\n\n<color=#5A3810><b>Outcome: {string.Join("  •  ", summaryBits)}</b></color>" : string.Empty;
            _storyText.text = (choice.ResultStory ?? "The encounter concludes.") + deltaLine;
            _storyText.rectTransform.DOPunchScale(Vector3.one * 0.04f, 0.24f, 4, 0.5f).SetUpdate(true);

            foreach (GameObject obj in _choiceObjects) if (obj != null) obj.SetActive(false);
            if (_continueBtnGo != null)
            {
                _continueBtnGo.SetActive(true);
                _continueBtnGo.transform.localScale = Vector3.one * 0.82f;
                _continueBtnGo.transform.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            RefreshStatusHeader();
            Tune.SFX(SfxID.event_choice_complete);
            return true;
        }

        public void ReturnToMap()
        {
            Tune.SFX(SfxID.return_to_map);
            if (!string.IsNullOrWhiteSpace(mapSceneName)) SceneLoader.LoadScene(mapSceneName);
        }

        private void PickAndDisplayEvent()
        {
            _choiceResolved = false;
            _activeEvent = eventPool != null && eventPool.Count > 0
                ? eventPool[Random.Range(0, eventPool.Count)]
                : null;

            if (_activeEvent == null)
            {
                _titleText.text = "Quiet Clearing";
                _subtitleText.text = "EVENT";
                _storyText.text = "You find a calm lantern resting beside the path.";
                _continueBtnGo.SetActive(true);
                return;
            }

            _titleText.text = _activeEvent.Title.ToUpper();
            _subtitleText.text = string.IsNullOrWhiteSpace(_activeEvent.Subtitle) ? "MYSTERIOUS EVENT" : _activeEvent.Subtitle.ToUpper();
            _storyText.text = _activeEvent.StoryText;
            _illustrationImg.sprite = _activeEvent.Illustration != null ? _activeEvent.Illustration : fallbackIllustration;
            _illustrationImg.enabled = _illustrationImg.sprite != null;

            RebuildChoices();
            RefreshStatusHeader();
        }

        private void RebuildChoices()
        {
            foreach (GameObject g in _choiceObjects) if (g != null) Destroy(g);
            _choiceObjects.Clear();
            _continueBtnGo.SetActive(false);

            int layer = _choicesContainer.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;

            for (int i = 0; i < _activeEvent.Choices.Count; i++)
            {
                NarrativeEventChoice c = _activeEvent.Choices[i];
                bool unlocked = MeetsRequirements(c);

                RectTransform rt = CreateRect(_choicesContainer, $"Choice_{i + 1}", layer);
                rt.sizeDelta = new Vector2(1060f, 68f);
                Image bg = rt.gameObject.AddComponent<Image>();
                bg.sprite = buttonSprite != null ? buttonSprite : GetSolid();
                bg.color = unlocked ? new Color(0.96f, 0.89f, 0.74f, 1f) : new Color(0.55f, 0.50f, 0.45f, 0.75f);

                Button btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.interactable = unlocked;
                int captured = i;
                btn.onClick.AddListener(() => SelectChoice(captured));

                TextMeshProUGUI lbl = MakeText(rt, "Label", font, 21f, FontStyles.Bold, unlocked ? new Color(0.22f, 0.12f, 0.04f, 1f) : new Color(0.32f, 0.28f, 0.24f, 0.85f));
                lbl.rectTransform.offsetMin = new Vector2(28f, 0f);
                lbl.rectTransform.offsetMax = new Vector2(-28f, 0f);
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                string lockTag = !unlocked ? $"  <color=#A82020>(Requires {RequirementText(c)})</color>" : string.Empty;
                lbl.text = $"<b>{c.Label}</b>   <color=#5A3A12>{c.OutcomePreview}</color>{lockTag}";

                rt.localScale = Vector3.one * 0.9f;
                rt.DOScale(Vector3.one, 0.26f).SetDelay(0.08f + i * 0.06f).SetEase(Ease.OutBack).SetUpdate(true);
                _choiceObjects.Add(rt.gameObject);
            }
        }

        private static bool MeetsRequirements(NarrativeEventChoice c)
        {
            RunManager rm = RunManager.Instance;
            if (rm == null) return true;
            if (c.RequiredMinGold > 0 && rm.Gold < c.RequiredMinGold) return false;
            if (c.RequiredMinHealth > 0 && rm.HeroCurrentHealth <= c.RequiredMinHealth) return false;
            return true;
        }

        private static string RequirementText(NarrativeEventChoice c)
        {
            if (c.RequiredMinGold > 0) return $"{c.RequiredMinGold}g";
            if (c.RequiredMinHealth > 0) return $">{c.RequiredMinHealth} HP";
            return "Condition";
        }

        private void RefreshStatusHeader()
        {
            RunManager rm = RunManager.Instance;
            if (_statusBarText == null || rm == null) return;
            _statusBarText.text = $"HP {rm.HeroCurrentHealth}/{Mathf.Max(1, rm.HeroMaxHealth)}   •   WILL {rm.HeroCurrentWill}/{rm.HeroMaxWill}   •   GOLD {rm.Gold}g";
        }

        private void EnsureRelicSwapModal()
        {
            if (FindFirstObjectByType<RelicSwapModalUI>() != null || targetCanvas == null) return;
            GameObject go = new("RelicSwapModalUI");
            go.transform.SetParent(targetCanvas.transform, false);
            go.AddComponent<RelicSwapModalUI>();
        }

        private void BuildUIIfNeeded()
        {
            if (_built || targetCanvas == null) return;
            _built = true;

            int layer = targetCanvas.gameObject.layer;
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            Sprite solid = GetSolid();

            _root = CreateRect(targetCanvas.transform, "NarrativeEventRoot", layer);
            Stretch(_root);

            RectTransform bgRt = CreateRect(_root, "Backdrop", layer);
            Stretch(bgRt);
            bgRt.localScale = new Vector3(1.05f, 1.05f, 1f);
            Image bg = bgRt.gameObject.AddComponent<Image>();
            bg.sprite = backgroundSprite != null ? backgroundSprite : solid;
            bg.color = new Color(0.55f, 0.48f, 0.42f, 1f);
            bg.raycastTarget = false;

            RectTransform wash = CreateRect(_root, "Wash", layer);
            Stretch(wash);
            Image w = wash.gameObject.AddComponent<Image>();
            w.sprite = solid;
            w.color = new Color(0.08f, 0.05f, 0.04f, 0.62f);
            w.raycastTarget = false;

            // Main Storybook Frame (Panel.png)
            RectTransform frame = CreateRect(_root, "EventStoryFrame", layer);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1260f, 880f);
            frame.gameObject.AddComponent<Image>().sprite = panelSprite != null ? panelSprite : solid;

            // Title Banner
            RectTransform banner = CreateRect(frame, "TitleBanner", layer);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 0.52f);
            banner.sizeDelta = new Vector2(580f, 68f);
            banner.gameObject.AddComponent<Image>().sprite = nameplateSprite != null ? nameplateSprite : solid;
            _titleText = MakeText(banner, "Title", font, 26f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            _titleText.alignment = TextAlignmentOptions.Center;

            // Status strip
            _statusBarText = MakeText(frame, "StatusBar", font, 18f, FontStyles.Bold, new Color(1f, 0.88f, 0.52f, 0.95f));
            _statusBarText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _statusBarText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _statusBarText.rectTransform.offsetMin = new Vector2(44f, -86f);
            _statusBarText.rectTransform.offsetMax = new Vector2(-44f, -50f);
            _statusBarText.alignment = TextAlignmentOptions.Center;

            // Upper Content: Illustration (Left) + Story Parchment Box (Right)
            RectTransform illFrame = CreateRect(frame, "IllustrationFrame", layer);
            illFrame.anchorMin = new Vector2(0f, 0.44f);
            illFrame.anchorMax = new Vector2(0.32f, 0.87f);
            illFrame.offsetMin = new Vector2(48f, 0f);
            illFrame.offsetMax = new Vector2(-12f, 0f);
            Image illBg = illFrame.gameObject.AddComponent<Image>();
            illBg.sprite = buttonSprite != null ? buttonSprite : solid;
            illBg.color = new Color(0.20f, 0.13f, 0.08f, 0.96f);

            RectTransform illRt = CreateRect(illFrame, "Art", layer);
            Stretch(illRt, 22f);
            _illustrationImg = illRt.gameObject.AddComponent<Image>();
            _illustrationImg.preserveAspect = true;
            _illustrationImg.raycastTarget = false;

            RectTransform storyBox = CreateRect(frame, "StoryBox", layer);
            storyBox.anchorMin = new Vector2(0.33f, 0.44f);
            storyBox.anchorMax = new Vector2(1f, 0.87f);
            storyBox.offsetMin = new Vector2(12f, 0f);
            storyBox.offsetMax = new Vector2(-48f, 0f);
            Image sbImg = storyBox.gameObject.AddComponent<Image>();
            sbImg.sprite = dialogueBoxSprite != null ? dialogueBoxSprite : (buttonSprite != null ? buttonSprite : solid);
            sbImg.color = Color.white;

            _subtitleText = MakeText(storyBox, "Subtitle", font, 15f, FontStyles.Bold, new Color(0.54f, 0.34f, 0.12f, 0.95f));
            _subtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitleText.rectTransform.offsetMin = new Vector2(44f, -64f);
            _subtitleText.rectTransform.offsetMax = new Vector2(-60f, -34f);
            _subtitleText.alignment = TextAlignmentOptions.MidlineLeft;

            _storyText = MakeText(storyBox, "StoryBody", font, 22f, FontStyles.Normal, new Color(0.20f, 0.11f, 0.04f, 1f));
            _storyText.rectTransform.anchorMin = Vector2.zero;
            _storyText.rectTransform.anchorMax = Vector2.one;
            _storyText.rectTransform.offsetMin = new Vector2(44f, 36f);
            _storyText.rectTransform.offsetMax = new Vector2(-68f, -70f);
            _storyText.alignment = TextAlignmentOptions.TopLeft;
            _storyText.textWrappingMode = TextWrappingModes.Normal;
            _storyText.enableAutoSizing = true;
            _storyText.fontSizeMin = 17f;
            _storyText.fontSizeMax = 23f;

            // Choices Container (Bottom 40%)
            RectTransform choicesRt = CreateRect(frame, "ChoicesList", layer);
            choicesRt.anchorMin = new Vector2(0f, 0.06f);
            choicesRt.anchorMax = new Vector2(1f, 0.41f);
            choicesRt.offsetMin = new Vector2(64f, 0f);
            choicesRt.offsetMax = new Vector2(-64f, 0f);
            VerticalLayoutGroup vlg = choicesRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            _choicesContainer = choicesRt;

            // Continue Button (revealed after picking a choice)
            RectTransform contRt = CreateRect(frame, "ContinueButton", layer);
            contRt.anchorMin = contRt.anchorMax = new Vector2(0.5f, 0.22f);
            contRt.sizeDelta = new Vector2(320f, 72f);
            Image cImg = contRt.gameObject.AddComponent<Image>();
            cImg.sprite = buttonSprite != null ? buttonSprite : solid;
            cImg.color = new Color(0.95f, 0.86f, 0.62f, 1f);
            _continueBtn = contRt.gameObject.AddComponent<Button>();
            _continueBtn.onClick.AddListener(ReturnToMap);
            TextMeshProUGUI cTxt = MakeText(contRt, "Label", font, 24f, FontStyles.Bold, new Color(0.24f, 0.13f, 0.04f, 1f));
            cTxt.alignment = TextAlignmentOptions.Center;
            cTxt.text = "CONTINUE TO MAP  >";
            _continueBtnGo = contRt.gameObject;
            _continueBtnGo.SetActive(false);
        }

        private static RectTransform CreateRect(Transform p, string n, int l) { GameObject g = new(n, typeof(RectTransform)); g.layer = l; RectTransform rt = (RectTransform)g.transform; rt.SetParent(p, false); return rt; }
        private static void Stretch(RectTransform rt, float pad = 0f) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad); }
        private static TextMeshProUGUI MakeText(RectTransform p, string n, TMP_FontAsset f, float sz, FontStyles st, Color c) { RectTransform rt = CreateRect(p, n, p.gameObject.layer); Stretch(rt); TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>(); if (f != null) t.font = f; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false; return t; }
        private static Sprite GetSolid() { if (s_solid != null) return s_solid; Texture2D t = Texture2D.whiteTexture; s_solid = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)); return s_solid; }
    }
}

