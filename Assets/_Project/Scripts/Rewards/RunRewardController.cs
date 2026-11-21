using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Relics;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace PlushieOdyssey.Rewards
{
    public enum RunRewardType
    {
        Gold,
        Heal,
        ReduceStress,
        CardReward,
        Relic,
        Potion,
        BossRelicChest
    }

    [Serializable]
    public class RunRewardDefinition
    {
        public RunRewardType Type = RunRewardType.CardReward;
        public string TitleOverride;
        [TextArea(2, 4)] public string DescriptionOverride;
        [Min(0)] public int Amount = 20;
        public CardRewardRequest CardRewardRequest = new();
        public RelicData RelicOverride;
        public PotionData PotionOverride;

        public string GetTitle()
        {
            if (!string.IsNullOrWhiteSpace(TitleOverride)) return TitleOverride;

            return Type switch
            {
                RunRewardType.Gold => $"{Amount} Gold",
                RunRewardType.Heal => Amount > 0 ? $"Heal {Amount} HP" : "Full Heal",
                RunRewardType.ReduceStress => Amount > 0 ? $"Restore {Amount} Will" : "Restore Full Will",
                RunRewardType.CardReward => "Card Reward",
                RunRewardType.Relic => RelicOverride != null ? $"Relic: {RelicOverride.Title}" : "Relic Reward",
                RunRewardType.Potion => PotionOverride != null ? $"Potion: {PotionOverride.Title}" : "Potion Reward",
                RunRewardType.BossRelicChest => "Boss Relic Chest (Choose 1 of 3)",
                _ => "Reward"
            };
        }

        public string GetDescription()
        {
            if (!string.IsNullOrWhiteSpace(DescriptionOverride)) return DescriptionOverride;

            return Type switch
            {
                RunRewardType.Gold => $"Gain {Amount} gold for this run.",
                RunRewardType.Heal => Amount > 0 ? $"Restore {Amount} HP." : "Restore HP to full.",
                RunRewardType.ReduceStress => Amount > 0 ? $"Restore {Amount} Will." : "Restore Will to maximum.",
                RunRewardType.CardReward => "Choose one card to add to your run deck. You may skip the card choice.",
                RunRewardType.Relic => RelicOverride != null && !string.IsNullOrWhiteSpace(RelicOverride.Description) ? RelicOverride.Description : "Claim a permanent relic for this run.",
                RunRewardType.Potion => PotionOverride != null && !string.IsNullOrWhiteSpace(PotionOverride.Description) ? PotionOverride.Description : "Add a combat potion to your belt.",
                RunRewardType.BossRelicChest => "Open the Boss Chest and pick 1 of 3 high-impact Boss Relics with trade-offs.",
                _ => string.Empty
            };
        }
    }

    [DisallowMultipleComponent]
    public class RunRewardController : MonoBehaviour
    {
        [Header("Reward Source")]
        [SerializeField] private List<RunRewardDefinition> defaultRewards = new()
        {
            new RunRewardDefinition { Type = RunRewardType.Gold, Amount = 20 },
            new RunRewardDefinition { Type = RunRewardType.CardReward }
        };

        [Header("Card Reward Scene")]
        [SerializeField] private string cardRewardSceneName = "CardReward";
        [SerializeField] private RelicRewardPool relicRewardPool;
        [SerializeField] private List<RelicData> bossRelicChoices = new();
        [SerializeField] private PotionRewardPool potionRewardPool;
        [SerializeField] private bool grantRelicOnEliteOrBoss = true;

        [Header("UI")]
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private Transform rewardContainer;
        [SerializeField] private RunRewardOptionView rewardOptionPrefab;
        [SerializeField] private bool autoFindOptionViewsInChildren = true;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private string title = "Rewards";

        [Header("Navigation")]
        [SerializeField] private string mapSceneName = "Map";
        [SerializeField] private bool autoOpenOnStart = true;

        [Header("Events")]
        public UnityEvent OnRewardsOpened;
        public UnityEvent OnRewardClaimed;
        public UnityEvent OnRewardsSkipped;
        public UnityEvent OnRewardsCompleted;

        [Header("Audio")]
        [SerializeField] private TuneSfxCue rewardsOpenedSfx;
        [SerializeField] private TuneSfxCue rewardClaimedSfx;
        [SerializeField] private TuneSfxCue rewardsSkippedSfx;
        [SerializeField] private TuneSfxCue rewardsCompletedSfx;
        [SerializeField] private TuneSfxCue cardRewardOpeningSfx;

        private readonly List<RunRewardDefinition> activeRewards = new();
        private readonly List<RunRewardOptionView> optionViews = new();
        private readonly HashSet<int> claimedRewardIndices = new();
        private bool isOpeningCardReward;

        private void Awake()
        {
            if (rewardRoot == null) rewardRoot = gameObject;
            if (rewardContainer == null) rewardContainer = transform;
            CollectOptionViews();
        }

        private void Start()
        {
            if (autoOpenOnStart)
            {
                OpenRewards();
            }
        }

        public void OpenRewards()
        {
            OpenRewards(defaultRewards);
        }

        public void OpenRewards(List<RunRewardDefinition> rewards)
        {
            activeRewards.Clear();
            claimedRewardIndices.Clear();

            if (rewards != null)
            {
                foreach (RunRewardDefinition reward in rewards)
                {
                    if (reward != null) activeRewards.Add(reward);
                }
            }

            // Automatically append a Boss 3-Relic Choice Chest after Act Bosses, or a random Relic drop after Elites
            if (grantRelicOnEliteOrBoss &&
                RunManager.Instance != null && RunManager.Instance.CurrentMap != null &&
                !string.IsNullOrEmpty(RunManager.Instance.CurrentMapNodeId))
            {
                var node = RunManager.Instance.CurrentMap.GetNode(RunManager.Instance.CurrentMapNodeId);
                if (node != null && node.Type == PlushieOdyssey.Map.MapNodeType.Boss &&
                    !activeRewards.Exists(r => r.Type == RunRewardType.BossRelicChest))
                {
                    activeRewards.Add(new RunRewardDefinition { Type = RunRewardType.BossRelicChest });
                }
                else if (node != null && node.Type == PlushieOdyssey.Map.MapNodeType.Elite &&
                         relicRewardPool != null && !activeRewards.Exists(r => r.Type == RunRewardType.Relic))
                {
                    if (relicRewardPool.TryGetRandomRelic(RunRelicManager.EnsureInstance(), out RelicData dropRelic, RelicRarityMask.All))
                    {
                        activeRewards.Add(new RunRewardDefinition { Type = RunRewardType.Relic, RelicOverride = dropRelic });
                    }
                }
            }

            if (rewardRoot != null) rewardRoot.SetActive(true);
            if (titleText != null) titleText.text = title;

            RefreshRewardViews();
            PlayStaggeredRewardsEntrance();
            rewardsOpenedSfx?.Play(this, transform);
            OnRewardsOpened?.Invoke();
        }

        private void PlayStaggeredRewardsEntrance()
        {
            if (rewardRoot != null)
            {
                Transform rootTf = rewardRoot.transform;
                rootTf.DOKill();
                if (!rewardRoot.TryGetComponent(out CanvasGroup rootCg)) rootCg = rewardRoot.AddComponent<CanvasGroup>();
                rootCg.DOKill();
                rootCg.alpha = 0f;
                rootCg.DOFade(1f, 0.24f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            if (titleText != null)
            {
                Transform titleTf = titleText.transform;
                titleTf.DOKill();
                titleTf.localScale = Vector3.one * 0.80f;
                titleTf.DOScale(Vector3.one, 0.32f).SetDelay(0.06f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            int shown = 0;
            for (int i = 0; i < optionViews.Count; i++)
            {
                RunRewardOptionView view = optionViews[i];
                if (view == null || !view.gameObject.activeSelf) continue;
                Transform tf = view.transform;
                tf.DOKill();
                tf.localScale = Vector3.one * 0.78f;
                if (!view.TryGetComponent(out CanvasGroup cg)) cg = view.gameObject.AddComponent<CanvasGroup>();
                cg.DOKill();
                cg.alpha = 0f;
                float delay = 0.14f + shown * 0.085f;
                tf.DOScale(Vector3.one, 0.30f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true);
                cg.DOFade(1f, 0.22f).SetDelay(delay).SetUpdate(true);
                shown++;
            }
        }

        public void ClaimReward(int rewardIndex)
        {
            if (isOpeningCardReward) return;
            if (rewardIndex < 0 || rewardIndex >= activeRewards.Count) return;
            if (claimedRewardIndices.Contains(rewardIndex)) return;

            RunRewardDefinition reward = activeRewards[rewardIndex];
            if (reward == null) return;

            switch (reward.Type)
            {
                case RunRewardType.Gold:
                    if (!TryGetRunManager(out RunManager goldRunManager)) return;
                    goldRunManager.AddGold(reward.Amount);
                    MarkRewardClaimed(rewardIndex);
                    break;

                case RunRewardType.Heal:
                    if (!TryGetRunManager(out RunManager healRunManager)) return;
                    if (reward.Amount > 0) healRunManager.HealHero(reward.Amount);
                    else healRunManager.HealHeroToFull();
                    MarkRewardClaimed(rewardIndex);
                    break;

                case RunRewardType.ReduceStress:
                    if (!TryGetRunManager(out RunManager stressRunManager)) return;
                    if (reward.Amount > 0) stressRunManager.ReduceStress(reward.Amount);
                    else stressRunManager.ClearStress();
                    MarkRewardClaimed(rewardIndex);
                    break;

                case RunRewardType.CardReward:
                    StartCoroutine(OpenCardRewardScene(rewardIndex, reward.CardRewardRequest));
                    break;

                case RunRewardType.Relic:
                    ClaimRelicReward(rewardIndex, reward);
                    break;

                case RunRewardType.Potion:
                    ClaimPotionReward(rewardIndex, reward);
                    break;

                case RunRewardType.BossRelicChest:
                    ClaimBossRelicChest(rewardIndex);
                    break;
            }
        }

        private void ClaimBossRelicChest(int rewardIndex)
        {
            BossRelicPickerUI picker = BossRelicPickerUI.EnsureInstance(GetComponentInParent<Canvas>()?.rootCanvas);
            picker.Open(relicRewardPool, chosenRelic =>
            {
                MarkRewardClaimed(rewardIndex);
            }, bossRelicChoices);
        }

        private void ClaimRelicReward(int rewardIndex, RunRewardDefinition reward)
        {
            RunRelicManager mgr = RunRelicManager.EnsureInstance();
            RelicData relic = reward.RelicOverride;
            if (relic == null && relicRewardPool != null)
            {
                relicRewardPool.TryGetRandomRelic(mgr, out relic, RelicRarityMask.All);
                reward.RelicOverride = relic;
            }
            if (relic == null) return;

            if (FindFirstObjectByType<RelicSwapModalUI>() == null)
            {
                Canvas c = FindFirstObjectByType<Canvas>();
                if (c != null)
                {
                    GameObject mGo = new("RelicSwapModalUI");
                    mGo.transform.SetParent(c.transform, false);
                    mGo.AddComponent<RelicSwapModalUI>();
                }
            }

            if (mgr.AddRelic(relic))
            {
                Tune.SFX(SfxID.relic_claim);
                MarkRewardClaimed(rewardIndex);
            }
            else if (mgr.IsFull)
            {
                // Swap modal opened automatically; mark claimed so player cannot double-roll
                MarkRewardClaimed(rewardIndex);
            }
        }

        private void ClaimPotionReward(int rewardIndex, RunRewardDefinition reward)
        {
            RunPotionManager mgr = RunPotionManager.EnsureInstance();
            PotionData potion = reward.PotionOverride;
            if (potion == null && potionRewardPool != null)
            {
                potionRewardPool.TryGetRandomPotion(mgr, out potion, PotionRarityMask.All);
                reward.PotionOverride = potion;
            }
            if (potion != null && mgr.AddPotion(potion))
            {
                Tune.SFX(SfxID.potion_claim);
                MarkRewardClaimed(rewardIndex);
            }
        }

        public void SkipRemainingRewards()
        {
            rewardsSkippedSfx?.Play(this, transform);
            OnRewardsSkipped?.Invoke();
            ContinueToMap();
        }

        public void ContinueToMap()
        {
            rewardsCompletedSfx?.Play(this, transform);
            OnRewardsCompleted?.Invoke();
            if (!string.IsNullOrWhiteSpace(mapSceneName))
            {
                SceneLoader.LoadScene(mapSceneName);
            }
        }

        private IEnumerator OpenCardRewardScene(int rewardIndex, CardRewardRequest request)
        {
            isOpeningCardReward = true;
            cardRewardOpeningSfx?.Play(this, transform);

            AsyncOperation loadOperation = SceneLoader.LoadSceneAdditive(cardRewardSceneName);
            if (loadOperation == null)
            {
                isOpeningCardReward = false;
                yield break;
            }

            yield return loadOperation;

            CardRewardController cardRewardController = FindCardRewardController(cardRewardSceneName);
            if (cardRewardController == null)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning($"Could not find a CardRewardController in additive scene '{cardRewardSceneName}'.");
                isOpeningCardReward = false;
                yield break;
            }

            bool finished = false;
            void HandleCardRewardFinished(bool _)
            {
                finished = true;
            }

            cardRewardController.RewardFinished += HandleCardRewardFinished;
            cardRewardController.ConfigureForAdditiveReward(request ?? new CardRewardRequest());

            while (!finished)
            {
                yield return null;
            }

            if (cardRewardController != null)
            {
                cardRewardController.RewardFinished -= HandleCardRewardFinished;
            }

            MarkRewardClaimed(rewardIndex);
            isOpeningCardReward = false;
        }

        private CardRewardController FindCardRewardController(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    CardRewardController controller = rootObject.GetComponentInChildren<CardRewardController>(true);
                    if (controller != null) return controller;
                }
            }

            return FindFirstObjectByType<CardRewardController>(FindObjectsInactive.Include);
        }

        private void MarkRewardClaimed(int rewardIndex)
        {
            claimedRewardIndices.Add(rewardIndex);
            RefreshRewardViews();
            rewardClaimedSfx?.Play(this, transform);
            OnRewardClaimed?.Invoke();
        }

        private void RefreshRewardViews()
        {
            CollectOptionViews();
            EnsureOptionViewCount(activeRewards.Count);

            for (int i = 0; i < optionViews.Count; i++)
            {
                if (i < activeRewards.Count)
                {
                    optionViews[i].Setup(this, i, activeRewards[i], claimedRewardIndices.Contains(i));
                }
                else
                {
                    optionViews[i].Clear();
                }
            }
        }

        private void CollectOptionViews()
        {
            optionViews.Clear();

            if (autoFindOptionViewsInChildren)
            {
                Transform root = rewardContainer != null ? rewardContainer : transform;
                optionViews.AddRange(root.GetComponentsInChildren<RunRewardOptionView>(true));
            }

            optionViews.RemoveAll(view => view == null);
        }

        private void EnsureOptionViewCount(int targetCount)
        {
            if (rewardOptionPrefab == null) return;
            if (targetCount <= optionViews.Count) return;

            Transform parent = rewardContainer != null ? rewardContainer : transform;
            while (optionViews.Count < targetCount)
            {
                RunRewardOptionView optionView = Instantiate(rewardOptionPrefab, parent);
                optionViews.Add(optionView);
            }
        }

        private static bool TryGetRunManager(out RunManager runManager)
        {
            runManager = RunManager.Instance;
            if (runManager != null) return true;

            global::PlushieOdyssey.Core.Logger.LogWarning("RunRewardController could not find RunManager. Keep persistent run systems on Resources/GameManager.");
            return false;
        }
    }
}
