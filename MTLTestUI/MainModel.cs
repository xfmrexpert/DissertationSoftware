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
using GeometryLib;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using MeshLib;
using TfmrLib;
using TfmrLib.FEM;
using TfmrLib.TransientModels;
using Vector_d = MathNet.Numerics.LinearAlgebra.Vector<double>;

namespace MTLTestUI
{
    // The time measurement stuff was vibe-coded
    public class MainModel
    {
        public Transformer tfmr;
        public TransientModelGeometryFactory model_geo_factory;
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
            model_geo_factory = new TransientModelGeometryFactory();
            Measure("GenerateGeometry", () => model_geo_factory.BuildGeometry(tfmr));
            geometry = model_geo_factory.Geometry;
            var meshgen = Measure("MeshGenerator ctor", () => new MeshGenerator());
            Measure("AddGeometry", () => meshgen.AddGeometry(geometry));
            mesh = Measure("GenerateMesh", () => meshgen.GenerateMesh("case.geo", 1000.0, 1));

            total.Stop();
            Console.WriteLine($"MainModel initialization total: {total.Elapsed.TotalMilliseconds:F3} ms");
        }


    }

}
