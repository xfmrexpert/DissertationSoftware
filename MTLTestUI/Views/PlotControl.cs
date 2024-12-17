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
using TDAP;

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
        
        private List<(Point, Point)> _edges;
        private List<SKPoint> _points;

        protected void OnWheel(object? sender, PointerWheelEventArgs e) {
            double oldZoom = zoom;
            zoom = zoom * (1 + 0.1 * e.Delta.Y / Math.Abs(e.Delta.Y));
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

        public static readonly StyledProperty<TDAP.Geometry> GeometryProperty =
            AvaloniaProperty.Register<PlotControl, TDAP.Geometry>(nameof(Geometry));

        public static readonly StyledProperty<Mesh> MeshProperty =
            AvaloniaProperty.Register<PlotControl, Mesh>(nameof(Mesh));

        public TDAP.Geometry Geometry
        {
            get => GetValue(GeometryProperty);
            set
            {
                SetValue(GeometryProperty, value);
                SetInitialScale();
            }
        }

        public Mesh Mesh
        {
            get => GetValue(MeshProperty);
            set
            {
                SetValue(MeshProperty, value);
                GetUniqueEdges();
            }
        }

        private void GetUniqueEdges()
        {
            var rtnList = new List<(Point, Point)>();
            _points = new List<SKPoint>();
            var edges = Mesh.GetUniqueEdges();
            foreach (var edge in edges) 
            {
                rtnList.Add((new Point(edge.Item1.Node.X, edge.Item1.Node.Y), new Point(edge.Item2.Node.X, edge.Item2.Node.Y)));
                var _pt = new Point(edge.Item1.Node.X, edge.Item1.Node.Y);
                _points.Add(new SKPoint((float)_pt.X, (float)_pt.Y));
                var _pt2 = new Point(edge.Item2.Node.X, edge.Item2.Node.Y);
                _points.Add(new SKPoint((float)_pt2.X, (float)_pt2.Y));
            }
            _edges = rtnList;
        }

        public void SetInitialScale()
        {
            WorldBounds = Geometry.GetBounds();
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

            if (_edges is null) GetUniqueEdges();

            var pen = new Pen(Brushes.Green, 1, lineCap: PenLineCap.Square);
            var pen_mesh = new Pen(Brushes.Gray, 1, lineCap: PenLineCap.Square);
            var matrix = GetTransform();
            drawingContext.Custom(new CustomDrawOp(Bounds, _points, matrix, WorldBounds));
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
            
        }

    }

    // All points passed in here will be in screen coordinates
    class CustomDrawOp : ICustomDrawOperation
    {
        public CustomDrawOp(Rect bounds, List<SKPoint> mesh, SKMatrix transform, BoundingBox world)
        {
            Bounds = bounds;
            Mesh = mesh.ToArray();
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
                canvas.Clear(SKColors.Gray);
                SKRect bounds;
                canvas.GetLocalClipBounds(out bounds);
                //canvas.ResetMatrix();
                canvas.Concat(ref _transform);
                canvas.GetLocalClipBounds(out bounds);
                using SKPaint p1 = new() { Color = SKColors.Green, IsAntialias = true };
                using SKPaint p2 = new() { Color = SKColors.White, IsAntialias = true };

                canvas.DrawRect((float)World.MinX, (float)World.MinY, (float)World.Width, (float)World.Height, p1);

                if (Mesh != null)
                {             
                    //SKMatrix matrix = SKMatrix.CreateScale(Scale,-Scale);
                    //canvas.SetMatrix(matrix);
                    //foreach (var edge in Mesh)
                    //{
                    //    canvas.DrawLine(new SKPoint((float)edge.Item1.Node.X, (float)edge.Item1.Node.Y), new SKPoint((float)edge.Item2.Node.X, (float)edge.Item2.Node.Y), p2);
                    //}
                    for (int i = 0; i < Mesh.Length; i=i+2)
                    {
                        canvas.DrawLine(Mesh[i], Mesh[i+1], p2);
                    }
                }
                
                canvas.Restore();
            }
        }
    }


}
