using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TfmrLib.FEM;

namespace LCCalc
{
    /// <summary>
    /// Renders the machine-readable progress stream from mfem-electromag into a Spectre
    /// status display: completed operations are logged as permanent lines, the operation
    /// currently running (plus its live wall-clock elapsed time and the latest solver
    /// message) drives the spinner's status line, and warnings/errors are called out in
    /// colour. A background ticker refreshes the elapsed time even when the solver is
    /// silent, so a long solve is visibly progressing rather than looking hung.
    /// </summary>
    internal sealed class SolverProgressReporter : IDisposable
    {
        private readonly StatusContext _ctx;
        private readonly bool _verbose;
        private readonly Lock _sync = new();
        private readonly List<(string Name, double Seconds)> _timings = [];
        private readonly Stopwatch _operationClock = new();
        private readonly Timer _ticker;

        private string? _currentOperation;
        private string? _latestDetail;
        private string _headline;
        private int _warnings;
        private int _errors;

        public SolverProgressReporter(StatusContext ctx, string headline, bool verbose)
        {
            _ctx = ctx;
            _headline = headline;
            _verbose = verbose;
            _ctx.Status(Markup.Escape(headline));

            // Refresh roughly at the spinner's own cadence so the elapsed counter advances
            // smoothly without fighting the renderer.
            _ticker = new Timer(_ => RefreshStatus(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// Sets the text shown when no solver operation is active (e.g. the frequency
        /// currently being calculated).
        /// </summary>
        public void SetHeadline(string headline)
        {
            lock (_sync)
            {
                _headline = headline;
            }

            RefreshStatus();
        }

        public void Report(MFEMProgressEvent progress)
        {
            // Progress arrives on the solver's stdout reader thread while the ticker fires
            // on a timer thread; serialise both so console writes never interleave.
            lock (_sync)
            {
                if (progress.EventType == MFEMProgressEventType.Operation)
                    ReportOperation(progress);
                else
                    ReportMessage(progress);
            }
        }

        private void ReportOperation(MFEMProgressEvent progress)
        {
            var name = progress.Name ?? "operation";

            switch (progress.State)
            {
                case "started":
                    lock (_sync)
                    {
                        _currentOperation = name;
                        _latestDetail = null;
                        _operationClock.Restart();
                    }
                    RefreshStatus();
                    break;

                case "completed":
                case "failed":
                    double seconds;
                    lock (_sync)
                    {
                        seconds = progress.ElapsedSeconds ?? _operationClock.Elapsed.TotalSeconds;
                        _timings.Add((name, seconds));
                        _currentOperation = null;
                        _latestDetail = null;
                        _operationClock.Reset();
                    }

                    bool failed = progress.State == "failed";
                    if (failed)
                        Interlocked.Increment(ref _errors);

                    var glyph = failed ? "[red]x[/]" : "[green]\u2713[/]";
                    var colour = failed ? "red" : "grey";
                    AnsiConsole.MarkupLine($"  {glyph} [{colour}]{Markup.Escape(name)}[/] [grey]({FormatDuration(seconds)})[/]");
                    RefreshStatus();
                    break;
            }
        }

        private void ReportMessage(MFEMProgressEvent progress)
        {
            var message = progress.Message;
            if (string.IsNullOrWhiteSpace(message))
                return;

            switch (progress.Level)
            {
                case "error":
                    Interlocked.Increment(ref _errors);
                    AnsiConsole.MarkupLine($"  [red]error:[/] {Markup.Escape(message)}");
                    break;

                case "warning":
                    Interlocked.Increment(ref _warnings);
                    AnsiConsole.MarkupLine($"  [yellow]warning:[/] {Markup.Escape(message)}");
                    break;

                case "solver":
                    // Solver iterations are far too chatty to log; they are the best
                    // liveness signal though, so they ride along on the status line.
                    lock (_sync)
                    {
                        _latestDetail = message;
                    }
                    RefreshStatus();
                    break;

                case "status":
                    AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(message)}[/]");
                    break;

                default:
                    if (_verbose)
                        AnsiConsole.MarkupLine($"  [grey42]{Markup.Escape(message)}[/]");
                    break;
            }
        }

        private void RefreshStatus()
        {
            lock (_sync)
            {
                string status;
                if (_currentOperation is null)
                {
                    status = _headline;
                }
                else
                {
                    status = $"{_currentOperation} [{FormatDuration(_operationClock.Elapsed.TotalSeconds)}]";
                    if (_latestDetail is not null)
                        status += $" - {_latestDetail}";
                }

                _ctx.Status(Markup.Escape(status));
                _ctx.Refresh();
            }
        }

        /// <summary>
        /// Prints a per-operation timing table plus a warning/error tally for the run.
        /// </summary>
        public void WriteSummary()
        {
            List<(string Name, double Seconds)> timings;
            lock (_sync)
            {
                if (_timings.Count == 0)
                    return;

                timings = [.. _timings];
                _timings.Clear();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Solver stage")
                    .AddColumn(new TableColumn("Elapsed").RightAligned());

                double total = 0;
                foreach (var (name, seconds) in timings)
                {
                    total += seconds;
                    table.AddRow(Markup.Escape(name), FormatDuration(seconds));
                }

                table.AddEmptyRow();
                table.AddRow("[bold]total[/]", $"[bold]{FormatDuration(total)}[/]");
                AnsiConsole.Write(table);

                if (_warnings > 0 || _errors > 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]{_warnings}[/] warning(s), [red]{_errors}[/] error(s) reported by the solver.");
                }
            }
        }

        private static string FormatDuration(double seconds) =>
            seconds >= 60
                ? $"{(int)(seconds / 60)}m {seconds % 60:F1}s"
                : $"{seconds:F1}s";

        public void Dispose() => _ticker.Dispose();
    }
}
