using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class MapLineView : MonoBehaviour
{
    [SerializeField] private Image lineImage;
    [SerializeField] private float thickness = 6f;

    private void Awake()
    {
        if (lineImage == null)
        {
            lineImage = GetComponent<Image>();
        }
    }

    public void Setup(Vector2 startPosition, Vector2 endPosition, bool visited)
    {
        RectTransform rectTransform = (RectTransform)transform;
        Vector2 direction = endPosition - startPosition;
        float distance = direction.magnitude;

        rectTransform.anchoredPosition = startPosition + direction * 0.5f;
        rectTransform.sizeDelta = new Vector2(distance, thickness);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        if (lineImage != null)
        {
            lineImage.color = visited ? new Color(0.95f, 0.85f, 0.25f, 0.95f) : new Color(0.32f, 0.32f, 0.38f, 0.75f);
        }
    }
}
