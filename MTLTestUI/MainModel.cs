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

        public MainModel()
        {
            tfmr = TestModels.TB904_SinglePhase();
            var geom = tfmr.GenerateGeometry();
            var meshgen = new MeshGenerator();
            meshgen.AddGeometry(geom);
            mesh = meshgen.GenerateMesh(1.0, 1);
        }
        
        
    }

}
