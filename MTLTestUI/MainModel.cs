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

namespace MTLTestUI
{
    public class MainModel
    {
        public Winding wdg = new Winding();
        public Mesh mesh = new Mesh();

        public void CalcMesh(double meshscale = 1.0, int meshorder = 1)
        {
            string onelab_dir = "C:\\Users\\tcraymond\\Downloads\\onelab-Windows64\\";
            string gmshPath = onelab_dir + "gmsh.exe";
            string model_prefix = "./";

            TDAP.GmshFile gmshFile = new TDAP.GmshFile("case.geo");
            gmshFile.lc = 0.1;
            TDAP.Geometry geometry = wdg.GenerateGeometry();
            gmshFile.CreateFromGeometry(geometry);
            gmshFile.writeFile();

            string model = model_prefix + "case";
            string model_msh = model + ".msh";
            string model_geo = model + ".geo";

            var sb = new StringBuilder();
            Process p = new Process();

            p.StartInfo.FileName = gmshPath;
            p.StartInfo.Arguments = $"{model_geo} -2 -order {meshorder} -clscale {meshscale} -v 3";
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
                throw new Exception($"Failed to run gmsh");
            }
            else
            {
                mesh.ReadFromMSH2File(model_msh);
            }
        }

        public Vector<double> CalcCapacitance(int posTurn)
        {
            string dir = posTurn.ToString();
            
            string model_prefix = $"./Results/{dir}/";
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"/Results/{dir}");

            var f = File.CreateText($"Results/{dir}/case.pro");

            f.WriteLine("Group{");
            f.WriteLine($"Air = Region[{wdg.phyAir}];");
            for (int i = 0; i < wdg.num_turns; i++)
            {
                f.WriteLine($"Turn{i} = Region[{wdg.phyTurnsCondBdry[i]}];");
            }
            //f.WriteLine($"TurnPos = Region[{phyTurnsCondBdry[posTurn]}];");
            //if (negTurn >= 0)
            //{
            //    f.WriteLine($"TurnNeg = Region[{phyTurnsCondBdry[negTurn]}];");
            //}
            //else
            //{
            //    f.WriteLine("TurnNeg = Region[{}];");
            //}
            //f.Write("TurnZero = Region[{");
            //bool firstTurn = true;
            //for (int i = 0; i < phyTurnsCond.Count(); i++)
            //{
            //    if ((i != posTurn) && (i != negTurn))
            //    {
            //        if (!firstTurn)
            //        {
            //            f.Write(", ");
            //        }
            //        else
            //        {
            //            firstTurn = false;
            //        }
            //        f.Write($"{phyTurnsCondBdry[i]}");
            //    }
            //}

            //f.Write("}];\n");
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

            // f.write(f"Ground = Region[{phyCore}];\n")
            f.WriteLine($"Axis = Region[{wdg.phyAxis}];");
            f.WriteLine($"Surface_Inf = Region[{wdg.phyInf}];");
            f.WriteLine("Vol_Ele = Region[{Air, TurnIns}];");
            f.Write("Sur_C_Ele = Region[{");
            //f.Write($"Turn{posTurn}}}];");
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
                dn[Region[Axis]] = 0; 
                epsr[Region[{{Air}}]] = {wdg.eps_oil};
                epsr[Region[{{TurnIns}}]] = {wdg.eps_paper};
            }}

            Constraint {{
                {{ Name ElectricScalarPotential; Type Assign;
                    Case {{
                        {{ Region Region[Surface_Inf]; Value 0; }}
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
            double freq = 1e3;

            var sb = new StringBuilder();
            Process p = new Process();

            //p.StartInfo.FileName = "cmd.exe";
            //p.StartInfo.Arguments = "/k " + mygetdp + " " + model_pro + " -msh " + model_msh + $" -setstring modelPath Results/{proc} -solve Electrostatics_v -pos Electrostatics_v -v 5";

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

            (double r, double z) = wdg.GetTurnMidpoint(posTurn);

            var resultFile = File.OpenText(model_prefix + "res/q.txt");
            string line = resultFile.ReadLine();
            var C_array = Array.ConvertAll(line.Split().Skip(2).ToArray(), Double.Parse);
            var C = Vector<double>.Build.Dense(C_array);
            C = C / r;
            resultFile.Close();
            return C;
        }

        public void CalcCapacitanceMatrix()
        {
            Matrix<double> C_getdp = Matrix<double>.Build.Dense(wdg.num_turns, wdg.num_turns);

            CalcMesh();

            //for (int i = 0; i < 8; i++)
            //{
            //    string procDirectory = Directory.GetCurrentDirectory() + $"/Results/{i}/";
            //    Directory.CreateDirectory(procDirectory);
            //    File.Copy("case.msh", procDirectory + "case.msh", true);
            //}

            //Parallel.For(0, num_discs * turns_per_disc, t =>
            //{
            for (int t = 0; t < wdg.num_turns; t++)
            {
                C_getdp.SetRow(t, CalcCapacitance(t));
                Console.WriteLine($"Self capacitance for turn {t}: {C_getdp[t, t]}");
            }//);


            //for (int t1 = 0; t1 < num_discs * turns_per_disc; t1++)
            //{
            //    for (int t2 = t1 + 1; t2 < num_discs * turns_per_disc; t2++)
            //    {
            //        double W = CalcCapacitance(t1, t2, proc);
            //        C_getdp[t1, t2] = C_getdp[t2, t1] = (W - (C_getdp[t1, t1] + C_getdp[t2, t2]) / 2) / 1;
            //        Debug.WriteLine($"Mutual capacitance between turn {t1} & {t2}: {C_getdp[t1, t2]}");
            //    }
            //}

            //Parallel.For(0, num_discs * turns_per_disc, t1 =>
            //{
            //    Parallel.For(t1+1, num_discs * turns_per_disc, t2 =>
            //    {
            //        int proc = Thread.CurrentThread.ManagedThreadId;
            //        string procDirectory = Directory.GetCurrentDirectory() + $"/Results/{proc}/";
            //        Directory.CreateDirectory(procDirectory);
            //        File.Copy("case.msh", procDirectory + "case.msh", true);
            //        double W = CalcCapacitance(t1, t2, proc);
            //        C_getdp[t1, t2] = C_getdp[t2, t1] = (W - (C_getdp[t1, t1] + C_getdp[t2, t2]) / 2) / 1;
            //        Console.WriteLine($"Mutual capacitance between turn {t1} & {t2}: {C_getdp[t1, t2]}");
            //    });
            //});
            Console.Write((C_getdp / 1e-12).ToMatrixString());
            DelimitedWriter.Write("C_getdp.csv", C_getdp, ",");
        }

        public Vector<double> CalcInductance(int posTurn, int negTurn, double freq, int order = 1)
        {

            string dir = posTurn.ToString();
            if (negTurn >= 0)
            {
                dir += "_" + negTurn.ToString();
            }
            string model_prefix = $"./Results/{dir}/";
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"/Results/{dir}");
#if true

            var f = File.CreateText($"Results/{dir}/case.pro");

            f.WriteLine($"FE_Order = {order};");

            f.WriteLine("Group{");

            bool firstTurn;

            if (false)
            {
                f.WriteLine($"Air = Region[{{{wdg.phyAir}}}];");
            }
            else
            {
                f.Write($"Air = Region[{{{wdg.phyAir}, ");
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
            f.WriteLine($"Axis = Region[{wdg.phyAxis}];");
            f.WriteLine($"Surface_Inf = Region[{wdg.phyInf}];");
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
            //f.WriteLine($"freq={freq};");
            f.WriteLine("Include \"../../GetDP_Files/L_s_inf.pro\";");
            f.Close();

            string onelab_dir = "C:\\Users\\tcraymond\\Downloads\\onelab-Windows64\\";
            string mygetdp = onelab_dir + "getdp.exe";
            

            string model = model_prefix + "case";
            string model_msh = "case.msh";
            string model_pro = model + ".pro";

            var sb = new StringBuilder();
            Process p = new Process();

            //p.StartInfo.FileName = "cmd.exe";
            //p.StartInfo.Arguments = "/k " + mygetdp + " " + model_pro + " -msh " + model_msh + $" -setstring modelPath Results/{dir}/ -setnumber freq " + freq.ToString() + " -solve Magnetodynamics2D_av -pos dyn -v 5";

            p.StartInfo.FileName = mygetdp;
            p.StartInfo.Arguments = model_pro + " -msh " + model_msh + $" -setstring modelPath Results/{dir}/ -setnumber freq " + freq.ToString() + " -solve Magnetodynamics2D_av -pos dyn -v 5";
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
#endif
            (double r, double z) = wdg.GetTurnMidpoint(posTurn);

            //with open(model_prefix +'L_s/res/out.txt') as f:
            //    line = f.readline()
            //    ind = float(line.split()[1])
            //return 2 * math.pi * ind

            //var resultFile = File.OpenText(model_prefix + "out.txt");
            //string line = resultFile.ReadLine();
            //var ind = double.Parse(line.Split()[2]);
            //resultFile.Close();
            //return ind;

            var resultFile = File.OpenText(model_prefix + "out.txt");
            string line = resultFile.ReadLine();
            var L_array = Array.ConvertAll(line.Split().Skip(1).Where((value, index) => index % 2 == 1).ToArray(), Double.Parse);
            var L = Vector<double>.Build.Dense(L_array);
            resultFile.Close();
            return L;
        }

        public void CalcInductanceMatrix(double freq, int order = 2)
        {
            Matrix<double> L_getdp = Matrix<double>.Build.Dense(wdg.num_turns, wdg.num_turns);

            Console.WriteLine($"Frequency: {freq.ToString("0.##E0")}");
            //CalcMesh();

            //Parallel.For(0, n_turns, t =>
            for (int t = 0; t < wdg.num_turns; t++)
            {
                L_getdp.SetRow(t, CalcInductance(t, -1, freq, order));
                (double r, double z) = wdg.GetTurnMidpoint(t);
                Console.WriteLine($"Self inductance for turn {t}: {L_getdp[t, t] / r / 1e-9}");
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

            Console.Write((L_getdp/1e-9).ToMatrixString());

            DelimitedWriter.Write($"L_getdp_{freq.ToString("0.00E0")}.csv", L_getdp, ",");
        }

        public void CalcFEMM(TDAP.Geometry geo)
        {
            var femm = new ActiveFEMM();
            femm.call2femm("newdocument(0)");
            double frequency = 60;
            femm.call2femm($"mi probdef({frequency},\"meters\",\"axi\",1e-8");
            foreach (var pt in geo.Points)
            {
                femm.call2femm($"mi_addnode({pt.x}, {pt.y})");
            }
            foreach (var line in geo.Lines)
            {
                femm.call2femm($"mi_addsegment({line.pt1.x}, {line.pt1.y}, {line.pt2.x}, {line.pt2.y})");
            }
        }

        public void CalcFEMM2(TDAP.Geometry geo)
        {
            FEMMFile femm = new FEMMFile();
            Dictionary<int, int> blockMap = new Dictionary<int, int>();
            Dictionary<int, int> circMap = new Dictionary<int, int>();
            int blkAir = femm.CreateNewBlockProp("Air");
            int blkPaper = femm.CreateNewBlockProp("Paper");
            int blkCu = femm.CreateNewBlockProp("Copper");
            blockMap[wdg.phyAir] = blkAir;
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
            
            femm.CreateFromGeometry(geo, blockMap, circMap);
            femm.ToFile("test.fem");
        }

       
    }

}
