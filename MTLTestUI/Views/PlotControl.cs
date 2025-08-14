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
                _meshpoints!,
                matrix,
                WorldBounds!,
                ShowWorldCoordinates && _haveWorldCursor,
                _worldCursor));

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
                            List<SKPoint> mesh,
                            SKMatrix transform,
                            BoundingBox world,
                            bool showCursor,
                            (double X, double Y) cursorWorld)
        {
            Bounds = bounds;
            if (mesh is not null)
            {
                Mesh = [.. mesh];
            }
            GeometryPath = geometryPath;
            _transform = transform;
            World = world;
            _showCursor = showCursor;
            _cursorWorld = cursorWorld;
        }

        public void Dispose() { }

        public BoundingBox World { get; }
        public Rect Bounds { get; }
        public SKPoint[] Mesh { get; }
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
            if (Mesh?.Length > 1)
            {
                for (int i = 0; i < Mesh.Length; i += 2)
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

            if (_showCursor)
            {
                using SKPaint textPaint = new()
                {
                    Color = SKColors.Yellow,
                    IsAntialias = true,
                    TextSize = 14,
                    Typeface = SKTypeface.FromFamilyName("Consolas")
                };
                string txt = $"X: {_cursorWorld.X:0.###}  Y: {_cursorWorld.Y:0.###}";
                const float margin = 6f;
                canvas.DrawText(txt, margin, (float)Bounds.Height - margin - 4f, textPaint);
            }
        }
    }
}
