using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Data.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TDAP;
using System.Collections.Concurrent;
using System.Threading;
using System.Data;
using Femm;
using Avalonia.Media;
using TfmrLib;
using MeshLib;
using CliWrap;
using MathNet.Numerics.LinearAlgebra.Storage;
using CliWrap.EventStream;
using System.Reflection.Metadata.Ecma335;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MTLTestUI
{
    public class MainModel
    {
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

        public MathNet.Numerics.LinearAlgebra.Vector<double> CalcCapacitance(int posTurn, int order=1)
        {
            string dir = posTurn.ToString();
            
            string model_prefix = $"./Results/{dir}/";
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"/Results/{dir}");

            var f = File.CreateText($"Results/{dir}/case.pro");
            f.WriteLine($"FE_Order = {order};");
            f.WriteLine("Group{");
            f.WriteLine($"Air = Region[{tfmr.phyAir}];");
            for (int i = 0; i < wdg.num_turns; i++)
            {
                f.WriteLine($"Turn{i} = Region[{wdg.phyTurnsCondBdry[i]}];");
            }
            
            f.Write("TurnIns = Region[{");
            bool firstTurn = true;
            for (int i = 0; i < wdg.phyTurnsIns.Count(); i++)
            {
                if (!firstTurn)
                {
                    f.Write(", ");
                }
                else
                {
                    firstTurn = false;
                }
                f.Write($"{wdg.phyTurnsIns[i]}");
            }

            f.Write("}];\n");

            //f.WriteLine($"Ground = Region[{tfmr.phyCore}];")
            f.WriteLine($"Axis = Region[{tfmr.phyAxis}];");
            f.WriteLine($"Surface_Inf = Region[{tfmr.phyInf}];");
            f.WriteLine("Vol_Ele = Region[{Air, TurnIns}];");
            f.Write("Sur_C_Ele = Region[{");
            
            for (int i = 0; i < wdg.num_turns; i++)
            {
                f.Write($"Turn{i}");
                if (i < (wdg.num_turns - 1))
                {
                    f.Write(", ");
                }
                else
                {
                    f.Write("}];\n");
                }
            }
            if (tfmr.r_core == 0)
            {
                f.WriteLine($"Sur_Neu_Ele = Region[{tfmr.phyAxis}];");
            }
            f.WriteLine("}");

            //TODO: Fix for case where posTurn is last turn
            firstTurn = true;
            string otherTurns = "";
            for (int i = 0; i < wdg.num_turns; i++)
            {
                if (i != posTurn)
                {
                    if (!firstTurn)
                    {
                        otherTurns += ", ";
                    }
                    else
                    {
                        firstTurn = false;
                    }
                    otherTurns += $"Turn{i}";
                }
            }

            f.WriteLine($@"
            Flag_Axi = 1;

            Include ""../../GetDP_Files/Lib_Materials.pro"";

            Function {{
                {(tfmr.r_core==0 ? "dn[Region[Axis]] = 0;" : "")} 
                epsr[Region[{{Air}}]] = {tfmr.eps_oil};
                epsr[Region[{{TurnIns}}]] = {wdg.eps_paper};
            }}

            Constraint {{
                {{ Name ElectricScalarPotential; Type Assign;
                    Case {{
                        {{ Region Region[Surface_Inf]; Value 0; }}
                        {(tfmr.r_core>0 ? "{ Region Region[Axis]; Value 0;}" : "")}
                    }}
                }}
            }}
            Constraint {{                             
                {{ Name GlobalElectricPotential; Type Assign;
                    Case {{
                        {{ Region Region[Turn{posTurn}]; Value 1.0; }}
                        {{ Region Region[{{{otherTurns}}}]; Value 0; }}
                    }}
                }}
            }}
            Constraint {{ {{ Name GlobalElectricCharge; Case {{ }} }} }}

            Include ""../../GetDP_Files/Lib_Electrostatics_v.pro"";
            ");



            f.Close();

            string onelab_dir = "C:\\Users\\tcraymond\\Downloads\\onelab-Windows64\\";
            string mygetdp = onelab_dir + "getdp.exe";

            string model = model_prefix + "case";
            string model_msh = "case.msh";
            string model_pro = model + ".pro";

            var sb = new StringBuilder();
            Process p = new Process();

            p.StartInfo.FileName = mygetdp;
            p.StartInfo.Arguments = model_pro + " -msh " + model_msh + $" -setstring modelPath Results/{dir} -solve Electrostatics_v -pos Electrostatics_v -v 5";
            p.StartInfo.CreateNoWindow = true;

            // redirect the output
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            // hookup the eventhandlers to capture the data that is received
            p.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
            p.ErrorDataReceived += (sender, args) => sb.AppendLine(args.Data);

            // direct start
            p.StartInfo.UseShellExecute = false;

            p.Start();

            // start our event pumps
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // until we are done
            p.WaitForExit();

            string output = sb.ToString();

            int return_code = p.ExitCode;
            if (return_code != 0)
            {
                throw new Exception($"Failed to run getdp in CalcCapacitance for turn {posTurn}");
            }

            var resultFile = File.OpenText(model_prefix + "res/q.txt");
            string line = resultFile.ReadLine();
            var C_array = Array.ConvertAll(line.Split().Skip(2).ToArray(), Double.Parse);
            var C = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(C_array);
            resultFile.Close();
            return C;
        }

        public void CalcCapacitanceMatrix(int order=1)
        {
            Matrix<double> C_getdp = Matrix<double>.Build.Dense(wdg.num_turns, wdg.num_turns);

            //Parallel.For(0, num_discs * turns_per_disc, t =>
            //{
            for (int t = 0; t < wdg.num_turns; t++)
            {
                (double r, double z) = wdg.GetTurnMidpoint(t);
                C_getdp.SetRow(t, CalcCapacitance(t)/r);
            }//);

            DelimitedWriter.Write("C_getdp.csv", C_getdp, ",");
        }

        public MathNet.Numerics.LinearAlgebra.Vector<double> CalcInductance(int posTurn, int negTurn, double freq, int order = 1)
        {
            Console.WriteLine($"Frequency: {freq.ToString("0.##E0")} Turn: {posTurn}");
            string dir, model_prefix;
            WriteGetDPInductanceFile(posTurn, negTurn, freq, order, out dir, out model_prefix);

            string onelab_dir = "C:\\Users\\tcraymond\\Downloads\\onelab-Windows64\\";
            string mygetdp = onelab_dir + "getdp.exe";

            string model = model_prefix + "case";
            string model_msh = "case.msh";
            string model_pro = model + ".pro";

            int return_code = -999;

            while (return_code < 0)
            {
                var sb = new StringBuilder();
                Process p = new Process();

                p.StartInfo.FileName = mygetdp;
                p.StartInfo.Arguments = model_pro + " -msh " + model_msh + $" -setstring modelPath Results/{dir}/ -solve Magnetodynamics2D_av -pos dyn -v 5";
                p.StartInfo.CreateNoWindow = true;

                // redirect the output
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                // hookup the eventhandlers to capture the data that is received
                p.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
                p.ErrorDataReceived += (sender, args) => sb.AppendLine(args.Data);

                // direct start
                p.StartInfo.UseShellExecute = false;

                // Start process watchdog timer
                var timer = new System.Timers.Timer(60000); // 60 seconds
                timer.Elapsed += (sender, e) =>
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        Console.WriteLine("Process killed due to timeout.");
                        timer.Stop();
                        return_code = -1; // Set return code to indicate timeout
                        // Try again
                    }
                };

                p.Start();

                timer.Start();

                // start our event pumps
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                // until we are done
                p.WaitForExit();

                string output = sb.ToString();
                return_code = p.ExitCode;
                timer.Stop(); // Stop the timer if process exits normally
                p.Close();

            }

            
            if (return_code != 0)
            {
                throw new Exception($"Failed to run getdp in CalcInductance for turn {posTurn}");
            }

            (double r, double z) = wdg.GetTurnMidpoint(posTurn);

            var resultFile = File.OpenText(model_prefix + "out.txt");
            string? line = resultFile.ReadLine() ?? throw new Exception("Failed to read line from result file.");
            var L_array = Array.ConvertAll(line.Split().Skip(1).Where((value, index) => index % 2 == 1).ToArray(), Double.Parse);

            var L = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(L_array);
            resultFile.Close();

            return L;
        }

        private void WriteGetDPInductanceFile(int posTurn, int negTurn, double freq, int order, out string dir, out string model_prefix)
        {
            dir = posTurn.ToString();
            if (negTurn >= 0)
            {
                dir += "_" + negTurn.ToString();
            }
            model_prefix = $"./Results/{dir}/";
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"/Results/{dir}");

            var f = File.CreateText($"Results/{dir}/case.pro");

            f.WriteLine($"FE_Order = {order};");

            f.WriteLine("Group{");

            bool firstTurn;

            if (false)
            {
                f.WriteLine($"Air = Region[{{{tfmr.phyAir}}}];");
            }
            else
            {
                f.Write($"Air = Region[{{{tfmr.phyAir}, ");
                firstTurn = true;
                for (int i = 0; i < wdg.num_turns; i++)
                {
                    if (!firstTurn)
                    {
                        f.Write(", ");
                    }
                    else
                    {
                        firstTurn = false;
                    }
                    f.Write($"{wdg.phyTurnsIns[i]}");
                }
                f.WriteLine("}];");
            }

            for (int i = 0; i < wdg.num_turns; i++)
            {
                f.WriteLine($"Turn{i} = Region[{wdg.phyTurnsCond[i]}];");
            }

            f.WriteLine($"TurnPos = Region[{wdg.phyTurnsCond[posTurn]}];");
            if (negTurn >= 0)
            {
                f.WriteLine($"TurnNeg = Region[{wdg.phyTurnsCond[negTurn]}];");
            }
            else
            {
                f.WriteLine("TurnNeg = Region[{}];");
            }
            f.Write("TurnZero = Region[{");
            firstTurn = true;
            for (int i = 0; i < wdg.num_turns; i++)
            {
                if ((i != posTurn) && (i != negTurn))
                {
                    if (!firstTurn)
                    {
                        f.Write(", ");
                    }
                    else
                    {
                        firstTurn = false;
                    }
                    f.Write($"{wdg.phyTurnsCond[i]}");
                }
            }
            f.WriteLine("}];");
            //f.WriteLine($"Ground = Region[{phyGnd}];");
            //Surface_bn0 doesn't appear to do anything (also, surface?)
            f.WriteLine($"Axis = Region[{tfmr.phyAxis}];");
            f.WriteLine($"Surface_Inf = Region[{tfmr.phyInf}];");
            //f.WriteLine("Vol_C_Mag += Region[{TurnPos, TurnNeg, TurnZero}];");
            f.Write("Turns = Region[{");
            for (int i = 0; i < wdg.num_turns; i++)
            {
                f.Write($"Turn{i}");
                if (i < (wdg.num_turns - 1))
                {
                    f.Write(", ");
                }
                else
                {
                    f.Write("}];\n");
                }
            }
            f.WriteLine("Vol_Mag += Region[{Air, Turns, Surface_Inf}];");
            f.WriteLine("Vol_C_Mag = Region[{Turns}];");
            f.WriteLine("}");
            f.WriteLine($"Freq={freq};");
            f.WriteLine("Include \"../../GetDP_Files/L_s_inf.pro\";");
            f.Close();
        }

        public void CalcInductanceMatrix(double freq, int order = 1)
        {
            Matrix<double> L_getdp = Matrix<double>.Build.Dense(wdg.num_turns, wdg.num_turns);

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 8  // Limit to 4 concurrent threads
            };

            Parallel.For(0, wdg.num_turns, options, t =>
            //for (int t = 0; t < wdg.num_turns; t++)
            {
                var row = CalcInductance(t, -1, freq, order);
                // Take a lock to prevent two threads from writing to the matrix at the same time (just in case)
                lock (L_getdp)
                {
                    L_getdp.SetRow(t, row);
                }
                (double r, double z) = wdg.GetTurnMidpoint(t);
            }
            );

            Console.Write($"L total at {freq.ToString("0.##E0")}Hz: {(L_getdp * 2 * Math.PI).RowSums().Sum()/1000.0}mH\n");

            for (int t1 = 0; t1 < wdg.num_turns; t1++)
            {
                (double r, double z) = wdg.GetTurnMidpoint(t1);
                for (int t2 = 0; t2 < wdg.num_turns; t2++)
                {
                    L_getdp[t1, t2] = L_getdp[t1, t2] / r;
                }
            }

            DelimitedWriter.Write($"L_getdp_{freq.ToString("0.00E0")}.csv", L_getdp, ",");
        }

        public void CalcInductanceMatrix_FEMM(TDAP.Geometry geom, double freq, int order = 2)
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

        public MathNet.Numerics.LinearAlgebra.Vector<double> CalcInductance_FEMM(TDAP.Geometry geo, double freq, int turn)
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
            MathNet.Numerics.LinearAlgebra.Vector<double> inductances = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(wdg.num_turns);
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
