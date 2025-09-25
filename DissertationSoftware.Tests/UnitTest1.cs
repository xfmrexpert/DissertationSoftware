using TfmrLib;
using GeometryLib;

namespace DissertationSoftware.Tests;

public class UnitTest1
{

    private static Transformer TwoTurnTfmr()
    {
        var tfmr = new Transformer()
        {
            Core = new Core
            {
                CoreLegRadius_mm = Conversions.in_to_mm(0.0),
                NumLegs = 1,
                NumWoundLegs = 1,
                WindowWidth_mm = Conversions.in_to_mm(40.0),
                WindowHeight_mm = Conversions.in_to_mm(40.0)
            },
            Windings =
            {
                new Winding
                {
                    Label = "Winding 1",
                    Segments =
                    {
                        new WindingSegment
                        {
                            Label = "Segment 1",
                            Geometry = new DiscWindingGeometry
                            {
                                ConductorType = new RectConductor
                                {
                                    StrandHeight_mm = Conversions.in_to_mm(0.3),
                                    StrandWidth_mm = Conversions.in_to_mm(0.085),
                                    CornerRadius_mm = Conversions.in_to_mm(0.032),
                                    InsulationThickness_mm = Conversions.in_to_mm(0.018)
                                },
                                NumDiscs = 1,
                                TurnsPerDisc = 1,
                                NumTurns = 1,
                                SpacerPattern = new RadialSpacerPattern
                                {
                                },
                                InnerRadius_mm = Conversions.in_to_mm(15.25),
                                DistanceAboveBottomYoke_mm = Conversions.in_to_mm(15.0)
                            }
                        }
                    }
                },
                new Winding
                {
                    Label = "Winding 2",
                    Segments =
                    {
                        new WindingSegment
                        {
                            Label = "Segment 1",
                            Geometry = new DiscWindingGeometry
                            {
                                ConductorType = new RectConductor
                                {
                                    StrandHeight_mm = Conversions.in_to_mm(0.3),
                                    StrandWidth_mm = Conversions.in_to_mm(0.085),
                                    CornerRadius_mm = Conversions.in_to_mm(0.032),
                                    InsulationThickness_mm = Conversions.in_to_mm(0.018)
                                },
                                NumDiscs = 1,
                                TurnsPerDisc = 1,
                                NumTurns = 1,
                                SpacerPattern = new RadialSpacerPattern
                                {
                                },
                                InnerRadius_mm = Conversions.in_to_mm(25.25),
                                DistanceAboveBottomYoke_mm = Conversions.in_to_mm(15.0)
                            }
                        }
                    }
                }
            }
        };
        return tfmr;
    }

    [Fact]
    public void InductanceTest()
    {
        var tfmr = TwoTurnTfmr();
        var meshGen = new MeshGenerator();
        var geometry = tfmr.GenerateGeometry();
        meshGen.AddGeometry(geometry);
        var mesh = meshGen.GenerateMesh("case.geo", 1000.0, 1);
        var femMatrixCalculator = new TfmrLib.FEMMatrixCalculator();
        var L = femMatrixCalculator.Calc_Lmatrix(tfmr, 60.0);
    }
}
