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
//using netDxf.Entities;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
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

        private BoundingBox WorldBounds;
        private double zoom = 1.0;
        private bool isInDrag = false;
        private Point mouseStart;
        private Point panOffset = new Point(0, 0);
        private double panSpeed = 1;

        private SKPath _geometry;
        private List<SKPoint> _meshpoints;

        protected void OnWheel(object? sender, PointerWheelEventArgs e) {
            double oldZoom = zoom;
            zoom *= (1 + 0.1 * e.Delta.Y / Math.Abs(e.Delta.Y));
            //Adjust Pan Offset for change in zoom to avoid wonky zooming when not centered
            panOffset = panOffset * zoom / oldZoom;
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
            if(isInDrag)
            {
                var currPos = e.GetPosition(this);
                var delta = (currPos - mouseStart);
                panOffset += delta * panSpeed;
                //System.Diagnostics.Debug.WriteLine($"Drag...pan offset: {panWorldOffset}");
                mouseStart = currPos;
                InvalidateVisual();
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (isInDrag)
            {
                isInDrag = false;
            }
        }

        public static readonly StyledProperty<Geometry> GeometryProperty =
            AvaloniaProperty.Register<PlotControl, Geometry>(nameof(Geometry));

        public static readonly StyledProperty<Mesh> MeshProperty =
            AvaloniaProperty.Register<PlotControl, Mesh>(nameof(Mesh));

        public Geometry Geometry
        {
            get => GetValue(GeometryProperty);
            set
            {
                SetValue(GeometryProperty, value);
                SetInitialScale();
                GetGeometry();
            }
        }

        public Mesh Mesh
        {
            get => GetValue(MeshProperty);
            set
            {
                SetValue(MeshProperty, value);
                GetMeshPoints();
            }
        }

        private void GetMeshPoints()
        {
            _meshpoints = new List<SKPoint>();
            if (Mesh is not null)
            {
                var edges = Mesh.GetUniqueEdges();
                foreach (var edge in edges)
                {
                    var _pt = new Point(edge.Item1.Node.X, edge.Item1.Node.Y);
                    _meshpoints.Add(new SKPoint((float)_pt.X, (float)_pt.Y));
                    var _pt2 = new Point(edge.Item2.Node.X, edge.Item2.Node.Y);
                    _meshpoints.Add(new SKPoint((float)_pt2.X, (float)_pt2.Y));
                }
            }
        }

        private void GetGeometry()
        {
            _geometry = new SKPath();
            foreach (var line in Geometry.Lines)
            {
                _geometry.MoveTo((float)line.pt1.x, (float)line.pt1.y);
                _geometry.LineTo((float)line.pt2.x, (float)line.pt2.y);
            }
            foreach (var arc in Geometry.Arcs)
            {
                // Calculate angles
                double startAngle = Math.Atan2(arc.StartPt.y - arc.Center.y, arc.StartPt.x - arc.Center.x) * (180.0 / Math.PI);
                //double endAngle = Math.Atan2(arc.EndPt.y - arc.Center.y, arc.EndPt.x - arc.Center.x) * (180.0 / Math.PI);
                double sweepAngle = arc.SweepAngle * (180.0 / Math.PI);

                _geometry.MoveTo((float)arc.StartPt.x, (float)arc.StartPt.y);
                var center = arc.Center;
                var radius = (float)arc.Radius;
                var rect = new SKRect((float)center.x - radius, (float)center.y - radius, (float)center.x + radius, (float)center.y + radius);
                _geometry.ArcTo(rect, (float)startAngle, (float)sweepAngle, false);
            }
        }

        public void SetInitialScale()
        {
            WorldBounds = Geometry?.GetBounds();
        }

        public SKMatrix GetTransform()
        {
            // Canvas dimensions and margin
            float canvasWidth = (float)Bounds.Width;
            float canvasHeight = (float)Bounds.Height;
            float margin = 10.0f; // Margin around the canvas

            // Compute canvas center
            float canvasCenterX = canvasWidth / 2;
            float canvasCenterY = canvasHeight / 2;

            // World rectangle dimensions
            float worldWidth = (float)WorldBounds.Width;
            float worldHeight = (float)WorldBounds.Height;

            // Compute world center
            float worldCenterX = (float)WorldBounds.Center.x;
            float worldCenterY = (float)WorldBounds.Center.y;

            // Effective canvas dimensions
            float effectiveCanvasWidth = canvasWidth - 2 * margin;
            float effectiveCanvasHeight = canvasHeight - 2 * margin;
         
            // Compute uniform scale
            float scale = (float)zoom * Math.Min(effectiveCanvasWidth / worldWidth, effectiveCanvasHeight / worldHeight);

            // Compute translation offsets (including pan offset)
            float translateX = canvasCenterX - scale * worldCenterX + (float)panOffset.X;
            float translateY = canvasCenterY - scale * worldCenterY - (float)panOffset.Y;

            // Adjust for Y-axis flip
            translateY = canvasHeight - translateY;

            // Create and return the transformation matrix
            return SKMatrix.CreateScaleTranslation(scale, -scale, translateX, translateY);
        }

        

        public override void Render(DrawingContext drawingContext)
        {
            if (WorldBounds is null) SetInitialScale();
            if (double.IsNaN(Bounds.Height) || double.IsNaN(Bounds.Width)) return;

            if (_meshpoints is null) GetMeshPoints();
            if (_geometry is null) GetGeometry();
            
            var matrix = GetTransform();
            drawingContext.Custom(new CustomDrawOp(Bounds, _geometry, _meshpoints, matrix, WorldBounds));
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
            
        }

    }

    // All points passed in here will be in screen coordinates
    class CustomDrawOp : ICustomDrawOperation
    {
        public CustomDrawOp(Rect bounds, SKPath geometry, List<SKPoint> mesh, SKMatrix transform, BoundingBox world)
        {
            Bounds = bounds;
            Mesh = [.. mesh];
            Geometry = geometry;
            _transform = transform;
            World = world;
        }

        public void Dispose()
        {
            // No-op
        }

        public BoundingBox World { get; }

        public Rect Bounds { get; }
        public SKPoint[] Mesh { get; }
        public SKPath Geometry { get; }

        private SKMatrix _transform;
        public SKMatrix Transform { get => _transform; }

        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;

        public SKMatrix GetTransform()
        {
            return Transform;
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) { }
            //context.DrawGlyphRun(Brushes.Black, _noSkia);
            else
            {
                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;
                canvas.Save();
                canvas.Clear(SKColors.Black);

                canvas.Concat(ref _transform);
                
                using SKPaint p1 = new() { Color = SKColors.Green, IsAntialias = true, StrokeWidth = 2f/_transform.ScaleX, Style = SKPaintStyle.Stroke };
                using SKPaint p2 = new() { Color = SKColors.White, IsAntialias = true };

                if (Mesh is not null)
                {
                    for (int i = 0; i < Mesh.Length; i += 2)
                    {
                        canvas.DrawLine(Mesh[i], Mesh[i+1], p2);
                    }
                }

                if (Geometry is not null)
                {
                    canvas.DrawPath(Geometry, p1);
                    //SKRect pathbounds;
                    //Geometry.GetBounds(out pathbounds);
                    //canvas.DrawRect(pathbounds, p1);
                }

                canvas.Restore();
            }
        }
    }


}
