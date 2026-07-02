using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelSelectNodeJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [BoxGroup("References")]
    [SerializeField] private RectTransform target;
    [BoxGroup("References")]
    [SerializeField] private Selectable selectable;
    [BoxGroup("References")]
    [SerializeField] private Graphic tintGraphic;

    [Header("Scale")]
    [BoxGroup("Scale")]
    [SerializeField] private bool useScaleTween = true;
    [BoxGroup("Scale")]
    [SerializeField] private float hoverScale = 1.08f;
    [BoxGroup("Scale")]
    [SerializeField] private float pressedScale = 0.94f;
    [BoxGroup("Scale")]
    [SerializeField] private float selectedScale = 1.16f;
    [BoxGroup("Scale")]
    [Min(0.01f)] [SerializeField] private float scaleDuration = 0.14f;
    [BoxGroup("Scale")]
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Selected Pulse")]
    [BoxGroup("Selected Pulse")]
    [SerializeField] private bool pulseWhileSelected = true;
    [BoxGroup("Selected Pulse")]
    [SerializeField] private float selectedPulseScale = 1.22f;
    [BoxGroup("Selected Pulse")]
    [Min(0.05f)] [SerializeField] private float selectedPulseDuration = 0.7f;

    [Header("Optional Tint")]
    [BoxGroup("Optional Tint")]
    [SerializeField] private bool tintOnState;
    [BoxGroup("Optional Tint")]
    [ShowIf("tintOnState")]
    [SerializeField] private Color normalTint = Color.white;
    [BoxGroup("Optional Tint")]
    [ShowIf("tintOnState")]
    [SerializeField] private Color hoverTint = new(1f, 0.93f, 0.55f, 1f);
    [BoxGroup("Optional Tint")]
    [ShowIf("tintOnState")]
    [SerializeField] private Color selectedTint = new(1f, 0.78f, 0.25f, 1f);
    [BoxGroup("Optional Tint")]
    [ShowIf("tintOnState")]
    [Min(0.01f)] [SerializeField] private float tintDuration = 0.12f;

    [Header("Events")]
    [BoxGroup("Events")]
    public UnityEvent OnHoverEntered;
    [BoxGroup("Events")]
    public UnityEvent OnHoverExited;
    [BoxGroup("Events")]
    public UnityEvent OnPressed;
    [BoxGroup("Events")]
    public UnityEvent OnReleased;
    [BoxGroup("Events")]
    public UnityEvent OnSelected;
    [BoxGroup("Events")]
    public UnityEvent OnDeselected;

    [Header("Audio")]
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue hoverSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue pressedSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue selectedSfx;

    private Vector3 baseScale = Vector3.one;
    private bool isHovered;
    private bool isPressed;
    private bool isSelected;
    private Tween scaleTween;
    private Tween tintTween;
    private Sequence selectedPulse;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CaptureBaseScale();
    }

    private void OnEnable()
    {
        CaptureBaseScale();
        RefreshVisual();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected) return;

        isSelected = selected;
        isPressed = false;
        if (isSelected)
        {
            selectedSfx?.Play(this, transform);
            OnSelected?.Invoke();
        }
        else
        {
            OnDeselected?.Invoke();
        }

        RefreshVisual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsUsable()) return;

        isHovered = true;
        hoverSfx?.Play(this, transform);
        OnHoverEntered?.Invoke();
        RefreshVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        OnHoverExited?.Invoke();
        RefreshVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsUsable()) return;

        isPressed = true;
        pressedSfx?.Play(this, transform);
        OnPressed?.Invoke();
        RefreshVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed) return;

        isPressed = false;
        OnReleased?.Invoke();
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        RefreshScale();
        RefreshTint();
    }

    private void RefreshScale()
    {
        if (target == null || !useScaleTween) return;

        selectedPulse?.Kill();
        selectedPulse = null;
        scaleTween?.Kill();

        float scale = 1f;
        if (isSelected) scale = selectedScale;
        else if (isPressed) scale = pressedScale;
        else if (isHovered) scale = hoverScale;

        scaleTween = target.DOScale(baseScale * scale, scaleDuration)
            .SetEase(scaleEase)
            .SetUpdate(true);

        if (isSelected && pulseWhileSelected)
        {
            selectedPulse = DOTween.Sequence()
                .SetUpdate(true)
                .AppendInterval(scaleDuration)
                .Append(target.DOScale(baseScale * selectedPulseScale, selectedPulseDuration).SetEase(Ease.InOutSine))
                .Append(target.DOScale(baseScale * selectedScale, selectedPulseDuration).SetEase(Ease.InOutSine))
                .SetLoops(-1);
        }
    }

    private void RefreshTint()
    {
        if (!tintOnState || tintGraphic == null) return;

        tintTween?.Kill();
        Color targetColor = isSelected ? selectedTint : isHovered ? hoverTint : normalTint;
        tintTween = tintGraphic.DOColor(targetColor, tintDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private bool IsUsable()
    {
        return selectable == null || selectable.interactable;
    }

    private void CaptureBaseScale()
    {
        if (target != null)
        {
            baseScale = target.localScale;
        }
    }

    private void KillTweens()
    {
        scaleTween?.Kill();
        tintTween?.Kill();
        selectedPulse?.Kill();
        scaleTween = null;
        tintTween = null;
        selectedPulse = null;
    }

    private void CacheReferences()
    {
        if (target == null) target = transform as RectTransform;
        if (selectable == null) selectable = GetComponent<Selectable>();
        if (tintGraphic == null) tintGraphic = GetComponent<Graphic>();
    }
}
