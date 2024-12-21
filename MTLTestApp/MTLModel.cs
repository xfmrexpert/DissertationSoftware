using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MatrixExponential;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TfmrLib;
using LinAlg = MathNet.Numerics.LinearAlgebra;

namespace TfmrLib
{
    using Matrix_d = LinAlg.Matrix<double>;
    using Matrix_c = LinAlg.Matrix<Complex>;
    using Vector_d = LinAlg.Vector<double>;
    using Vector_c = LinAlg.Vector<Complex>;

    public class MTLModel : FreqResponseModel
    {
        private Matrix_d HA;
        private Matrix_d Gamma;

        private Matrix_d C;

        public MTLModel(Winding wdg) : base(wdg) { }
        public MTLModel(Winding wdg, double minFreq, double maxFreq, int numSteps) : base(wdg, minFreq, maxFreq, numSteps) { }

        // The following follows the axisymmetric MTL calulation outline in the Fattal paper
        // HA is lower left-hand quadrant of left-hand side matrix
        // HB is lower right-hand quadrant of the LHS matric
        // HA and HB are "terminal" constraints dictated by winding type (terminal here meaning the
        // terminations of each winding turn when viewed as parallel transmission lines)
        public Matrix_d CalcHA()   
        {
            Matrix_d HA11 = M_d.DenseIdentity(Wdg.num_turns);
            Matrix_d HA21 = M_d.Dense(Wdg.num_turns, Wdg.num_turns);
            Matrix_d HA12 = M_d.Dense(Wdg.num_turns, Wdg.num_turns);
            for (int t = 0; t < (Wdg.num_turns - 1); t++)
            {
                HA12[t + 1, t] = -1.0;
            }
            Matrix_d HA22 = M_d.Dense(Wdg.num_turns, Wdg.num_turns);
            HA22[Wdg.num_turns - 1, Wdg.num_turns - 1] = 1.0;
            Matrix_d HA1 = HA11.Append(HA12);
            Matrix_d HA2 = HA21.Append(HA22);
            return HA1.Stack(HA2);
        }

        public Matrix_c CalcHB(double f)
        {
            Matrix_c HB11 = M_c.Dense(Wdg.num_turns, Wdg.num_turns);
            HB11[0, 0] = Wdg.Rs; //Source impedance
            Matrix_c HB12 = M_c.Dense(Wdg.num_turns, Wdg.num_turns);
            Matrix_c HB21 = M_c.Dense(Wdg.num_turns, Wdg.num_turns);
            for (int t = 0; t < (Wdg.num_turns - 1); t++)
            {
                HB21[t, t + 1] = 1.0;
            }
            Matrix_c HB22 = -1.0 * M_c.DenseIdentity(Wdg.num_turns);
            HB22[Wdg.num_turns - 1, Wdg.num_turns - 1] = Wdg.Rl; //Impedance to ground
            Matrix_c HB1 = HB11.Append(HB12);
            Matrix_c HB2 = HB21.Append(HB22);
            Matrix_c HB = HB1.Stack(HB2);
            return HB;
        }

        public override Vector_c CalcResponseAtFreq(double f)
        {
            Matrix_c HB = CalcHB(f);

            Matrix_c B2 = HA.ToComplex().Append(HB);

            Matrix_d L = Wdg.Calc_Lmatrix(f);
            Matrix_d R_f = Wdg.Calc_Rmatrix(f);

            // A = [           0              -Gamma*(R+j*2*pi*f*L)]
            //     [ -Gamma*(G+j*2*pi*f*C)                0        ]
            Matrix_c A11 = M_c.Dense(Wdg.num_turns, Wdg.num_turns);
            Matrix_c A12 = -Gamma.ToComplex() * (R_f.ToComplex() + Complex.ImaginaryOne * 2d * Math.PI * f * L.ToComplex());
            Matrix_c A21 = -Gamma.ToComplex() * (Complex.ImaginaryOne * 2 * Math.PI * f * C.ToComplex());
            Matrix_c A22 = M_c.Dense(Wdg.num_turns, Wdg.num_turns);
            //Matrix_c A1 = M_c.Dense(Wdg.num_turns, Wdg.num_turns).Append(A12);
            //Matrix_c A2 = A21.Append(M_c.Dense(Wdg.num_turns, Wdg.num_turns));
            Matrix_c A = M_c.DenseOfMatrixArray(new Matrix_c[,] { { A11, A12 }, { A21, A22 } });
            Matrix_c Phi = A.Exponential();
            Matrix_c Phi1 = Phi.SubMatrix(0, Phi.RowCount, 0, Wdg.num_turns); //Phi[:,:n]
            Matrix_c Phi2 = Phi.SubMatrix(0, Phi.RowCount, Wdg.num_turns, Phi.ColumnCount - Wdg.num_turns); //Phi[:, n:]
            Matrix_c B11 = Phi1.Append((-1.0 * M_c.DenseIdentity(Wdg.num_turns)).Stack(M_c.Dense(Wdg.num_turns, Wdg.num_turns)));
            Matrix_c B12 = Phi2.Append(M_c.Dense(Wdg.num_turns, Wdg.num_turns).Stack(-1.0 * M_c.DenseIdentity(Wdg.num_turns)));
            Matrix_c B1 = B11.Append(B12);
            Matrix_c B = B1.Stack(B2);
            Vector_c v = V_c.Dense(4 * Wdg.num_turns);
            v[2 * Wdg.num_turns] = 1.0; // Set applied voltage
            return B.Solve(v);
        }

        protected override void Initialize()
        {
            C = Wdg.Calc_Cmatrix();

            // Gamma is the diagonal matrix of conductors radii (eq. 2)
            Gamma = M_d.DenseOfDiagonalVector(2d * Math.PI * Wdg.Calc_TurnRadii());
            HA = CalcHA();
        }
    }
}
