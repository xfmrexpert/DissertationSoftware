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
        }

        public async Task InitializeAsync()
        {
            Console.WriteLine("MainModel initialization start");
            var total = Stopwatch.StartNew();

            tfmr = Measure("TB904_SinglePhase", TestModels.TB904_SinglePhase);
            //tfmr = Measure("ModelWinding", TestModels.ModelWinding);
            //tfmr = Measure("TestTransformer", TestModels.TestTransformer);
            geometry = Measure("GenerateGeometry", () => tfmr.GenerateGeometry());
            var meshgen = Measure("MeshGenerator ctor", () => new MeshGenerator());
            Measure("AddGeometry", () => meshgen.AddGeometry(geometry));
            mesh = Measure("GenerateMesh", () => meshgen.GenerateMesh("case.geo", 1000.0, 1));

            total.Stop();
            Console.WriteLine($"MainModel initialization total: {total.Elapsed.TotalMilliseconds:F3} ms");
        }

        public void RunOnce()
        {
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
                { "epsr", tfmr.Windings[0].Segments[0].Geometry.ConductorType.eps_paper },
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
            fem.EntityGroups.Add(new EntityGroup() { Name = "InteriorDomain", Dimension = 2, AttributeIds = new List<int>() { tfmr.TagManager.GetTagByString("InteriorDomain") } });
            fem.Regions.Add(new Region() { Name = "InteriorDomain", EntityGroupName = "InteriorDomain", Material = oil });
            fem.EntityGroups.Add(new EntityGroup() { Name = "DirichletBoundary", Dimension = 1, AttributeIds = new List<int>() { tfmr.TagManager.GetTagByString("CoreLeg"), tfmr.TagManager.GetTagByString("TopYoke"), tfmr.TagManager.GetTagByString("BottomYoke"), tfmr.TagManager.GetTagByString("RightEdge") } });
            fem.BoundaryConditions.Add(new DirichletBoundaryCondition() { Name = "Dirichlet", EntityGroupName = "DirichletBoundary", Potential = 0.0 });
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
                                fem.EntityGroups.Add(new EntityGroup() { Name = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Ins", Dimension = 2, AttributeIds = new List<int>() { tfmr.TagManager.GetTagByLocation(locKey, TagType.InsulationSurface) } });
                                fem.EntityGroups.Add(new EntityGroup() { Name = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Cond", Dimension = 2, AttributeIds = new List<int>() { tfmr.TagManager.GetTagByLocation(locKey, TagType.ConductorSurface) } });
                                var regionIns = new Region() { Name = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Ins", EntityGroupName = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Ins", Material = paper };
                                var regionCond = new Region() { Name = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Cond", EntityGroupName = $"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Cond", Material = copper };
                                fem.Regions.Add(regionIns);
                                fem.Regions.Add(regionCond);
                                fem.Terminals.Add(new TfmrLib.FEM.Terminal() { EntityGroup = fem.EntityGroups[$"Wdg{wdgNum}Turn{localTurn}Std{localStrand}Cond"], ExcitationType = Quantity.Current });
                                if (globalTurn == excitedTurn && localStrand == excitedStrand)
                                {
                                    
                                }
                                else
                                {
                                    
                                }
                            }
                        }
                    }
                }
            }
            fem.Solve();
        }
    }

}
