using Plotly.NET.LayoutObjects;
using Plotly.NET;
using MathNet.Numerics.LinearAlgebra;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.Analysis;
using TfmrLib;
using Spectre.Console;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;

namespace MTLTestApp
{
    internal class Program
    {

        static double min_freq = 100e3;
        static double max_freq = 1e6;
        static int num_freqs = 1000;

        static int num_turns;

        static async Task Main(string[] args)
        {
            await Calc1();
            //await Calc2();
        }

        // static void FerasTfmr()
        // {
        //     var tfmr = new Transformer();
        //     var wdg = new Winding();
        //     tfmr.Windings.Add(wdg);
        //     wdg.r_inner = 2.27 / (2.0 * Math.PI) - 0.012;
        //     wdg.num_discs = 6;
        //     wdg.turns_per_disc = 6;
        //     wdg.h_cond = 0.012;
        //     wdg.t_cond = 0.003;
        //     wdg.t_ins = 0.0005;
        //     wdg.h_spacer = 6;
        //     wdg.dist_wdg_tank_bottom = 0.040;
        //     wdg.dist_wdg_tank_right = 0.040;
        //     wdg.dist_wdg_tank_top = 0.040;
        // }

        static async Task RunTasksWithProgress(Dictionary<string, Func<IProgress<int>, Task<(Complex[], List<Complex[]>)>>> taskDefinitions, List<DataFrame> measuredResponses, DataFrame measuredImpedances)
        {
            var tasks = new List<Task<(Complex[], List<Complex[]>)>>();

            // Use Spectre.Console's Progress
            await AnsiConsole.Progress()
                .AutoClear(false) // Keep the progress bars after completion
                .HideCompleted(false)
                .Columns(new ProgressColumn[]
                {
                        new TaskDescriptionColumn(),    // Task description
                        new ProgressBarColumn(),        // Progress bar
                        new PercentageColumn(),         // Percentage completed
                        new RemainingTimeColumn(),      // Remaining time
                })
                .StartAsync(async ctx =>
                {
                    foreach (var taskDefinition in taskDefinitions)
                    {
                        var taskName = taskDefinition.Key;
                        var taskFunc = taskDefinition.Value;

                        var progressTask = ctx.AddTask(taskName, maxValue: 100);

                        var progress = new Progress<int>(percent =>
                        {
                            progressTask.Value = percent;
                        });

                        var task = Task.Run(async () =>
                        {
                            var result = await taskFunc(progress);
                            progressTask.Value = 100; // Ensure completion
                            return result;
                        });

                        tasks.Add(task);
                    }

                    // Wait for all tasks to complete
                    await Task.WhenAll(tasks);
                });

            var results = await Task.WhenAll(tasks);
            var calculatedResponses = new Dictionary<string, List<Complex[]>>();
            var calculatedImpedances = new Dictionary<string, Complex[]>();

            int index = 0;
            foreach (var taskDefinition in taskDefinitions)
            {
                calculatedImpedances[taskDefinition.Key] = results[index].Item1;
                calculatedResponses[taskDefinition.Key] = results[index].Item2;
                index++;
            }

            ShowPlots(measuredResponses, calculatedResponses);
            ShowPlots_Z(measuredImpedances, calculatedImpedances);
            Show3DPlot(calculatedResponses.First().Value);
        }

        static async Task Calc1()
        {
            min_freq = 10e3;
            max_freq = 1e6;
            num_freqs = 1000;

            Console.WriteLine("Howdy! This here is the dumbest middle-life crisis ever.");

            string directoryPath = @"./PULImpedances/Core"; // Specify the directory path

            var measuredData = ReadMeasuredData(@"./Measured/NoCore");
            var impedanceData = ReadImpedanceData(@"./Measured/Core");

            //Show3DPlot_Meas(measuredData);

            var tfmr = TestModels.ModelWinding();
            
            var analyticMatrixCalc = new AnalyticMatrixCalculator();
            var getDPMatrixCalc = new ExtMatrixCalculator(directoryPath);

            // tfmr.ins_loss_factor = 0.02;
            // wdg.eps_paper = 2.0;

            // num_turns = wdg.num_turns;

            var analyticModel = new MTLModel(tfmr, analyticMatrixCalc, min_freq, max_freq, num_freqs);
            var getDPModel = new MTLModel(tfmr, getDPMatrixCalc, min_freq, max_freq, num_freqs);
            var lumpedModel = new LumpedModel(tfmr, getDPMatrixCalc, min_freq, max_freq, num_freqs);

            // wdg.Rs = 0;
            // wdg.Ls = 1.5e-6;
            // wdg.Rl = 10.5;
            // wdg.Ll = 1.5e-6;
            // getDPMatrixCalc.InductanceFudgeFactor = 0.88;
            // getDPMatrixCalc.SelfCapacitanceFudgeFactor = 1.0;
            // getDPMatrixCalc.MutualCapacitanceFudgeFactor = 1.35;
            // wdg.ResistanceFudgeFactor = 1.0;

            var taskDefinitions = new Dictionary<string, Func<IProgress<int>, Task<(Complex[], List<Complex[]>)>>>
                {
                    { "MTL Model w/ GetDP LCs", async progress => getDPModel.CalcResponse(progress) },
                    //{ "MTL Model w/ Analytic LCs", async progress => analyticModel.CalcResponse(progress) },
                    //{ "Lumped Model w/ GetDP LCs", async progress => lumpedModel.CalcResponse(progress) }
                };

            await RunTasksWithProgress(taskDefinitions, measuredData, impedanceData);

            var freqs = MathNet.Numerics.Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));

            AnsiConsole.MarkupLine("[bold green]All tasks completed successfully![/]");
        }

        static async Task Calc2()
        {
            min_freq = 1e6;
            max_freq = 100e6;
            num_freqs = 1000;

            Console.WriteLine("Howdy! This here is the dumbest middle-life crisis ever.");

            string directoryPath = @"~/src/DissertationSoftware/MTLTestApp/bin/Debug/net8.0/PULImpedances"; // Specify the directory path

            var measuredData = ReadMeasuredData(@"C:\Users\tcraymond\source\repos\DissertationSoftware\MTLTestApp\bin\Debug\net8.0\9FEB2025_NoCore");
            var impedanceData = ReadImpedanceData(@"C:\Users\tcraymond\source\repos\DissertationSoftware\MTLTestApp\bin\Debug\net8.0\9FEB2025_NoCore");

            var tfmr = TestModels.ModelWinding();

            var analyticMatrixCalc = new AnalyticMatrixCalculator();

            // wdg.num_discs = 1;
            // wdg.turns_per_disc = 2;

            // num_turns = wdg.num_turns;

            var analyticModel = new MTLModel(tfmr, analyticMatrixCalc, min_freq, max_freq, num_freqs);
            var lumpedModel = new LumpedModel(tfmr, analyticMatrixCalc, min_freq, max_freq, num_freqs);

            var taskDefinitions = new Dictionary<string, Func<IProgress<int>, Task<(Complex[], List<Complex[]>)>>>
                {
                    { "MTL Model w/ Analytic LCs", async progress => analyticModel.CalcResponse(progress) },
                    { "Lumped Model w/ Analytic LCs", async progress => lumpedModel.CalcResponse(progress) }
                };

            await RunTasksWithProgress(taskDefinitions, measuredData, impedanceData);

            AnsiConsole.MarkupLine("[bold green]All tasks completed successfully![/]");
        }

        public static void ShowPlots(List<DataFrame> measuredData, Dictionary<string, List<Complex[]>> calculatedResponses)
        {
            var freqs = MathNet.Numerics.Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));

            LinearAxis xAxis = new LinearAxis();
            xAxis.SetValue("title", "Frequency (Hz)");
            xAxis.SetValue("showgrid", false);
            xAxis.SetValue("showline", true);
            xAxis.SetValue("type", "log");

            LinearAxis yAxis = new LinearAxis();
            yAxis.SetValue("title", "Gain (dB)");
            yAxis.SetValue("showgrid", false);
            yAxis.SetValue("showline", true);

            Plotly.NET.Layout layout = new Plotly.NET.Layout();
            layout.SetValue("xaxis", xAxis);
            layout.SetValue("yaxis", yAxis);
            layout.SetValue("showlegend", true);

            var colors = new[] { "Green", "Blue", "Red", "Orange", "Purple", "Brown" };
            

            var charts = new List<GenericChart>();
            int i = 0;
            for (int t = 39; t < (num_turns - 1); t = t + 40)
            {
                int colorIndex = 0;
                var combinedCharts = new List<GenericChart>();

                var measuredChart = Chart2D.Chart.Line<double, double, string>(x: measuredData[i]["Frequency(Hz)"].Cast<double>().ToList(), y: measuredData[i]["CH2 Amplitude(dB)"].Cast<double>().ToList(), Name: "Measured", LineColor: Plotly.NET.Color.fromString(colors[colorIndex % colors.Length])).WithLayout(layout);
                colorIndex++;
                combinedCharts.Add(measuredChart);

                foreach (var response in calculatedResponses)
                {
                    var calculatedChart = Chart2D.Chart.Line<double, double, string>(x: freqs, y: response.Value[t].Select(c => 20d * Math.Log10(c.Magnitude)), Name: response.Key, LineColor: Plotly.NET.Color.fromString(colors[colorIndex % colors.Length])).WithLayout(layout);
                    colorIndex++;
                    combinedCharts.Add(calculatedChart);
                }

                i++;
                var combinedChart = Plotly.NET.Chart.Combine(combinedCharts).WithTitle($"Turn {t}");
                
                charts.Add(combinedChart);
            }

            var subplotGrid = Plotly.NET.Chart.Grid<IEnumerable<string>, IEnumerable<GenericChart>>(3, 2).Invoke(charts).WithSize(1600, 1200);
            subplotGrid.Show();
        }

        public static void ShowPlots_Z(DataFrame measuredData, Dictionary<string, Complex[]> calculatedImpedances)
        {
            var freqs = MathNet.Numerics.Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));

            LinearAxis xAxis = new LinearAxis();
            xAxis.SetValue("title", "Frequency (Hz)");
            xAxis.SetValue("showgrid", false);
            xAxis.SetValue("showline", true);
            xAxis.SetValue("type", "log");

            LinearAxis yAxis_mag = new LinearAxis();
            yAxis_mag.SetValue("title", "Impedance Magnitude (dB)");
            yAxis_mag.SetValue("showgrid", false);
            yAxis_mag.SetValue("showline", true);

            Plotly.NET.Layout layout_mag = new Plotly.NET.Layout();
            layout_mag.SetValue("xaxis", xAxis);
            layout_mag.SetValue("yaxis", yAxis_mag);
            layout_mag.SetValue("showlegend", true);

            LinearAxis yAxis_ph = new LinearAxis();
            yAxis_ph.SetValue("title", "Impedance Phase (deg)");
            yAxis_ph.SetValue("showgrid", false);
            yAxis_ph.SetValue("showline", true);

            Plotly.NET.Layout layout_ph = new Plotly.NET.Layout();
            layout_ph.SetValue("xaxis", xAxis);
            layout_ph.SetValue("yaxis", yAxis_ph);
            layout_ph.SetValue("showlegend", true);

            var charts = new List<GenericChart>();

            var combinedMagCharts = new List<GenericChart>();
            var combinedPhaseCharts = new List<GenericChart>();

            var colors = new[] { "Green", "Blue", "Red", "Orange", "Purple", "Brown" };
            int colorIndex = 0;
            
            List<double> Mag_Z_measured = measuredData["CH2 Amplitude(dB)"].Cast<double>().Select(z => 20 * Math.Log10(10.2) + z).ToList();
            List<double> Phase_Z_measured = measuredData["CH2 Phase(Deg)"].Cast<double>().ToList();

            var measuredMagChart = Chart2D.Chart.Line<double, double, string>(x: measuredData["Frequency(Hz)"].Cast<double>().ToList(), y: Mag_Z_measured, Name: "Measured", LineColor: Plotly.NET.Color.fromString(colors[colorIndex % colors.Length])).WithLayout(layout_mag);
            var measuredPhaseChart = Chart2D.Chart.Line<double, double, string>(x: measuredData["Frequency(Hz)"].Cast<double>().ToList(), y: Phase_Z_measured, Name: "Measured", ShowLegend: false, LineColor: Plotly.NET.Color.fromString(colors[colorIndex % colors.Length])).WithLayout(layout_ph);
            combinedMagCharts.Add(measuredMagChart);
            combinedPhaseCharts.Add(measuredPhaseChart);
            colorIndex++;

            foreach (var impedance in calculatedImpedances)
            {
                List<double> Mag_Z_calculated = impedance.Value.Select(z => 20d * Math.Log10(z.Magnitude)).ToList();
                List<double> Phase_Z_calculated = impedance.Value.Select(z => z.Phase * 180.0 / Math.PI).ToList();

                var calculatedMagChart = Chart2D.Chart.Line<double, double, string>(x: freqs, y: Mag_Z_calculated, Name: impedance.Key, LineColor: Plotly.NET.Color.fromString(colors[colorIndex % colors.Length])).WithLayout(layout_mag);
                var calculatedPhaseChart = Chart2D.Chart.Line<double, double, string>(x: freqs, y: Phase_Z_calculated, Name: impedance.Key, ShowLegend: false, LineColor: Plotly.NET.Color.fromString(colors[colorIndex % colors.Length])).WithLayout(layout_ph);

                combinedMagCharts.Add(calculatedMagChart);
                combinedPhaseCharts.Add(calculatedPhaseChart);

                colorIndex++;
            }

            charts.Add(Plotly.NET.Chart.Combine(combinedMagCharts));
            charts.Add(Plotly.NET.Chart.Combine(combinedPhaseCharts));

            var subplotGrid = Plotly.NET.Chart.Grid<IEnumerable<string>, IEnumerable<GenericChart>>(2, 1).Invoke(charts).WithSize(1600, 1200);
            subplotGrid.Show();
        }

        public static void Show3DPlot(List<Complex[]> calculatedResponses)
        {
            var numTurns = calculatedResponses.Count;
            var turns = MathNet.Numerics.Generate.LinearSpaced(numTurns, 0, numTurns - 1);
            var freqs = MathNet.Numerics.Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));
            List<double> x = new List<double>();
            List<double> y = new List<double>();
            List<List<double>> z = new List<List<double>>();
            foreach (var turn in turns)
            {
                int i = 0;
                var zRow = new List<double>();
                foreach (var freq in freqs)
                {
                    var response = calculatedResponses[(int)turn][i];
                    x.Add(turn);
                    y.Add(freq);
                    zRow.Add(response.Magnitude);
                    i++;
                }
                z.Add(zRow);
            }
            
            var chart = Chart3D.Chart.Surface<IEnumerable<double>, double, double, double, double>(
                zData: z,
                X: freqs,
                Y: turns
            ).WithTitle("3D Plot").WithSize(1600, 1200);
            chart.Show();
        }

        public static void Show3DPlot_Meas(List<DataFrame> measuredResponses)
        {
            var freqs = measuredResponses[0]["Frequency(Hz)"].Cast<double>().ToList(); //MathNet.Numerics.Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));
            List<double> x = new List<double>();
            List<double> y = new List<double>();
            List<List<double>> z = new List<List<double>>();
            var zRow = new List<double>();
            y.Add(0);
            foreach (var freq in freqs)
            {
                zRow.Add(1.0);
                x.Add(freq);
            }
            z.Add(zRow);
            for (int turn = 0; turn < measuredResponses.Count; turn++)
            {
                int i = 0;
                zRow = new List<double>();
                y.Add(-1 + 40 * (turn + 1));
                foreach (var freq in freqs)
                {
                    var response = measuredResponses[turn]["CH2 Amplitude(dB)"].Cast<double>().Select(v => Math.Pow(10, v / 20)).ToList()[i];
                    
                    //x.Add(freq);
                    zRow.Add(response);
                    i++;
                }
                z.Add(zRow);
            }
            zRow = new List<double>();
            y.Add(279);
            foreach (var freq in freqs)
            {
                zRow.Add(0.0);
                //x.Add(freq);
            }
            z.Add(zRow);

            var chart = Chart3D.Chart.Surface<IEnumerable<double>, double, double, double, double>(
                zData: z//,
                //X: x,
                //Y: y
            ).WithTitle("3D Plot").WithSize(1600, 1200);
            chart.Show();
        }

        static void DisplayMatrixAsTable(Matrix<double> matrix)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<html><head><title>Matrix Display</title></head><body>");
            html.AppendLine("<table border='1'>");

            for (int i = 0; i < matrix.RowCount; i++)
            {
                html.AppendLine("<tr>");
                for (int j = 0; j < matrix.ColumnCount; j++)
                {
                    html.AppendFormat("<td>{0}</td>", matrix[i, j]);
                }
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");
            html.AppendLine("</body></html>");

            // Generate a unique filename in the temporary directory
            string fileName = Path.Combine(Path.GetTempPath(), $"MatrixDisplay_{Guid.NewGuid()}.html");
            File.WriteAllText(fileName, html.ToString());

            // Open the HTML file in the default web browser
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            });
        }

        static List<DataFrame> ReadMeasuredData(string directoryPath)
        {
            List<DataFrame> measuredData = new List<DataFrame>();
            for (int j = 1; j <= 6; j++)
            {
                measuredData.Add(ReadCSVToDF($"{directoryPath}/SDS0000{j}_bode.csv"));
            }

            return measuredData;
        }

        static DataFrame ReadImpedanceData(string directoryPath)
        {
            DataFrame measuredData = new DataFrame();
            return ReadCSVToDF($"{directoryPath}/impedance_bode.csv");
        }

        static DataFrame ReadCSVToDF(string filename)
        {
            // Read all lines from the file
            string[] allLines = File.ReadAllLines(filename);

            // Skip the first 24 lines
            var dataLines = allLines[27..];

            // Assuming the first non-skipped line contains headers
            string[] headers = dataLines[0].Split(',');

            // Prepare lists to hold data for each column
            List<DoubleDataFrameColumn> columns = new List<DoubleDataFrameColumn>();
            foreach (var header in headers)
            {
                columns.Add(new DoubleDataFrameColumn(header, 0));
            }

            // Parse each line and fill the columns
            foreach (var line in dataLines[1..]) // Skipping header line in dataLines
            {
                var values = line.Split(',');
                for (int i = 0; i < values.Length; i++)
                {
                    columns[i].Append(Double.Parse(values[i]));
                }
            }

            // Create the DataFrame
            DataFrame df = new DataFrame(columns);

            return df;
        }
    }

}
