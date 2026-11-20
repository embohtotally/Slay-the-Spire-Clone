using System.Collections;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

namespace PlushieOdyssey.Core
{
    public class GameManager : PersistentSingleton<GameManager>
    {
        [Header("Floating Text Settings")]
        [SerializeField] private Canvas _targetCanvas;
        [SerializeField] private float _textFontSize = 20;
        [SerializeField] private TMP_FontAsset _textFont = default;
        [SerializeField] private Color _textColor = Color.white;

        [Header("Information")]
        [SerializeField, ReadOnly] private Camera _cam;

        [field: SerializeField, ReadOnly]
        public bool IsPaused { get; private set; }

        public void SetPaused(bool pause)
        {
            IsPaused = pause;
            Time.timeScale = pause ? 0f : 1f;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                PlayerPrefs.DeleteAll();
                Debug.Log("PlayerPrefs deleted!");
            }
        }
#endif

        public static void GenerateFloatingText(string text, Transform target, float duration = 1f, float speed = 1f, string colorHex = "", float scaleMultiplier = 1f)
        {
            if (Instance == null) return;
            if (Instance._targetCanvas == null) return;

            if (Instance._cam == null) Instance._cam = Camera.main;
            if (Instance._cam == null) return;

            Instance.StartCoroutine(Instance.GenerateFloatingTextCoroutine(
                text, target, duration, speed, colorHex, scaleMultiplier
            ));
        }

        private IEnumerator GenerateFloatingTextCoroutine(string text, Transform target, float duration = 1f, float speed = 1f, string colorHex = "", float scaleMultiplier = 1f)
        {
            GameObject textObj = new("Damage Floating Text");
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 68f);

            TextMeshProUGUI tmPro = textObj.AddComponent<TextMeshProUGUI>();
            tmPro.text = text;
            tmPro.horizontalAlignment = HorizontalAlignmentOptions.Center;
            tmPro.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmPro.fontStyle = FontStyles.Bold;
            tmPro.fontSize = Mathf.Max(26f, _textFontSize) * Mathf.Clamp(scaleMultiplier, 0.75f, 1.8f);
            tmPro.raycastTarget = false;
            if (_textFont) tmPro.font = _textFont;

            Color baseColor = _textColor;
            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var parsed))
                baseColor = parsed;
            tmPro.color = baseColor;

            UnityEngine.UI.Shadow dropShadow = textObj.AddComponent<UnityEngine.UI.Shadow>();
            dropShadow.effectColor = new Color(0.05f, 0.02f, 0.01f, 0.92f);
            dropShadow.effectDistance = new Vector2(2.5f, -2.5f);

            textObj.transform.SetParent(Instance._targetCanvas.transform, false);
            textObj.transform.SetAsLastSibling();

            Vector3 baseWorld = target != null ? target.position : Vector3.zero;
            float spreadX = UnityEngine.Random.Range(-0.55f, 0.55f);
            float peakHeight = Mathf.Lerp(1.05f, 1.65f, Mathf.Clamp01(speed * 0.7f));
            float startYOffset = UnityEngine.Random.Range(0.55f, 0.95f);

            float t = 0f;
            float safeDuration = Mathf.Max(0.45f, duration);
            while (t < safeDuration)
            {
                if (rect == null || _cam == null) break;
                if (target != null) baseWorld = target.position;

                float u = Mathf.Clamp01(t / safeDuration);
                // Parabolic fountain arc with snappy initial punch-scale
                float arcY = startYOffset + 4f * peakHeight * u * (1f - 0.62f * u);
                float arcX = spreadX * (u * 1.45f);
                rect.position = _cam.WorldToScreenPoint(baseWorld + new Vector3(arcX, arcY, 0f));

                float scale = u < 0.16f
                    ? Mathf.Lerp(0.55f, 1.24f, u / 0.16f)
                    : Mathf.Lerp(1.24f, 0.92f, (u - 0.16f) / 0.84f);
                rect.localScale = Vector3.one * scale;

                float alpha = u < 0.62f ? 1f : Mathf.Clamp01(1f - (u - 0.62f) / 0.38f);
                tmPro.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                yield return null;
                t += Time.unscaledDeltaTime;
            }

            if (textObj != null) Destroy(textObj);
        }
    }
}
