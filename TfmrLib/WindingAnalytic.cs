using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TfmrLib
{
    public class WindingAnalytic : Winding
    {
        //PUL Inductances
        public override Matrix<double> Calc_Lmatrix(double f = 60)
        {
            var M = Matrix<double>.Build;

            Matrix<double> L = M.Dense(num_turns, num_turns);

            for (int i = 0; i < num_turns; i++)
            {
                (double r_i, double z_i) = GetTurnMidpoint(i);
                L[i, i] = CalcSelfInductance(h_cond, t_cond, r_i, f) / (2 * Math.PI * r_i);
                for (int j = i + 1; j < num_turns; j++)
                {
                    (double r_j, double z_j) = GetTurnMidpoint(j);
                    L[i, j] = CalcMutualInductance_Lyle(r_i, z_i, h_cond, t_cond, r_j, z_j, h_cond, t_cond) / (2 * Math.PI * r_i);
                    L[j, i] = CalcMutualInductance_Lyle(r_i, z_i, h_cond, t_cond, r_j, z_j, h_cond, t_cond) / (2 * Math.PI * r_j);
                }
            }

            return L;
        }

        public override Matrix<double> Calc_Cmatrix()
        {
            double eps_0 = 8.854e-12;

            var M = Matrix<double>.Build;

            Matrix<double> C = M.Dense(num_turns, num_turns);

            //TODO: Need to modify to go from out to in and in to out
            for (int i = 0; i < num_discs; i++)
            {
                double C_abv;
                double C_bel;
                int n_abv;
                int n_bel;
                double dist_to_ground = 10;
                double k = 1.0 / 3.0;
                Console.WriteLine($"Disc {i}");
                // For now, let's calculate four capacitances for each turn
                // If last disc (section), above is prev disc, below is tank
                // TODO: How to handle segments above and below
                if (i == (num_discs - 1))
                {
                    C_abv = eps_0 * eps_oil * t_cond / (h_spacer + 2 * t_ins);
                    C_abv = eps_0 * (k / (2 * t_ins / eps_paper + (2 * t_ins + h_spacer) /  eps_oil) + (1 - k) / (2 * t_ins / eps_paper + (2 * t_ins + h_spacer) / eps_paper)) * (t_cond + 2 * t_ins);
                    n_abv = i - 1;
                    C_bel = eps_0 * eps_oil * t_cond / dist_to_ground;
                    n_bel = -1;
                    Console.WriteLine($"Last (bottom) Disc: C_abv={C_abv} C_bel={C_bel}");
                }
                else if (i == 0) // If first disc, above is tank, below is next disc
                {
                    C_abv = eps_0 * eps_oil * t_cond / dist_to_ground;
                    n_abv = -1;
                    C_bel = eps_0 * eps_oil * t_cond / (h_spacer + 2 * t_ins);
                    C_bel = eps_0 * (k / (2 * t_ins / eps_paper + (2 * t_ins + h_spacer) / eps_oil) + (1 - k) / (2 * t_ins / eps_paper + (2 * t_ins + h_spacer) / eps_paper)) * (t_cond + 2 * t_ins);
                    n_bel = i + 1;
                    Console.WriteLine($"First (top) Disc: C_abv={C_abv} C_bel={C_bel}");
                }
                else
                {
                    C_abv = C_bel = eps_0 * eps_oil * t_cond / (h_spacer + 2 * t_ins);
                    C_abv = C_bel = eps_0 * (k / (2 * t_ins / eps_paper + (2 * t_ins + h_spacer) / eps_oil) + (1 - k) / (2 * t_ins / eps_paper + (2 * t_ins + h_spacer) / eps_paper)) * (t_cond + 2 * t_ins);
                    n_abv = i - 1;
                    n_bel = i + 1;
                    Console.WriteLine($"Middle Disc: C_abv={C_abv} C_bel={C_bel}");
                }

                for (int j = 0; j < turns_per_disc; j++)
                {
                    double C_lt;
                    double C_rt;

                    bool out_to_in = (i % 2 == 0);

                    // If first turn in section, left is inner winding or core, right is next turn
                    if ((j == 0 && !out_to_in) || (j == (turns_per_disc - 1) && out_to_in))
                    {
                        C_lt = eps_0 * eps_paper * h_cond / dist_to_ground;
                        C_rt = eps_0 * eps_paper * (h_cond + 2 * t_ins) / (2 * t_ins);

                    }
                    else if ((j == (turns_per_disc - 1) && !out_to_in) || (j == 0 && out_to_in)) // If last turn in section, left is previous turn, right is outer winding or tank
                    {
                        C_lt = eps_0 * eps_paper * (h_cond + 2 * t_ins) / (2 * t_ins);
                        C_rt = eps_0 * eps_paper * h_cond / dist_to_ground;
                    }
                    else
                    {
                        C_lt = C_rt = eps_0 * eps_paper * (h_cond + 2 * t_ins) / (2 * t_ins);
                    }

                    int disc_abv = i - 1;
                    if (disc_abv < 0)
                    {
                        // ground above
                        n_abv = -1;
                    }
                    else
                    {
                        n_abv = disc_abv * turns_per_disc + (turns_per_disc - j - 1);
                    }
                    int disc_bel = i + 1;
                    if (disc_bel >= num_discs)
                    {
                        // ground below
                        n_bel = -1;
                    }
                    else
                    {
                        n_bel = disc_bel * turns_per_disc + (turns_per_disc - j - 1);
                    }

                    int n_lt;
                    int n_rt;

                    if (i % 2 == 0) //out to in
                    {
                        n_lt = i * turns_per_disc + j + 1;
                        n_rt = i * turns_per_disc + j - 1;
                        if (j == 0)
                        {
                            n_rt = -1;
                        }
                        if (j >= (turns_per_disc - 1))
                        {
                            n_lt = -1;
                        }
                        //Console.WriteLine("Out to in");
                    }
                    else //in to out
                    {
                        n_lt = i * turns_per_disc + j - 1;
                        n_rt = i * turns_per_disc + j + 1;
                        if (j == 0)
                        {
                            n_lt = -1;
                        }
                        if (j >= (turns_per_disc - 1))
                        {
                            n_rt = -1;
                        }
                        //Console.WriteLine($"{j}");
                        //Console.WriteLine("In to out");
                    }

                    int n = i * turns_per_disc + j;
                    Console.WriteLine($"n: {n} n_abv: {n_abv} n_bel: {n_bel} n_lt: {n_lt} n_rt: {n_rt}");

                    // Assemble C_abv, C_bel, C_lt, C_rt into C_seg
                    C[n, n] = C_abv + C_bel + C_lt + C_rt;
                    if (n_abv >= 0)
                    {
                        C[n, n_abv] = -C_abv;
                        C[n_abv, n] = -C_abv;
                    }
                    if (n_bel >= 0)
                    {
                        C[n, n_bel] = -C_bel;
                        C[n_bel, n] = -C_bel;
                    }
                    if (n_lt >= 0)
                    {
                        C[n, n_lt] = -C_lt;
                        C[n_lt, n] = -C_lt;
                    }
                    if (n_rt >= 0)
                    {
                        C[n, n_rt] = -C_rt;
                        C[n_rt, n] = -C_rt;
                    }
                }
            }

            return C;
        }

        private double CalcSelfInductance(double h, double w, double r_avg, double f)
        {
            double mu_0 = 4 * Math.PI * 1e-7;
            double mu_r = 1.0;
            double sigma = 5.8e7; // Conductivity of copper (S/m)
            double GMD = Math.Exp(0.5 * Math.Log(h * h + w * w) + 2 * w / (3 * h) * Math.Atan(h / w) + 2 * h / (3 * w) * Math.Atan(w / h) - w * w / (12 * h * h) * Math.Log(1 + h * h / (w * w)) - h * h / (12 * w * w) * Math.Log(1 + w * w / (h * h)) - 25 / 12);
            // Internal inductance
            double L_int_low = mu_0 * mu_r / (8 * Math.PI);  // Low-frequency internal inductance
            double omega = 2 * Math.PI * f;      // Angular frequency
            double delta = Math.Sqrt(2 / (omega * mu_0 * mu_r * sigma));  // Skin depth
            double L_int = L_int_low * Math.Min(1, delta / (GMD / 2));  // Smooth transition
            double L_s = L_int + mu_0 * r_avg * (Math.Log(8 * r_avg / GMD) - 2);
            Console.WriteLine($"r_avg: {r_avg} GMD: {GMD} L_s: {L_s / 1e-9} L_s/l: {L_s / (2 * Math.PI * r_avg) / 1e-9}");
            return L_s;
        }

        private double CalcInductanceCoaxLoops(double r_a, double z_a, double r_b, double z_b)
        {
            double mu_0 = 4 * Math.PI * 1e-7;
            double d = Math.Abs(z_b - z_a);
            double k = Math.Sqrt(4 * r_a * r_b / ((r_a + r_b) * (r_a + r_b) + d * d));
            double L_ab = 2 * mu_0 / k * Math.Sqrt(r_a * r_b) * ((1 - k * k / 2) * Elliptic.EllipticK(k) - Elliptic.EllipticE(k));
            return L_ab;
        }

        // r is mean radius of each turn
        // z is height of mid-point
        private (double r_i, double z_i, double r_j, double z_j) CalcLyleLoops(double r, double z, double w, double h)
        {
            double r_i, z_i, r_j, z_j;

            if (h > w)
            {
                // Two rings with r_avg = r_1 and at d +/- beta
                double r_adj = r * (1 + w * w / (24 * r * r));
                double beta = Math.Sqrt(((h * h) - (w * w)) / 12);
                r_i = r_adj;
                z_i = z + beta;
                r_j = r;
                z_j = z - beta;
            }
            else
            {
                // Two rings with r_avg = r_1 and r_1 + 2*delta and at z = d
                double r_adj = r * (1 + h * h / (24 * r * r));
                double delta = Math.Sqrt(((w * w) - (h * h)) / 12);
                r_i = r;
                z_i = z;
                r_j = r + 2 * delta;
                z_j = z;
            }

            return (r_i, z_i, r_j, z_j);
        }

        private double CalcMutualInductance_Lyle(double r_a, double z_a, double h_a, double w_a, double r_b, double z_b, double h_b, double w_b)
        {
            var ta = CalcLyleLoops(r_a, z_a, w_a, h_a);
            double r_1 = ta.r_i;
            double z_1 = ta.z_i;
            double r_2 = ta.r_j;
            double z_2 = ta.z_j;

            var tb = CalcLyleLoops(r_b, z_b, w_b, h_b);
            double r_3 = tb.r_i;
            double z_3 = tb.z_i;
            double r_4 = tb.r_j;
            double z_4 = tb.z_j;

            double L_13 = CalcInductanceCoaxLoops(r_1, z_1, r_3, z_3);
            double L_14 = CalcInductanceCoaxLoops(r_1, z_1, r_4, z_4);
            double L_23 = CalcInductanceCoaxLoops(r_2, z_2, r_3, z_3);
            double L_24 = CalcInductanceCoaxLoops(r_2, z_2, r_4, z_4);
            double L_ab = (L_13 + L_14 + L_23 + L_24) / 4;

            return L_ab;

        }

    }
}
