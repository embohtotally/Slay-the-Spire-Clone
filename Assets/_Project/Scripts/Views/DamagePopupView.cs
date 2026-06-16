using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamagePopupView : MonoBehaviour
{
    [SerializeField] private TMP_Text textMesh;
    [SerializeField] private float moveDistance = 1.5f;
    [SerializeField] private float duration = 1f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 1.5f, 0);

    public void Setup(int damageAmount, Vector3 startPos)
    {
        Setup(damageAmount.ToString(), startPos, null);
    }

    public void Setup(string text, Vector3 startPos, Color? colorOverride = null)
    {
        if (textMesh == null) textMesh = GetComponentInChildren<TMP_Text>();

        Vector3 finalStartPos = startPos + spawnOffset;
        finalStartPos.z = -5f; // Force Z depth so it doesn't hide behind 2D backgrounds
        
        transform.position = finalStartPos;
        gameObject.SetActive(true);

        if (textMesh != null)
        {
            textMesh.text = text;
            
            if (colorOverride.HasValue)
            {
                textMesh.color = colorOverride.Value;
            }

            // Reset Alpha
            Color c = textMesh.color;
            c.a = 1f;
            textMesh.color = c;

            // Fade out
            textMesh.DOFade(0, duration).SetEase(Ease.InExpo);
        }

        // Float up and return to spawn position
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMoveY(finalStartPos.y + moveDistance, duration / 2f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOMoveY(finalStartPos.y, duration / 2f).SetEase(Ease.InQuad));
        seq.OnComplete(() => gameObject.SetActive(false));
    }
}
