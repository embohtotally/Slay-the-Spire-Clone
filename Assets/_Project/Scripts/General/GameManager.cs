using System.Collections;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

namespace Gameseed26
{
    public class GameManager : PersistentSingleton<GameManager>
    {
        [Header("Floating Text Settings")]
        [SerializeField] private Canvas _targetCanvas;
        [SerializeField] private float _textFontSize = 20;
        [SerializeField] private TMP_FontAsset _textFont = default;
        [SerializeField] private Color _textColor = Color.white;

        [Header("Information")]
        private Camera _cam;

        [field: SerializeField, ReadOnly]
        public bool IsPaused { get; private set; }


        public void SetPaused(bool pause)
        {
            IsPaused = pause;

            if (pause) Time.timeScale = 1f;
            else Time.timeScale = 0f;
        }

        public static void GenerateFloatingText(string text, Transform target, float duration = 1f, float speed = 1f, string colorHex = "")
        {
            if (!Instance._targetCanvas) return;

            if (!Instance._cam) Instance._cam = Camera.main;

            Instance.StartCoroutine(Instance.GenerateFloatingTextCoroutine(
                text, target, duration, speed, colorHex
            ));
        }

        private IEnumerator GenerateFloatingTextCoroutine(string text, Transform target, float duration = 1f, float speed = 50f, string colorHex = "")
        {
            GameObject textObj = new("Damage Floating Text");
            RectTransform rect = textObj.AddComponent<RectTransform>();
            TextMeshProUGUI tmPro = textObj.AddComponent<TextMeshProUGUI>();
            tmPro.text = text;
            tmPro.horizontalAlignment = HorizontalAlignmentOptions.Center;
            tmPro.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmPro.fontSize = _textFontSize;
            if (_textFont) tmPro.font = _textFont;
            if (string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var color))
                tmPro.color = color;
            else
                tmPro.color = _textColor;
            rect.position = _cam.WorldToScreenPoint(target.position);

            Destroy(textObj, duration);

            textObj.transform.SetParent(Instance._targetCanvas.transform);
            textObj.transform.SetSiblingIndex(0);

            WaitForEndOfFrame w = new WaitForEndOfFrame();
            float t = 0;
            float yOffset = 0;
            Vector3 lastKnownPosition = target.position;
            while (t < duration)
            {
                if (!rect) break;

                tmPro.color = new Color(tmPro.color.r, tmPro.color.g, tmPro.color.b, 1 - t / duration);

                if (target) lastKnownPosition = target.position;

                yOffset += speed * Time.deltaTime;
                rect.position = _cam.WorldToScreenPoint(lastKnownPosition + new Vector3(0, yOffset));

                yield return w;
                t += Time.deltaTime;
            }
        }
    }
}
