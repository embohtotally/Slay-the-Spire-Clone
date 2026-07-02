using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;

public class ColorTransition : MonoBehaviour
{
    [Header("Targets (Assign at least one)")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private SpriteRenderer targetSprite;
    
    [Header("Settings")]
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("Events")]
    public UnityEvent onTurnedWhite;
    public UnityEvent onTurnedBlack;

    private void Reset()
    {
        // Auto-assign if attached to the same GameObject
        targetGraphic = GetComponent<Graphic>();
        targetSprite = GetComponent<SpriteRenderer>();
    }

    public void TransitionToWhite()
    {
        TransitionToColor(Color.white);
    }

    public void TransitionToBlack()
    {
        TransitionToColor(Color.black);
    }

    public void TransitionToColor(Color targetColor)
    {
        Tween tween = null;

        if (targetGraphic != null)
        {
            targetGraphic.DOKill();
            tween = targetGraphic.DOColor(targetColor, transitionDuration);
        }

        if (targetSprite != null)
        {
            targetSprite.DOKill();
            tween = targetSprite.DOColor(targetColor, transitionDuration);
        }

        if (tween != null)
        {
            tween.OnComplete(() => InvokeColorEvents(targetColor));
        }
        else
        {
            InvokeColorEvents(targetColor);
        }
    }

    public void TurnCompletelyWhiteInstant()
    {
        SetColorInstant(Color.white);
    }

    public void TurnCompletelyBlackInstant()
    {
        SetColorInstant(Color.black);
    }

    public void SetColorInstant(Color targetColor)
    {
        if (targetGraphic != null)
        {
            targetGraphic.DOKill();
            targetGraphic.color = targetColor;
        }

        if (targetSprite != null)
        {
            targetSprite.DOKill();
            targetSprite.color = targetColor;
        }

        InvokeColorEvents(targetColor);
    }

    private void InvokeColorEvents(Color color)
    {
        if (color == Color.white) onTurnedWhite?.Invoke();
        else if (color == Color.black) onTurnedBlack?.Invoke();
    }
}
