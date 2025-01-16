using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TDAP;
using LinAlg = MathNet.Numerics.LinearAlgebra;

namespace TfmrLib
{
    public abstract class Winding
    {
        private static double in_to_m(double x_in)
        {
            return x_in * 25.4 / 1000;
        }

        public double dist_wdg_tank_right = 4;
        public double dist_wdg_tank_top = 4;
        public double dist_wdg_tank_bottom = 2;
        public double r_inner = in_to_m(15.25);
        public double t_cond = in_to_m(0.085);
        public double h_cond = in_to_m(0.3);
        public double t_ins = in_to_m(0.018);
        public double h_spacer = in_to_m(0.188);
        public double r_cond_corner = 0.1;
        public int num_discs = 14;
        public int turns_per_disc = 20;
        public double eps_oil = 1.0; //2.2;
        public double eps_paper = 2.2; //3.5;
        public double rho_c = 1.68e-8; //ohm-m;
        public Complex Rs = Complex.Zero;
        public Complex Rl = Complex.Zero;

        public double bdry_radius = 1.0; //radius of outer boundary of finite element model

        public int phyAir;
        public int phyExtBdry;
        public int phyAxis;
        public int phyInf;
        public int[] phyTurnsCondBdry;
        public int[] phyTurnsCond;
        public int[] phyTurnsIns;

        public int num_turns
        {
            get
            {
                return num_discs * turns_per_disc;
            }
        }

        public Winding()
        {
            dist_wdg_tank_right = 4;
            dist_wdg_tank_top = 2;
            dist_wdg_tank_bottom = 2;
            r_inner = in_to_m(15.25);
            t_cond = in_to_m(0.085);
            h_cond = in_to_m(0.3);
            t_ins = in_to_m(0.018);
            h_spacer = in_to_m(0.188);
            r_cond_corner = 0.1 * t_cond;
            num_discs = 14;
            turns_per_disc = 20;
            eps_oil = 1.0; //2.2;
            eps_paper = 2.2; //3.5;
            rho_c = 1.68e-8; //ohm-m;
            Rs = Complex.Zero;
            Rl = Complex.Zero;
            bdry_radius = 1.0;
        }

        //static double dist_wdg_tank_right = 40.0 / 1000.0;
        //static double dist_wdg_tank_top = 40.0 / 1000.0;
        //static double dist_wdg_tank_bottom = 40.0 / 1000.0;
        //static double r_inner = 20.0 / 1000.0;
        //static double t_cond = 3.0 / 1000.0;
        //static double h_cond = 12.0 / 1000.0;
        //static double t_ins = 0.5 / 1000.0;
        //static double h_spacer = 6.0 / 1000.0;
        //static int num_discs = 2;
        //static int turns_per_disc = 6;
        //static double eps_oil = 2.2;
        //static double eps_paper = 3.5;

        public (double r, double z) GetTurnMidpoint(int n)
        {
            double r, z;
            int disc = (int)Math.Floor((double)n / (double)turns_per_disc);
            int turn = n % turns_per_disc;
            //Console.WriteLine($"turn: {n} disc: {disc} turn in disc: {turn}");

            if (disc % 2 == 0)
            {
                //out to in
                r = r_inner + (turns_per_disc - turn) * (t_cond + 2 * t_ins) - (t_cond / 2 + t_ins);
            }
            else
            {
                //in to out
                r = r_inner + turn * (t_cond + 2 * t_ins) + (t_cond / 2 + t_ins);
            }
            z = dist_wdg_tank_bottom + num_discs * (h_cond + 2 * t_ins) + (num_discs - 1) * h_spacer - (h_cond / 2 + t_ins) - disc * (h_cond + 2 * t_ins + h_spacer);
            //Console.WriteLine($"turn: {n} disc: {disc} turn in disc: {turn} r: {r} z:{z}");
            return (r, z);
        }

        public Geometry GenerateGeometry()
        {
            bool include_ins = true;

            double z_offset = (num_discs * (h_cond + 2 * t_ins) + (num_discs - 1) * h_spacer + dist_wdg_tank_bottom + dist_wdg_tank_top) / 2;

            var geometry = new Geometry();

            var conductorins_bdrys = new GeomLineLoop[num_discs * turns_per_disc];
            //var turn_surfaces = new GeomSurface[num_discs * turns_per_disc];
            //var ins_surfaces = 

            phyTurnsCond = new int[num_turns];
            phyTurnsCondBdry = new int[num_turns];
            if (include_ins)
            {
                phyTurnsIns = new int[num_turns];
            }

            for (int i = 0; i < num_turns; i++)
            {
                (double r, double z) = GetTurnMidpoint(i);
                z = z - z_offset;
                var conductor_bdry = geometry.AddRoundedRectangle(r, z, h_cond, t_cond, r_cond_corner, 0.0004);
                conductor_bdry.AttribID = phyTurnsCondBdry[i] = i + 2 * num_turns + 5;
                if (include_ins)
                {
                    var insulation_bdry = geometry.AddRoundedRectangle(r, z, h_cond + 2 * t_ins, t_cond + 2 * t_ins, r_cond_corner + t_ins, 0.003);
                    var insulation_surface = geometry.AddSurface(insulation_bdry, conductor_bdry);
                    insulation_surface.AttribID = phyTurnsIns[i] = i + num_turns + 5;
                    conductorins_bdrys[i] = insulation_bdry;
                }
                var conductor_surface = geometry.AddSurface(conductor_bdry);
                conductor_surface.AttribID = phyTurnsCond[i] = i + 5;
                if (!include_ins)
                {
                    conductorins_bdrys[i] = conductor_bdry;
                }
            }

            var pt_origin = geometry.AddPoint(0, 0, 0.1);
            var pt_axis_top = geometry.AddPoint(0, bdry_radius, 0.1);
            var pt_axis_top_inf = geometry.AddPoint(0, 1.1 * bdry_radius, 0.1);
            var pt_axis_bottom = geometry.AddPoint(0, -bdry_radius, 0.1);
            var pt_axis_bottom_inf = geometry.AddPoint(0, -1.1 * bdry_radius, 0.1);
            var axis = geometry.AddLine(pt_axis_bottom, pt_axis_top);
            var axis_top_inf = geometry.AddLine(pt_axis_top, pt_axis_top_inf);
            var axis_bottom_inf = geometry.AddLine(pt_axis_bottom_inf, pt_axis_bottom);
            //var axis_lower = geometry.AddLine(pt_axis_bottom, pt_origin);
            axis.AttribID = phyAxis = 3;
            //axis_lower.AttribID = 3;
            var right_bdry = geometry.AddArc(pt_axis_top, pt_axis_bottom, bdry_radius, -Math.PI);
            var right_bdry_inf = geometry.AddArc(pt_axis_top_inf, pt_axis_bottom_inf, 1.1 * bdry_radius, -Math.PI);
            var outer_bdry = geometry.AddLineLoop(axis, right_bdry);
            var outer_bdry_inf = geometry.AddLineLoop(axis_bottom_inf, right_bdry, axis_top_inf, right_bdry_inf);
            outer_bdry.AttribID = phyExtBdry = 2;

            var interior_surface = geometry.AddSurface(outer_bdry, conductorins_bdrys);
            interior_surface.AttribID = phyAir = 1;

            var inf_surface = geometry.AddSurface(outer_bdry_inf);
            inf_surface.AttribID = phyInf = 4;

            return geometry;
        }

        public abstract LinAlg.Matrix<double> Calc_Lmatrix(double f = 60);

        public abstract LinAlg.Matrix<double> Calc_Cmatrix();

        public LinAlg.Vector<double> Calc_TurnRadii()
        {
            LinAlg.Vector<double> r_t = LinAlg.Vector<double>.Build.Dense(num_turns);
            for (int disc = 0; disc < num_discs; disc++)
            {
                for (int turn = 0; turn < turns_per_disc; turn++)
                {
                    int n = disc * turns_per_disc + turn;
                    r_t[n] = GetTurnMidpoint(n).r;
                }
            }
            return r_t;
        }

        public LinAlg.Matrix<double> Calc_Rmatrix(double f = 60)
        {
            var R = LinAlg.Matrix<double>.Build.Dense(num_turns, num_turns);

            for (int t = 1; t < num_turns; t++)
            {
                R[t, t] = R_c(h_cond, t_cond);
            }

            double mu_c = 4 * Math.PI * 1e-7;
            double sigma_c = 1 / rho_c;
            Complex eta = Complex.Sqrt(2d * Math.PI * f * mu_c * sigma_c * Complex.ImaginaryOne) * t_cond / 2d;
            double R_skin = (1 / (sigma_c * h_cond * t_cond) * eta * Complex.Cosh(eta) / Complex.Sinh(eta)).Real;
            //Console.WriteLine($"R_skin: {R_skin}");

            var R_f = R + LinAlg.Matrix<double>.Build.DenseIdentity(num_turns, num_turns) * R_skin;
            return R_f;
        }

        public double[] Calc_RVector()
        {
            double[] R_t = new double[num_turns];
            for (int t = 0; t < num_turns; t++)
            {
                R_t[t] = R_c(h_cond, t_cond);
                //R_t[t] = R_c_old(r_c, t_cwall);
            }

            //TODO: Factor in skin effect mo' better
            double[] K_t = new double[num_turns];

            for (int t = 0; t < num_turns; t++)
            {
                K_t[t] = 0;
                //K_t[t] = 2/(r_c/1000)*Math.Sqrt(rho_c*4*Math.PI*1e-7/Math.PI);
            }

            return R_t;
        }
        public double R_c(double h_m, double w_m)
        {
            return rho_c / (h_m * w_m);
        }
    }
}
