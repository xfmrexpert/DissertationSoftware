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
            //mesh = Measure("GenerateMesh", () => meshgen.GenerateMesh(100.0, 1));

            total.Stop();
            Console.WriteLine($"MainModel constructor total: {total.Elapsed.TotalMilliseconds:F3} ms");
        }
    }

}
