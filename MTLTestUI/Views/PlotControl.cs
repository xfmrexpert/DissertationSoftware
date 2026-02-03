// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using MeshLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using GeometryLib;
using Geometry = GeometryLib.Geometry;
using TfmrLib;

namespace MTLTestUI.Views
{
    public class PlotControl : UserControl
    {
        static PlotControl()
        {
            AffectsRender<PlotControl>(GeometryProperty);
            AffectsRender<PlotControl>(MeshProperty);
        }

        public PlotControl()
        {
            PointerWheelChanged += OnWheel;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            Background = new SolidColorBrush(Colors.Transparent);
        }

        private readonly Stopwatch _st = Stopwatch.StartNew();

        private BoundingBox? WorldBounds;
        private double zoom = 1.0;
        private bool isInDrag = false;
        private Point mouseStart;
        private Point panOffset = new(0, 0);
        private double panSpeed = 1;

        private SKPath? _geometryPath;
        private List<SKPoint>? _meshpoints;

        private List<(SKPoint Center, string Text, SKColor Color, float WorldW, float WorldH)>? _turnNumbers;

        // Store last mouse world coordinates
        private bool _haveWorldCursor;
        private (double X, double Y) _worldCursor;

        // Optional toggle to show coordinates
        public static readonly StyledProperty<bool> ShowWorldCoordinatesProperty =
            AvaloniaProperty.Register<PlotControl, bool>(nameof(ShowWorldCoordinates), true);

        public bool ShowWorldCoordinates
        {
            get => GetValue(ShowWorldCoordinatesProperty);
            set => SetValue(ShowWorldCoordinatesProperty, value);
        }

        private int? _hoverSurfaceTag;
        public TagManager? TagManager
        {
            get => GetValue(TagManagerProperty);
            set
            {
                SetValue(TagManagerProperty, value);
                InvalidateVisual();
            }
        }

        private LocationKey? _hoverLocation;
        private TagType _hoverTagType = TagType.None;

        protected void OnWheel(object? sender, PointerWheelEventArgs e)
        {
            if (e.Delta.Y == 0) return;

            // Capture world point under cursor BEFORE zoom change
            var mouseScreen = e.GetPosition(this);
            var oldMatrix = GetTransform();
            (double X, double Y) preWorld = default;
            bool havePreWorld = oldMatrix.TryInvert(out var oldInv);
            if (havePreWorld)
            {
                preWorld = TransformScreenToWorld(mouseScreen, oldInv);
            }

            // Apply zoom
            zoom *= (1 + 0.1 * Math.Sign(e.Delta.Y));
            if (zoom < 1e-6) zoom = 1e-6;

            // Adjust pan so that the same world point stays under the cursor
            if (havePreWorld)
            {
                var newMatrix = GetTransform();
                var newScreen = TransformWorldToScreen(preWorld, newMatrix);
                var deltaScreen = mouseScreen - new Point(newScreen.X, newScreen.Y);
                panOffset += deltaScreen;
            }

            InvalidateVisual();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!isInDrag)
            {
                mouseStart = e.GetPosition(this);
                isInDrag = true;
            }
        }

        protected void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var currPos = e.GetPosition(this);

            if (isInDrag)
            {
                var delta = (currPos - mouseStart);
                panOffset += delta * panSpeed;
                mouseStart = currPos;
            }

            UpdateWorldCursor(currPos);
            InvalidateVisual();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            isInDrag = false;
        }

        public static readonly StyledProperty<Geometry?> GeometryProperty =
            AvaloniaProperty.Register<PlotControl, Geometry?>(nameof(Geometry));

        public static readonly StyledProperty<Mesh?> MeshProperty =
            AvaloniaProperty.Register<PlotControl, Mesh?>(nameof(Mesh));

        public static readonly StyledProperty<TagManager?> TagManagerProperty =
            AvaloniaProperty.Register<PlotControl, TagManager?>(nameof(TagManager));

        public Geometry? Geometry
        {
            get => GetValue(GeometryProperty);
            set
            {
                SetValue(GeometryProperty, value);
                RebuildWorldBounds();
                RebuildGeometryPath();
                InvalidateVisual();
            }
        }

        public Mesh? Mesh
        {
            get => GetValue(MeshProperty);
            set
            {
                SetValue(MeshProperty, value);
                RebuildMeshPoints();
                InvalidateVisual();
            }
        }

        private void RebuildMeshPoints()
        {
            _meshpoints = null;
            if (Mesh is null) return;

            var pts = new List<SKPoint>();
            var edges = Mesh.GetUniqueEdges();
            foreach (var edge in edges)
            {
                pts.Add(new SKPoint((float)edge.Item1.Node.X, (float)edge.Item1.Node.Y));
                pts.Add(new SKPoint((float)edge.Item2.Node.X, (float)edge.Item2.Node.Y));
            }
            _meshpoints = pts;
        }

        private void RebuildGeometryPath()
        {
            _geometryPath = null;
            if (Geometry is null) return;

            var path = new SKPath();
            // Lines
            foreach (var line in Geometry.Lines)
            {
                path.MoveTo((float)line.pt1.x, (float)line.pt1.y);
                path.LineTo((float)line.pt2.x, (float)line.pt2.y);
            }
            // Arcs
            foreach (var arc in Geometry.Arcs)
            {
                double startAngle = Math.Atan2(arc.StartPt.y - arc.Center.y, arc.StartPt.x - arc.Center.x) * (180.0 / Math.PI);
                double sweepAngle = arc.SweepAngle * (180.0 / Math.PI);

                path.MoveTo((float)arc.StartPt.x, (float)arc.StartPt.y);
                var center = arc.Center;
                var radius = (float)arc.Radius;
                var rect = new SKRect(
                    (float)center.x - radius,
                    (float)center.y - radius,
                    (float)center.x + radius,
                    (float)center.y + radius);
                path.ArcTo(rect, (float)startAngle, (float)sweepAngle, false);
            }

            _turnNumbers = new List<(SKPoint Center, string Text, SKColor Color, float WorldW, float WorldH)>();
            foreach (var surface in Geometry.Surfaces)
            {
                if (TagManager?.TryGetLocationByTag(surface.Boundary.Tag, out var loc, out var tag_type) == true &&
                    tag_type == TagType.ConductorBoundary)
                {
                    var center = surface.GetCentroid();
                    var pt = new SKPoint((float)center.x, (float)center.y);

                    // Get world‑space bounding box of the conductor surface
                    var (minX, maxX, minY, maxY) = surface.Boundary.GetBoundingBox();
                    float ww = (float)(maxX - minX);
                    float wh = (float)(maxY - minY);
                    if (ww <= 0 || wh <= 0) continue;

                    SKColor color =
                        loc.StrandNumber == 0 ? SKColors.White :
                        loc.StrandNumber == 1 ? SKColors.Red :
                        loc.StrandNumber == 2 ? SKColors.Blue :
                        loc.StrandNumber == 3 ? SKColors.Lime :
                        SKColors.Yellow;

                    _turnNumbers.Add((pt, $"{loc.TurnNumber}", color, ww, wh));
                }
            }

            _geometryPath = path;
        }

        private void RebuildWorldBounds()
        {
            if (Geometry is null) { WorldBounds = null; return; }
            WorldBounds = Geometry.GetBounds();
        }

        public void ResetView()
        {
            zoom = 1.0;
            panOffset = new Point(0, 0);
            RebuildWorldBounds();
            InvalidateVisual();
        }

        public SKMatrix GetTransform()
        {
            if (WorldBounds is null)
                return SKMatrix.CreateIdentity();

            float canvasWidth = (float)Bounds.Width;
            float canvasHeight = (float)Bounds.Height;
            if (canvasWidth <= 0 || canvasHeight <= 0)
                return SKMatrix.CreateIdentity();

            float margin = 10f;

            float worldWidth = (float)WorldBounds.Width;
            float worldHeight = (float)WorldBounds.Height;
            if (worldWidth <= 0 || worldHeight <= 0)
                return SKMatrix.CreateIdentity();

            float worldCenterX = (float)WorldBounds.Center.x;
            float worldCenterY = (float)WorldBounds.Center.y;

            float effectiveCanvasWidth = canvasWidth - 2 * margin;
            float effectiveCanvasHeight = canvasHeight - 2 * margin;

            float scale = (float)zoom * Math.Min(effectiveCanvasWidth / worldWidth, effectiveCanvasHeight / worldHeight);

            // Center
            float translateX = canvasWidth / 2f - scale * worldCenterX + (float)panOffset.X;
            float translateY = canvasHeight / 2f - scale * worldCenterY - (float)panOffset.Y;

            // Flip Y (world up -> screen down)
            translateY = canvasHeight - translateY;

            return SKMatrix.CreateScaleTranslation(scale, -scale, translateX, translateY);
        }

        private void UpdateWorldCursor(Point screenPt)
        {
            var matrix = GetTransform();
            if (!matrix.TryInvert(out var inv))
            {
                _haveWorldCursor = false;
                return;
            }

            var w = TransformScreenToWorld(screenPt, inv);
            _worldCursor = (w.X, w.Y);
            _haveWorldCursor = true;

            if (Geometry is not null)
            {
                var surface = Geometry.HitTestSurface(w.X, w.Y);
                _hoverSurfaceTag = surface?.Tag;
            }
            else
            {
                _hoverSurfaceTag = null;
            }

            if (_hoverSurfaceTag is int tagVal && TagManager is not null)
            {
                if (TagManager.TryGetLocationByTag(tagVal, out var loc, out var tagType))
                {
                    _hoverLocation = loc;
                    _hoverTagType = tagType;
                }
                else
                {
                    _hoverLocation = null;
                    _hoverTagType = TagType.None;
                }
            }
            else
            {
                _hoverLocation = null;
                _hoverTagType = TagType.None;
            }
        }

        private static (double X, double Y) TransformScreenToWorld(Point screen, SKMatrix inv)
        {
            SKPoint mapped = inv.MapPoint((float)screen.X, (float)screen.Y);
            return (mapped.X, mapped.Y);
        }

        private static SKPoint TransformWorldToScreen((double X, double Y) world, SKMatrix direct)
        {
            return direct.MapPoint((float)world.X, (float)world.Y);
        }

        public override void Render(DrawingContext drawingContext)
        {
            if (Geometry is null) return;
            if (WorldBounds is null) RebuildWorldBounds();

            // Lazy rebuilds if needed (e.g. after geometry mutated externally)
            if (_geometryPath is null) RebuildGeometryPath();
            if (_meshpoints is null) RebuildMeshPoints();

            var matrix = GetTransform();

            drawingContext.Custom(new CustomDrawOp(
                Bounds,
                _geometryPath!,
                _turnNumbers!,
                _meshpoints!,
                matrix,
                WorldBounds!,
                ShowWorldCoordinates && _haveWorldCursor,
                _worldCursor,
                _hoverSurfaceTag,
                _hoverLocation,
                _hoverTagType));

            // Request next frame only while interacting (avoid continuous redraw)
            if (isInDrag)
                Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }
    }

    // Custom draw operation
    class CustomDrawOp : ICustomDrawOperation
    {
        public CustomDrawOp(Rect bounds,
                            SKPath geometryPath,
                            List<(SKPoint Center, string Text, SKColor Color, float WorldW, float WorldH)> turnNumbers,
                            IList<SKPoint> mesh,
                            SKMatrix transform,
                            BoundingBox world,
                            bool showCursor,
                            (double X, double Y) cursorWorld,
                            int? hoverTag,
                            LocationKey? hoverLocation,
                            TagType hoverTagType)
        {
            Bounds = bounds;
            Mesh = mesh;
            GeometryPath = geometryPath;
            _transform = transform;
            World = world;
            _turnNumbers = turnNumbers;
            _showCursor = showCursor;
            _cursorWorld = cursorWorld;
            _hoverTag = hoverTag;
            _hoverLocation = hoverLocation;
            _hoverTagType = hoverTagType;
        }

        private readonly List<(SKPoint Center, string Text, SKColor Color, float WorldW, float WorldH)> _turnNumbers;

        private readonly int? _hoverTag;
        private readonly LocationKey? _hoverLocation;
        private readonly TagType _hoverTagType;

        public void Dispose() { }

        public BoundingBox World { get; }
        public Rect Bounds { get; }
        public IList<SKPoint>? Mesh { get; }
        public SKPath GeometryPath { get; }

        private readonly SKMatrix _transform;
        private readonly bool _showCursor;
        private readonly (double X, double Y) _cursorWorld;

        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            canvas.Clear(SKColors.Black);

            var transform = _transform;
            canvas.Concat(ref transform);

            using SKPaint pGeom = new() { Color = SKColors.Green, IsAntialias = true, StrokeWidth = 2f / Math.Max(1f, _transform.ScaleX), Style = SKPaintStyle.Stroke };
            using SKPaint pMesh = new() { Color = SKColors.White, IsAntialias = true, StrokeWidth = 1f / Math.Max(1f, _transform.ScaleX) };

            // Mesh
            if (Mesh?.Count > 1)
            {
                for (int i = 0; i < Mesh.Count; i += 2)
                {
                    canvas.DrawLine(Mesh[i], Mesh[i + 1], pMesh);
                }
            }

            // Geometry
            if (GeometryPath is not null && !GeometryPath.IsEmpty)
            {
                canvas.DrawPath(GeometryPath, pGeom);
            }

            canvas.Restore();

            // Uniform-sized turn numbers: choose one size so ALL (fittable) labels fit.
            // If a conductor is too small to fit even the minimum font size, skip just that label.
            if (_turnNumbers is not null && _turnNumbers.Count > 0)
            {
                using SKPaint pText = new()
                {
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Consolas")
                };

                float scaleX = Math.Abs(_transform.ScaleX);
                float scaleY = Math.Abs(_transform.ScaleY);
                if (scaleX <= 0 || scaleY <= 0) return;

                const float baseSize = 20f;   // trial measurement size
                const float inset = 0.80f;    // occupy 80% of bbox
                const float minSize = 6f;
                const float maxSize = 200f;

                pText.TextSize = baseSize;
                pText.GetFontMetrics(out var fmTrial);
                float trialHeight = fmTrial.Descent - fmTrial.Ascent;
                if (trialHeight <= 0) return;

                float minAllowedScale = minSize / baseSize;
                float globalScaleFactor = float.PositiveInfinity;

                // Track only labels that can at least fit min font size
                var usableLabels = new List<(SKPoint Center, string Text, SKColor Color, float W, float H)>(_turnNumbers.Count);

                foreach (var (center, text, color, worldW, worldH) in _turnNumbers)
                {
                    if (worldW <= 0 || worldH <= 0) continue;

                    float trialWidth = pText.MeasureText(text);
                    if (trialWidth <= 0) continue;

                    float availPxW = worldW * scaleX * inset;
                    float availPxH = worldH * scaleY * inset;
                    if (availPxW <= 0 || availPxH <= 0) continue;

                    float scaleFactorW = availPxW / trialWidth;
                    float scaleFactorH = availPxH / trialHeight;
                    float labelFactor = MathF.Min(scaleFactorW, scaleFactorH);

                    // If even the minimum size would be too large, skip this label entirely
                    if (labelFactor < minAllowedScale)
                        continue;

                    usableLabels.Add((center, text, color, worldW, worldH));

                    if (labelFactor < globalScaleFactor)
                        globalScaleFactor = labelFactor;
                }

                if (usableLabels.Count == 0) return; // nothing can fit

                if (float.IsInfinity(globalScaleFactor) || globalScaleFactor <= 0)
                    return;

                float targetSize = baseSize * globalScaleFactor;
                if (targetSize < minSize) targetSize = minSize;
                else if (targetSize > maxSize) targetSize = maxSize;

                pText.TextSize = targetSize;
                pText.GetFontMetrics(out var fmFinal);
                float finalHeight = fmFinal.Descent - fmFinal.Ascent;
                float baselineCenterOffset = (finalHeight / 2f) - fmFinal.Descent;

                foreach (var (center, text, color, _, _) in usableLabels)
                {
                    pText.Color = color;
                    float textWidth = pText.MeasureText(text);
                    var screenPt = _transform.MapPoint(center);
                    lease.SkCanvas.DrawText(
                        text,
                        screenPt.X - textWidth / 2f,
                        screenPt.Y + baselineCenterOffset,
                        pText);
                }
            }

            if (_showCursor)
            {
                using SKPaint textPaint = new()
                {
                    Color = SKColors.Yellow,
                    IsAntialias = true,
                    TextSize = 14,
                    Typeface = SKTypeface.FromFamilyName("Consolas")
                };
                string line1 = $"X: {_cursorWorld.X:0.###}  Y: {_cursorWorld.Y:0.###}";
                string line2 = "";
                if (_hoverTag is int t)
                {
                    if (_hoverLocation is LocationKey loc)
                    {
                        line2 = $"Tag: {t}  Loc: W{loc.WindingId} S{loc.SegmentId} T{loc.TurnNumber} Str{loc.StrandNumber}  Type: {_hoverTagType}";
                    }
                    else
                    {
                        line2 = $"Tag: {t}";
                    }
                }
                const float margin = 6f;
                canvas.DrawText(line1, margin, (float)Bounds.Height - margin - 20f, textPaint);
                if (!string.IsNullOrEmpty(line2))
                    canvas.DrawText(line2, margin, (float)Bounds.Height - margin - 4f, textPaint);
            }
        }
    }
}
