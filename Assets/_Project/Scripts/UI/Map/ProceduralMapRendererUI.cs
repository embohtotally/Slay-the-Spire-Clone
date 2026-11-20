using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Map
{
    /// <summary>
    /// Reusable Left-to-Right Slay-the-Spire procedural map renderer built entirely from existing project Map art assets:
    /// - Warm parchment backdrop (map jawa.jpg)
    /// - Hand-inked curved dotted paths between connected nodes
    /// - Existing Icon_*.png sprites per node type + Pin Map.png "YOU ARE HERE" marker
    /// - Juicy Left-to-Right staggered procedural pop-up animation for nodes, dots, and pin drop
    /// - Node hover magnification, SFX, and rich node info tooltip
    /// Works both interactively (Map.unity) and as an in-combat peek overlay (Game.unity).
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralMapRendererUI : MonoBehaviour
    {
        [Header("Existing Project Map Art Assets")]
        [SerializeField] private Sprite _parchmentSprite;
        [SerializeField] private Sprite _currentPinSprite;
        [SerializeField] private Sprite _enemyIcon;
        [SerializeField] private Sprite _eliteIcon;
        [SerializeField] private Sprite _bossIcon;
        [SerializeField] private Sprite _eventIcon;
        [SerializeField] private Sprite _shopIcon;
        [SerializeField] private Sprite _restIcon;
        [SerializeField] private Sprite _treasureIcon;
        [SerializeField] private TMP_FontAsset _font;

        [Header("Visual Sizing & Animation")]
        [SerializeField] private bool _renderOwnParchmentBackground = true;
        [SerializeField] private float _nodeSize = 60f;
        [SerializeField] private float _bossNodeScale = 1.44f;
        [SerializeField] private float _dotSize = 7f;
        [SerializeField] private float _dotSpacing = 16f;
        [SerializeField] private float _columnStagger = 0.085f;
        [SerializeField] private Vector2 _boardPadding = new(62f, 52f);
        [SerializeField] private bool _showLegendBar = true;

        private sealed class NodeVisual
        {
            public MapNode Node;
            public RectTransform Root;
            public RectTransform AnimPivot;
            public CanvasGroup Group;
            public Image IconImage;
            public Image HaloImage;
            public Image VisitedRing;
            public Button Button;
            public Vector2 MappedPos;
            public float BaseScale;
            public bool IsCurrent;
            public bool IsAvailable;
            public bool IsReachableFuture;
            public bool IsInteractive;
            public bool Hovering;
            public float IdlePhase;
        }

        private static Sprite _circleSprite;
        private static Sprite _ringSprite;
        private static Sprite _solidSprite;

        private RectTransform _container;
        private RectTransform _parchmentRt;
        private RectTransform _mapPlotArea;
        private RectTransform _linesLayer;
        private RectTransform _nodesLayer;
        private RectTransform _pinLayer;
        private RectTransform _legendBar;
        private RectTransform _tooltipRoot;
        private CanvasGroup _tooltipGroup;
        private Image _tooltipBorder;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipStatus;
        private TextMeshProUGUI _tooltipDesc;

        private RectTransform _currentPinRt;
        private Vector2 _currentPinBasePos;
        private bool _pinSettled;

        private struct DotAnim
        {
            public RectTransform Rt;
            public Image Img;
            public float StartTime;
            public float Duration;
            public float TargetAlpha;
        }

        private readonly List<NodeVisual> _nodeVisuals = new();
        private readonly List<Tween> _activeTweens = new();
        private readonly List<DotAnim> _dotAnims = new();

        private MapGraph _graph;
        private string _currentNodeId;
        private Action<MapNode> _onNodeClicked;
        private NodeVisual _hoveredNode;
        private bool _isInitialized;

        public int RenderedNodeCount => _nodeVisuals.Count;
        public string CurrentPinnedNodeId => _currentNodeId;

        public void ConfigureAssets(
            Sprite parchment,
            Sprite currentPin,
            Sprite enemy,
            Sprite elite,
            Sprite boss,
            Sprite eventSprite,
            Sprite shop,
            Sprite rest,
            Sprite treasure,
            TMP_FontAsset font)
        {
            _parchmentSprite = parchment;
            _currentPinSprite = currentPin;
            _enemyIcon = enemy;
            _eliteIcon = elite;
            _bossIcon = boss;
            _eventIcon = eventSprite;
            _shopIcon = shop;
            _restIcon = rest;
            _treasureIcon = treasure;
            if (font != null) _font = font;
        }

        public void SetShowLegendBar(bool show) => _showLegendBar = show;
        public void SetRenderOwnParchmentBackground(bool renderOwn) => _renderOwnParchmentBackground = renderOwn;

        public void RenderMap(
            MapGraph graph,
            string currentNodeId,
            bool interactive,
            Action<MapNode> onNodeClicked = null,
            bool animateEntrance = true)
        {
            EnsureStructure();
            ClearVisuals();

            _graph = graph;
            _currentNodeId = currentNodeId;
            _onNodeClicked = onNodeClicked;
            _pinSettled = false;

            if (_graph == null || _graph.Nodes == null || _graph.Nodes.Count == 0) return;

            _graph.RefreshAvailability(currentNodeId);
            HashSet<string> reachableFuture = _graph.GetReachableFutureNodeIds(currentNodeId);

            // Force layout calculation so _mapPlotArea.rect has exact dimensions
            Canvas.ForceUpdateCanvases();
            Rect plotRect = _mapPlotArea.rect;
            if (plotRect.width < 100f || plotRect.height < 100f)
            {
                plotRect = new Rect(-680f, -260f, 1360f, 520f);
            }

            Dictionary<string, Vector2> mappedPositions = ComputeFittedPositions(_graph, plotRect);
            int totalLayers = Mathf.Max(1, _graph.Nodes.Max(n => n.Layer) + 1);

            // 1. Spawn curved hand-inked dotted trail connectors between connected nodes
            SpawnDottedConnections(_graph, mappedPositions, reachableFuture, currentNodeId, animateEntrance);

            // 2. Spawn all map nodes (Left -> Right) with existing Icon_*.png assets & procedural pop-in wave
            List<MapNode> orderedNodes = _graph.Nodes
                .OrderBy(n => n.Layer)
                .ThenByDescending(n => mappedPositions[n.Id].y)
                .ToList();

            Dictionary<int, int> rowCounterByLayer = new();
            NodeVisual currentVisual = null;

            foreach (MapNode node in orderedNodes)
            {
                if (!rowCounterByLayer.TryGetValue(node.Layer, out int rowIdx)) rowIdx = 0;
                rowCounterByLayer[node.Layer] = rowIdx + 1;

                Vector2 pos = mappedPositions[node.Id];
                bool isCurrent = !string.IsNullOrEmpty(currentNodeId) && node.Id == currentNodeId;
                bool isReachable = reachableFuture.Contains(node.Id);
                bool canClick = interactive && node.IsAvailable && !node.IsVisited;

                NodeVisual vis = CreateNodeVisual(node, pos, isCurrent, isReachable, canClick, rowIdx);
                _nodeVisuals.Add(vis);
                if (isCurrent) currentVisual = vis;

                float colDelay = 0.05f + node.Layer * _columnStagger + rowIdx * 0.025f;
                if (animateEntrance)
                {
                    AnimateNodePopIn(vis, colDelay, node.Type == MapNodeType.Boss);
                }
                else
                {
                    vis.Group.alpha = 1f;
                    vis.AnimPivot.localScale = Vector3.one * vis.BaseScale;
                }
            }

            // 3. Drop the hand-drawn Pin Map.png marker right onto the player's current node ("YOU ARE HERE")
            if (currentVisual != null)
            {
                float pinDelay = animateEntrance
                    ? 0.05f + currentVisual.Node.Layer * _columnStagger + 0.24f
                    : 0f;
                SpawnCurrentPin(currentVisual, pinDelay, animateEntrance);
            }
        }

        public void KillAnimations()
        {
            for (int i = 0; i < _activeTweens.Count; i++)
            {
                _activeTweens[i]?.Kill();
            }
            _activeTweens.Clear();
            _dotAnims.Clear();
        }

        private void OnDisable()
        {
            KillAnimations();
            HideTooltip();
        }

        private void Update()
        {
            float t = Time.unscaledTime;

            // Lightweight allocation-free trail dot pop-in animation (avoids spawning hundreds of DOTween sequences)
            for (int i = _dotAnims.Count - 1; i >= 0; i--)
            {
                DotAnim anim = _dotAnims[i];
                if (anim.Rt == null || anim.Img == null)
                {
                    _dotAnims.RemoveAt(i);
                    continue;
                }

                float elapsed = t - anim.StartTime;
                if (elapsed < 0f) continue;

                float u = Mathf.Clamp01(elapsed / anim.Duration);
                // Smooth OutBack spring curve
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float back = 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
                anim.Rt.localScale = Vector3.one * Mathf.Max(0f, back);

                Color c = anim.Img.color;
                c.a = anim.TargetAlpha * Mathf.Clamp01(u * 1.25f);
                anim.Img.color = c;

                if (u >= 1f)
                {
                    anim.Rt.localScale = Vector3.one;
                    c.a = anim.TargetAlpha;
                    anim.Img.color = c;
                    _dotAnims.RemoveAt(i);
                }
            }

            // Alive idle motion for available nodes & current node halo
            for (int i = 0; i < _nodeVisuals.Count; i++)
            {
                NodeVisual vis = _nodeVisuals[i];
                if (vis.AnimPivot == null) continue;

                if (vis.IsCurrent && vis.HaloImage != null)
                {
                    float pulse = 1.0f + Mathf.Sin(t * 3.4f) * 0.12f;
                    vis.HaloImage.rectTransform.localScale = Vector3.one * pulse;
                    Color hc = vis.HaloImage.color;
                    hc.a = 0.55f + Mathf.Sin(t * 3.4f) * 0.25f;
                    vis.HaloImage.color = hc;
                }

                if (!vis.Hovering && vis.IsAvailable && !vis.Node.IsVisited && !DOTween.IsTweening(vis.AnimPivot))
                {
                    float breathe = 1.0f + Mathf.Sin(t * 3.2f + vis.IdlePhase) * 0.065f;
                    vis.AnimPivot.localScale = Vector3.one * (vis.BaseScale * breathe);
                }
            }

            // Gentle bob on Pin Map.png once settled
            if (_currentPinRt != null && _pinSettled)
            {
                float bob = Mathf.Sin(t * 3.2f) * 5.5f;
                _currentPinRt.anchoredPosition = _currentPinBasePos + new Vector2(0f, bob);
            }
        }

        // ------------------------------------------------------------------
        // Layout Fitting (Left -> Right inside Container)
        // ------------------------------------------------------------------
        private Dictionary<string, Vector2> ComputeFittedPositions(MapGraph graph, Rect plotRect)
        {
            var result = new Dictionary<string, Vector2>(graph.Nodes.Count);
            float minX = graph.Nodes.Min(n => n.Position.x);
            float maxX = graph.Nodes.Max(n => n.Position.x);
            float minY = graph.Nodes.Min(n => n.Position.y);
            float maxY = graph.Nodes.Max(n => n.Position.y);

            float rawW = Mathf.Max(1f, maxX - minX);
            float rawH = Mathf.Max(1f, maxY - minY);
            float rawMidX = (minX + maxX) * 0.5f;
            float rawMidY = (minY + maxY) * 0.5f;

            float availW = Mathf.Max(220f, plotRect.width - _boardPadding.x * 2f);
            float availH = Mathf.Max(180f, plotRect.height - _boardPadding.y * 2f);

            float scaleX = availW / rawW;
            float scaleY = availH / rawH;
            // Allow gentle horizontal fill while preserving organic vertical proportions
            float uniformY = Mathf.Min(scaleY, scaleX * 1.15f);
            float usedScaleX = Mathf.Min(scaleX, 1.18f);
            float usedScaleY = Mathf.Min(uniformY, 1.12f);

            foreach (MapNode node in graph.Nodes)
            {
                float x = (node.Position.x - rawMidX) * usedScaleX;
                float y = (node.Position.y - rawMidY) * usedScaleY;
                result[node.Id] = new Vector2(x, y);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Dotted Curved Ink Trail Connectors
        // ------------------------------------------------------------------
        private void SpawnDottedConnections(
            MapGraph graph,
            Dictionary<string, Vector2> mappedPositions,
            HashSet<string> reachableFuture,
            string currentNodeId,
            bool animateEntrance)
        {
            Sprite dotSprite = GetCircleSprite();
            int layer = gameObject.layer;

            foreach (MapNode from in graph.Nodes)
            {
                if (from.NextNodeIds == null || from.NextNodeIds.Count == 0) continue;
                Vector2 p0 = mappedPositions[from.Id];

                foreach (string nextId in from.NextNodeIds)
                {
                    MapNode to = graph.GetNode(nextId);
                    if (to == null) continue;
                    Vector2 p2 = mappedPositions[to.Id];

                    bool traveled = graph.WasEdgeTraveled(from.Id, to.Id);
                    bool nextStep = (!string.IsNullOrEmpty(currentNodeId) && from.Id == currentNodeId && to.IsAvailable) ||
                                    (string.IsNullOrEmpty(currentNodeId) && from.IsAvailable);
                    bool futureOnRoute = (from.IsVisited || reachableFuture.Contains(from.Id)) && reachableFuture.Contains(to.Id);

                    Color dotColor;
                    float dotScale;
                    if (traveled)
                    {
                        dotColor = new Color(0.86f, 0.52f, 0.12f, 0.96f); // Warm inked amber-gold traveled trail
                        dotScale = 1.20f;
                    }
                    else if (nextStep)
                    {
                        dotColor = new Color(0.24f, 0.17f, 0.11f, 0.95f); // Crisp dark charcoal ink for immediate next choice
                        dotScale = 1.12f;
                    }
                    else if (futureOnRoute)
                    {
                        dotColor = new Color(0.30f, 0.24f, 0.18f, 0.72f); // Classic StS parchment dot color
                        dotScale = 1.0f;
                    }
                    else
                    {
                        dotColor = new Color(0.38f, 0.31f, 0.24f, 0.28f); // Dimmed skipped branch
                        dotScale = 0.85f;
                    }

                    // Trim endpoints so dots start cleanly outside node icon radii
                    Vector2 dir = p2 - p0;
                    float totalDist = dir.magnitude;
                    if (totalDist < 36f) continue;
                    Vector2 unit = dir / totalDist;

                    float startTrim = (from.Type == MapNodeType.Boss ? _nodeSize * 0.65f : _nodeSize * 0.44f);
                    float endTrim = (to.Type == MapNodeType.Boss ? _nodeSize * 0.66f : _nodeSize * 0.44f);
                    Vector2 start = p0 + unit * startTrim;
                    Vector2 end = p2 - unit * endTrim;

                    float drawDist = Vector2.Distance(start, end);
                    if (drawDist < 12f) continue;

                    // Subtle organic curve control point (deterministic per edge so redraws stay identical)
                    Vector2 perp = new(-unit.y, unit.x);
                    float hash = ((from.Layer * 31 + from.Column * 17 + to.Column * 13) % 7) - 3f;
                    float curveOffset = hash * 3.8f;
                    Vector2 ctrl = (start + end) * 0.5f + perp * curveOffset;

                    int dotCount = Mathf.Clamp(Mathf.RoundToInt(drawDist / _dotSpacing), 3, 24);
                    float edgeBaseDelay = 0.08f + from.Layer * _columnStagger;

                    for (int d = 0; d < dotCount; d++)
                    {
                        float u = dotCount == 1 ? 0.5f : (float)d / (dotCount - 1);
                        Vector2 dotPos = EvalQuadraticBezier(start, ctrl, end, u);

                        GameObject dotGo = new("TrailDot", typeof(RectTransform), typeof(Image));
                        dotGo.layer = layer;
                        RectTransform dotRt = (RectTransform)dotGo.transform;
                        dotRt.SetParent(_linesLayer, false);
                        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
                        dotRt.pivot = new Vector2(0.5f, 0.5f);
                        dotRt.anchoredPosition = dotPos;
                        dotRt.sizeDelta = Vector2.one * (_dotSize * dotScale);

                        Image dotImg = dotGo.GetComponent<Image>();
                        dotImg.sprite = dotSprite;
                        dotImg.raycastTarget = false;

                        if (animateEntrance)
                        {
                            dotImg.color = new Color(dotColor.r, dotColor.g, dotColor.b, 0f);
                            dotRt.localScale = Vector3.zero;
                            float dotDelay = edgeBaseDelay + d * 0.014f;
                            _dotAnims.Add(new DotAnim
                            {
                                Rt = dotRt,
                                Img = dotImg,
                                StartTime = Time.unscaledTime + dotDelay,
                                Duration = 0.17f,
                                TargetAlpha = dotColor.a
                            });
                        }
                        else
                        {
                            dotImg.color = dotColor;
                            dotRt.localScale = Vector3.one;
                        }
                    }
                }
            }
        }

        private static Vector2 EvalQuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float inv = 1f - t;
            return inv * inv * a + 2f * inv * t * b + t * t * c;
        }

        // ------------------------------------------------------------------
        // Map Node Visual Construction & Procedural Pop-In
        // ------------------------------------------------------------------
        private NodeVisual CreateNodeVisual(
            MapNode node,
            Vector2 pos,
            bool isCurrent,
            bool isReachableFuture,
            bool canClick,
            int rowIdx)
        {
            int layer = gameObject.layer;
            float baseScale = node.Type == MapNodeType.Boss ? _bossNodeScale : 1f;

            GameObject rootGo = new($"MapNode_{node.Id}_{node.Type}", typeof(RectTransform));
            rootGo.layer = layer;
            RectTransform rootRt = (RectTransform)rootGo.transform;
            rootRt.SetParent(_nodesLayer, false);
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = pos;
            rootRt.sizeDelta = new Vector2(_nodeSize, _nodeSize);

            GameObject pivotGo = new("AnimPivot", typeof(RectTransform), typeof(CanvasGroup));
            pivotGo.layer = layer;
            RectTransform pivotRt = (RectTransform)pivotGo.transform;
            pivotRt.SetParent(rootRt, false);
            pivotRt.anchorMin = pivotRt.anchorMax = new Vector2(0.5f, 0.5f);
            pivotRt.pivot = new Vector2(0.5f, 0.5f);
            pivotRt.sizeDelta = new Vector2(_nodeSize, _nodeSize);

            CanvasGroup cg = pivotGo.GetComponent<CanvasGroup>();

            // Soft glowing ring behind Current / Available / Boss nodes
            GameObject haloGo = new("Halo", typeof(RectTransform), typeof(Image));
            haloGo.layer = layer;
            RectTransform haloRt = (RectTransform)haloGo.transform;
            haloRt.SetParent(pivotRt, false);
            haloRt.anchorMin = Vector2.zero;
            haloRt.anchorMax = Vector2.one;
            haloRt.offsetMin = new Vector2(-12f, -12f);
            haloRt.offsetMax = new Vector2(12f, 12f);
            Image haloImg = haloGo.GetComponent<Image>();
            haloImg.sprite = GetRingSprite();
            haloImg.raycastTarget = false;

            if (isCurrent)
            {
                haloImg.color = new Color(1.00f, 0.78f, 0.22f, 0.85f);
                haloGo.SetActive(true);
            }
            else if (node.IsAvailable && !node.IsVisited)
            {
                haloImg.color = new Color(1.00f, 0.92f, 0.48f, 0.58f);
                haloGo.SetActive(true);
            }
            else if (node.Type == MapNodeType.Boss)
            {
                haloImg.color = new Color(0.85f, 0.24f, 0.22f, 0.42f);
                haloGo.SetActive(true);
            }
            else
            {
                haloGo.SetActive(false);
            }

            // Existing Map Icon Sprite (Icon_Enemy, Icon_Elite Boss, Icon_Boss, Icon_Event2, Icon_Merchant, Icon_Rest, Icon_Event)
            GameObject iconGo = new("Icon", typeof(RectTransform), typeof(Image), typeof(Button));
            iconGo.layer = layer;
            RectTransform iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(pivotRt, false);
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = GetNodeSprite(node.Type);
            iconImg.preserveAspect = true;
            iconImg.color = GetNodeTint(node, isCurrent, isReachableFuture);

            Button btn = iconGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.interactable = true; // Keep hover active even in peek mode; click checks canClick

            // Hand-inked cleared ring stamp for visited previous nodes
            Image visitedRing = null;
            if (node.IsVisited && !isCurrent)
            {
                GameObject stampGo = new("VisitedRing", typeof(RectTransform), typeof(Image));
                stampGo.layer = layer;
                RectTransform stampRt = (RectTransform)stampGo.transform;
                stampRt.SetParent(pivotRt, false);
                stampRt.anchorMin = Vector2.zero;
                stampRt.anchorMax = Vector2.one;
                stampRt.offsetMin = new Vector2(-5f, -5f);
                stampRt.offsetMax = new Vector2(5f, 5f);
                visitedRing = stampGo.GetComponent<Image>();
                visitedRing.sprite = GetRingSprite();
                visitedRing.color = new Color(0.32f, 0.62f, 0.28f, 0.85f);
                visitedRing.raycastTarget = false;
            }

            var vis = new NodeVisual
            {
                Node = node,
                Root = rootRt,
                AnimPivot = pivotRt,
                Group = cg,
                IconImage = iconImg,
                HaloImage = haloImg,
                VisitedRing = visitedRing,
                Button = btn,
                MappedPos = pos,
                BaseScale = baseScale,
                IsCurrent = isCurrent,
                IsAvailable = node.IsAvailable,
                IsReachableFuture = isReachableFuture,
                IsInteractive = canClick,
                IdlePhase = (node.Layer * 0.9f + rowIdx * 1.4f) % (Mathf.PI * 2f)
            };

            btn.onClick.AddListener(() => HandleNodeClicked(vis));
            MapNodeHoverProxy proxy = iconGo.AddComponent<MapNodeHoverProxy>();
            proxy.Init(this, vis);

            return vis;
        }

        private void AnimateNodePopIn(NodeVisual vis, float delay, bool isBoss)
        {
            vis.Group.alpha = 0f;
            vis.AnimPivot.anchoredPosition = new Vector2(0f, -20f);
            vis.AnimPivot.localScale = new Vector3(0.24f, 1.38f, 1f) * vis.BaseScale;
            vis.AnimPivot.localRotation = Quaternion.Euler(0f, 0f, isBoss ? -12f : -8f);

            float peakScaleX = (isBoss ? 1.26f : 1.18f) * vis.BaseScale;
            float peakScaleY = (isBoss ? 0.84f : 0.87f) * vis.BaseScale;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.AppendInterval(delay);

            // Phase 1: Energetic upward pop with squash overshoot
            seq.Append(vis.Group.DOFade(1f, 0.14f).SetEase(Ease.OutQuad));
            seq.Join(vis.AnimPivot.DOAnchorPosY(9f, 0.16f).SetEase(Ease.OutCubic));
            seq.Join(vis.AnimPivot.DOScale(new Vector3(peakScaleX, peakScaleY, 1f), 0.16f).SetEase(Ease.OutCubic));
            seq.Join(vis.AnimPivot.DOLocalRotate(new Vector3(0f, 0f, 3.5f), 0.16f).SetEase(Ease.OutCubic));

            // Phase 2: Spring rebound
            seq.Append(vis.AnimPivot.DOAnchorPosY(-2f, 0.11f).SetEase(Ease.InOutSine));
            seq.Join(vis.AnimPivot.DOScale(new Vector3(0.95f, 1.05f, 1f) * vis.BaseScale, 0.11f).SetEase(Ease.InOutSine));
            seq.Join(vis.AnimPivot.DOLocalRotate(new Vector3(0f, 0f, -1f), 0.11f).SetEase(Ease.InOutSine));

            // Phase 3: Crisp settle
            seq.Append(vis.AnimPivot.DOAnchorPosY(0f, 0.10f).SetEase(Ease.OutSine));
            seq.Join(vis.AnimPivot.DOScale(Vector3.one * vis.BaseScale, 0.10f).SetEase(Ease.OutSine));
            seq.Join(vis.AnimPivot.DOLocalRotate(Vector3.zero, 0.10f).SetEase(Ease.OutSine));

            _activeTweens.Add(seq);
        }

        private void SpawnCurrentPin(NodeVisual currentVis, float delay, bool animate)
        {
            int layer = gameObject.layer;
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;

            GameObject pinGo = new("CurrentNodePin", typeof(RectTransform), typeof(CanvasGroup));
            pinGo.layer = layer;
            _currentPinRt = (RectTransform)pinGo.transform;
            _currentPinRt.SetParent(_pinLayer, false);
            _currentPinRt.anchorMin = _currentPinRt.anchorMax = new Vector2(0.5f, 0.5f);
            _currentPinRt.pivot = new Vector2(0.5f, 0f);
            _currentPinRt.sizeDelta = new Vector2(54f, 64f);

            float yOffset = (_nodeSize * currentVis.BaseScale * 0.44f);
            _currentPinBasePos = currentVis.MappedPos + new Vector2(0f, yOffset);

            CanvasGroup pinCg = pinGo.GetComponent<CanvasGroup>();
            pinCg.blocksRaycasts = false;
            pinCg.interactable = false;

            // Existing Pin Map.png sprite!
            GameObject imgGo = new("PinSprite", typeof(RectTransform), typeof(Image));
            imgGo.layer = layer;
            RectTransform imgRt = (RectTransform)imgGo.transform;
            imgRt.SetParent(_currentPinRt, false);
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = new Vector2(0f, -12f);
            Image pinImg = imgGo.GetComponent<Image>();
            pinImg.sprite = _currentPinSprite != null ? _currentPinSprite : GetCircleSprite();
            pinImg.preserveAspect = true;
            pinImg.color = Color.white;
            pinImg.raycastTarget = false;

            // Crisp "YOU" pill directly above the pin
            GameObject tagGo = new("YouBadge", typeof(RectTransform), typeof(Image));
            tagGo.layer = layer;
            RectTransform tagRt = (RectTransform)tagGo.transform;
            tagRt.SetParent(_currentPinRt, false);
            tagRt.anchorMin = tagRt.anchorMax = new Vector2(0.5f, 1f);
            tagRt.pivot = new Vector2(0.5f, 0.5f);
            tagRt.anchoredPosition = new Vector2(0f, 4f);
            tagRt.sizeDelta = new Vector2(56f, 20f);
            Image tagBg = tagGo.GetComponent<Image>();
            tagBg.sprite = GetSolidSprite();
            tagBg.color = new Color(0.18f, 0.12f, 0.06f, 0.94f);
            tagBg.raycastTarget = false;

            TextMeshProUGUI tagText = CreateText(tagRt, "Label", font, 11f, FontStyles.Bold, new Color(1f, 0.88f, 0.42f, 1f), TextAlignmentOptions.Center);
            tagText.rectTransform.anchorMin = Vector2.zero;
            tagText.rectTransform.anchorMax = Vector2.one;
            tagText.rectTransform.offsetMin = Vector2.zero;
            tagText.rectTransform.offsetMax = Vector2.zero;
            tagText.text = "HERE";

            if (animate)
            {
                pinCg.alpha = 0f;
                _currentPinRt.anchoredPosition = _currentPinBasePos + new Vector2(0f, 48f);
                _currentPinRt.localScale = Vector3.one * 0.6f;

                Sequence seq = DOTween.Sequence().SetUpdate(true);
                seq.AppendInterval(delay);
                seq.Append(pinCg.DOFade(1f, 0.14f));
                seq.Join(_currentPinRt.DOAnchorPos(_currentPinBasePos, 0.34f).SetEase(Ease.OutBounce));
                seq.Join(_currentPinRt.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack));
                seq.OnComplete(() => _pinSettled = true);
                _activeTweens.Add(seq);
            }
            else
            {
                pinCg.alpha = 1f;
                _currentPinRt.anchoredPosition = _currentPinBasePos;
                _currentPinRt.localScale = Vector3.one;
                _pinSettled = true;
            }
        }

        // ------------------------------------------------------------------
        // Interaction & Hover Tooltip
        // ------------------------------------------------------------------
        private void HandleNodeClicked(NodeVisual vis)
        {
            if (vis == null || !vis.IsInteractive) return;
            Tune.SFX(SfxID.relic_claim);
            _onNodeClicked?.Invoke(vis.Node);
        }

        private void HandleNodeHoverEnter(NodeVisual vis)
        {
            if (vis == null) return;
            _hoveredNode = vis;
            vis.Hovering = true;

            vis.AnimPivot.DOKill();
            vis.AnimPivot.DOScale(Vector3.one * (vis.BaseScale * 1.18f), 0.14f).SetEase(Ease.OutBack).SetUpdate(true);
            Tune.SFX(SfxID.node_hover);
            ShowTooltip(vis);
        }

        private void HandleNodeHoverExit(NodeVisual vis)
        {
            if (vis == null) return;
            vis.Hovering = false;
            vis.AnimPivot.DOKill();
            vis.AnimPivot.DOScale(Vector3.one * vis.BaseScale, 0.14f).SetEase(Ease.OutQuad).SetUpdate(true);

            if (_hoveredNode == vis)
            {
                _hoveredNode = null;
                HideTooltip();
            }
        }

        private void ShowTooltip(NodeVisual vis)
        {
            if (_tooltipRoot == null || vis?.Node == null) return;

            (string title, string desc, Color accent) = GetNodeInfo(vis.Node.Type);
            string statusText;
            if (vis.IsCurrent)
                statusText = "<color=#FFD95E>YOU ARE HERE</color>";
            else if (vis.Node.IsVisited)
                statusText = "<color=#7EE88D>CLEARED</color>";
            else if (vis.IsInteractive)
                statusText = "<color=#7CE8FF>CLICK TO TRAVEL</color>";
            else if (vis.IsAvailable)
                statusText = "<color=#FFE07D>NEXT REACHABLE</color>";
            else if (vis.IsReachableFuture)
                statusText = $"<color=#C4CCD8>STEP {vis.Node.Layer + 1} / {Mathf.Max(1, _graph.LayerCount)}</color>";
            else
                statusText = "<color=#8E96A4>UNREACHABLE PATH</color>";

            _tooltipBorder.color = accent;
            _tooltipTitle.text = title;
            _tooltipTitle.color = Color.Lerp(accent, Color.white, 0.35f);
            _tooltipStatus.text = statusText;
            _tooltipDesc.text = desc;

            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();

            // Position cleanly above the hovered node (clamped inside _mapPlotArea)
            Vector2 tipSize = _tooltipRoot.sizeDelta;
            Rect bounds = _mapPlotArea.rect;
            float x = Mathf.Clamp(vis.MappedPos.x, bounds.xMin + tipSize.x * 0.5f + 12f, bounds.xMax - tipSize.x * 0.5f - 12f);
            float y = vis.MappedPos.y + (_nodeSize * vis.BaseScale * 0.5f) + tipSize.y * 0.5f + 16f;
            if (y + tipSize.y * 0.5f > bounds.yMax - 8f)
            {
                y = vis.MappedPos.y - (_nodeSize * vis.BaseScale * 0.5f) - tipSize.y * 0.5f - 16f;
            }
            _tooltipRoot.anchoredPosition = new Vector2(x, y);

            _tooltipGroup.DOKill();
            _tooltipGroup.alpha = 0f;
            _tooltipGroup.DOFade(1f, 0.11f).SetUpdate(true);
        }

        private void HideTooltip()
        {
            if (_tooltipRoot == null) return;
            _tooltipGroup?.DOKill();
            _tooltipRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Asset Helpers & Node Styling
        // ------------------------------------------------------------------
        private Sprite GetNodeSprite(MapNodeType type)
        {
            return type switch
            {
                MapNodeType.Enemy => _enemyIcon,
                MapNodeType.Elite => _eliteIcon != null ? _eliteIcon : _enemyIcon,
                MapNodeType.Boss => _bossIcon != null ? _bossIcon : _eliteIcon,
                MapNodeType.Event => _eventIcon,
                MapNodeType.Shop => _shopIcon,
                MapNodeType.Rest => _restIcon,
                MapNodeType.Treasure => _treasureIcon != null ? _treasureIcon : _eventIcon,
                _ => _enemyIcon
            };
        }

        private static Color GetNodeTint(MapNode node, bool isCurrent, bool isReachableFuture)
        {
            Color baseTint = node.Type == MapNodeType.Treasure
                ? new Color(1.00f, 0.88f, 0.44f, 1f)
                : Color.white;

            if (isCurrent) return baseTint;
            if (node.IsVisited) return new Color(baseTint.r * 0.72f, baseTint.g * 0.78f, baseTint.b * 0.68f, 0.90f);
            if (node.IsAvailable) return baseTint;
            if (isReachableFuture) return new Color(baseTint.r * 0.92f, baseTint.g * 0.92f, baseTint.b * 0.90f, 0.92f);
            return new Color(baseTint.r * 0.60f, baseTint.g * 0.56f, baseTint.b * 0.52f, 0.42f);
        }

        private static (string title, string desc, Color accent) GetNodeInfo(MapNodeType type)
        {
            return type switch
            {
                MapNodeType.Enemy => ("Enemy Encounter", "Battle hostile foes to earn Gold and Card Rewards.", new Color(0.95f, 0.45f, 0.38f, 1f)),
                MapNodeType.Elite => ("Elite Enemy", "A dangerous battle guarding higher rewards and rare Relics.", new Color(1.00f, 0.34f, 0.28f, 1f)),
                MapNodeType.Boss => ("Act Boss", "The final guardian waiting at the end of the path.", new Color(1.00f, 0.25f, 0.38f, 1f)),
                MapNodeType.Event => ("Mystery Event", "An unknown encounter with risky choices and surprises.", new Color(0.68f, 0.56f, 0.98f, 1f)),
                MapNodeType.Shop => ("Merchant", "Spend Gold on Cards, Upgrades, Relics, or Card Removal.", new Color(0.98f, 0.80f, 0.32f, 1f)),
                MapNodeType.Rest => ("Rest Site", "Catch your breath to restore HP or recover Will.", new Color(0.38f, 0.88f, 0.54f, 1f)),
                MapNodeType.Treasure => ("Treasure", "Claim valuable Gold or a Relic along the journey.", new Color(1.00f, 0.86f, 0.34f, 1f)),
                _ => (type.ToString(), "Journey node.", Color.white)
            };
        }

        // ------------------------------------------------------------------
        // Container & Legend Construction
        // ------------------------------------------------------------------
        private void EnsureStructure()
        {
            if (_isInitialized && _mapPlotArea != null) return;
            _isInitialized = true;

            _container = transform as RectTransform;
            int layer = gameObject.layer;

            // Optional parchment background (disable when parent already provides scroll.png!)
            Transform existingBg = transform.Find("ParchmentBg");
            if (_renderOwnParchmentBackground)
            {
                if (existingBg != null)
                {
                    _parchmentRt = (RectTransform)existingBg;
                    _parchmentRt.gameObject.SetActive(true);
                }
                else
                {
                    GameObject bgGo = new("ParchmentBg", typeof(RectTransform), typeof(Image));
                    bgGo.layer = layer;
                    _parchmentRt = (RectTransform)bgGo.transform;
                    _parchmentRt.SetParent(_container, false);
                    _parchmentRt.anchorMin = Vector2.zero;
                    _parchmentRt.anchorMax = Vector2.one;
                    _parchmentRt.offsetMin = Vector2.zero;
                    _parchmentRt.offsetMax = Vector2.zero;
                }

                Image bgImg = _parchmentRt.GetComponent<Image>();
                if (_parchmentSprite != null)
                {
                    bgImg.sprite = _parchmentSprite;
                    bgImg.color = Color.white;
                    bgImg.preserveAspect = false;
                }
                else
                {
                    bgImg.color = new Color(0.76f, 0.62f, 0.42f, 1f);
                }
                bgImg.raycastTarget = false;
            }
            else if (existingBg != null)
            {
                existingBg.gameObject.SetActive(false);
            }

            // Map plot region (leaves clean room at bottom for Legend bar if enabled)
            float bottomReserve = _showLegendBar ? 48f : 10f;
            _mapPlotArea = CreateChildRect(_container, "MapPlotArea", Vector2.zero, Vector2.one, new Vector2(8f, bottomReserve), new Vector2(-8f, -6f));
            _linesLayer = CreateChildRect(_mapPlotArea, "LinesLayer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _nodesLayer = CreateChildRect(_mapPlotArea, "NodesLayer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pinLayer = CreateChildRect(_mapPlotArea, "PinLayer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (_showLegendBar)
            {
                BuildLegendBar();
            }

            BuildTooltip();
        }

        private void BuildLegendBar()
        {
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = gameObject.layer;

            GameObject barGo = new("LegendBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            barGo.layer = layer;
            _legendBar = (RectTransform)barGo.transform;
            _legendBar.SetParent(_container, false);
            _legendBar.anchorMin = new Vector2(0.02f, 0f);
            _legendBar.anchorMax = new Vector2(0.98f, 0f);
            _legendBar.pivot = new Vector2(0.5f, 0f);
            _legendBar.anchoredPosition = new Vector2(0f, 4f);
            _legendBar.sizeDelta = new Vector2(0f, 36f);

            Image bg = barGo.GetComponent<Image>();
            bg.sprite = GetSolidSprite();
            bg.color = new Color(0.22f, 0.14f, 0.07f, 0.82f);
            bg.raycastTarget = false;

            HorizontalLayoutGroup hlg = barGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(14, 14, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            AddLegendItem("Enemy", _enemyIcon, Color.white, font);
            AddLegendItem("Elite", _eliteIcon, Color.white, font);
            AddLegendItem("Event", _eventIcon, Color.white, font);
            AddLegendItem("Shop", _shopIcon, Color.white, font);
            AddLegendItem("Rest", _restIcon, Color.white, font);
            AddLegendItem("Treasure", _treasureIcon, new Color(1f, 0.88f, 0.44f, 1f), font);
            AddLegendItem("Boss", _bossIcon, Color.white, font);
            if (_currentPinSprite != null)
            {
                AddLegendItem("Here", _currentPinSprite, Color.white, font);
            }
        }

        private void AddLegendItem(string label, Sprite icon, Color iconTint, TMP_FontAsset font)
        {
            int layer = gameObject.layer;
            GameObject itemGo = new($"Legend_{label}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            itemGo.layer = layer;
            RectTransform rt = (RectTransform)itemGo.transform;
            rt.SetParent(_legendBar, false);
            rt.sizeDelta = new Vector2(88f, 26f);

            HorizontalLayoutGroup hlg = itemGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            if (icon != null)
            {
                GameObject iconGo = new("Icon", typeof(RectTransform), typeof(Image));
                iconGo.layer = layer;
                RectTransform iconRt = (RectTransform)iconGo.transform;
                iconRt.SetParent(rt, false);
                iconRt.sizeDelta = new Vector2(22f, 22f);
                Image img = iconGo.GetComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.color = iconTint;
                img.raycastTarget = false;
            }

            TextMeshProUGUI txt = CreateText(rt, "Label", font, 12.5f, FontStyles.Bold, new Color(0.98f, 0.92f, 0.78f, 1f), TextAlignmentOptions.Left);
            txt.rectTransform.sizeDelta = new Vector2(58f, 22f);
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.text = label;
        }

        private void BuildTooltip()
        {
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = gameObject.layer;

            GameObject tipGo = new("MapNodeTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            tipGo.layer = layer;
            _tooltipRoot = (RectTransform)tipGo.transform;
            _tooltipRoot.SetParent(_mapPlotArea, false);
            _tooltipRoot.anchorMin = _tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            _tooltipRoot.sizeDelta = new Vector2(280f, 92f);

            _tooltipGroup = tipGo.GetComponent<CanvasGroup>();
            _tooltipGroup.blocksRaycasts = false;
            _tooltipGroup.interactable = false;

            _tooltipBorder = tipGo.GetComponent<Image>();
            _tooltipBorder.sprite = GetSolidSprite();
            _tooltipBorder.raycastTarget = false;

            GameObject bgGo = new("Bg", typeof(RectTransform), typeof(Image));
            bgGo.layer = layer;
            RectTransform bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(_tooltipRoot, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = GetSolidSprite();
            bgImg.color = new Color(0.11f, 0.08f, 0.05f, 0.96f);
            bgImg.raycastTarget = false;

            _tooltipTitle = CreateText(bgRt, "Title", font, 16f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            _tooltipTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tooltipTitle.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            _tooltipTitle.rectTransform.offsetMin = new Vector2(12f, -30f);
            _tooltipTitle.rectTransform.offsetMax = new Vector2(0f, -6f);

            _tooltipStatus = CreateText(bgRt, "Status", font, 11f, FontStyles.Bold, Color.white, TextAlignmentOptions.Right);
            _tooltipStatus.rectTransform.anchorMin = new Vector2(0.52f, 1f);
            _tooltipStatus.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tooltipStatus.rectTransform.offsetMin = new Vector2(0f, -28f);
            _tooltipStatus.rectTransform.offsetMax = new Vector2(-10f, -8f);

            _tooltipDesc = CreateText(bgRt, "Desc", font, 13f, FontStyles.Normal, new Color(0.92f, 0.88f, 0.80f, 1f), TextAlignmentOptions.TopLeft);
            _tooltipDesc.rectTransform.anchorMin = Vector2.zero;
            _tooltipDesc.rectTransform.anchorMax = Vector2.one;
            _tooltipDesc.rectTransform.offsetMin = new Vector2(12f, 8f);
            _tooltipDesc.rectTransform.offsetMax = new Vector2(-12f, -32f);
            _tooltipDesc.textWrappingMode = TextWrappingModes.Normal;

            tipGo.SetActive(false);
        }

        private void ClearVisuals()
        {
            KillAnimations();
            HideTooltip();
            _hoveredNode = null;
            _nodeVisuals.Clear();
            _currentPinRt = null;

            DestroyAllChildren(_linesLayer);
            DestroyAllChildren(_nodesLayer);
            DestroyAllChildren(_pinLayer);
        }

        private static void DestroyAllChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static RectTransform CreateChildRect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return (RectTransform)existing;

            GameObject go = new(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            return rt;
        }

        private static TextMeshProUGUI CreateText(
            RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        // Crisp procedural circular dot & ring textures for inked trails and current-node halos
        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int sz = 32;
            Texture2D tex = new(sz, sz, TextureFormat.RGBA32, false);
            float r = (sz - 2) * 0.5f;
            Vector2 c = new(sz * 0.5f - 0.5f, sz * 0.5f - 0.5f);
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = Mathf.Clamp01((r - d) * 0.95f + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int sz = 64;
            Texture2D tex = new(sz, sz, TextureFormat.RGBA32, false);
            float outerR = 29f;
            float innerR = 23f;
            Vector2 c = new(sz * 0.5f - 0.5f, sz * 0.5f - 0.5f);
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float aOut = Mathf.Clamp01((outerR - d) * 0.85f + 0.5f);
                    float aIn = Mathf.Clamp01((d - innerR) * 0.85f + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, aOut * aIn));
                }
            }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }

        private sealed class MapNodeHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private ProceduralMapRendererUI _owner;
            private NodeVisual _vis;

            public void Init(ProceduralMapRendererUI owner, NodeVisual vis)
            {
                _owner = owner;
                _vis = vis;
            }

            public void OnPointerEnter(PointerEventData eventData) => _owner?.HandleNodeHoverEnter(_vis);
            public void OnPointerExit(PointerEventData eventData) => _owner?.HandleNodeHoverExit(_vis);
        }
    }
}
