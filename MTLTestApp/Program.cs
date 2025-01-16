using LinAlg = MathNet.Numerics.LinearAlgebra;
using Plotly.NET.LayoutObjects;
using Plotly.NET;
using System.Numerics;
using TDAP;
using System;
using MathNet.Numerics;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.Analysis;
using TfmrLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.IO;
using ShellProgressBar;

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

            // Configure the main progress bar options
            var mainProgressBarOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkGray,
                ProgressCharacter = '─',
                CollapseWhenFinished = false
            };

            // Configure child progress bar options
            var childProgressBarOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Cyan,
                BackgroundColor = ConsoleColor.DarkGray,
                ProgressCharacter = '─',
                CollapseWhenFinished = false,
                DisplayTimeInRealTime = false // Optional: Prevents updating time every tick
            };

            // Initialize the main progress bar
            using (var mainProgress = new ProgressBar(100, "Overall Progress", mainProgressBarOptions))
            {
                // Spawn child progress bars for each task
                var getdpBar = mainProgress.Spawn(100, "GetDPModel", childProgressBarOptions);
                var analyticBar = mainProgress.Spawn(100, "AnalyticModel", childProgressBarOptions);
                var lumpedBar = mainProgress.Spawn(100, "LumpedModel", childProgressBarOptions);

                // Create IProgress<int> instances linked to each child progress bar
                IProgress<int> progressGetDP = new Progress<int>(percent =>
                {
                    getdpBar.Tick(percent);
                    UpdateMainProgress(mainProgress, getdpBar, analyticBar, lumpedBar);
                });

                IProgress<int> progressAnalytic = new Progress<int>(percent =>
                {
                    analyticBar.Tick(percent);
                    UpdateMainProgress(mainProgress, getdpBar, analyticBar, lumpedBar);
                });

                IProgress<int> progressLumped = new Progress<int>(percent =>
                {
                    lumpedBar.Tick(percent);
                    UpdateMainProgress(mainProgress, getdpBar, analyticBar, lumpedBar);
                });

                // Start the tasks
                tasks.Add(Task.Run(() =>
                {
                    V_response_getdp = getDPModel.CalcResponse(progressGetDP);
                    getdpBar.Tick(100); // Ensure completion
                }));

                tasks.Add(Task.Run(() =>
                {
                    V_response_analytic = analyticModel.CalcResponse(progressAnalytic);
                    analyticBar.Tick(100); // Ensure completion
                }));

                tasks.Add(Task.Run(() =>
                {
                    V_response_lumped = lumpedModel.CalcResponse(progressLumped);
                    lumpedBar.Tick(100); // Ensure completion
                }));

                // Await all tasks to complete
                await Task.WhenAll(tasks);

                // Optionally, mark the main progress as complete
                mainProgress.Tick(100);

            }
                
            ShowPlots(measuredData, V_response_getdp, V_response_analytic, V_response_lumped);
        }

        static private void UpdateMainProgress(ProgressBar mainProgress, ChildProgressBar getdpBar, ChildProgressBar analyticBar, ChildProgressBar lumpedBar)
        {
            // Calculate average progress
            double average = (getdpBar.CurrentTick + analyticBar.CurrentTick + lumpedBar.CurrentTick) / 3.0;
            mainProgress.Tick((int)average);
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

            Layout layout = new Layout();
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
                var chart1 = Chart2D.Chart.Line<double, double, string>(x: freqs, y: V_response_getdp[t], Name: "Calculated", LineColor: Color.fromString("Red")).WithLayout(layout);
                //trace.SetValue("x", freqs);
                //trace.SetValue("y", V_response_analytic[t]);
                //trace.SetValue("mode", "lines");
                //trace.SetValue("name", $"Turn {t + 1}");

                //var trace2 = new Plotly.NET.Trace("scatter");

                var chart2 = Chart2D.Chart.Line<double, double, string>(x: measuredData[i]["Frequency(Hz)"].Cast<double>().ToList(), y: measuredData[i]["CH2 Amplitude(dB)"].Cast<double>().ToList(), Name: "Measured", LineColor: Color.fromString("Blue")).WithLayout(layout);
                //trace2.SetValue("x", measuredData[i]["Frequency(Hz)"]);
                //trace2.SetValue("y", measuredData[i]["CH2 Amplitude(dB)"]);
                //trace2.SetValue("mode", "lines");
                //trace2.SetValue("name", $"Turn {t + 1}");

                var chart4 = Chart2D.Chart.Line<double, double, string>(x: freqs, y: V_response_lumped[t], Name: "Lumped", LineColor: Color.fromString("Green")).WithLayout(layout);

                var chart3 = Chart2D.Chart.Line<double, double, string>(x: freqs, y: V_response_analytic[t], Name: "Analytic", LineColor: Color.fromString("Green")).WithLayout(layout);

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
