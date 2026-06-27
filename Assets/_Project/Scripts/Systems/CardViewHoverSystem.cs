using UnityEngine;

[System.Serializable]
public class CardViewHoverSystem : Singleton<CardViewHoverSystem>
{
    [SerializeField] private CardView cardViewHover;

    private Collider[] hoverColliders;
    private Collider2D[] hoverColliders2D;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        PrepareHoverPreview();
    }

    private void OnValidate()
    {
        PrepareHoverPreview();
    }

    public void Show(Card card, Vector3 position)
    {
        if (cardViewHover == null || card == null) return;

        PrepareHoverPreview();
        cardViewHover.gameObject.SetActive(true);
        cardViewHover.Setup(card);
        cardViewHover.transform.position = position;
        SetHoverCollidersEnabled(false);
    }

    public void Hide()
    {
        if (cardViewHover == null) return;
        cardViewHover.gameObject.SetActive(false);
    }

    private void PrepareHoverPreview()
    {
        if (cardViewHover == null) return;

        cardViewHover.SetCombatInteractionsEnabled(false);
        cardViewHover.SetCombatHoverPreviewEnabled(false);
        cardViewHover.SetHoverJuiceEnabled(false);

        hoverColliders = cardViewHover.GetComponentsInChildren<Collider>(true);
        hoverColliders2D = cardViewHover.GetComponentsInChildren<Collider2D>(true);
        SetHoverCollidersEnabled(false);
    }

    private void SetHoverCollidersEnabled(bool enabled)
    {
        if (hoverColliders != null)
        {
            foreach (Collider hoverCollider in hoverColliders)
            {
                if (hoverCollider != null) hoverCollider.enabled = enabled;
            }
        }

        if (hoverColliders2D != null)
        {
            foreach (Collider2D hoverCollider in hoverColliders2D)
            {
                if (hoverCollider != null) hoverCollider.enabled = enabled;
            }
        }
    }
}
