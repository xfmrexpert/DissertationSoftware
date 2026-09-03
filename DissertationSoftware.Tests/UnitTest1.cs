using TfmrLib;
using GeometryLib;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;

namespace DissertationSoftware.Tests;

public class UnitTest1
{

    private static Transformer TwoTurnTfmr()
    {
        double WindowHt = Conversions.in_to_mm(120.0);

        var tfmr = new Transformer()
        {
            Core = new Core
            {
                CoreLegRadius_mm = Conversions.in_to_mm(0.0),
                NumLegs = 1,
                NumWoundLegs = 1,
                WindowWidth_mm = WindowHt,
                WindowHeight_mm = WindowHt
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
                                    InsulationThickness_mm = Conversions.in_to_mm(0.018),
                                    //rho_c = 0
                                },
                                NumDiscs = 1,
                                TurnsPerDisc = 1,
                                NumTurns = 1,
                                SpacerPattern = new RadialSpacerPattern
                                {
                                },
                                InnerRadius_mm = Conversions.in_to_mm(15.25),
                                DistanceAboveBottomYoke_mm = WindowHt / 2
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
                                DistanceAboveBottomYoke_mm = WindowHt / 2
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

        var femMatrixCalculator = new TfmrLib.FEMMatrixCalculator();
        var L = femMatrixCalculator.Calc_Lmatrix(tfmr, new TfmrLib.FEM.FrequencySpec.Scalar(60.0));
        var L_fem = L.First().Item2;
        var turn_lengths = tfmr.GetTurnLengths_m();
        Console.WriteLine("Turn Lengths (m):");
        PrintMatrix(turn_lengths.ToColumnMatrix());
        var one_over_turn_lengths = turn_lengths.Map(x => 1.0 / x);
        Console.WriteLine("Inductance Matrix (uH):");
        PrintMatrix(L_fem * 1e6);
        Console.WriteLine("Inductance per unit length (uH/m):");
        PrintMatrix(Matrix<double>.Build.DenseOfDiagonalVector(one_over_turn_lengths) * L_fem * 1e6);
        var L_PUL = Matrix<double>.Build.Dense(L_fem.RowCount, L_fem.ColumnCount);
        for (int i = 0; i < L_fem.RowCount; i++)
        {
            for (int j = 0; j < L_fem.ColumnCount; j++)
            {
                L_PUL[i, j] = L_fem[i, j] / turn_lengths[i];
            }
        }
        Console.WriteLine("Inductance per unit length (uH/m) calculated manually:");
        PrintMatrix(L_PUL * 1e6);

        var expected_L = Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { 2.681e-6, 0.5448e-6 },
            { 0.5448e-6, 4.845e-6 }
        });

        var analyticMatrixCalculator = new TfmrLib.AnalyticMatrixCalculator();
        var L_analytic = analyticMatrixCalculator.Calc_Lmatrix(tfmr, new TfmrLib.FEM.FrequencySpec.Scalar(60.0)).First().Item2;
        Console.WriteLine("Inductance (uH) from analytic calcs:");
        PrintMatrix(L_analytic * 1e6);

        var L_PUL_analytic = Matrix<double>.Build.Dense(L.First().Item2.RowCount, L.First().Item2.ColumnCount);
        for (int i = 0; i < L.First().Item2.RowCount; i++)
        {
            for (int j = 0; j < L.First().Item2.ColumnCount; j++)
            {
                L_PUL_analytic[i, j] = L_analytic[i, j] / turn_lengths[i];
            }
        }

        Console.WriteLine("Inductance per unit length (uH/m) from analytic calcs:");
        PrintMatrix(L_PUL_analytic * 1e6);

        for (int i = 0; i < expected_L.RowCount; i++)
        {
            for (int j = 0; j < expected_L.ColumnCount; j++)
            {
                Assert.InRange(L_fem[i, j], L_analytic[i, j] * 0.95, L_analytic[i, j] * 1.05);
            }
        }
    }

    [Fact]
    public void CapacitanceTest()
    {
        var tfmr = TwoTurnTfmr();
        Console.WriteLine($"Wdg1 radius: {tfmr.Windings[1].Segments[0].Geometry.InnerRadius_mm}");
        tfmr.Windings[1].Segments[0].Geometry.InnerRadius_mm = Conversions.in_to_mm(15.25 + 0.085 + 2 * 0.018);
        Console.WriteLine($"Wdg1 radius after: {tfmr.Windings[1].Segments[0].Geometry.InnerRadius_mm}");

        var femMatrixCalculator = new TfmrLib.FEMMatrixCalculator();
        var C = femMatrixCalculator.Calc_Cmatrix(tfmr);
        var turn_lengths = tfmr.GetTurnLengths_m();
        Console.WriteLine("Turn Lengths (m):");
        PrintMatrix(turn_lengths.ToColumnMatrix());
        var one_over_turn_lengths = turn_lengths.Map(x => 1.0 / x);
        Console.WriteLine("Capacitance Matrix (pF):");
        PrintMatrix(C * 1e12);
        Console.WriteLine("Capacitance per unit length (pF/m):");
        PrintMatrix(Matrix<double>.Build.DenseOfDiagonalVector(one_over_turn_lengths) * C * 1e12);
        var C_PUL = Matrix<double>.Build.Dense(C.RowCount, C.ColumnCount);
        for (int i = 0; i < C.RowCount; i++)
        {
            for (int j = 0; j < C.ColumnCount; j++)
            {
               C_PUL[i, j] = C[i, j] / turn_lengths[i];
            }
        }
        Console.WriteLine("Capacitance per unit length (pF/m) calculated manually:");
        PrintMatrix(C_PUL * 1e12);

        var expected_C = Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { 2.681e-6, 0.5448e-6 },
            { 0.5448e-6, 4.845e-6 }
        });

        var analyticMatrixCalculator = new TfmrLib.AnalyticMatrixCalculator();
        var C_PUL_analytic = analyticMatrixCalculator.Calc_Cmatrix(tfmr);
        Console.WriteLine("Capacitance per unit length (pF/m) from analytic calcs:");
        PrintMatrix(C_PUL_analytic * 1e12);

        var C_analytic = Matrix<double>.Build.Dense(C.RowCount, C.ColumnCount);
        for (int i = 0; i < C.RowCount; i++)
        {
            for (int j = 0; j < C.ColumnCount; j++)
            {
                C_analytic[i, j] = C_PUL_analytic[i, j];// * turn_lengths[i];
            }
        }

        Console.WriteLine("Capacitance Matrix (pF) from analytic calcs:");
        PrintMatrix(C_analytic * 1e12);

        for (int i = 0; i < expected_C.RowCount; i++)
        {
            for (int j = 0; j < expected_C.ColumnCount; j++)
            {
                Assert.InRange(C[i, j], C_analytic[i, j] * 0.95, C_analytic[i, j] * 1.05);
            }
        }
    }
}
