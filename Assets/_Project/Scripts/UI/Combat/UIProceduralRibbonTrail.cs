using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// High-contrast native Canvas MaskableGraphic ribbon trail for flying cards.
    /// Uses a soft-feathered procedural glow texture so each card leaves a velvety, luminous
    /// comet ribbon that pops clearly over both bright and dark combat backgrounds.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIProceduralRibbonTrail : MaskableGraphic
    {
        private struct TrailSample
        {
            public Vector2 Pos;
            public float Age;
        }

        private static Texture2D _featheredRibbonTex;

        private readonly List<TrailSample> _samples = new(28);
        private float _headWidth = 44f;
        private float _lifetime = 0.28f;
        private Color _headColor = new(1f, 0.99f, 0.90f, 1f);
        private Color _midColor = new(1f, 0.72f, 0.16f, 0.90f);
        private Color _tailColor = new(0.82f, 0.28f, 0.08f, 0f);
        private Color _outerGlowColor = new(0.28f, 0.12f, 0.42f, 0.55f);

        public int SampleCount => _samples.Count;
        public bool HasPoints => _samples.Count > 1;
        public override Texture mainTexture => GetFeatheredRibbonTexture();

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = Color.white;
        }

        public void Configure(
            float headWidth,
            float lifetime,
            Color headColor,
            Color midColor,
            Color tailColor,
            Color outerGlowColor)
        {
            _headWidth = Mathf.Max(4f, headWidth);
            _lifetime = Mathf.Max(0.05f, lifetime);
            _headColor = headColor;
            _midColor = midColor;
            _tailColor = tailColor;
            _outerGlowColor = outerGlowColor;
            raycastTarget = false;
            color = Color.white;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void AddPoint(Vector2 localPos)
        {
            if (_samples.Count > 0 && (_samples[_samples.Count - 1].Pos - localPos).sqrMagnitude < 9f)
            {
                return;
            }

            _samples.Add(new TrailSample { Pos = localPos, Age = 0f });
            if (_samples.Count > 28)
            {
                _samples.RemoveAt(0);
            }
            SetVerticesDirty();
        }

        public void Tick(float dt)
        {
            if (_samples.Count == 0) return;

            for (int i = _samples.Count - 1; i >= 0; i--)
            {
                TrailSample s = _samples[i];
                s.Age += dt;
                if (s.Age >= _lifetime)
                {
                    _samples.RemoveAt(i);
                }
                else
                {
                    _samples[i] = s;
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = _samples.Count;
            if (count < 2) return;

            // Pass 1: Wider soft twilight/amber outer aura so the golden ribbon reads dramatically on bright sky/grass too!
            AppendRibbonPass(vh, widthScale: 1.85f, isOuterAura: true);

            // Pass 2: Bright luminous golden-white core ribbon
            AppendRibbonPass(vh, widthScale: 1.0f, isOuterAura: false);
        }

        private void AppendRibbonPass(VertexHelper vh, float widthScale, bool isOuterAura)
        {
            int count = _samples.Count;
            int baseVertIndex = vh.currentVertCount;

            for (int i = 0; i < count; i++)
            {
                TrailSample sample = _samples[i];
                float lifeFrac = 1f - Mathf.Clamp01(sample.Age / _lifetime);
                float indexFrac = (float)i / (count - 1);
                float strength = Mathf.Clamp01(lifeFrac * Mathf.Pow(indexFrac, 0.72f));

                Vector2 tangent;
                if (i == 0)
                    tangent = _samples[1].Pos - sample.Pos;
                else if (i == count - 1)
                    tangent = sample.Pos - _samples[i - 1].Pos;
                else
                    tangent = _samples[i + 1].Pos - _samples[i - 1].Pos;

                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector2.left;
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x);

                float halfW = _headWidth * 0.5f * widthScale * Mathf.Pow(strength, 0.70f);
                Color col;
                if (isOuterAura)
                {
                    col = _outerGlowColor;
                    col.a *= strength;
                }
                else
                {
                    col = strength > 0.55f
                        ? Color.Lerp(_midColor, _headColor, (strength - 0.55f) / 0.45f)
                        : Color.Lerp(_tailColor, _midColor, strength / 0.55f);
                }

                UIVertex vLeft = UIVertex.simpleVert;
                vLeft.position = new Vector3(sample.Pos.x + normal.x * halfW, sample.Pos.y + normal.y * halfW, 0f);
                vLeft.color = col;
                vLeft.uv0 = new Vector2(indexFrac, 0f);

                UIVertex vRight = UIVertex.simpleVert;
                vRight.position = new Vector3(sample.Pos.x - normal.x * halfW, sample.Pos.y - normal.y * halfW, 0f);
                vRight.color = col;
                vRight.uv0 = new Vector2(indexFrac, 1f);

                vh.AddVert(vLeft);
                vh.AddVert(vRight);

                if (i > 0)
                {
                    int idx = baseVertIndex + i * 2;
                    vh.AddTriangle(idx - 2, idx - 1, idx);
                    vh.AddTriangle(idx - 1, idx + 1, idx);
                }
            }
        }

        private static Texture2D GetFeatheredRibbonTexture()
        {
            if (_featheredRibbonTex != null) return _featheredRibbonTex;

            const int w = 16;
            const int h = 32;
            Texture2D tex = new(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                // V = 0..1 across width: bright opaque core in middle (0.5), smooth feathered edge at 0 and 1
                float distFromCenter = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                float crossAlpha = Mathf.Pow(Mathf.Clamp01(1f - distFromCenter), 1.35f);
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = new Color(1f, 1f, 1f, crossAlpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _featheredRibbonTex = tex;
            return _featheredRibbonTex;
        }
    }
}

