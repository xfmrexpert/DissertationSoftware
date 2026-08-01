using MathNet.Numerics.LinearAlgebra;
using MatrixExponential;
using System.Numerics;
using Xunit;

namespace DissertationSoftware.Tests;

public class MatrixExponentialTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.5)]
    [InlineData(10.0)]
    public void Exponential_ComputesNilpotentMatrixAcrossPadeRanges(double value)
    {
        Matrix<Complex> matrix = Matrix<Complex>.Build.DenseOfArray(new[,]
        {
            { Complex.Zero, new Complex(value, 0.0) },
            { Complex.Zero, Complex.Zero }
        });
        Matrix<Complex> expected = Matrix<Complex>.Build.DenseOfArray(new[,]
        {
            { Complex.One, new Complex(value, 0.0) },
            { Complex.Zero, Complex.One }
        });

        Matrix<Complex> result = matrix.Exponential();

        AssertMatrixClose(expected, result, 1e-12);
    }

    [Fact]
    public void Exponential_ComputesRotationWithScalingAndSquaring()
    {
        const double angle = 20.0;
        Matrix<Complex> matrix = Matrix<Complex>.Build.DenseOfArray(new[,]
        {
            { Complex.Zero, new Complex(-angle, 0.0) },
            { new Complex(angle, 0.0), Complex.Zero }
        });
        Matrix<Complex> expected = Matrix<Complex>.Build.DenseOfArray(new[,]
        {
            { new Complex(Math.Cos(angle), 0.0), new Complex(-Math.Sin(angle), 0.0) },
            { new Complex(Math.Sin(angle), 0.0), new Complex(Math.Cos(angle), 0.0) }
        });

        Matrix<Complex> result = matrix.Exponential();

        AssertMatrixClose(expected, result, 1e-12);
    }

    [Fact]
    public void Exponential_ComputesComplexDiagonalMatrix()
    {
        MathNet.Numerics.LinearAlgebra.Vector<Complex> diagonal =
            MathNet.Numerics.LinearAlgebra.Vector<Complex>.Build.Dense(new[]
        {
            new Complex(1.0, 2.0),
            new Complex(-0.5, -3.0)
        });
        Matrix<Complex> matrix = Matrix<Complex>.Build.DenseOfDiagonalVector(diagonal);
        Matrix<Complex> expected = Matrix<Complex>.Build.DenseOfDiagonalVector(diagonal.PointwiseExp());

        Matrix<Complex> result = matrix.Exponential();

        AssertMatrixClose(expected, result, 1e-14);
    }

    [Fact]
    public void Exponential_RejectsNonSquareMatrix()
    {
        Matrix<Complex> matrix = Matrix<Complex>.Build.Dense(2, 3);

        Assert.Throws<ArgumentException>(() => matrix.Exponential());
    }

    [Fact]
    public void Exponential_RejectsZeroSizeMatrix()
    {
        Matrix<Complex> matrix = Matrix<Complex>.Build.Dense(0, 0);

        Assert.Throws<ArgumentException>(() => matrix.Exponential());
    }

    private static void AssertMatrixClose(Matrix<Complex> expected, Matrix<Complex> actual, double tolerance)
    {
        Assert.Equal(expected.RowCount, actual.RowCount);
        Assert.Equal(expected.ColumnCount, actual.ColumnCount);

        for (int row = 0; row < expected.RowCount; row++)
        {
            for (int column = 0; column < expected.ColumnCount; column++)
            {
                double error = Complex.Abs(expected[row, column] - actual[row, column]);
                Assert.True(
                    error <= tolerance,
                    $"Entry ({row}, {column}) differed by {error:G17}; expected {expected[row, column]}, actual {actual[row, column]}.");
            }
        }
    }
}
