using MathNet.Numerics.Distributions;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LinAlg = MathNet.Numerics.LinearAlgebra;

namespace TfmrLib
{
    using Matrix_d = LinAlg.Matrix<double>;
    using Matrix_c = LinAlg.Matrix<Complex>;
    using Vector_d = LinAlg.Vector<double>;
    using Vector_c = LinAlg.Vector<Complex>;

    public abstract class FreqResponseModel
    {
        static protected MatrixBuilder<double> M_d = Matrix_d.Build;
        static protected MatrixBuilder<Complex> M_c = Matrix_c.Build;
        static protected VectorBuilder<double> V_d = Vector_d.Build;
        static protected VectorBuilder<Complex> V_c = Vector_c.Build;

        public Winding Wdg { get; set; }
        public double MinFreq { get; set; }
        public double MaxFreq { get; set; }
        public int NumSteps { get; set; }

        public FreqResponseModel(Winding wdg, double minFreq = 10e3, double maxFreq = 100e3, int numSteps = 1000)
        {
            Wdg = wdg;
            MinFreq = minFreq;
            MaxFreq = maxFreq;
            NumSteps = numSteps;
        }

        protected abstract void Initialize();

        // Should return complex vector of calculated voltage (magnitude and phase) at each turn
        public abstract Vector_c CalcResponseAtFreq(double f);

        // Impedances passed here are per unit length
        // C matrix should be in self/mutual form, not Maxwell form
        public List<double[]> CalcResponse(IProgress<int> progress = null)
        {
            Initialize();

            // Create vector of frequencies
            var freqs = Generate.LogSpaced(NumSteps, Math.Log10(MinFreq), Math.Log10(MaxFreq));

            List<Vector_c> V_turn = [];
            List<Complex> Y = [];
            int totalSteps = freqs.Count();
            int i = 0;
            foreach (var f in freqs)
            {
                ++i;
                //Console.WriteLine($"Calculating at {f / 1e6}MHz");
                var gain_at_freq = CalcResponseAtFreq(f);
                //var gain_at_freq = vi_vec / vi_vec[0];
                V_turn.Add(gain_at_freq); //[n: 2 * n]
                //Y.Add(vi_vec[2 * Wdg.num_turns]); //Original code took absolute val of vi_vec[2*n]
                progress?.Report((int)((i + 1) / (double)totalSteps * 100));
            }

            var V_response = new List<double[]>();
            for (int t = 0; t < Wdg.num_turns; t++)
            {
                V_response.Add(new double[NumSteps]);
            }

            // Enumerate through raw turn responses (vector of Complex Gain for each turn) at each frequency
            for (int f = 0; f < NumSteps; f++)
            {
                // Enumerate each turn
                for (int t = 0; t < Wdg.num_turns; t++)
                {
                    //Translate to dB
                    V_response[t][f] = 20d * Math.Log10(V_turn[f][t].Magnitude);
                }
            }

            return V_response;
        }

        public List<List<double>> CalcResponseLumped_Deprecated()
        {


            // Create vector of frequencies
            var freqs = Generate.LogSpaced(NumSteps, Math.Log10(MinFreq), Math.Log10(MaxFreq));

            var Z_term = new List<double>();
            //List of lists
            var V_r = new List<List<double>>();

            for (int t = 0; t < Wdg.num_turns; t++)
            {
                V_r.Add(new List<double>());
            }

            foreach (var f in freqs)
            {
                // TODO: sort out return values
                CalcResponseAtFreq(f);
            }

            return V_r;
        }
    }
}
