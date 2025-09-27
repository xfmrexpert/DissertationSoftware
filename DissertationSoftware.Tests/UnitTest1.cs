using TfmrLib;
using GeometryLib;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;

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

    private static void PrintMatrix(Matrix<double> matrix)
    {
        int rows = matrix.RowCount;
        int cols = matrix.ColumnCount;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                Console.Write($"{matrix[i, j]:F4} ");
            }
            Console.WriteLine();
        }
    }

    [Fact]
    public void InductanceTest()
    {
        var tfmr = TwoTurnTfmr();
        var meshGen = new MeshGenerator();
        var geometry = tfmr.GenerateGeometry();
        meshGen.AddGeometry(geometry);
        var mesh = meshGen.GenerateMesh("case.geo", 100.0, 1);
        var femMatrixCalculator = new TfmrLib.FEMMatrixCalculator();
        var L = femMatrixCalculator.Calc_Lmatrix(tfmr, 60.0);
        var turn_lengths = tfmr.GetTurnLengths_m();
        Console.WriteLine("Turn Lengths (m):");
        PrintMatrix(turn_lengths.ToColumnMatrix());
        var one_over_turn_lengths = turn_lengths.Map(x => 1.0 / x);
        PrintMatrix(Matrix<double>.Build.DenseOfDiagonalVector(one_over_turn_lengths) * L);
    }
}
