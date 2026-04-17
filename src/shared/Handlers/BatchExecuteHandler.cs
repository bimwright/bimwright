using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Bimwright.Plugin.Handlers
{
    /// <summary>
    /// A6 batch execution (aspect #3 §A6). Wraps N MCP commands in a single
    /// <see cref="TransactionGroup"/> so the whole batch resolves to one Revit undo
    /// step. Inner handlers each open their own <see cref="Transaction"/>; TransactionGroup
    /// permits that (nested Transactions are forbidden, but inner-tx-inside-tx-group is fine).
    ///
    /// Semantics:
    /// - On any sub-command failure with <c>continueOnError=false</c> (default): stop, call
    ///   <c>TransactionGroup.RollBack</c>, return collected results + <c>rolledBack=true</c>.
    /// - On any failure with <c>continueOnError=true</c>: record per-command ok/error,
    ///   continue the batch, call <c>Assimilate</c> at the end (surviving writes committed).
    /// - On full success: <c>Assimilate</c> → single undo step.
    /// </summary>
    public class BatchExecuteHandler : IRevitCommand
    {
        private readonly CommandDispatcher _dispatcher;

        public BatchExecuteHandler(CommandDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "batch_execute";
        public string Description => "Run multiple MCP commands in one Revit TransactionGroup (one undo step).";
        public string ParametersSchema => @"{""type"":""object"",""properties"":{""commands"":{""type"":""array"",""items"":{""type"":""object""}},""continueOnError"":{""type"":""boolean""}},""required"":[""commands""]}";

        public CommandResult Execute(UIApplication app, string paramsJson)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return CommandResult.Fail("No document is open.");

            var request = JObject.Parse(paramsJson);
            var commandsArr = request["commands"] as JArray;
            if (commandsArr == null || commandsArr.Count == 0)
                return CommandResult.Fail("'commands' must be a non-empty array.");

            var continueOnError = request.Value<bool?>("continueOnError") ?? false;
            var results = new List<object>();
            var anyFailed = false;

            using (var tg = new TransactionGroup(doc, "MCP: batch_execute"))
            {
                tg.Start();

                for (var i = 0; i < commandsArr.Count; i++)
                {
                    var cmd = commandsArr[i] as JObject;
                    var cmdName = cmd?.Value<string>("command");

                    if (string.IsNullOrEmpty(cmdName))
                    {
                        results.Add(new { index = i, ok = false, error = "Missing 'command' field." });
                        anyFailed = true;
                        if (!continueOnError) break;
                        continue;
                    }

                    var handler = _dispatcher.GetCommand(cmdName);
                    if (handler == null)
                    {
                        results.Add(new { index = i, ok = false, error = $"Unknown command: {cmdName}" });
                        anyFailed = true;
                        if (!continueOnError) break;
                        continue;
                    }

                    // Forbid recursion — a batch_execute inside a batch would double-wrap
                    // the TransactionGroup and throw. Fail fast with a clear message.
                    if (string.Equals(cmdName, "batch_execute", StringComparison.Ordinal))
                    {
                        results.Add(new { index = i, ok = false, error = "Nested batch_execute is not supported." });
                        anyFailed = true;
                        if (!continueOnError) break;
                        continue;
                    }

                    var subParams = cmd["params"]?.ToString() ?? "{}";

                    try
                    {
                        var r = handler.Execute(app, subParams);
                        if (r.Success)
                        {
                            results.Add(new { index = i, ok = true, data = r.Data });
                        }
                        else
                        {
                            results.Add(new { index = i, ok = false, error = r.Error });
                            anyFailed = true;
                            if (!continueOnError) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { index = i, ok = false, error = ex.Message });
                        anyFailed = true;
                        if (!continueOnError) break;
                    }
                }

                bool rolledBack;
                if (anyFailed && !continueOnError)
                {
                    tg.RollBack();
                    rolledBack = true;
                }
                else
                {
                    tg.Assimilate();
                    rolledBack = false;
                }

                return CommandResult.Ok(new { results, rolledBack });
            }
        }
    }
}
