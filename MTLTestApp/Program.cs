using Plotly.NET.LayoutObjects;
using Plotly.NET;
using MathNet.Numerics.LinearAlgebra;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.Analysis;
using TfmrLib;
using Spectre.Console;

namespace MTLTestApp
{
    //using Matrix_d = LinAlg.Matrix<double>;
    //using Matrix_c = LinAlg.Matrix<Complex>;
    //using Vector_d = LinAlg.Vector<double>;
    //using Vector_c = LinAlg.Vector<Complex>;

    internal class Program
    {
        //static private MatrixBuilder<double> M_d = Matrix_d.Build;
        //static private MatrixBuilder<Complex> M_c = Matrix_c.Build;
        //static private VectorBuilder<double> V_d = Vector_d.Build;
        //static private VectorBuilder<Complex> V_c = Vector_c.Build;

        // static Winding wdg = new Winding();

        static double min_freq = 100e3;
        static double max_freq = 1e6;
        static int num_freqs = 100;

        static int num_turns;

        static async Task Main(string[] args)
        {

            Console.WriteLine("Howdy! This here is the dumbest middle-life crisis ever.");

            //TestPlots();

            string directoryPath = @"C:\Users\tcraymond\source\repos\DissertationSoftware\MTLTestApp\bin\Debug\net8.0\PULImpedances"; // Specify the directory path

            var measuredData = ReadMeasuredData(@"C:\Users\tcraymond\source\repos\DissertationSoftware\MTLTestApp\bin\Debug\net8.0\26DEC2023_Rough_NoCore");

            var wdgAnalytic = new WindingAnalytic();
            var wdgGetDP = new WindingExtModel(directoryPath);

            num_turns = wdgAnalytic.num_turns;

            var tasks = new List<Task>();

            var analyticModel = new MTLModel(wdgAnalytic, min_freq, max_freq, num_freqs);
            var getDPModel = new MTLModel(wdgGetDP, min_freq, max_freq, num_freqs);
            var lumpedModel = new LumpedModel(wdgGetDP, min_freq, max_freq, num_freqs);

            List<double[]> V_response_getdp = null;
            List<double[]> V_response_analytic = null;
            List<double[]> V_response_lumped = null;

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
                    //new SpinnerColumn(),            // Spinner
                })
                .StartAsync(async ctx =>
                {
                    // Define tasks in Spectre.Console's progress context
                    var getdpTask = ctx.AddTask("MTL Model w/ GetDP LCs", maxValue: 100);
                    var analyticTask = ctx.AddTask("MTL Model w/ Analytic LCs", maxValue: 100);
                    var lumpedTask = ctx.AddTask("Lumped Model w/ GetDP LCs", maxValue: 100);

                    // Create IProgress<int> instances linked to Spectre.Console tasks
                    var progressGetDP = new Progress<int>(percent =>
                    {
                        getdpTask.Value = percent;
                    });

                    var progressAnalytic = new Progress<int>(percent =>
                    {
                        analyticTask.Value = percent;
                    });

                    var progressLumped = new Progress<int>(percent =>
                    {
                        lumpedTask.Value = percent;
                    });

                    // Start the tasks
                    var task1 = Task.Run(() =>
                    {
                        V_response_getdp = getDPModel.CalcResponse(progressGetDP);
                        getdpTask.Value = 100; // Ensure completion
                    });

                    var task2 = Task.Run(() =>
                    {
                        V_response_analytic = analyticModel.CalcResponse(progressAnalytic);
                        analyticTask.Value = 100; // Ensure completion
                    });

                    var task3 = Task.Run(() =>
                    {
                        V_response_lumped = lumpedModel.CalcResponse(progressLumped);
                        lumpedTask.Value = 100; // Ensure completion
                    });

                    tasks.Add(task1);
                    tasks.Add(task2);
                    tasks.Add(task3);

                    // Wait for all tasks to complete
                    await Task.WhenAll(tasks);
                });

            ShowPlots(measuredData, V_response_getdp, V_response_analytic, V_response_lumped);
            AnsiConsole.MarkupLine("[bold green]All tasks completed successfully![/]");
        }


        public static void ShowPlots(List<DataFrame> measuredData, List<double[]> V_response_getdp, List<double[]> V_response_analytic, List<double[]> V_response_lumped)
        {
            //var freqs = Generate.LinearSpaced(num_freqs, min_freq, max_freq);
            var freqs = MathNet.Numerics.Generate.LogSpaced(num_freqs, Math.Log10(min_freq), Math.Log10(max_freq));

            LinearAxis xAxis = new LinearAxis();
            xAxis.SetValue("title", "xAxis");
            xAxis.SetValue("showgrid", false);
            xAxis.SetValue("showline", true);
            xAxis.SetValue("type", "log");

            LinearAxis yAxis = new LinearAxis();
            yAxis.SetValue("title", "yAxis");
            yAxis.SetValue("showgrid", false);
            yAxis.SetValue("showline", true);

            Plotly.NET.Layout layout = new Plotly.NET.Layout();
            layout.SetValue("xaxis", xAxis);
            layout.SetValue("yaxis", yAxis);
            layout.SetValue("showlegend", true);

            var charts = new List<GenericChart>();
            int i = 0;
            // TODO: Verify this
            for (int t = 40; t < (num_turns - 1); t = t + 40)
            {
                //Console.WriteLine($"Turn: {t - 1}\tr: {wdg.GetTurnMidpoint(t - 1).r}\tz: {wdg.GetTurnMidpoint(t - 1).z}");
                //Console.WriteLine($"Turn: {t}\tr: {wdg.GetTurnMidpoint(t).r}\tz: {wdg.GetTurnMidpoint(t).z}");
                //Console.WriteLine($"Turn: {t + 1}\tr: {wdg.GetTurnMidpoint(t + 1).r}\tz: {wdg.GetTurnMidpoint(t + 1).z}");

                //var traces = new List<Plotly.NET.Trace>();
                var chart1 = Chart2D.Chart.Line<double, double, string>(x: freqs, y: V_response_getdp[t], Name: "Calculated", LineColor: Plotly.NET.Color.fromString("Red")).WithLayout(layout);
                //trace.SetValue("x", freqs);
                //trace.SetValue("y", V_response_analytic[t]);
                //trace.SetValue("mode", "lines");
                //trace.SetValue("name", $"Turn {t + 1}");

                //var trace2 = new Plotly.NET.Trace("scatter");

                var chart2 = Chart2D.Chart.Line<double, double, string>(x: measuredData[i]["Frequency(Hz)"].Cast<double>().ToList(), y: measuredData[i]["CH2 Amplitude(dB)"].Cast<double>().ToList(), Name: "Measured", LineColor: Plotly.NET.Color.fromString("Blue")).WithLayout(layout);
                //trace2.SetValue("x", measuredData[i]["Frequency(Hz)"]);
                //trace2.SetValue("y", measuredData[i]["CH2 Amplitude(dB)"]);
                //trace2.SetValue("mode", "lines");
                //trace2.SetValue("name", $"Turn {t + 1}");

                var chart4 = Chart2D.Chart.Line<double, double, string>(x: freqs, y: V_response_lumped[t], Name: "Lumped", LineColor: Plotly.NET.Color.fromString("Green")).WithLayout(layout);

                var chart3 = Chart2D.Chart.Line<double, double, string>(x: freqs, y: V_response_analytic[t], Name: "Analytic", LineColor: Plotly.NET.Color.fromString("Green")).WithLayout(layout);

                i++;

                charts.Add(Plotly.NET.Chart.Combine([chart1, chart2, chart3, chart4]).WithTitle($"Turn {t}"));
                //charts.Add(chart2);
            }

            var subplotGrid = Plotly.NET.Chart.Grid<IEnumerable<string>, IEnumerable <GenericChart>> (3, 2).Invoke(charts).WithSize(1600, 1200);

            // Show the combined chart with subplots
            subplotGrid.Show();
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

        static DataFrame ReadCSVToDF(string filename)
        {
            // Read all lines from the file
            string[] allLines = File.ReadAllLines(filename);

            // Skip the first 24 lines
            var dataLines = allLines[24..];

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
