#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using static Game.App.DevRuntime.Scenarios.ScenarioStatement;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Bounded main-thread scenario execution and final JSON evidence (SPEC-70 §5.3).</summary>
    public sealed class ScenarioRunner : IDisposable
    {
        readonly struct StepResult
        {
            internal readonly int Line;
            internal readonly string Text, Outcome, Output;
            internal readonly double Seconds;
            internal StepResult(ScenarioStatement step, string outcome, double seconds, string output)
            { Line = step.Line; Text = step.Text; Outcome = outcome; Seconds = seconds; Output = output; }
        }
        readonly ScenarioScript script;
        readonly DevContext context;
        readonly string outputDirectory;
        readonly DateTime startedUtc;
        readonly double started;
        readonly Dictionary<string, ScenarioValue> variables = new Dictionary<string, ScenarioValue>(StringComparer.Ordinal);
        readonly List<StepResult> results;
        int position, pendingOperation;
        long calls, startCall, errorAllowance;
        double stepStarted, startTick;
        bool entered;
        ulong seed;
        public ScenarioRun Run { get; }
        public bool Finished => Run.Outcome != ScenarioOutcome.Running;
        public ScenarioRunner(ScenarioScript script, string filePath, int runId, DevContext context, string outputDirectory = null)
        {
            this.script = script ?? throw new ArgumentNullException(nameof(script));
            this.context = context.CreateScenarioContext();
            this.outputDirectory = outputDirectory;
            startedUtc = DateTime.UtcNow; started = Time.realtimeSinceStartupAsDouble;
            results = new List<StepResult>(script.Statements.Count);
            Run = new ScenarioRun { Name = script.Name, FilePath = filePath ?? "", RunId = runId };
            if (script.Status == "draft") Finish(ScenarioOutcome.Ignored, "draft");
        }
        public void Step()
        {
            if (Finished) return;
            calls++;
            double now = Time.realtimeSinceStartupAsDouble;
            Run.ElapsedSeconds = now - started;
            if (Run.ElapsedSeconds > script.TimeoutSeconds) { Fail(ScenarioOutcome.Failed, "scenario timeout"); return; }
            for (int work = 0; work < 32 && !Finished; work++)
            {
                if (position >= script.Statements.Count)
                {
                    if (context.LogErrors > errorAllowance)
                        Finish(ScenarioOutcome.Failed, "unexpected error logs: " + string.Join(" | ", context.Logs.ErrorMessagesSince(context.LogStartSequence, 3)));
                    else Finish(ScenarioOutcome.Passed, "");
                    return;
                }
                var step = script.Statements[position]; Run.CurrentLine = step.Line;
                bool first = !entered;
                if (first) { entered = true; stepStarted = now; startCall = calls; startTick = context.Tick; }
                try
                {
                    string output = "";
                    switch (step.Kind)
                    {
                        case StatementKind.Command:
                            if (first)
                            {
                                if (step.Text.StartsWith("newgame ", StringComparison.Ordinal) && DevArgs.TryParse(step.Text, out _, out var args, out _)) seed = args.GetULong("seed", 0);
                                var result = context.Host.Commands.Execute(step.Text, context);
                                if (result.Kind == DevResultKind.Failed) { Fail(ScenarioOutcome.Error, result.Text); return; }
                                if (result.Kind == DevResultKind.Pending) { pendingOperation = result.OperationId; return; }
                                output = result.Text;
                            }
                            else if (pendingOperation != 0)
                            {
                                if (!context.Host.TryGetOperation(pendingOperation, out var operation)) { Fail(ScenarioOutcome.Error, "unknown operation " + pendingOperation); return; }
                                if (operation.Status == DevOperationStatus.Running) return;
                                if (operation.Status == DevOperationStatus.Failed) { Fail(ScenarioOutcome.Error, operation.Message); return; }
                                output = "op " + pendingOperation + " succeeded: " + operation.Message;
                            }
                            break;
                        case StatementKind.WaitCondition:
                            if (!ScenarioExpression.RequireBool(step.Expression.Evaluate(context, variables)))
                            {
                                if (now - stepStarted > step.TimeoutSeconds) Fail(ScenarioOutcome.Failed, "wait timed out: " + step.ExpressionText);
                                return;
                            }
                            break;
                        case StatementKind.WaitFrames:
                            if (calls - startCall < step.Count) return;
                            break;
                        case StatementKind.WaitTicks:
                            double tick = context.Tick;
                            if (startTick < 0 || tick < 0) { if (calls - startCall < step.Count) return; }
                            else if (unchecked((uint)tick - (uint)startTick) < (ulong)step.Count) return;
                            break;
                        case StatementKind.Assert:
                            if (!ScenarioExpression.RequireBool(step.Expression.Evaluate(context, variables)))
                            {
                                string values = step.Expression.ComparisonValues(context, variables);
                                Fail(ScenarioOutcome.Failed, (step.Message ?? "assert failed: " + step.ExpressionText) + (values.Length > 0 ? " (" + values + ")" : "")); return;
                            }
                            break;
                        case StatementKind.Let: variables[step.Name] = step.Expression.Evaluate(context, variables); break;
                        case StatementKind.ExpectErrors: errorAllowance = step.Count; break;
                    }
                    results.Add(new StepResult(step, "ok", Time.realtimeSinceStartupAsDouble - stepStarted, output));
                    position++; entered = false; pendingOperation = 0;
                }
                catch (Exception e) { Fail(ScenarioOutcome.Error, e.GetType().Name + ": " + e.Message); }
            }
        }
        void Fail(ScenarioOutcome outcome, string message)
        {
            if (position < script.Statements.Count)
            {
                var step = script.Statements[position];
                Run.CurrentLine = step.Line;
                results.Add(new StepResult(step, outcome == ScenarioOutcome.Failed ? "fail" : "error", entered ? Time.realtimeSinceStartupAsDouble - stepStarted : 0, message));
                message = "line " + step.Line + ": " + message;
            }
            Finish(outcome, message);
        }
        public void Abort(string message = "play mode exited") { if (!Finished) Fail(ScenarioOutcome.Error, message); }
        void Finish(ScenarioOutcome outcome, string message)
        {
            Run.Outcome = outcome; Run.ExitCode = ExitCode(outcome); Run.Message = message;
            Run.ElapsedSeconds = Time.realtimeSinceStartupAsDouble - started;
            try
            {
                var json = Header(Run, script.Status, startedUtc, seed).Append(",\"steps\":[");
                for (int i = 0; i < results.Count; i++)
                {
                    if (i > 0) json.Append(','); var result = results[i];
                    json.Append("{\"line\":").Append(result.Line).Append(",\"text\":").Append(DevJsonWriter.Quote(result.Text))
                        .Append(",\"outcome\":").Append(DevJsonWriter.Quote(result.Outcome)).Append(",\"seconds\":").Append(DevJsonWriter.Number(result.Seconds))
                        .Append(",\"output\":").Append(DevJsonWriter.Quote(result.Output)).Append('}');
                }
                json.Append("],\"logs\":{\"errors\":").Append(context.LogErrors).Append(",\"exceptions\":").Append(context.LogExceptions).Append(",\"warnings\":").Append(context.LogWarnings).Append("}}");
                SaveJson(Run, json.ToString(), startedUtc, outputDirectory);
            }
            catch (Exception e) { Run.Outcome = ScenarioOutcome.Error; Run.ExitCode = 2; Run.Message += "; result write failed: " + e.Message; }
            finally { context.Dispose(); }
        }
        static int ExitCode(ScenarioOutcome outcome) => outcome == ScenarioOutcome.Error ? 2 : outcome == ScenarioOutcome.Failed ? 1 : 0;
        static string Result(ScenarioOutcome outcome) => outcome == ScenarioOutcome.Passed ? "pass" : outcome == ScenarioOutcome.Failed ? "fail" : outcome == ScenarioOutcome.Ignored ? "ignored" : "error";
        static StringBuilder Header(ScenarioRun run, string status, DateTime started, ulong seed)
            => new StringBuilder().Append("{\"scenario\":").Append(DevJsonWriter.Quote(run.Name)).Append(",\"file\":").Append(DevJsonWriter.Quote(run.FilePath))
                .Append(",\"status\":").Append(DevJsonWriter.Quote(status)).Append(",\"result\":").Append(DevJsonWriter.Quote(Result(run.Outcome)))
                .Append(",\"exitCode\":").Append(run.ExitCode).Append(",\"message\":").Append(DevJsonWriter.Quote(run.Message))
                .Append(",\"startedUtc\":").Append(DevJsonWriter.Quote(started.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))
                .Append(",\"durationSeconds\":").Append(DevJsonWriter.Number(run.ElapsedSeconds)).Append(",\"failedLine\":").Append(run.ExitCode == 0 ? 0 : run.CurrentLine)
                .Append(",\"seed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
        static void SaveJson(ScenarioRun run, string json, DateTime started, string directory)
        {
            directory ??= Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "playtest", "scenarios"));
            Directory.CreateDirectory(directory);
            string safeName = ""; foreach (char c in run.Name ?? "scenario") if (char.IsLetterOrDigit(c) || c == '_') safeName += c;
            if (safeName.Length == 0) safeName = "scenario";
            string stem = Path.Combine(directory, safeName + "-" + started.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + run.RunId);
            string path = stem + ".json"; int suffix = 0;
            while (File.Exists(path)) path = stem + "-" + ++suffix + ".json";
            File.WriteAllText(path, json + "\n", new UTF8Encoding(false)); run.ResultJsonPath = path;
        }
        public static ScenarioRun ErrorRun(string name, string filePath, int runId, string message, string outputDirectory = null)
        {
            var run = new ScenarioRun { Name = name, FilePath = filePath, RunId = runId, Outcome = ScenarioOutcome.Error, ExitCode = 2, Message = message };
            var now = DateTime.UtcNow;
            try { SaveJson(run, Header(run, "error", now, 0).Append(",\"steps\":[],\"logs\":{\"errors\":0,\"exceptions\":0,\"warnings\":0}}").ToString(), now, outputDirectory); }
            catch (Exception e) { run.Message += "; result write failed: " + e.Message; }
            return run;
        }
        public void Dispose() { Abort("scenario disposed"); }
    }
}
#endif
