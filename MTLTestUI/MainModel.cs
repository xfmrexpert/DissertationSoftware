using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using Vector_d = MathNet.Numerics.LinearAlgebra.Vector<double>;
using MeshLib;
using TfmrLib;
using GeometryLib;

namespace MTLTestUI
{
    public class MainModel
    {
        private const string onelab_dir = "C:\\Users\\tcraymond\\Downloads\\onelab-Windows64\\";
        public Transformer tfmr;
        public Winding wdg;
        public Mesh mesh;

        public MainModel()
        {
            tfmr = new Transformer();
            wdg = new Winding();
            tfmr.Windings.Add(wdg);
            mesh = new Mesh();
        }
        
        public void CalcInductanceMatrix_FEMM(Geometry geom, double freq, int order = 2)
        {
            Matrix<double> L_getdp = Matrix<double>.Build.Dense(wdg.num_turns, wdg.num_turns);

            //Console.WriteLine($"Frequency: {freq.ToString("0.##E0")}");
            //CalcMesh();

            //Parallel.For(0, n_turns, t =>
            for (int t = 0; t < wdg.num_turns; t++)
            {
                L_getdp.SetRow(t, CalcInductance_FEMM(geom, freq, t));
                (double r, double z) = wdg.GetTurnMidpoint(t);
                //Console.WriteLine($"Self inductance for turn {t}: {L_getdp[t, t] / r / 1e-9}");
            }
            //);

            //Parallel.For(0, n_turns, t1 =>
            ////for (int t1 = 0; t1 < n_turns; t1++)
            //{
            //    Parallel.For(t1 + 1, n_turns, t2 =>
            //    //for (int t2 = t1 + 1; t2 < n_turns; t2++)
            //    {
            //        double W = CalcInductance(t1, t2, freq, order) / 2;
            //        L_getdp[t1, t2] = L_getdp[t2, t1] = (2 * W - (L_getdp[t1, t1] + L_getdp[t2, t2]))/2/-1;
            //        (double r, double z) = GetTurnMidpoint(t1);
            //        Console.WriteLine($"Mutual inductance between turn {t1} & {t2}: {L_getdp[t1, t2] / r / 1e-9}");
            //    }
            //    );
            //}
            //);

            Console.Write((L_getdp * 2 * Math.PI / 1e-9).ToMatrixString());

            for (int t1 = 0; t1 < wdg.num_turns; t1++)
            {
                (double r, double z) = wdg.GetTurnMidpoint(t1);
                for (int t2 = 0; t2 < wdg.num_turns; t2++)
                {
                    L_getdp[t1, t2] = L_getdp[t1, t2] / r;
                }
            }

            //Console.Write((L_getdp/1e-9).ToMatrixString());

            DelimitedWriter.Write($"L_femm_{freq.ToString("0.00E0")}.csv", L_getdp, ",");
        }

        public Vector_d CalcInductance_FEMM(Geometry geo, double freq, int turn)
        {
            FEMMFile femm = new FEMMFile();
            Dictionary<int, int> blockMap = new Dictionary<int, int>();
            Dictionary<int, int> circMap = new Dictionary<int, int>();
            femm.Frequency = freq;
            int blkAir = femm.CreateNewBlockProp("Air");
            int blkPaper = femm.CreateNewBlockProp("Paper");
            int blkCu = femm.CreateNewBlockProp("Copper");
            blockMap[tfmr.phyAir] = blkAir;
            int i = 0;
            foreach (var idx in wdg.phyTurnsCond)
            {
                blockMap[idx] = blkCu;
                circMap[idx] = i;
                i++;
            }
            foreach (var idx in wdg.phyTurnsIns)
            {
                blockMap[idx] = blkPaper;
            }
            blockMap[tfmr.phyInf] = blkAir;
            int blkAxis = femm.CreateNewBdryProp("Axis");

            femm.CreateFromGeometry(geo, blockMap, circMap);

            femm.BlockProps[blkAir].Mu_x = 1.0f;
            femm.BlockProps[blkAir].Mu_y = 1.0f;
            femm.BlockProps[blkPaper].Mu_x = 1.0f;
            femm.BlockProps[blkPaper].Mu_y = 1.0f;
            femm.BlockProps[blkCu].Mu_x = 1.0f;
            femm.BlockProps[blkCu].Mu_y = 1.0f;
            femm.BlockProps[blkCu].Sigma = 58f;

            femm.BdryProps[blkAxis].BdryType = FEMMBdryType.prescribedA;
            femm.BdryProps[blkAxis].A_0 = 0.0f;
            femm.BdryProps[blkAxis].A_1 = 0.0f;
            femm.BdryProps[blkAxis].A_2 = 0.0f;

            femm.CircuitProps[turn].TotalAmps_re = 1.0f;

            // TODO: Don't hard code this shit you lazy twat
            femm.ToFile("case.fem");

            Cli.Wrap("./bin/fkn.exe").WithArguments("case").ExecuteAsync().GetAwaiter().GetResult();

            string filePath = "inductances.txt";
            Vector_d inductances = Vector_d.Build.Dense(wdg.num_turns);
            Regex complexRegex = new Regex(@"([\d.eE+-]+)\s*\+\s*j([\d.eE+-]+)", RegexOptions.Compiled);

            try
            {
                int t = 0;
                foreach (string line in File.ReadLines(filePath))
                {
                    Match match = complexRegex.Match(line);
                    if (match.Success &&
                        double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double real) &&
                        double.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double imaginary))
                    {
                        inductances[t] = real / (2 * Math.PI);
                        t++;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Could not parse '{line}' as a complex number.");
                    }
                }

                //Console.WriteLine("Parsed Inductances:");
                //foreach (double inductance in inductances)
                //{
                //    Console.WriteLine(inductance);
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file: {ex.Message}");
            }

            return inductances;
        }
    }

}
