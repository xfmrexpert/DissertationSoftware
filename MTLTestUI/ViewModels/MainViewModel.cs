using Avalonia.Media;
using MathNet.Numerics;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using GeometryLib;
using TfmrLib;
using Geometry = GeometryLib.Geometry;

namespace MTLTestUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private MainModel _mainModel;

    public Geometry Geometry { get; set; }

    public MeshLib.Mesh Mesh { get; set; }

    public TagManager TagManager { get; set; }

    public MainViewModel()
    {
        _mainModel = new MainModel();
        Geometry = _mainModel.tfmr.GenerateGeometry();
        TagManager = _mainModel.tfmr.TagManager;

    }

}
