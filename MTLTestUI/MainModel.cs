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
using TfmrLib.FEM;
using GeometryLib;

namespace MTLTestUI
{
    public class MainModel
    {
        public Transformer tfmr;
        public Mesh mesh;

        public Geometry geometry;

        // Simple timing helpers
        private static T Measure<T>(string label, Func<T> func)
        {
            var sw = Stopwatch.StartNew();
            T result = func();
            sw.Stop();
            Console.WriteLine($"{label}: {sw.Elapsed.TotalMilliseconds:F3} ms");
            return result;
        }

        private static void Measure(string label, Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            Console.WriteLine($"{label}: {sw.Elapsed.TotalMilliseconds:F3} ms");
        }

        public MainModel()
        {
            Console.WriteLine("MainModel construction timing start");
            var total = Stopwatch.StartNew();

            //tfmr = Measure("TB904_SinglePhase", TestModels.TB904_SinglePhase);
            tfmr = Measure("TestTransformer", TestModels.TestTransformer);
            geometry = Measure("GenerateGeometry", () => tfmr.GenerateGeometry());
            var meshgen = Measure("MeshGenerator ctor", () => new MeshGenerator());
            Measure("AddGeometry", () => meshgen.AddGeometry(geometry));
            mesh = Measure("GenerateMesh", () => meshgen.GenerateMesh("bin/Debug/net9.0/case.geo",1000.0, 1));

            double freq = 60.0;
            int excitedTurn = 0;
            int excitedStrand = 0;
            int order = 1;

            Console.WriteLine($"Frequency: {freq.ToString("0.##E0")} Turn: {excitedTurn}");

            var fem = new GetDPAxiMagProblem();

            var oil = new Material("Oil")
            {
                Properties = new Dictionary<string, double> {
                { "mu_r", 1.0 },
                { "epsr", tfmr.eps_oil },
                { "loss_tan", tfmr.ins_loss_factor } }
            };

            var paper = new Material("Paper")
            {
                Properties = new Dictionary<string, double> {
                { "mu_r", 1.0 },
                { "epsr", tfmr.Windings[0].eps_paper },
                { "loss_tan", tfmr.ins_loss_factor } }
            };

            var copper = new Material("Copper")
            {
                Properties = new Dictionary<string, double> {
                { "mu_r", 1.0 },
                { "sigma", 5.96e7 } }
            };

            fem.Materials.Add(oil);
            fem.Materials.Add(paper);
            fem.Materials.Add(copper);
            fem.Regions.Add(new Region() { Name = "InteriorDomain", Tags = new List<int>() { tfmr.TagManager.GetTagByString("InteriorDomain") }, Material = oil });
            fem.BoundaryConditions.Add(new BoundaryCondition() { Name = "Dirichlet", Tags = new List<int>() { tfmr.TagManager.GetTagByString("CoreLeg"), tfmr.TagManager.GetTagByString("TopYoke"), tfmr.TagManager.GetTagByString("BottomYoke"), tfmr.TagManager.GetTagByString("RightEdge") } });
            int globalTurn = -1;
            for (int wdgNum = 0; wdgNum < tfmr.Windings.Count; wdgNum++)
            {
                var wdg = tfmr.Windings[wdgNum];
                for (int segNum = 0; segNum < wdg.Segments.Count; segNum++)
                {
                    var seg = wdg.Segments[segNum];
                    if (seg.Geometry != null)
                    {
                        var seg_geom = seg.Geometry;
                        for (int localTurn = 0; localTurn < seg_geom.NumTurns; localTurn++, globalTurn++)
                        {
                            for (int localStrand = 0; localStrand < seg_geom.NumParallelConductors; localStrand++)
                            {
                                var locKey = new LocationKey(wdgNum, segNum, localTurn, localStrand);
                                var regionIns = new Region() { Name = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Ins", Tags = new List<int>() { tfmr.TagManager.GetTagByLocation(locKey, TagType.InsulationSurface) }, Material = paper };
                                var regionCond = new Region() { Name = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Cond", Tags = new List<int>() { tfmr.TagManager.GetTagByLocation(locKey, TagType.ConductorSurface) }, Material = copper };
                                fem.Regions.Add(regionIns);
                                fem.Regions.Add(regionCond);
                                if (globalTurn == excitedTurn && localStrand == excitedStrand)
                                {
                                    fem.Excitations.Add(new Excitation() { Region = regionCond, Value = 1.0 });
                                }
                                else
                                {
                                    fem.Excitations.Add(new Excitation() { Region = regionCond, Value = 0.0 });
                                }
                            }
                        }
                    }
                }
            }

            fem.Solve();

            total.Stop();
            Console.WriteLine($"MainModel constructor total: {total.Elapsed.TotalMilliseconds:F3} ms");
        }
    }

}
