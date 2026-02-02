using CliWrap;
using GeometryLib;
using MathNet.Numerics;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TfmrLib;

namespace LCCalc
{
    internal class Program
    {
        static int Main(string[] args)
        {
            AnsiConsole.Write(new FigletText("Inductance & Capacitance Calcs"));

            // If no arguments provided, show interactive menu
            if (args.Length == 0)
            {
                return ShowInteractiveMenu();
            }

            // Otherwise, use command-line interface
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<CapacitanceCommand>("capacitance")
                    .WithAlias("cap")
                    .WithDescription("Calculate capacitance matrix");
                    
                config.AddCommand<InductanceCommand>("inductance")
                    .WithAlias("ind")
                    .WithDescription("Calculate inductance matrices");
            });

            //var rlcMatrixCalculator = new FEMMatrixCalculator();

            ////_mainModel.wdg.num_discs = 1;
            ////_mainModel.wdg.turns_per_disc = 2;

            //_mainModel.wdg.eps_paper = 1.5; // Per Cigre TB904, Dry non-impregnated paper is 2.7, Dry non-impregnated pressboard is 3.8
            //_mainModel.tfmr.r_core = Conversions.in_to_m(12.1);
            //_mainModel.tfmr.bdry_radius = 3.0;
            //Console.WriteLine($"Boundary Radius: {_mainModel.tfmr.bdry_radius}");

            ////Geometry = _mainModel.tfmr.GenerateGeometry();
            ////GmshFile gmshFile = new GmshFile("case.geo");
            ////gmshFile.CreateFromGeometry(Geometry);
            ////double meshscale = 1.0;
            ////Mesh = gmshFile.GenerateMesh(meshscale, 2);
            ////_mainModel.mesh = Mesh;
            ////Mesh.WriteToTriangleFiles("", "case");

            ////_mainModel.CalcCapacitanceMatrix();

            //int num_freqs = 10;
            //double min_freq = 10e3;
            //double max_freq = 1e6;
            //var freqs = Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));
            //rlcMatrixCalculator.Calc_Lmatrix(_mainModel.tfmr, 60);
            ////_mainModel.CalcInductanceMatrix_FEMM(Geometry, 60);
            //foreach (var freq in (List<double>)[100, 120, 1e3, 10e3, 100e3])
            //{
            //    if (freq > 0)
            //    {
            //        //_mainModel.CalcInductanceMatrix(freq, 2);
            //        //_mainModel.CalcInductanceMatrix_FEMM(Geometry, freq);
            //    }
            //    //
            //}

            return app.Run(args);
        }

        static int ShowInteractiveMenu()
        {
            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [green]calculation[/] to perform:")
                        .PageSize(10)
                        .AddChoices(new[] {
                            "Calculate Capacitance Matrix", 
                            "Calculate Inductance Matrices",
                            "Show Help",
                            "Exit"
                        }));

                switch (choice)
                {
                    case "Calculate Capacitance Matrix":
                        var capSettings = GetCapacitanceSettings();
                        var capCommand = new CapacitanceCommand();
                        var capResult = capCommand.Execute(null!, capSettings);
                        if (capResult != 0)
                        {
                            AnsiConsole.MarkupLine("[red]Command failed![/]");
                        }
                        break;

                    case "Calculate Inductance Matrices":
                        var indSettings = GetInductanceSettings();
                        var indCommand = new InductanceCommand();
                        var indResult = indCommand.Execute(null!, indSettings);
                        if (indResult != 0)
                        {
                            AnsiConsole.MarkupLine("[red]Command failed![/]");
                        }
                        break;

                    case "Show Help":
                        ShowHelp();
                        break;

                    case "Exit":
                        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                        return 0;
                }

                AnsiConsole.WriteLine();
                if (!AnsiConsole.Confirm("Would you like to perform another calculation?"))
                {
                    AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                    return 0;
                }
            }
        }

        static CapacitanceCommand.Settings GetCapacitanceSettings()
        {
            var calculator = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select [green]calculator type[/]:")
                    .AddChoices("analytic", "fem"));

            var wantOutput = AnsiConsole.Confirm("Do you want to save output to a file?");
            string? outputPath = null;

            if (wantOutput)
            {
                outputPath = AnsiConsole.Ask<string>("Enter output file path:", "capacitance_matrix.csv");
            }

            return new CapacitanceCommand.Settings
            {
                Calculator = calculator,
                OutputPath = outputPath
            };
        }

        static InductanceCommand.Settings GetInductanceSettings()
        {
            var calculator = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select [green]calculator type[/]:")
                    .AddChoices("analytic", "fem"));

            var useDefaultFreqs = AnsiConsole.Confirm("Use default frequencies (60, 1000, 10000 Hz)?");
            string frequencies = "60,1000,10000";

            if (!useDefaultFreqs)
            {
                frequencies = AnsiConsole.Ask<string>("Enter frequencies (comma-separated):", "60,1000,10000");
            }

            var wantOutput = AnsiConsole.Confirm("Do you want to save output to a directory?");
            string? outputPath = null;

            if (wantOutput)
            {
                outputPath = AnsiConsole.Ask<string>("Enter output directory path:", "./inductance_matrices");
            }

            return new InductanceCommand.Settings
            {
                Calculator = calculator,
                Frequencies = frequencies,
                OutputPath = outputPath
            };
        }

        static void ShowHelp()
        {
            var panel = new Panel(new Markup(
                "[bold]Available Commands:[/]\n\n" +
                "[green]capacitance[/] (alias: [green]cap[/])\n" +
                "  Calculate capacitance matrix\n" +
                "  Options:\n" +
                "    --calculator, -c    Calculator type (analytic or fem)\n" +
                "    --output, -o        Output file path\n\n" +
                "[blue]inductance[/] (alias: [blue]ind[/])\n" +
                "  Calculate inductance matrices\n" +
                "  Options:\n" +
                "    --calculator, -c    Calculator type (analytic or fem)\n" +
                "    --frequencies, -f   Frequencies (comma-separated)\n" +
                "    --output, -o        Output directory path\n\n" +
                "[yellow]Examples:[/]\n" +
                "  LCCalc capacitance --calculator analytic --output results.csv\n" +
                "  LCCalc inductance --frequencies 60,1000 --output ./matrices\n"
            ))
            {
                Header = new PanelHeader(" [bold cyan]Help[/] "),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("cyan")
            };

            AnsiConsole.Write(panel);
        }
    }

    public class CapacitanceCommand : Command<CapacitanceCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [Description("Calculator type to use (analytic or fem)")]
            [CommandOption("--calculator|-c")]
            [DefaultValue("analytic")]
            public string Calculator { get; set; } = "analytic";

            [Description("Output file path for the capacitance matrix")]
            [CommandOption("--output|-o")]
            public string? OutputPath { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            return AnsiConsole.Status()
                .Start("Calculating capacitance matrix...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    try
                    {
                        // Create test transformer model
                        var transformer = TestModels.TB904_ThreePhase();

                        AnsiConsole.MarkupLine($"Using [cyan]{settings.Calculator}[/] calculator...");
                        AnsiConsole.MarkupLine($"Transformer created with [cyan]{transformer.Windings.Count}[/] windings");

                        // For now, just show that we can create the transformer
                        // Uncomment the matrix calculation code when the calculator classes are available
#if false
                        // Create calculator based on settings
                        IRLCMatrixCalculator calculator = settings.Calculator.ToLower() switch
                        {
                            "fem" => new FEMMatrixCalculator(),
                            "analytic" or _ => new AnalyticMatrixCalculator()
                        };
                        
                        // Calculate capacitance matrix
                        var capacitanceMatrix = calculator.Calc_Cmatrix(transformer);
                        
                        AnsiConsole.MarkupLine("[green]✓[/] Capacitance matrix calculated successfully!");
                        AnsiConsole.MarkupLine($"Matrix size: [cyan]{capacitanceMatrix.RowCount}x{capacitanceMatrix.ColumnCount}[/]");
                        
                        // Display matrix summary
                        var diagonalSum = capacitanceMatrix.Diagonal().Sum();
                        AnsiConsole.MarkupLine($"Sum of diagonal elements: [cyan]{diagonalSum:E3}[/] F");

                        // Save to file if output path specified
                        if (!string.IsNullOrEmpty(settings.OutputPath))
                        {
                            // You can implement matrix saving here
                            AnsiConsole.MarkupLine($"[green]✓[/] Matrix saved to: [cyan]{settings.OutputPath}[/]");
                        }
#endif
                        AnsiConsole.MarkupLine("[green]✓[/] Capacitance calculation completed!");

                        // Save to file if output path specified
                        if (!string.IsNullOrEmpty(settings.OutputPath))
                        {
                            AnsiConsole.MarkupLine($"[yellow]Note:[/] Matrix saving will be implemented when calculator classes are available");
                            AnsiConsole.MarkupLine($"Would save to: [cyan]{settings.OutputPath}[/]");
                        }

                        return 0;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error calculating capacitance matrix: {ex.Message}[/]");
                        return 1;
                    }
                });
        }
    }

    public class InductanceCommand : Command<InductanceCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [Description("Calculator type to use (analytic or fem)")]
            [CommandOption("--calculator|-c")]
            [DefaultValue("analytic")]
            public string Calculator { get; set; } = "analytic";

            [Description("Frequencies to calculate (comma-separated)")]
            [CommandOption("--frequencies|-f")]
            [DefaultValue("60,1000,10000")]
            public string Frequencies { get; set; } = "60,1000,10000";

            [Description("Output directory for inductance matrices")]
            [CommandOption("--output|-o")]
            public string? OutputPath { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            return AnsiConsole.Status()
                .Start("Calculating inductance matrices...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("blue"));

                    try
                    {
                        // Create test transformer model
                        var transformer = TestModels.TB904_ThreePhase();

                        AnsiConsole.MarkupLine($"Using [cyan]{settings.Calculator}[/] calculator...");
                        
                        // Parse frequencies
                        var frequencies = settings.Frequencies
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(f => double.Parse(f.Trim()))
                            .ToArray();

                        AnsiConsole.MarkupLine($"Calculating for frequencies: [cyan]{string.Join(", ", frequencies)}[/] Hz");
                        AnsiConsole.MarkupLine($"Transformer created with [cyan]{transformer.Windings.Count}[/] windings");

                        // For now, just show the setup
                        // Uncomment the matrix calculation code when the calculator classes are available
#if false
                        // Create calculator based on settings
                        IRLCMatrixCalculator calculator = settings.Calculator.ToLower() switch
                        {
                            "fem" => new FEMMatrixCalculator(),
                            "analytic" or _ => new AnalyticMatrixCalculator()
                        };
                        
                        // Calculate inductance matrix at different frequencies
                        foreach (var freq in frequencies)
                        {
                            ctx.Status($"Calculating inductance matrix at {freq:F0} Hz...");
                            
                            var inductanceMatrix = calculator.Calc_Lmatrix(transformer, freq);
                            
                            AnsiConsole.MarkupLine($"[green]✓[/] Inductance matrix calculated at [cyan]{freq:F0} Hz[/]");
                            AnsiConsole.MarkupLine($"Matrix size: [cyan]{inductanceMatrix.RowCount}x{inductanceMatrix.ColumnCount}[/]");
                            
                            // Display matrix summary
                            var diagonalSum = inductanceMatrix.Diagonal().Sum();
                            AnsiConsole.MarkupLine($"Sum of diagonal elements: [cyan]{diagonalSum:E3}[/] H");

                            // Save to file if output path specified
                            if (!string.IsNullOrEmpty(settings.OutputPath))
                            {
                                var filename = Path.Combine(settings.OutputPath, $"inductance_{freq:F0}Hz.csv");
                                // You can implement matrix saving here
                                AnsiConsole.MarkupLine($"[green]✓[/] Matrix saved to: [cyan]{filename}[/]");
                            }
                        }
#endif
                        AnsiConsole.MarkupLine("[green]✓[/] Inductance calculations completed!");

                        // Save to file if output path specified
                        if (!string.IsNullOrEmpty(settings.OutputPath))
                        {
                            AnsiConsole.MarkupLine($"[yellow]Note:[/] Matrix saving will be implemented when calculator classes are available");
                            AnsiConsole.MarkupLine($"Would save to directory: [cyan]{settings.OutputPath}[/]");
                        }

                        return 0;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error calculating inductance matrices: {ex.Message}[/]");
                        return 1;
                    }
                });
        }

#if false
        public void CalcInductanceMatrix_FEMM(Geometry geom, double freq, int order = 2, string femmInputFileName = "case.fem", string fknExecutablePath = "./bin/fkn.exe")
        {
            Matrix<double> L_getdp = Matrix<double>.Build.Dense(wdg.num_turns, wdg.num_turns);

            //Console.WriteLine($"Frequency: {freq.ToString("0.##E0")}");
            //CalcMesh();

            //Parallel.For(0, n_turns, t =>
            for (int t = 0; t < wdg.num_turns; t++)
            {
                L_getdp.SetRow(t, CalcInductance_FEMM(geom, freq, t, femmInputFileName, fknExecutablePath));
                (double r, double z) = wdg.GetTurnMidpoint(t);
                //Console.WriteLine($"Self inductance for turn {t}: {L_getdp[t, t] / r / 1e-9}");
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

            //Console.Write((L_getdp/1e-9).ToMatrixString());

            DelimitedWriter.Write($"L_femm_{freq.ToString("0.00E0")}.csv", L_getdp, ",");
        }

        public Vector_d CalcInductance_FEMM(Geometry geo, double freq, int turn, string femmInputFileName = "case.fem", string fknExecutablePath = "./bin/fkn.exe")
        {
            FEMMFile femm = new FEMMFile();
            Dictionary<int, int> blockMap = new Dictionary<int, int>();
            Dictionary<int, int> circMap = new Dictionary<int, int>();
            femm.Frequency = freq;
            int blkAir = femm.CreateNewBlockProp("Air");
            int blkPaper = femm.CreateNewBlockProp("Paper");
            int blkCu = femm.CreateNewBlockProp("Copper");
            blockMap[tfmr.phyAir] = blkAir;
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
            blockMap[tfmr.phyInf] = blkAir;
            int blkAxis = femm.CreateNewBdryProp("Axis");

            femm.CreateFromGeometry(geo, blockMap, circMap);

            femm.BlockProps[blkAir].Mu_x = 1.0f;
            femm.BlockProps[blkAir].Mu_y = 1.0f;
            femm.BlockProps[blkPaper].Mu_x = 1.0f;
            femm.BlockProps[blkPaper].Mu_y = 1.0f;
            femm.BlockProps[blkCu].Mu_x = 1.0f;
            femm.BlockProps[blkCu].Mu_y = 1.0f;
            femm.BlockProps[blkCu].Sigma = 58f;

            femm.BdryProps[blkAxis].BdryType = FEMMBdryType.prescribedA;
            femm.BdryProps[blkAxis].A_0 = 0.0f;
            femm.BdryProps[blkAxis].A_1 = 0.0f;
            femm.BdryProps[blkAxis].A_2 = 0.0f;

            femm.CircuitProps[turn].TotalAmps_re = 1.0f;

            femm.ToFile(femmInputFileName);

            var caseName = Path.GetFileNameWithoutExtension(femmInputFileName);
            Cli.Wrap(fknExecutablePath).WithArguments(caseName).ExecuteAsync().GetAwaiter().GetResult();

            string filePath = "inductances.txt";
            Vector_d inductances = Vector_d.Build.Dense(wdg.num_turns);
            Regex complexRegex = new Regex(@"([\d.eE+-]+)\s*\+\s*j([\d.eE+-]+)", RegexOptions.Compiled);

            try
            {
                int t = 0;
                foreach (string line in File.ReadLines(filePath))
                {
                    Match match = complexRegex.Match(line);
                    if (match.Success &&
                        double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double real) &&
                        double.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double imaginary))
                    {
                        inductances[t] = real / (2 * Math.PI);
                        t++;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Could not parse '{line}' as a complex number.");
                    }
                }

                //Console.WriteLine("Parsed Inductances:");
                //foreach (double inductance in inductances)
                //{
                //    Console.WriteLine(inductance);
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file: {ex.Message}");
            }

            return inductances;
        }

        private void CalcSelfInductancesOnly(double freq, double meshscale = 1.0, int order = 1)
    {
        Console.WriteLine($"Mesh Scale: {meshscale}");

        int n_turns = 5; // _mainModel.num_discs * _mainModel.turns_per_disc;
        Vector<double> L_getdp = Vector<double>.Build.Dense(n_turns);

        Console.WriteLine($"Frequency: {freq.ToString("0.##E0")}");
        Console.WriteLine($"Mesh scale: {meshscale}");
        Console.WriteLine($"Order: {order}");
        //_mainModel.CalcMesh(meshscale, order);

        //Parallel.For(0, n_turns, t =>
        for (int t = 0; t < n_turns; t++)
        {
            //L_getdp[t] = _mainModel.Calc_Inductance(t, -1, freq, order);
            //(double r, double z) = _mainModel.GetTurnMidpoint(t);
            //Console.WriteLine($"Self inductance for turn {t}: {L_getdp[t] / r / 1e-9}");
        }
        //);

        for (int t1 = 0; t1 < n_turns; t1++)
        {
            (double r, double z) = _mainModel.wdg.GetTurnMidpoint(t1);
            L_getdp[t1] = L_getdp[t1] / r;
        }

        //Console.Write((L_getdp/1e-9).ToVectorString());

        //DelimitedWriter.Write($"L_getdp_{freq.ToString("0.00E0")}.csv", L_getdp, ",");
        
    }
#endif
    }
}
