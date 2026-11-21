using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Levels;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Map;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlushieOdyssey.Map
{
    public class MapSystem : MonoBehaviour
    {
        [Header("Procedural Generation")]
        [SerializeField] private MapGeneratorConfig generatorConfig;
        [SerializeField] private MapEncounterPool encounterPool;

        [Header("Views (ProceduralMapRendererUI preferred; MapView supported as legacy fallback)")]
        [SerializeField] private ProceduralMapRendererUI proceduralRenderer;
        [SerializeField] private MapView mapView;
        [SerializeField] private TMP_Text mapTitleText;

        [Header("Scene Routing")]
        [SerializeField] private string combatSceneName = "Game";
        [SerializeField] private string merchantSceneName = "Merchant";
        [SerializeField] private string restSceneName = "Rest";
        [SerializeField] private string eventSceneName = "Event";
        [SerializeField] private string treasureSceneName = "Treasure";
        [SerializeField] private string levelSelectSceneName = "Levels";
        [SerializeField] private bool completeCurrentLevelOnBossCleared = true;

        [Header("Chapter Auto-Progression")]
        [Tooltip("When enabled, defeating a chapter's Boss immediately enters the next chapter (2, 3, 4, ...) without returning to the chapter-selection screen. Only returns to Levels after the final chapter is cleared.")]
        [SerializeField] private bool autoAdvanceToNextChapterOnBossCleared = true;
        [SerializeField] private List<LevelDefinition> fallbackLevels = new();

        private MapGraph graph;
        private GameObject chapterBannerRoot;
        private bool isActivatingNode;

        public MapGraph CurrentGraph => graph;
        public string ActiveChapterHeaderText => mapTitleText != null ? mapTitleText.text : string.Empty;
        public bool IsChapterBannerVisible => chapterBannerRoot != null && chapterBannerRoot.activeSelf;
        public bool IsActivatingNode => isActivatingNode;

        private void Start()
        {
            if (proceduralRenderer == null)
            {
                proceduralRenderer = FindFirstObjectByType<ProceduralMapRendererUI>();
            }

            ResolveMapTitleText();

            LevelProgressionManager progression = LevelProgressionManager.Instance;
            if (progression != null)
            {
                progression.EnsureActiveLevel(fallbackLevels);
            }

            if (RunManager.Instance == null)
            {
                // Allow standalone Map.unity preview even if GameManager wasn't loaded first
                graph = GenerateMapForLevel(progression?.CurrentLevel);
                UpdateChapterHeader(progression?.CurrentLevel, null);
                BeginStaggeredMapEntrance(null, null, 0, string.Empty);
                return;
            }

            if (!RunManager.Instance.HasActiveRun)
            {
                LevelDefinition startLevel = progression?.CurrentLevel;
                RunManager.Instance.StartNewRun(GenerateMapForLevel(startLevel));
                if (progression != null && progression.HasCampaignPlayerState)
                {
                    progression.RestoreCampaignStateToActiveRun();
                }

                if (startLevel != null)
                {
                    RunManager.Instance.SetChapterInfo(startLevel.LevelNumber, startLevel.Description);
                }
            }
            else if (progression != null && progression.HasCurrentLevel && string.IsNullOrWhiteSpace(RunManager.Instance.CurrentChapterName))
            {
                RunManager.Instance.SetChapterInfo(progression.CurrentLevel.LevelNumber, progression.CurrentLevel.Description);
            }

            graph = RunManager.Instance.CurrentMap;

            LevelDefinition newlyEnteredChapter = null;

            // Returning to Map after beating the chapter's Boss
            if (completeCurrentLevelOnBossCleared && graph.IsBossCleared())
            {
                if (autoAdvanceToNextChapterOnBossCleared)
                {
                    if (TryAdvanceDirectlyToNextChapter(progression, out newlyEnteredChapter))
                    {
                        // If the next chapter routes to a different Map scene, scene load was triggered inside TryAdvanceDirectlyToNextChapter.
                        if (newlyEnteredChapter != null &&
                            !string.IsNullOrWhiteSpace(newlyEnteredChapter.MapSceneName) &&
                            !string.Equals(newlyEnteredChapter.MapSceneName, SceneManager.GetActiveScene().name, System.StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        graph = RunManager.Instance.CurrentMap;
                    }
                    else
                    {
                        // Final chapter boss defeated — campaign complete! Open Victory Run Summary screen.
                        PlushieOdyssey.UI.Menus.RunEndSummaryUI.PendingVictoryMode = true;
                        SceneLoader.LoadScene("GameOver");
                        return;
                    }
                }
                else if (progression != null && progression.HasCurrentLevel)
                {
                    progression.CompleteCurrentLevel();
                    if (!string.IsNullOrWhiteSpace(levelSelectSceneName))
                    {
                        SceneLoader.LoadScene(levelSelectSceneName);
                        return;
                    }
                }
            }

            if (newlyEnteredChapter == null && progression != null)
            {
                newlyEnteredChapter = progression.ConsumePendingChapterIntro();
            }

            LevelDefinition activeLevel = progression?.CurrentLevel;
            UpdateChapterHeader(activeLevel, RunManager.Instance);

            graph.RefreshAvailability(RunManager.Instance.CurrentMapNodeId);

            int bannerChapterNum = 0;
            string bannerSubtitle = string.Empty;
            if (newlyEnteredChapter != null)
            {
                bannerChapterNum = newlyEnteredChapter.LevelNumber;
                bannerSubtitle = newlyEnteredChapter.Description;
            }
            else if (RunManager.Instance.CurrentChapterNumber > 1 && string.IsNullOrEmpty(RunManager.Instance.CurrentMapNodeId) && (activeLevel == null))
            {
                bannerChapterNum = RunManager.Instance.CurrentChapterNumber;
                bannerSubtitle = RunManager.Instance.CurrentChapterName;
            }

            BeginStaggeredMapEntrance(RunManager.Instance.CurrentMapNodeId, newlyEnteredChapter, bannerChapterNum, bannerSubtitle);
        }

        private void BeginStaggeredMapEntrance(string currentNodeId, LevelDefinition newlyEnteredChapter, int bannerChapterNum, string bannerSubtitle)
        {
            // Pre-hide map title & renderer while eyelids are still closed so the entire staggered cascade
            // plays live as the blink opens instead of everything popping on screen at once!
            CanvasGroup titleCg = null;
            RectTransform titleRt = mapTitleText != null ? mapTitleText.rectTransform : null;
            Vector2 titleHomePos = titleRt != null ? titleRt.anchoredPosition : Vector2.zero;

            if (titleRt != null)
            {
                if (!titleRt.TryGetComponent(out titleCg))
                {
                    titleCg = titleRt.gameObject.AddComponent<CanvasGroup>();
                }
                titleCg.DOKill();
                titleRt.DOKill();
                titleCg.alpha = 0f;
                titleRt.anchoredPosition = titleHomePos + new Vector2(0f, 36f);
            }

            if (proceduralRenderer != null)
            {
                proceduralRenderer.PrepareHiddenForEntrance();
            }

            BlinkSceneTransition.RunWhenRevealing(this, () =>
            {
                if (this == null || !isActiveAndEnabled) return;

                if (titleRt != null && titleCg != null)
                {
                    titleCg.DOFade(1f, 0.28f).SetDelay(0.04f).SetEase(Ease.OutQuad).SetUpdate(true);
                    titleRt.DOAnchorPos(titleHomePos, 0.34f).SetDelay(0.04f).SetEase(Ease.OutCubic).SetUpdate(true);
                }

                RenderCurrentGraph(currentNodeId, animateEntrance: true);

                if (bannerChapterNum > 0)
                {
                    ShowChapterTransitionBanner(bannerChapterNum, bannerSubtitle);
                }
            });
        }

        private bool TryAdvanceDirectlyToNextChapter(LevelProgressionManager progression, out LevelDefinition nextLevel)
        {
            nextLevel = null;

            if (progression != null && progression.HasCurrentLevel)
            {
                bool hasNext = progression.CompleteCurrentLevelAndAdvance(out nextLevel, loadMapScene: false, abandonRunForNextChapter: false);
                if (!hasNext || nextLevel == null)
                {
                    return false;
                }

                // Consume the pending intro flag now if we're staying in the same Map scene; keep it if loading a different scene.
                bool loadsDifferentMapScene = !string.IsNullOrWhiteSpace(nextLevel.MapSceneName) &&
                                              !string.Equals(nextLevel.MapSceneName, SceneManager.GetActiveScene().name, System.StringComparison.OrdinalIgnoreCase);

                MapGraph nextMap = GenerateMapForLevel(nextLevel);
                RunManager.Instance.AdvanceToNextChapterMap(
                    nextMap,
                    nextLevel.LevelNumber,
                    nextLevel.Description,
                    resetPlayerState: !progression.UsesCampaignPlayerState);

                if (loadsDifferentMapScene)
                {
                    SceneLoader.LoadScene(nextLevel.MapSceneName);
                    return true;
                }

                progression.ConsumePendingChapterIntro();
                return true;
            }

            // Fallback when LevelProgressionManager has no configured levels: continue seamlessly to Chapter N+1.
            int nextChapterNum = RunManager.Instance.CurrentChapterNumber + 1;
            MapGraph endlessMap = GenerateMapForLevel(null);
            RunManager.Instance.AdvanceToNextChapterMap(endlessMap, nextChapterNum, string.Empty, resetPlayerState: false);
            return true;
        }

        private MapGraph GenerateMapForLevel(LevelDefinition level)
        {
            MapGeneratorConfig effectiveConfig = level != null && level.MapGeneratorConfigOverride != null
                ? level.MapGeneratorConfigOverride
                : generatorConfig;
            MapEncounterPool effectivePool = level != null && level.EncounterPoolOverride != null
                ? level.EncounterPoolOverride
                : encounterPool;
            return ProceduralMapGenerator.Generate(effectiveConfig, effectivePool);
        }

        private void ResolveMapTitleText()
        {
            if (mapTitleText != null) return;

            GameObject titleGo = GameObject.Find("MapTitle");
            if (titleGo != null)
            {
                mapTitleText = titleGo.GetComponent<TMP_Text>();
            }
        }

        private void UpdateChapterHeader(LevelDefinition level, RunManager runManager)
        {
            ResolveMapTitleText();
            if (mapTitleText == null) return;

            string chapterPrefix;
            if (level != null)
            {
                chapterPrefix = level.GetChapterHeaderLabel();
            }
            else if (runManager != null)
            {
                chapterPrefix = runManager.GetChapterHeaderLabel();
            }
            else
            {
                chapterPrefix = "CHAPTER 1";
            }

            mapTitleText.text = $"{chapterPrefix}   •   START (LEFT)  →  BOSS (RIGHT)";
        }

        private void ShowChapterTransitionBanner(int chapterNumber, string chapterSubtitle)
        {
            Canvas rootCanvas = proceduralRenderer != null
                ? proceduralRenderer.GetComponentInParent<Canvas>()?.rootCanvas
                : FindFirstObjectByType<Canvas>()?.rootCanvas;
            if (rootCanvas == null) return;

            if (chapterBannerRoot != null)
            {
                Destroy(chapterBannerRoot);
                chapterBannerRoot = null;
            }

            TMP_FontAsset font = mapTitleText != null && mapTitleText.font != null
                ? mapTitleText.font
                : TMP_Settings.defaultFontAsset;

            chapterBannerRoot = new GameObject("ChapterTransitionBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            chapterBannerRoot.layer = rootCanvas.gameObject.layer;
            RectTransform rt = (RectTransform)chapterBannerRoot.transform;
            rt.SetParent(rootCanvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 18f);
            rt.sizeDelta = new Vector2(640f, 132f);
            rt.SetAsLastSibling();

            CanvasGroup cg = chapterBannerRoot.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;

            Image bg = chapterBannerRoot.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.08f, 0.05f, 0.92f);
            bg.raycastTarget = false;

            Outline outline = chapterBannerRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.68f, 0.32f, 0.95f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            GameObject titleGo = new("ChapterTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.layer = rootCanvas.gameObject.layer;
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.SetParent(rt, false);
            titleRt.anchorMin = new Vector2(0f, 0.42f);
            titleRt.anchorMax = new Vector2(1f, 0.92f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            if (font != null) titleTmp.font = font;
            titleTmp.text = $"CHAPTER {chapterNumber}";
            titleTmp.fontSize = 40f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.98f, 0.85f, 0.48f, 1f);
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            GameObject subGo = new("ChapterSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.layer = rootCanvas.gameObject.layer;
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.SetParent(rt, false);
            subRt.anchorMin = new Vector2(0f, 0.08f);
            subRt.anchorMax = new Vector2(1f, 0.45f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;
            TextMeshProUGUI subTmp = subGo.GetComponent<TextMeshProUGUI>();
            if (font != null) subTmp.font = font;
            subTmp.text = string.IsNullOrWhiteSpace(chapterSubtitle)
                ? "A New Journey Begins"
                : chapterSubtitle.Trim().ToUpperInvariant();
            subTmp.fontSize = 22f;
            subTmp.fontStyle = FontStyles.Bold;
            subTmp.color = new Color(0.94f, 0.91f, 0.84f, 0.96f);
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.raycastTarget = false;

            rt.localScale = new Vector3(0.84f, 0.84f, 1f);
            Tune.SFX(SfxID.combat_won);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(cg.DOFade(1f, 0.24f));
            seq.Join(rt.DOScale(Vector3.one, 0.34f).SetEase(Ease.OutBack));
            seq.AppendInterval(2.1f);
            seq.Append(cg.DOFade(0f, 0.42f));
            seq.Join(rt.DOAnchorPosY(46f, 0.42f).SetEase(Ease.InCubic));
            seq.OnComplete(() =>
            {
                if (chapterBannerRoot != null)
                {
                    Destroy(chapterBannerRoot);
                    chapterBannerRoot = null;
                }
            });
        }

        public void SelectNode(MapNode selectedNode)
        {
            if (isActivatingNode || BlinkSceneTransition.IsTransitioning) return;
            if (selectedNode == null || graph == null) return;
            if (!selectedNode.IsAvailable || selectedNode.IsVisited) return;

            isActivatingNode = true;
            graph.MarkVisited(selectedNode.Id);
            if (RunManager.Instance != null)
            {
                RunManager.Instance.SelectMapNode(selectedNode);
            }

            graph.RefreshAvailability(selectedNode.Id);

            if (proceduralRenderer != null)
            {
                proceduralRenderer.PlayNodeActivationTransition(selectedNode, () =>
                {
                    isActivatingNode = false;
                    HandleNode(selectedNode);
                });
                return;
            }

            isActivatingNode = false;
            RenderCurrentGraph(selectedNode.Id, animateEntrance: false);
            HandleNode(selectedNode);
        }

        public void StartNewRun()
        {
            LevelDefinition activeLevel = LevelProgressionManager.Instance?.CurrentLevel;
            MapGraph fresh = GenerateMapForLevel(activeLevel);
            if (RunManager.Instance != null)
            {
                RunManager.Instance.StartNewRun(fresh);
                if (activeLevel != null)
                {
                    RunManager.Instance.SetChapterInfo(activeLevel.LevelNumber, activeLevel.Description);
                }
                graph = RunManager.Instance.CurrentMap;
            }
            else
            {
                graph = fresh;
            }

            UpdateChapterHeader(activeLevel, RunManager.Instance);
            graph.RefreshAvailability(null);
            RenderCurrentGraph(null, animateEntrance: true);
        }

        private void RenderCurrentGraph(string currentNodeId, bool animateEntrance)
        {
            if (proceduralRenderer != null)
            {
                proceduralRenderer.RenderMap(graph, currentNodeId, interactive: true, onNodeClicked: SelectNode, animateEntrance: animateEntrance);
                return;
            }

            if (mapView != null)
            {
                mapView.ShowMap(graph, this);
            }
        }

        private void HandleNode(MapNode node)
        {
            if (node.StartsCombat)
            {
                if (node.Encounter == null)
                {
                    global::PlushieOdyssey.Core.Logger.LogWarning($"Map node {node.Id} ({node.Type}) has no EncounterData. Combat will use MatchSetupSystem fallback enemies.");
                }

                string sceneToLoad = combatSceneName;
                if (PlayerPrefs.GetInt("HasPlayedCardTutorial", 0) == 0 && !string.IsNullOrEmpty(sceneToLoad))
                {
                    sceneToLoad = combatSceneName; // Route straight to active Game.unity scene
                }

                SceneLoader.LoadScene(sceneToLoad);
                return;
            }

            RunManager.Instance?.ClearSelectedEncounter();

            string targetScene = node.Type switch
            {
                MapNodeType.Shop => merchantSceneName,
                MapNodeType.Rest => restSceneName,
                MapNodeType.Event => eventSceneName,
                MapNodeType.Treasure => treasureSceneName,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(targetScene))
            {
                SceneLoader.LoadScene(targetScene);
            }
            else
            {
                global::PlushieOdyssey.Core.Logger.Log($"Resolved map node: {node.Type}.");
            }
        }
    }
}
