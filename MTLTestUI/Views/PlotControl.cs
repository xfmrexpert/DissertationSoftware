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

        private BoundingBox worldBounds;
        private double scale = 1.0;
        private bool isInDrag = false;
        private Point mouseStart;
        private Point panWorldOffset = new Point(0, 0);
        private double panSpeed = 1;
        
        private List<(Point, Point)> _edges;
        private List<SKPoint> _points;

        protected void OnWheel(object? sender, PointerWheelEventArgs e) {
            var currPos = e.GetPosition(this);
            var mouseWorldBeforeZoom = ScreenToWorld(currPos);
            scale = scale * (1 + 0.1 * e.Delta.Y / Math.Abs(e.Delta.Y));
            var mouseWorldAfterZoom = ScreenToWorld(currPos);
            panWorldOffset = new Point(panWorldOffset.X - (mouseWorldBeforeZoom - mouseWorldAfterZoom).X, panWorldOffset.Y + (mouseWorldBeforeZoom - mouseWorldAfterZoom).Y);
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
                var delta = (currPos - mouseStart) / scale;
                panWorldOffset += delta * panSpeed;
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

        private List<SKPoint> GetMeshTriangles()
        {
            _points = new List<SKPoint>();
            if (Mesh != null)
            {
                foreach (var face in Mesh.Faces)
                {
                    MeshHalfEdge halfEdge = face.HalfEdge;
                    var _pt = WorldToScreen(new Point(halfEdge.PrevVertex.Node.X, halfEdge.PrevVertex.Node.Y));
                    _points.Add(new SKPoint((float)_pt.X, (float)_pt.Y));
                    while (halfEdge.NextHalfEdge != face.HalfEdge)
                    {
                        var _pt2 = WorldToScreen(new Point(halfEdge.NextVertex.Node.X, halfEdge.NextVertex.Node.Y));
                        _points.Add(new SKPoint((float)_pt2.X, (float)_pt2.Y));
                        halfEdge = halfEdge.NextHalfEdge;
                    }
                }
            }
            return _points;
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
            double _margin = 10;
            worldBounds = Geometry.GetBounds();

            if ((Bounds.Height / worldBounds.Height) > (Bounds.Width / worldBounds.Width))
            {
                scale = (Bounds.Width - _margin) / worldBounds.Width;
                //scale = Bounds.Height / height;
            }
            else
            {
                scale = (Bounds.Height - _margin) / worldBounds.Height;
                //scale = Bounds.Width / width;
            }
            var ScreenCenterInWorldCoords = new Point((Bounds.Width - _margin) / scale / 2, (Bounds.Height - _margin) / scale / 2);
            panWorldOffset = new Point(ScreenCenterInWorldCoords.X - worldBounds.Center.x, - ScreenCenterInWorldCoords.Y + worldBounds.Center.y);
        }

        

        public override void Render(DrawingContext drawingContext)
        {
            if (scale == 1.0) SetInitialScale();
            if (double.IsNaN(Bounds.Height) || double.IsNaN(Bounds.Width)) return;

            if (_edges is null) GetUniqueEdges();

            if (true)
            {
                drawingContext.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), _points));
                Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
            }
            else
            {

                var pen = new Pen(Brushes.Green, 1, lineCap: PenLineCap.Square);
                var pen_mesh = new Pen(Brushes.Gray, 1, lineCap: PenLineCap.Square);

                var renderSize = Bounds.Size;
                drawingContext.FillRectangle(Brushes.Black, new Rect(renderSize));

                if (Mesh != null)
                {

                    //foreach (var face in Mesh.Faces)
                    //{
                    //    List<Avalonia.Point> points = new List<Avalonia.Point>();
                    //    MeshHalfEdge halfEdge = face.HalfEdge;
                    //    points.Add(WorldToScreen(new Avalonia.Point(halfEdge.PrevVertex.Node.X, halfEdge.PrevVertex.Node.Y)));
                    //    while (halfEdge.NextHalfEdge != face.HalfEdge)
                    //    {
                    //        points.Add(WorldToScreen(new Avalonia.Point(halfEdge.NextVertex.Node.X, halfEdge.NextVertex.Node.Y)));
                    //        halfEdge = halfEdge.NextHalfEdge;
                    //    }
                    //    drawingContext.DrawGeometry(null, pen_mesh, new PolylineGeometry(points, false));
                    //}

                    if (_edges is null) GetUniqueEdges();

                    foreach (var edge in _edges)
                    {
                        drawingContext.DrawLine(pen_mesh, WorldToScreen(edge.Item1), WorldToScreen(edge.Item2));
                    }
                }

                if (Geometry != null)
                {
                    foreach (var line in Geometry.Lines)
                    {
                        drawingContext.DrawLine(pen, WorldToScreen(new Avalonia.Point(line.pt1.x, line.pt1.y)), WorldToScreen(new Avalonia.Point(line.pt2.x, line.pt2.y)));
                    }
                    foreach (var arc in Geometry.Arcs)
                    {
                        var sg = new StreamGeometry();
                        using (var sgc = sg.Open())
                        {
                            sgc.BeginFigure(WorldToScreen(new Avalonia.Point(arc.StartPt.x, arc.StartPt.y)), false);
                            sgc.ArcTo(WorldToScreen(new Avalonia.Point(arc.EndPt.x, arc.EndPt.y)), new Avalonia.Size(arc.Radius * scale, arc.Radius * scale), arc.SweepAngle, false, SweepDirection.Clockwise);
                            sgc.EndFigure(false);
                            drawingContext.DrawGeometry(Brushes.Red, pen, sg);
                        }

                    }
                }
            }
            
        }

        // World coordinates have origin at lower left and positive y is up and positive x is right
        // Screen coordinates have origin at top left, with positive y down and positive x to the right
        private Point WorldToScreen(Point worldPt)
        {
            return new Point((worldPt.X + panWorldOffset.X) * scale, (worldBounds.Height - worldPt.Y + panWorldOffset.Y) * scale);
        }

        private Point ScreenToWorld(Point screenPt)
        {
            return new Point(screenPt.X / scale - panWorldOffset.X, worldBounds.Height + panWorldOffset.Y - screenPt.Y / scale);
        }

    }

    // All points passed in here will be in screen coordinates
    class CustomDrawOp : ICustomDrawOperation
    {
        public CustomDrawOp(Rect bounds, List<SKPoint> mesh)
        {
            Bounds = bounds;
            Mesh = mesh.ToArray();
        }

        public void Dispose()
        {
            // No-op
        }

        public Rect Bounds { get; }
        public SKPoint[] Mesh { get; }
        public float Scale { get; }
        public SKPoint PanOffset {  get; }

        public bool HitTest(Avalonia.Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) { }
            //context.DrawGlyphRun(Brushes.Black, _noSkia);
            else
            {
                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;
                //canvas.Save();
                //canvas.Clear(SKColors.Black);

                using SKPaint p1 = new() { Color = SKColors.Green, IsAntialias = true };
                using SKPaint p2 = new() { Color = SKColors.White, IsAntialias = true };

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
                
                //canvas.Restore();
            }
        }
    }


}
