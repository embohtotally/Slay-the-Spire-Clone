using UnityEngine;

namespace PlushieOdyssey.Levels
{
    /// <summary>
    /// Procedural high-resolution UI artwork generator for the celestial Party Setup screen:
    /// warm parchment gradient, corner sunburst, organic terracotta waves, golden thread ribbons,
    /// 3 bespoke tarot slot motifs (Gold Sun, Crimson Star, Azure Moon), diamond X badges,
    /// rounded frames, padlock icon, radial glow, and sparkle particles.
    /// </summary>
    internal static class PartySetupArtFactory
    {
        private static Sprite s_solid;
        private static Sprite s_parchmentBg;
        private static Sprite s_sunburst;
        private static Sprite s_topRightWave;
        private static Sprite s_bottomLeftWave;
        private static Sprite s_goldenThreads;
        private static Sprite s_diamondBadge;
        private static Sprite s_roundedRect;
        private static Sprite s_roundedOutline;
        private static Sprite s_padlock;
        private static Sprite s_softGlow;
        private static Sprite s_sparkle;
        private static readonly Sprite[] s_tarotBackdrops = new Sprite[3];
        private static readonly Sprite[] s_tarotFrames = new Sprite[3];

        public static Sprite GetSolid()
        {
            if (s_solid != null) return s_solid;
            Texture2D t = Texture2D.whiteTexture;
            s_solid = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
            return s_solid;
        }

        public static Sprite GetParchmentBackground()
        {
            if (s_parchmentBg != null) return s_parchmentBg;
            const int w = 512, h = 288;
            Texture2D tex = CreateTex(w, h);
            Color[] px = new Color[w * h];

            Color cTopLeft = new(0.95f, 0.84f, 0.56f, 1f);
            Color cTopRight = new(0.89f, 0.77f, 0.50f, 1f);
            Color cBotLeft = new(0.96f, 0.89f, 0.70f, 1f);
            Color cBotRight = new(0.92f, 0.82f, 0.58f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ty = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float tx = x / (float)(w - 1);
                    Color bot = Color.Lerp(cBotLeft, cBotRight, tx);
                    Color top = Color.Lerp(cTopLeft, cTopRight, tx);
                    Color c = Color.Lerp(bot, top, ty);

                    // Subtle warm glow near top-left sun
                    float dSun = Vector2.Distance(new Vector2(tx, ty), new Vector2(0.05f, 0.96f));
                    float sunWarm = Mathf.Clamp01(1f - dSun * 1.45f);
                    c = Color.Lerp(c, new Color(0.99f, 0.90f, 0.62f, 1f), sunWarm * 0.35f);

                    // Delicate parchment grain
                    float grain = (Mathf.PerlinNoise(tx * 18f, ty * 12f) - 0.5f) * 0.028f;
                    c.r = Mathf.Clamp01(c.r + grain);
                    c.g = Mathf.Clamp01(c.g + grain * 0.9f);
                    c.b = Mathf.Clamp01(c.b + grain * 0.7f);
                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_parchmentBg = ToSprite(tex);
            return s_parchmentBg;
        }

        public static Sprite GetSunburstEmblem()
        {
            if (s_sunburst != null) return s_sunburst;
            const int sz = 420;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];
            Vector2 center = new(sz * 0.5f, sz * 0.5f);

            Color goldLight = new(0.99f, 0.87f, 0.45f, 1f);
            Color goldCore = new(0.93f, 0.75f, 0.31f, 1f);

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float r = p.magnitude / (sz * 0.5f);
                    float ang = Mathf.Atan2(p.y, p.x);
                    if (ang < 0f) ang += Mathf.PI * 2f;

                    float alpha = 0f;
                    Color col = goldLight;

                    // Soft core disk
                    if (r < 0.43f)
                    {
                        float t = SmoothStep(0.43f, 0.40f, r);
                        alpha = Mathf.Max(alpha, t * 0.78f);
                        col = goldCore;
                    }

                    // Double celestial rings
                    float ring1 = RingAlpha(r, 0.48f, 0.024f);
                    float ring2 = RingAlpha(r, 0.56f, 0.017f);
                    alpha = Mathf.Max(alpha, Mathf.Max(ring1, ring2) * 0.94f);

                    // 12 crisp alternating long & hollow triangle rays
                    const int rays = 12;
                    float sector = (Mathf.PI * 2f) / rays;
                    int rayIdx = Mathf.FloorToInt(ang / sector);
                    float local = (ang - rayIdx * sector) / sector - 0.5f; // -0.5..0.5
                    if (r >= 0.59f && r <= 0.96f)
                    {
                        float rayProgress = (r - 0.59f) / 0.37f;
                        float halfW = (1f - rayProgress) * 0.25f;
                        float absL = Mathf.Abs(local);
                        if (absL <= halfW)
                        {
                            bool filledRay = (rayIdx % 2 == 0);
                            if (filledRay)
                            {
                                float edge = SmoothStep(halfW, halfW - 0.03f, absL);
                                alpha = Mathf.Max(alpha, edge * 0.88f);
                            }
                            else
                            {
                                // Hollow triangle ray (matching Image 1!)
                                bool onEdge = absL > halfW - 0.045f || rayProgress < 0.09f;
                                if (onEdge) alpha = Mathf.Max(alpha, 0.82f);
                            }
                        }
                    }

                    px[y * sz + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_sunburst = ToSprite(tex);
            return s_sunburst;
        }

        public static Sprite GetTopRightWave()
        {
            if (s_topRightWave != null) return s_topRightWave;
            const int w = 720, h = 220;
            Texture2D tex = CreateTex(w, h);
            Color[] px = new Color[w * h];

            Color terracottaA = new(0.75f, 0.54f, 0.36f, 0.92f);
            Color terracottaB = new(0.84f, 0.65f, 0.45f, 0.88f);
            Color creamEdge = new(0.99f, 0.96f, 0.88f, 0.96f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1); // 0 bottom .. 1 top
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1); // 0 left .. 1 right
                    // Seamless S-curve emerging cleanly from the top edge near nx=0.05 and dipping smoothly on the right
                    float curve = 1.08f
                        - 0.24f * SmoothStep(0.02f, 0.52f, nx)
                        - 0.52f * SmoothStep(0.44f, 0.96f, nx)
                        + 0.07f * Mathf.Sin(nx * Mathf.PI * 2.4f) * SmoothStep(0.04f, 0.25f, nx);

                    float dist = ny - curve;
                    Color c = Color.clear;
                    if (dist >= 0.025f)
                    {
                        c = Color.Lerp(terracottaB, terracottaA, Mathf.Clamp01(dist * 2.2f));
                    }
                    else if (dist >= -0.028f)
                    {
                        float rim = 1f - Mathf.Abs(dist) / 0.028f;
                        c = new Color(creamEdge.r, creamEdge.g, creamEdge.b, creamEdge.a * SmoothStep(0f, 0.55f, rim));
                    }
                    else if (dist >= -0.068f)
                    {
                        float outerLine = 1f - Mathf.Abs(dist + 0.052f) / 0.016f;
                        if (outerLine > 0f)
                            c = new Color(creamEdge.r, creamEdge.g, creamEdge.b, 0.65f * outerLine);
                    }

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_topRightWave = ToSprite(tex);
            return s_topRightWave;
        }

        public static Sprite GetBottomLeftWave()
        {
            if (s_bottomLeftWave != null) return s_bottomLeftWave;
            const int w = 780, h = 240;
            Texture2D tex = CreateTex(w, h);
            Color[] px = new Color[w * h];

            Color terracottaDark = new(0.68f, 0.48f, 0.31f, 0.95f);
            Color terracottaLight = new(0.83f, 0.64f, 0.44f, 0.92f);
            Color creamEdge = new(0.99f, 0.96f, 0.88f, 0.96f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    // Sweeps cleanly below ny=0 before reaching nx=0.96 so there is zero hard right seam
                    float taper = 1f - SmoothStep(0.06f, 0.88f, nx);
                    float curve = -0.09f + 0.86f * taper
                        + 0.14f * Mathf.Sin(nx * Mathf.PI * 2.3f + 0.25f) * taper;

                    float dist = curve - ny;
                    Color c = Color.clear;
                    if (dist >= 0.028f)
                    {
                        c = Color.Lerp(terracottaLight, terracottaDark, Mathf.Clamp01(dist * 1.9f));
                    }
                    else if (dist >= -0.026f)
                    {
                        float rim = 1f - Mathf.Abs(dist) / 0.026f;
                        c = new Color(creamEdge.r, creamEdge.g, creamEdge.b, creamEdge.a * SmoothStep(0f, 0.5f, rim));
                    }
                    else if (dist >= -0.064f)
                    {
                        float outerLine = 1f - Mathf.Abs(dist + 0.048f) / 0.015f;
                        if (outerLine > 0f)
                            c = new Color(creamEdge.r, creamEdge.g, creamEdge.b, 0.60f * outerLine);
                    }

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_bottomLeftWave = ToSprite(tex);
            return s_bottomLeftWave;
        }

        public static Sprite GetGoldenThreads()
        {
            if (s_goldenThreads != null) return s_goldenThreads;
            const int w = 960, h = 540;
            Texture2D tex = CreateTex(w, h);
            Color[] px = new Color[w * h];
            Color gold = new(0.92f, 0.68f, 0.22f, 0.52f);

            // Draw two graceful overlapping sine/ribbon curves across the parchment
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float c1 = 0.56f + 0.27f * Mathf.Sin(nx * Mathf.PI * 2.2f - 0.55f) + 0.08f * Mathf.Cos(nx * Mathf.PI * 4.1f);
                    float c2 = 0.34f + 0.22f * Mathf.Cos(nx * Mathf.PI * 1.85f + 0.9f);
                    float d1 = Mathf.Abs(ny - c1);
                    float d2 = Mathf.Abs(ny - c2);
                    float a = Mathf.Max(SmoothStep(0.0085f, 0.002f, d1), SmoothStep(0.007f, 0.0018f, d2) * 0.85f);
                    if (a > 0.005f)
                        px[y * w + x] = new Color(gold.r, gold.g, gold.b, gold.a * a);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_goldenThreads = ToSprite(tex);
            return s_goldenThreads;
        }

        public static Sprite GetTarotCardBackdrop(int slotIndex)
        {
            int idx = Mathf.Clamp(slotIndex, 0, 2);
            if (s_tarotBackdrops[idx] != null) return s_tarotBackdrops[idx];

            const int w = 360, h = 640;
            Texture2D tex = CreateTex(w, h);
            Color[] px = new Color[w * h];

            Color paper = new(0.992f, 0.985f, 0.965f, 1f);
            Color paperTint = idx switch
            {
                0 => new Color(0.99f, 0.96f, 0.88f, 1f),
                1 => new Color(0.99f, 0.94f, 0.94f, 1f),
                _ => new Color(0.94f, 0.97f, 1.00f, 1f)
            };

            Color accent = GetSlotAccentColor(idx);
            Color accentSoft = new(accent.r, accent.g, accent.b, 0.45f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    Color c = Color.Lerp(paperTint, paper, SmoothStep(0.15f, 0.85f, ny));

                    if (idx == 0)
                    {
                        // SLOT 1: Gold Sunburst Rays & Top Sun Crest
                        Vector2 sunCenter = new(0.5f, 0.92f);
                        Vector2 p = new(nx - sunCenter.x, (ny - sunCenter.y) * (h / (float)w));
                        float r = p.magnitude;
                        float ang = Mathf.Atan2(p.y, p.x);

                        // Inner golden circle
                        if (r < 0.24f) c = Color.Lerp(c, new Color(0.96f, 0.82f, 0.34f, 1f), SmoothStep(0.24f, 0.21f, r) * 0.72f);
                        float halo = RingAlpha(r, 0.28f, 0.015f);
                        c = Blend(c, accent, halo * 0.85f);

                        // Radiating golden diagonal rays
                        float rayWave = Mathf.Abs(Mathf.Sin((ang + Mathf.PI * 0.5f) * 6f));
                        float rayLine = SmoothStep(0.06f, 0.01f, rayWave) * SmoothStep(0.26f, 0.32f, r);
                        c = Blend(c, accentSoft, rayLine * 0.75f);

                        // Central luminous golden arc lower down
                        Vector2 mid = new(0.5f, 0.56f);
                        float dMid = Vector2.Distance(new Vector2(nx, ny * 1.4f), new Vector2(mid.x, mid.y * 1.4f));
                        c = Blend(c, accent, RingAlpha(dMid, 0.38f, 0.008f) * 0.42f);
                    }
                    else if (idx == 1)
                    {
                        // SLOT 2: Crimson Starburst Arch & Central Diamond
                        Vector2 archCenter = new(0.5f, 0.78f);
                        Vector2 pa = new(nx - archCenter.x, (ny - archCenter.y) * (h / (float)w));
                        float ra = pa.magnitude;
                        c = Blend(c, accent, RingAlpha(ra, 0.43f, 0.010f) * 0.82f);

                        // Star beads along the arch
                        float angA = Mathf.Atan2(pa.y, pa.x);
                        float starMod = Mathf.Abs(Mathf.Sin(angA * 12f));
                        if (Mathf.Abs(ra - 0.49f) < 0.032f && starMod < 0.22f)
                            c = Blend(c, accent, 0.85f);

                        // Diagonal X lines
                        float dDiag1 = Mathf.Abs((nx - 0.5f) - (ny - 0.54f) * 1.15f);
                        float dDiag2 = Mathf.Abs((nx - 0.5f) + (ny - 0.54f) * 1.15f);
                        float diag = Mathf.Max(SmoothStep(0.008f, 0.002f, dDiag1), SmoothStep(0.008f, 0.002f, dDiag2));
                        c = Blend(c, accentSoft, diag * 0.55f);

                        // Bold Crimson Center Diamond (matching Image 1!)
                        float manhattan = Mathf.Abs(nx - 0.5f) * 1.05f + Mathf.Abs(ny - 0.50f) * 3.1f;
                        if (manhattan < 0.46f)
                            c = Blend(c, new Color(0.86f, 0.16f, 0.19f, 1f), SmoothStep(0.46f, 0.43f, manhattan) * 0.86f);
                    }
                    else
                    {
                        // SLOT 3: Azure Lunar Crescent Orbit & Concentric Rings
                        Vector2 topMoon = new(0.5f, 0.96f);
                        Vector2 pm = new(nx - topMoon.x, (ny - topMoon.y) * (h / (float)w));
                        float rm = pm.magnitude;
                        if (rm < 0.20f) c = Blend(c, new Color(0.28f, 0.68f, 0.97f, 1f), SmoothStep(0.20f, 0.17f, rm) * 0.85f);

                        // Upper dotted lunar orbit arc
                        Vector2 orbTop = new(0.5f, 0.78f);
                        Vector2 po = new(nx - orbTop.x, (ny - orbTop.y) * (h / (float)w));
                        float ro = po.magnitude;
                        c = Blend(c, accent, RingAlpha(ro, 0.44f, 0.008f) * 0.65f);

                        // Central twin lunar rings
                        Vector2 mid = new(0.5f, 0.52f);
                        Vector2 pMid = new(nx - mid.x, (ny - mid.y) * (h / (float)w));
                        float rMid = pMid.magnitude;
                        c = Blend(c, accent, RingAlpha(rMid, 0.36f, 0.014f) * 0.72f);
                        c = Blend(c, accentSoft, RingAlpha(rMid, 0.45f, 0.008f) * 0.52f);
                        c = Blend(c, accentSoft, RingAlpha(rMid, 0.26f, 0.008f) * 0.45f);
                    }

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_tarotBackdrops[idx] = ToSprite(tex);
            return s_tarotBackdrops[idx];
        }

        public static Sprite GetTarotCardFrame(int slotIndex)
        {
            int idx = Mathf.Clamp(slotIndex, 0, 2);
            if (s_tarotFrames[idx] != null) return s_tarotFrames[idx];

            const int w = 360, h = 640;
            Texture2D tex = CreateTex(w, h);
            Color[] px = new Color[w * h];

            Color outerCol = idx switch
            {
                0 => new Color(0.45f, 0.40f, 0.28f, 1f),
                1 => new Color(0.56f, 0.20f, 0.20f, 1f),
                _ => new Color(0.32f, 0.43f, 0.54f, 1f)
            };
            Color trimCol = GetSlotAccentColor(idx);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x);
                    int dy = Mathf.Min(y, h - 1 - y);
                    int edge = Mathf.Min(dx, dy);

                    Color c = Color.clear;
                    // Outer dark bronze/crimson/slate frame band (14px thick)
                    if (edge < 15)
                    {
                        c = outerCol;
                        // Crisp bright outer metallic line & inner metallic trim
                        if (edge == 2 || edge == 3 || edge == 13 || edge == 14)
                            c = trimCol;
                    }

                    // Corner diagonal chamfer & 4-point filigree star
                    int cornerDist = dx + dy;
                    if (cornerDist < 42)
                    {
                        c = outerCol;
                        if (cornerDist >= 37 && cornerDist <= 41)
                            c = trimCol;

                        // Little 4-point corner diamond star
                        int cx = dx - 15;
                        int cy = dy - 15;
                        if (Mathf.Abs(cx) + Mathf.Abs(cy) < 7)
                            c = trimCol;
                    }

                    // Top subtle arch header bar (matching Image 1!)
                    if (y > h - 38 && x > 65 && x < w - 65)
                    {
                        float tx = (x - w * 0.5f) / (w * 0.34f);
                        float archY = h - 15f - (1f - tx * tx) * 20f;
                        if (y >= archY)
                        {
                            c = idx == 2 ? trimCol : outerCol;
                            if (Mathf.Abs(y - archY) < 2.5f) c = trimCol;
                        }
                    }

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_tarotFrames[idx] = ToSprite(tex);
            return s_tarotFrames[idx];
        }

        public static Sprite GetDiamondBadge()
        {
            if (s_diamondBadge != null) return s_diamondBadge;
            const int sz = 128;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];
            float half = sz * 0.5f;

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float m = Mathf.Abs(nx) + Mathf.Abs(ny);

                    Color c = Color.clear;
                    if (m <= 0.94f)
                    {
                        if (m >= 0.82f)
                            c = new Color(1f, 0.98f, 0.93f, 0.95f); // Crisp ivory border
                        else
                            c = new Color(0.62f, 0.60f, 0.58f, 0.88f); // Warm silver-slate fill

                        // Crisp white X inside the diamond (exact match to Image 1!)
                        if (Mathf.Abs(nx) < 0.34f && Mathf.Abs(ny) < 0.34f)
                        {
                            float d1 = Mathf.Abs(nx - ny);
                            float d2 = Mathf.Abs(nx + ny);
                            if (Mathf.Min(d1, d2) < 0.085f)
                                c = Color.white;
                        }
                    }
                    px[y * sz + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_diamondBadge = ToSprite(tex);
            return s_diamondBadge;
        }

        public static Sprite GetRoundedRect()
        {
            if (s_roundedRect != null) return s_roundedRect;
            const int sz = 96;
            const float rad = 16f;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float d = SignedDistRoundedBox(x - sz * 0.5f + 0.5f, y - sz * 0.5f + 0.5f, sz * 0.5f - 2f, sz * 0.5f - 2f, rad);
                    float a = SmoothStep(1.2f, -1.2f, d);
                    px[y * sz + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_roundedRect = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
            return s_roundedRect;
        }

        public static Sprite GetRoundedOutline()
        {
            if (s_roundedOutline != null) return s_roundedOutline;
            const int sz = 96;
            const float rad = 16f;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float d = SignedDistRoundedBox(x - sz * 0.5f + 0.5f, y - sz * 0.5f + 0.5f, sz * 0.5f - 3f, sz * 0.5f - 3f, rad);
                    float a = SmoothStep(2.2f, 0.3f, Mathf.Abs(d + 2.5f));
                    px[y * sz + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_roundedOutline = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            return s_roundedOutline;
        }

        public static Sprite GetPadlockIcon()
        {
            if (s_padlock != null) return s_padlock;
            const int sz = 96;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];

            for (int y = 0; y < sz; y++)
            {
                float ny = y / (float)(sz - 1);
                for (int x = 0; x < sz; x++)
                {
                    float nx = x / (float)(sz - 1);
                    float a = 0f;

                    // Lock body (rounded rect lower half)
                    float bodyD = SignedDistRoundedBox((nx - 0.5f) * sz, (ny - 0.36f) * sz, 24f, 19f, 5f);
                    a = Mathf.Max(a, SmoothStep(1.2f, -1.2f, bodyD));

                    // Keyhole cutout
                    float keyCircle = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.39f));
                    bool keyStem = Mathf.Abs(nx - 0.5f) < 0.032f && ny > 0.27f && ny < 0.39f;
                    if (keyCircle < 0.055f || keyStem) a = 0f;

                    // Shackle arch (upper half)
                    if (ny >= 0.52f)
                    {
                        float shackleR = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.62f));
                        if (ny >= 0.62f)
                            a = Mathf.Max(a, RingAlpha(shackleR, 0.165f, 0.042f));
                        else
                        {
                            float leftBar = Mathf.Abs(nx - (0.5f - 0.165f));
                            float rightBar = Mathf.Abs(nx - (0.5f + 0.165f));
                            if (Mathf.Min(leftBar, rightBar) < 0.042f) a = 1f;
                        }
                    }

                    px[y * sz + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_padlock = ToSprite(tex);
            return s_padlock;
        }

        public static Sprite GetSoftRadialGlow()
        {
            if (s_softGlow != null) return s_softGlow;
            const int sz = 160;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];
            float half = sz * 0.5f;

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.15f);
                    px[y * sz + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_softGlow = ToSprite(tex);
            return s_softGlow;
        }

        public static Sprite GetSparkle()
        {
            if (s_sparkle != null) return s_sparkle;
            const int sz = 64;
            Texture2D tex = CreateTex(sz, sz);
            Color[] px = new Color[sz * sz];
            float half = sz * 0.5f;

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float nx = Mathf.Abs(x - half) / half;
                    float ny = Mathf.Abs(y - half) / half;
                    float star = Mathf.Clamp01(1f - Mathf.Pow(nx, 0.55f) - Mathf.Pow(ny, 0.55f));
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) * 2.1f);
                    float a = Mathf.Clamp01(Mathf.Max(star * 1.15f, core));
                    px[y * sz + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            s_sparkle = ToSprite(tex);
            return s_sparkle;
        }

        public static Color GetSlotAccentColor(int slotIndex)
        {
            return slotIndex switch
            {
                0 => new Color(0.92f, 0.72f, 0.24f, 1f), // Celestial Gold (Sun)
                1 => new Color(0.84f, 0.24f, 0.26f, 1f), // Crimson (Starburst)
                _ => new Color(0.28f, 0.66f, 0.94f, 1f)  // Azure (Lunar)
            };
        }

        private static Texture2D CreateTex(int w, int h)
        {
            Texture2D tex = new(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            return tex;
        }

        private static Sprite ToSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float RingAlpha(float r, float target, float halfThickness)
        {
            float d = Mathf.Abs(r - target);
            return SmoothStep(halfThickness, halfThickness * 0.25f, d);
        }

        private static float SignedDistRoundedBox(float px, float py, float bx, float by, float r)
        {
            float qx = Mathf.Abs(px) - bx + r;
            float qy = Mathf.Abs(py) - by + r;
            float outDist = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inDist = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outDist + inDist - r;
        }

        private static Color Blend(Color bg, Color fg, float alpha)
        {
            float a = Mathf.Clamp01(alpha * fg.a);
            return new Color(
                Mathf.Lerp(bg.r, fg.r, a),
                Mathf.Lerp(bg.g, fg.g, a),
                Mathf.Lerp(bg.b, fg.b, a),
                1f);
        }
    }
}

