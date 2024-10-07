// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDAP
{
    public enum FEMMLengthUnit
    {
        meters,
        centimeters,
        millimeters,
        inches
    }

    public enum FEMMProblemType
    {
        planar,
        axisymmetric
    }

    public enum FEMMBdryType
    {
        prescribedA=1,
        smallSkinDepth=2,
        mixed=3,
        strategicDualImage=4,
        periodic=5,
        antiPeriodic=6
    }

    public class FEMMFile
    {
        public double Format {  get; set; }
        public double Frequency { get; set; }
        public double Precision { get; set; }
        public double MinAngle { get; set; }
        public double Depth { get; set; }
        public FEMMLengthUnit LengthUnits { get; set; }
        public string Coordinates { get; set; }
        public FEMMProblemType ProblemType { get; set; }
        public double ExtZo {  get; set; }
        public double ExtRo { get; set; }
        public double ExtRi { get; set; }
        public string Comment { get; set; }

        List<FEMMPoint> Points { get; set; } = new List<FEMMPoint>();
        List<FEMMSegment> Segments { get; set; } = new List<FEMMSegment>();
        List<FEMMArcSegment> ArcSegments { get; set; } = new List<FEMMArcSegment>();
        List<FEMMHole> Holes { get; set; } = new List<FEMMHole>();
        List<FEMMBlockLabel> BlockLabels { get; set; } = new List<FEMMBlockLabel>();

        public void CreateFromGeometry(Geometry geometry)
        {
            Points.Clear();
            Segments.Clear();
            ArcSegments.Clear();

            foreach (var point in geometry.Points)
            {
                CreateNewPoint(point.x, point.y);
            }

            foreach (var line in geometry.Lines)
            {
                
            }

            foreach (var arc in geometry.Arcs)
            {
                
            }

            foreach (var loop in geometry.LineLoops)
            {
                
            }

            foreach (var surface in geometry.Surfaces)
            {
                
            }
        }

        public FEMMPoint CreateNewPoint(double x, double y)
        {
            return new FEMMPoint(x, y);
        }
    }

    public class FEMMPointProp
    {
        public double A_re { get; set; }
        public double A_im { get; set; }
        public double I_re { get; set; }
        public double I_im { get; set; }
    }

    public class FEMMBdryProp
    {
        public string BdryName { get; set; }
        public FEMMBdryType Type { get; set; }
        public double Mussd { get; set; }
        public double Sigmassd { get; set; }
        public double C0 { get; set; }
        public double C0i { get; set; }
        public double C1 { get; set; }
        public double C1i { get; set; }
        public double A_0 { get; set; }
        public double A_1 { get; set; }
        public double A_2 { get; set; }
        public double Phi { get; set; }
    }

    public class FEMMBlockProp
    {
        public string BlockName { get; set; }
        public double Mu_x { get; set; }
        public double Mu_y { get; set; }

    public class FEMMPoint
    {
        double X { get; set; }
        double Y { get; set; }
        int PointProp { get; set; }
        int GroupNum { get; set; }

        public FEMMPoint(double x, double y, int pointProp = 0, int groupNum = 0)
        {
            X = x;
            Y = y;
            PointProp = pointProp;
            GroupNum = groupNum;
        }
    }

    public class FEMMSegment
    {
        int StartPt { get; set; }
        int EndPt { get; set; }
        double MeshSize { get; set; }
        int BdryProp { get; set; }
        int HideInPost { get; set; }
        int GroupNum  { get; set; }

        public FEMMSegment(int startPt, int endPt, double meshSize, int bdryProp, int hideInPost, int groupNum)
        {
            StartPt = startPt;
            EndPt = endPt;
            MeshSize = meshSize;
            BdryProp = bdryProp;
            HideInPost = hideInPost;
            GroupNum = groupNum;
        }
    }

    public class FEMMArcSegment
    {
        int StartPt { get; set; }
        int EndPt { get; set; }
        double ArcAngle { get; set; }
        double MaxSegment {  get; set; }
        int BdryProp { get; set; }
        int HideInPost { get; set; }
        int GroupNum { get; set; }

        public FEMMArcSegment(int startPt, int endPt, double arcAngle, double maxSegment, int bdryProp, int hideInPost, int groupNum)
        {
            StartPt = startPt;
            EndPt = endPt;
            ArcAngle = arcAngle;
            MaxSegment = maxSegment;
            BdryProp = bdryProp;
            HideInPost = hideInPost;
            GroupNum = groupNum;
        }
    }

    public class FEMMHole
    {
        public double LabelX { get; set; }
        public double LabelY { get; set; }
        public int GroupNum { get; set; }

        public FEMMHole(double labelX, double labelY, int groupNum)
        {
            LabelX = labelX;
            LabelY = labelY;
            GroupNum = groupNum;
        }
    }

    public class FEMMBlockLabel
    {
        public double LabelX { get; set; }
        public double LabelY { get; set; }
        public int BlockType { get; set; }
        public double MeshSize { get; set; }
        public int Circuit { get; set; } = 0;
        public double MagDir { get; set; }
        public int GroupNum { get; set; }
        public int NumTurns { get; set; } = 1;
        public bool IsExternal { get; set; } = false;

        public FEMMBlockLabel(double labelX, double labelY, int blockType, double meshSize, int circuit, double magDir, int groupNum, int numTurns, bool isExternal)
        {
            LabelX = labelX;
            LabelY = labelY;
            BlockType = blockType;
            MeshSize = meshSize;
            Circuit = circuit;
            MagDir = magDir;
            GroupNum = groupNum;
            NumTurns = numTurns;
            IsExternal = isExternal;
        }
    }
}
