using DG.Tweening;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelDetailsPanelController : MonoBehaviour
{
    [Header("References")]
    [BoxGroup("References")]
    [SerializeField] private RectTransform panelRoot;
    [BoxGroup("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text titleText;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text levelNumberText;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text statusText;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text descriptionText;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text layoutIdText;
    [BoxGroup("References")]
    [SerializeField] private Button enterButton;
    [BoxGroup("References")]
    [SerializeField] private Button cancelButton;

    [Header("Text")]
    [BoxGroup("Text")]
    [SerializeField] private string completedStatus = "Completed";
    [BoxGroup("Text")]
    [SerializeField] private string unlockedStatus = "Available";
    [BoxGroup("Text")]
    [SerializeField] private string lockedStatus = "Locked";
    [BoxGroup("Text")]
    [SerializeField] private string emptyDescription = "No description yet.";
    [BoxGroup("Text")]
    [SerializeField] private string layoutPrefix = "Dungeon: ";

    [Header("DOTween Fallback Animation")]
    [BoxGroup("DOTween Fallback Animation")]
    [Tooltip("Turn this off if Animation Sequencer is wired through OnOpenRequested/OnCloseRequested.")]
    [SerializeField] private bool useCodeSlideAnimation = true;
    [BoxGroup("DOTween Fallback Animation")]
    [SerializeField] private bool hideOnAwake = true;
    [BoxGroup("DOTween Fallback Animation")]
    [SerializeField] private bool disableRootWhenHidden = true;
    [BoxGroup("DOTween Fallback Animation")]
    [SerializeField] private Vector2 hiddenOffset = new(520f, 0f);
    [BoxGroup("DOTween Fallback Animation")]
    [Min(0.01f)] [SerializeField] private float animationDuration = 0.28f;
    [BoxGroup("DOTween Fallback Animation")]
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [BoxGroup("DOTween Fallback Animation")]
    [SerializeField] private Ease closeEase = Ease.InCubic;

    [Header("Events")]
    [BoxGroup("Events")]
    public UnityEvent OnOpenRequested;
    [BoxGroup("Events")]
    public UnityEvent OnCloseRequested;
    [BoxGroup("Events")]
    public UnityEvent OnEnterRequested;
    [BoxGroup("Events")]
    public UnityEvent OnCancelRequested;

    [Header("Audio")]
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue openSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue closeSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue enterSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue cancelSfx;

    private ManualLevelSelectController owner;
    private Vector2 visibleAnchoredPosition;
    private Tween slideTween;
    private Tween fadeTween;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        visibleAnchoredPosition = panelRoot != null ? panelRoot.anchoredPosition : Vector2.zero;
        WireButtons();

        if (hideOnAwake)
        {
            HideImmediate();
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    public void ConfigureOwner(ManualLevelSelectController newOwner)
    {
        owner = newOwner;
    }

    public void Show(LevelDefinition level, bool canEnter, bool completed)
    {
        CacheReferences();
        if (panelRoot == null || level == null) return;

        gameObject.SetActive(true);
        panelRoot.gameObject.SetActive(true);

        if (titleText != null) titleText.text = level.DisplayName;
        if (levelNumberText != null) levelNumberText.text = $"Level {level.LevelNumber}";
        if (statusText != null) statusText.text = completed ? completedStatus : canEnter ? unlockedStatus : lockedStatus;
        if (descriptionText != null) descriptionText.text = string.IsNullOrWhiteSpace(level.Description) ? emptyDescription : level.Description;
        if (layoutIdText != null) layoutIdText.text = string.IsNullOrWhiteSpace(level.ManualMapLayoutId) ? string.Empty : $"{layoutPrefix}{level.ManualMapLayoutId}";

        if (enterButton != null)
        {
            enterButton.interactable = canEnter;
        }

        OnOpenRequested?.Invoke();
        openSfx?.Play(this, transform);

        if (useCodeSlideAnimation)
        {
            PlayOpenAnimation();
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }

    public void Hide()
    {
        CacheReferences();
        if (panelRoot == null) return;

        OnCloseRequested?.Invoke();
        closeSfx?.Play(this, transform);

        if (useCodeSlideAnimation)
        {
            PlayCloseAnimation();
        }
        else
        {
            HideImmediate();
        }
    }

    public void HideImmediate()
    {
        CacheReferences();
        KillTweens();

        if (panelRoot != null)
        {
            panelRoot.anchoredPosition = visibleAnchoredPosition + hiddenOffset;
            if (disableRootWhenHidden)
            {
                panelRoot.gameObject.SetActive(false);
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void RequestEnter()
    {
        OnEnterRequested?.Invoke();
        enterSfx?.Play(this, transform);
        owner?.ConfirmSelectedLevel();
    }

    public void RequestCancel()
    {
        OnCancelRequested?.Invoke();
        cancelSfx?.Play(this, transform);
        owner?.CancelSelection();
    }

    private void PlayOpenAnimation()
    {
        KillTweens();

        panelRoot.gameObject.SetActive(true);
        panelRoot.anchoredPosition = visibleAnchoredPosition + hiddenOffset;
        slideTween = panelRoot.DOAnchorPos(visibleAnchoredPosition, animationDuration)
            .SetEase(openEase)
            .SetUpdate(true);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.alpha = 0f;
            fadeTween = canvasGroup.DOFade(1f, animationDuration * 0.75f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    private void PlayCloseAnimation()
    {
        KillTweens();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            fadeTween = canvasGroup.DOFade(0f, animationDuration * 0.75f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        slideTween = panelRoot.DOAnchorPos(visibleAnchoredPosition + hiddenOffset, animationDuration)
            .SetEase(closeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (disableRootWhenHidden && panelRoot != null)
                {
                    panelRoot.gameObject.SetActive(false);
                }
            });
    }

    private void WireButtons()
    {
        if (enterButton != null)
        {
            enterButton.onClick.RemoveListener(RequestEnter);
            enterButton.onClick.AddListener(RequestEnter);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(RequestCancel);
            cancelButton.onClick.AddListener(RequestCancel);
        }
    }

    private void KillTweens()
    {
        slideTween?.Kill();
        fadeTween?.Kill();
        slideTween = null;
        fadeTween = null;
    }

    private void CacheReferences()
    {
        if (panelRoot == null) panelRoot = transform as RectTransform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }
}
