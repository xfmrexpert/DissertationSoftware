using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GeometryLib;
using MathNet.Numerics;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using TfmrLib;
using Geometry = GeometryLib.Geometry;

namespace MTLTestUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private MainModel _mainModel;

    [ObservableProperty]
    private Geometry? _geometry;

    [ObservableProperty]
    private MeshLib.Mesh? _mesh;

    [ObservableProperty]
    private TagManager? _tagManager;

    [ObservableProperty]
    private bool _isBusy;

    public MainViewModel()
    {
        _mainModel = new MainModel();
        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            IsBusy = true;
            await _mainModel.InitializeAsync();

            Geometry = _mainModel.geometry;
            TagManager = _mainModel.model_geo_factory.TagManager;
            Mesh = _mainModel.mesh;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization failed: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
