using System.IO;
using PlushieOdyssey.Map.Merchant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlushieOdyssey.Editor.Tools
{
    /// <summary>
    /// Bakes MerchantShopUI's complete hierarchy and persistent Sprite PNG assets directly into
    /// Merchant.unity so the entire Merchant Shop appears in Edit Mode (Scene View &amp; Game View)
    /// and every panel, slot, vendor sprite, dialogue box, and button can be edited freely in Unity.
    /// </summary>
    public static class MerchantShopEditorBaker
    {
        private const string MerchantScenePath = "Assets/_Project/Scenes/Merchant.unity";
        private const string SpriteDir = "Assets/_Project/Art/UI/Merchant";

        [MenuItem("Tools/Merchant/Bake Editable Merchant UI Into Scene", priority = 200)]
        public static void BakeIntoMerchantScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[MerchantShopEditorBaker] Exit Play Mode before baking Merchant.unity.");
                return;
            }

            EnsureDirectory(SpriteDir);

            Sprite solid = SaveSpritePng(SpriteDir + "/Merchant_Solid.png", BuildSolidTex(), Vector4.zero);
            Sprite circle = SaveSpritePng(SpriteDir + "/Merchant_Circle.png", BuildCircleTex(128), Vector4.zero);
            Sprite ring = SaveSpritePng(SpriteDir + "/Merchant_CircleRing.png", BuildCircleRingTex(128), Vector4.zero);
            Sprite glow = SaveSpritePng(SpriteDir + "/Merchant_SoftGlow.png", BuildSoftGlowTex(96), Vector4.zero);
            Sprite stamp = SaveSpritePng(SpriteDir + "/Merchant_StampBorder.png", BuildStampBorderTex(32, 4), new Vector4(4, 4, 4, 4));
            Sprite refresh = SaveSpritePng(SpriteDir + "/Merchant_RefreshArrows.png", BuildRefreshIconTex(160), Vector4.zero);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(MerchantScenePath, OpenSceneMode.Single);

            // Reload sprites inside the opened scene block so references stay rock-solid
            solid = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Merchant_Solid.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Merchant_Circle.png");
            ring = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Merchant_CircleRing.png");
            glow = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Merchant_SoftGlow.png");
            stamp = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Merchant_StampBorder.png");
            refresh = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Merchant_RefreshArrows.png");

            MerchantShopUI shop = Object.FindFirstObjectByType<MerchantShopUI>(FindObjectsInactive.Include);
            if (shop == null)
            {
                Debug.LogError("[MerchantShopEditorBaker] MerchantShopUI not found in Merchant.unity!");
                return;
            }

            SerializedObject so = new(shop);
            so.FindProperty("solidSprite").objectReferenceValue = solid;
            so.FindProperty("circleSprite").objectReferenceValue = circle;
            so.FindProperty("circleRingSprite").objectReferenceValue = ring;
            so.FindProperty("softGlowSprite").objectReferenceValue = glow;
            so.FindProperty("stampBorderSprite").objectReferenceValue = stamp;
            so.FindProperty("refreshIconSprite").objectReferenceValue = refresh;
            so.ApplyModifiedPropertiesWithoutUndo();

            RectTransform bakedRoot = shop.BuildBakedHierarchyForEditor();

            so.Update();
            so.FindProperty("bakedShopRoot").objectReferenceValue = bakedRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(shop);
            if (bakedRoot != null) EditorUtility.SetDirty(bakedRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[MerchantShopEditorBaker] Baked editable MerchantShopRoot hierarchy and 6 persistent UI sprites into Merchant.unity!");
        }

        private static void EnsureDirectory(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static Sprite SaveSpritePng(string assetPath, Texture2D tex, Vector4 border)
        {
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Texture2D BuildSolidTex()
        {
            Texture2D tex = new(8, 8, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildCircleTex(int size)
        {
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            float radius = half - 1.5f;
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    float a = Mathf.Clamp01(radius - d + 0.8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildCircleRingTex(int size)
        {
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            float outerR = half - 2f;
            float innerR = half - 7f;
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    float a = Mathf.Clamp01(outerR - d + 0.8f) * Mathf.Clamp01(d - innerR + 0.8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildSoftGlowTex(int size)
        {
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float norm = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half);
                    float a = Mathf.Pow(1f - norm, 2.2f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildStampBorderTex(int size, int border)
        {
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isB = x < border || x >= size - border || y < border || y >= size - border;
                    px[y * size + x] = isB ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildRefreshIconTex(int size)
        {
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            float ringR = size * 0.36f;
            float strokeHalf = size * 0.048f;
            Color32[] px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (deg < 0f) deg += 360f;

                    bool onArc = (deg >= 28f && deg <= 156f) || (deg >= 208f && deg <= 336f);
                    float a = onArc ? Mathf.Clamp01(strokeHalf - Mathf.Abs(dist - ringR) + 1.0f) : 0f;
                    a = Mathf.Max(a, SampleArrowhead(dx, dy, ringR, 26f * Mathf.Deg2Rad, true, size * 0.11f));
                    a = Mathf.Max(a, SampleArrowhead(dx, dy, ringR, 206f * Mathf.Deg2Rad, true, size * 0.11f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static float SampleArrowhead(float px, float py, float ringR, float angleRad, bool clockwise, float arrowSize)
        {
            Vector2 radial = new(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector2 tangent = clockwise ? new Vector2(radial.y, -radial.x) : new Vector2(-radial.y, radial.x);
            Vector2 anchor = radial * ringR;
            Vector2 tip = anchor + tangent * (arrowSize * 0.75f);
            Vector2 left = anchor - tangent * (arrowSize * 0.45f) + radial * (arrowSize * 0.72f);
            Vector2 right = anchor - tangent * (arrowSize * 0.45f) - radial * (arrowSize * 0.72f);

            Vector2 p = new(px, py);
            Vector2 e0 = left - tip, e1 = right - left, e2 = tip - right;
            Vector2 v0 = p - tip, v1 = p - left, v2 = p - right;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Mathf.Max(0.0001f, Vector2.Dot(e0, e0)));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Mathf.Max(0.0001f, Vector2.Dot(e1, e1)));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Mathf.Max(0.0001f, Vector2.Dot(e2, e2)));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(
                Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return Mathf.Clamp01(1f + Mathf.Sqrt(d.x) * Mathf.Sign(d.y));
        }
    }
}

