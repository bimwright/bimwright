// Usage:
//   stdio (default):  Bimwright.Rvt.Server.exe              — spawned by Claude/GPT/Cursor
//   HTTP SSE:          Bimwright.Rvt.Server.exe --http 8200  — for Ollama/LM Studio/custom
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Rvt.Plugin; // BimwrightConfig
using Bimwright.Rvt.Server.Bake;
using Bimwright.Rvt.Server.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Any(a => a == "--help" || a == "-h"))
            {
                PrintHelp();
                return;
            }

            // A9 3-layer config precedence (JSON < env < CLI). AuthToken.Target + transport
            // mode (--http) stay as separate CLI parses for now; A3 toolsets gating uses
            // BimwrightConfig.
            var config = BimwrightConfig.Load(args);
            if (!string.IsNullOrWhiteSpace(config.Target))
            {
                var target = config.Target.ToUpperInvariant();
                if (Array.IndexOf(AuthToken.AllVersions, target) < 0)
                {
                    Console.Error.WriteLine("[Bimwright] Invalid target. Expected: R22|R23|R24|R25|R26|R27");
                    Environment.Exit(1);
                    return;
                }
                AuthToken.Target = target;
            }

            var bakePaths = new BakePaths();
            TryInitializeBakeStorage(bakePaths, out _);

            // Initialize memory system (shared across tool classes + resources)
            var session = new Memory.SessionContext();
            ToolGateway.Session = session;
            ToolGateway.UsageLogger = new UsageEventLogger(bakePaths, config);
            RevitResources.Session = session;

            int httpIndex = Array.IndexOf(args, "--http");
            if (httpIndex >= 0)
            {
                if (httpIndex + 1 >= args.Length || !int.TryParse(args[httpIndex + 1], out var port)
                    || port < 1 || port > 65535)
                {
                    Console.Error.WriteLine("[Bimwright] Invalid --http argument. Expected: --http <port> (1-65535)");
                    Environment.Exit(1);
                    return;
                }
                await RunHttpSse(config, port);
            }
            else
            {
                await RunStdio(config);
            }
        }

        internal static LegacyBakedToolImportResult InitializeBakeStorage(BakePaths paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            using var db = new BakeDb(paths);
            db.Migrate();
            var importer = new LegacyBakedToolImporter(paths, db, new ToolBakerAuditLog(paths.AuditJsonl));
            return importer.ImportIfNeeded();
        }

        internal static bool TryInitializeBakeStorage(BakePaths paths, out LegacyBakedToolImportResult result)
        {
            try
            {
                result = InitializeBakeStorage(paths);
                return true;
            }
            catch (Exception ex)
            {
                result = null;
                var pathHint = paths?.Root ?? "(unknown path)";
                Console.Error.WriteLine(
                    $"[Bimwright] Warning: ToolBaker bake storage initialization failed for {pathHint}. " +
                    "The MCP server will continue; baked-tool migration/import can be retried on next startup. " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static async Task RunStdio(BimwrightConfig config)
        {
            var enabled = ToolsetFilter.Resolve(config);
            var builder = Host.CreateApplicationBuilder();
            var mcp = builder.Services
                .AddMcpServer()
                .WithStdioServerTransport();
            mcp = RegisterToolsets(mcp, enabled, config);
            mcp.WithResources<RevitResources>();
            var app = builder.Build();
            await app.RunAsync();
        }

        private static async Task RunHttpSse(BimwrightConfig config, int port)
        {
            var enabled = ToolsetFilter.Resolve(config);
            var builder = WebApplication.CreateBuilder();
            var mcp = builder.Services
                .AddMcpServer()
                .WithHttpTransport();
            mcp = RegisterToolsets(mcp, enabled, config);
            mcp.WithResources<RevitResources>();

            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

            var app = builder.Build();

            app.Use(async (context, next) =>
            {
                var host = context.Request.Host.Host;
                if (host != "127.0.0.1" && host != "localhost")
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Forbidden: non-localhost host");
                    return;
                }
                await next();
            });

            app.MapMcp();

            Console.Error.WriteLine($"[Bimwright] SSE server listening on http://127.0.0.1:{port}");
            Console.Error.WriteLine($"[Bimwright] Toolsets enabled: {string.Join(",", enabled.OrderBy(n => n))}");
            await app.RunAsync();
        }

        private static void PrintHelp()
        {
            var usage = string.Join("\n", new[]
            {
                "bimwright — Revit MCP server (bimwright.dev)",
                "",
                "Usage: bimwright [options]",
                "",
                "Transport:",
                "  --http <port>           Run HTTP SSE on 127.0.0.1:<port> (1-65535). Default = stdio.",
                "",
                "Routing:",
                "  --target R22|R23|R24|R25|R26|R27",
                "                          Pin to a specific Revit version (when multiple Revits run).",
                "                          Default: auto-detect via discovery files in %LOCALAPPDATA%\\Bimwright\\.",
                "",
                "Tool exposure (A3 Progressive Disclosure):",
                "  --toolsets <csv>        Comma list of toolsets to enable. Default: query,create,view,meta.",
                "                          Known toolsets: " + string.Join(", ", ToolsetFilter.KnownToolsets),
                "                          Use 'all' to expose every toolset.",
                "  --read-only             Shortcut that excludes create, modify, and delete toolsets.",
                "",
                "ToolBaker:",
                "  --enable-toolbaker      Allow ToolBaker when selected via --toolsets (default ON).",
                "  --disable-toolbaker     Disable ToolBaker even if selected via --toolsets.",
                "  --enable-adaptive-bake  Enable adaptive ToolBaker suggestions (default OFF).",
                "  --disable-adaptive-bake Disable adaptive ToolBaker suggestions.",
                "  --cache-send-code-bodies",
                "                          Cache send_code_to_revit code bodies locally (default OFF).",
                "  --no-cache-send-code-bodies",
                "                          Disable local send_code_to_revit code body caching.",
                "",
                "Transport security (S7):",
                "  --allow-lan-bind        (plugin-side only — set BIMWRIGHT_ALLOW_LAN_BIND env var in",
                "                          the Revit process environment; server-side flag is documented",
                "                          here for future cross-process propagation.)",
                "",
                "Env vars (override JSON, overridden by CLI):",
                "  BIMWRIGHT_TARGET, BIMWRIGHT_TOOLSETS, BIMWRIGHT_READ_ONLY,",
                "  BIMWRIGHT_ALLOW_LAN_BIND, BIMWRIGHT_ENABLE_TOOLBAKER,",
                "  BIMWRIGHT_ENABLE_ADAPTIVE_BAKE, BIMWRIGHT_CACHE_SEND_CODE_BODIES",
                "",
                "Config file (lowest precedence):",
                "  %LOCALAPPDATA%\\Bimwright\\bimwright.config.json",
                "",
                "Other:",
                "  -h, --help              Show this help and exit.",
            });
            Console.WriteLine(usage);
        }

        private static IMcpServerBuilder RegisterToolsets(IMcpServerBuilder mcp, HashSet<string> enabled, BimwrightConfig config)
        {
            if (enabled.Contains("query"))      mcp = mcp.WithTools<QueryTools>();
            if (enabled.Contains("create"))     mcp = mcp.WithTools<CreateTools>();
            if (enabled.Contains("modify"))     mcp = mcp.WithTools<ModifyTools>();
            if (enabled.Contains("delete"))     mcp = mcp.WithTools<DeleteTools>();
            if (enabled.Contains("view"))       mcp = mcp.WithTools<ViewTools>();
            if (enabled.Contains("export"))     mcp = mcp.WithTools<ExportTools>();
            if (enabled.Contains("annotation")) mcp = mcp.WithTools<AnnotationTools>();
            if (enabled.Contains("mep"))        mcp = mcp.WithTools<MepTools>();
            if (enabled.Contains("toolbaker"))  mcp = mcp.WithTools<ToolbakerTools>();
            if (enabled.Contains("toolbaker") && config?.EnableAdaptiveBakeOrDefault == true)
                mcp = mcp.WithTools<AdaptiveBakeTools>();
            if (enabled.Contains("meta"))       mcp = mcp.WithTools<MetaTools>();
            if (enabled.Contains("lint"))       mcp = mcp.WithTools<LintTools>();
            return mcp;
        }

        private static Type[] ResolveRegisteredToolTypes(HashSet<string> enabled, BimwrightConfig config)
        {
            var types = new List<Type>();
            if (enabled.Contains("query"))      types.Add(typeof(QueryTools));
            if (enabled.Contains("create"))     types.Add(typeof(CreateTools));
            if (enabled.Contains("modify"))     types.Add(typeof(ModifyTools));
            if (enabled.Contains("delete"))     types.Add(typeof(DeleteTools));
            if (enabled.Contains("view"))       types.Add(typeof(ViewTools));
            if (enabled.Contains("export"))     types.Add(typeof(ExportTools));
            if (enabled.Contains("annotation")) types.Add(typeof(AnnotationTools));
            if (enabled.Contains("mep"))        types.Add(typeof(MepTools));
            if (enabled.Contains("toolbaker"))  types.Add(typeof(ToolbakerTools));
            if (enabled.Contains("toolbaker") && config?.EnableAdaptiveBakeOrDefault == true)
                types.Add(typeof(AdaptiveBakeTools));
            if (enabled.Contains("meta"))       types.Add(typeof(MetaTools));
            if (enabled.Contains("lint"))       types.Add(typeof(LintTools));
            return types.ToArray();
        }
    }

    /// <summary>
    /// Shared plugin-connection plumbing used by every toolset class. Owns the socket/
    /// pipe lifecycle, response read loop, pending-request correlation, and session
    /// call recording. Toolset classes contain only the MCP tool-method shells.
    /// </summary>
    internal static class ToolGateway
    {
        public static Memory.SessionContext Session { get; set; }
        public static UsageEventLogger UsageLogger { get; set; }
        public static string CurrentRevitVersion { get; private set; }

        private static TcpClient _client;
        private static NamedPipeClientStream _pipeStream;
        private static StreamReader _reader;
        private static StreamWriter _writer;
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
        private static readonly object _connectLock = new object();
        private static volatile bool _connected;
        private static string _token;

        private static void EnsureConnected()
        {
            if (_connected && (_client?.Connected == true || _pipeStream?.IsConnected == true))
                return;

            lock (_connectLock)
            {
                if (_connected && (_client?.Connected == true || _pipeStream?.IsConnected == true))
                    return;

                _connected = false;
                try { _client?.Close(); } catch { }
                try { _pipeStream?.Close(); } catch { }
                _client = null;
                _pipeStream = null;

                Stream stream = null;

                var target = AuthToken.Target; // null = auto, "R22"-"R27" = specific version

                // Try Named Pipe first (R25-R27).
                // If the discovery file exists but the connect itself fails (plugin unloaded
                // while Revit stayed alive, or some transient state), fall through to TCP
                // rather than giving up the whole connection attempt.
                if (AuthToken.TryReadPipe(out var pipeName, out var pipeToken, out var pipeVer))
                {
                    try
                    {
                        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                            PipeOptions.Asynchronous);
                        pipe.Connect(5000);
                        _token = pipeToken;
                        CurrentRevitVersion = pipeVer;
                        _pipeStream = pipe;
                        stream = pipe;
                        Console.Error.WriteLine($"[Bimwright] Connected to Revit {pipeVer} via Named Pipe: {pipeName}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Bimwright] Pipe connect failed ({pipeVer}: {ex.Message}) — falling back to TCP");
                        try { _pipeStream?.Close(); } catch { }
                        _pipeStream = null;
                    }
                }

                // Fall back to TCP (R22-R24) if pipe did not connect.
                if (stream == null && AuthToken.TryReadTcp(out var port, out var tcpToken, out var tcpVer))
                {
                    _token = tcpToken;
                    CurrentRevitVersion = tcpVer;
                    _client = new TcpClient();
                    _client.Connect("127.0.0.1", port);
                    stream = _client.GetStream();
                    Console.Error.WriteLine($"[Bimwright] Connected to Revit {tcpVer} via TCP on port {port}");
                }

                if (stream == null)
                {
                    var which = target != null ? $"(target={target})" : "(auto-detect R22-R27)";
                    throw new InvalidOperationException(
                        $"Revit MCP plugin not running {which}. Check discovery files in %LOCALAPPDATA%\\Bimwright\\");
                }

                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                _connected = true;

                var readThread = new Thread(ReadLoop) { IsBackground = true, Name = "Bimwright.ResponseReader" };
                readThread.Start();
            }
        }

        private static void ReadLoop()
        {
            try
            {
                while (_connected)
                {
                    var line = _reader?.ReadLine();
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var obj = JObject.Parse(line);
                        var id = obj.Value<string>("id");
                        if (id != null && _pending.TryRemove(id, out var tcs))
                        {
                            tcs.TrySetResult(line);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                _connected = false;
            }
        }

        /// <summary>
        /// Close the current Server↔Plugin connection and set a new target version.
        /// Next <see cref="SendToRevit"/> call will reconnect against the new target.
        /// Pass <c>null</c> to clear the pin and re-enable auto-detect.
        /// Cancels any in-flight requests — they'd be routed to the now-dead connection.
        /// </summary>
        public static void Reconnect(string newTarget)
        {
            lock (_connectLock)
            {
                _connected = false;
                try { _client?.Close(); } catch { }
                try { _pipeStream?.Close(); } catch { }
                _client = null;
                _pipeStream = null;
                _reader = null;
                _writer = null;
                _token = null;
                CurrentRevitVersion = null;
                foreach (var kv in _pending)
                {
                    kv.Value.TrySetException(new OperationCanceledException(
                        "switch_target initiated — in-flight request cancelled."));
                }
                _pending.Clear();
                AuthToken.Target = newTarget;
            }
        }

        public static async Task<JObject> SendToRevit(string command, object parameters = null)
        {
            EnsureConnected();

            var id = $"req-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            var request = JsonConvert.SerializeObject(new { id, command, @params = parameters ?? new { }, token = _token });

            var tcs = new TaskCompletionSource<string>();
            _pending[id] = tcs;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _writer.WriteLine(request);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pending.TryRemove(id, out _);
                sw.Stop();
                var paramsStr = parameters != null ? JsonConvert.SerializeObject(parameters) : null;
                Session?.RecordCall(command, paramsStr, false, sw.ElapsedMilliseconds, "Timeout (60s)");
                UsageLogger?.RecordToolCall(command, paramsStr, false);
                throw new TimeoutException("Request timed out (60s). Revit may be in a modal dialog.");
            }

            sw.Stop();
            var responseLine = await tcs.Task;
            var response = JObject.Parse(responseLine);
            var paramsJson = parameters != null ? JsonConvert.SerializeObject(parameters) : null;

            if (response.Value<bool>("success"))
            {
                var data = response["data"] as JObject ?? new JObject();
                Session?.RecordCall(command, paramsJson, true, sw.ElapsedMilliseconds,
                    resultJson: data.ToString(Formatting.None));
                UsageLogger?.RecordToolCall(command, paramsJson, true);
                return data;
            }
            else
            {
                var error = response.Value<string>("error") ?? "Unknown error from Revit";
                Session?.RecordCall(command, paramsJson, false, sw.ElapsedMilliseconds, error);
                UsageLogger?.RecordToolCall(command, paramsJson, false);
                throw new InvalidOperationException(error);
            }
        }
    }

    // =====================================================================
    // Toolset classes — one per aspect #3 §A3 group. Registration happens in
    // Program.RegisterToolsets() driven by config.Toolsets. Each method wraps
    // ToolGateway.SendToRevit with a catch-all that surfaces the error to the
    // MCP client as plain text instead of throwing.
    // =====================================================================

    [McpServerToolType, Toolset("query")]
    public class QueryTools
    {
        [McpServerTool(Name = "get_current_view_info", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Get active view info. Returns viewName, viewType (FloorPlan/Section/3D/Sheet), level, scale, detailLevel, displayStyle. Call before creating elements to know active level.")]
        public static async Task<string> GetCurrentViewInfo()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("get_current_view_info");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "get_selected_elements", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Get currently selected Revit elements. Returns array of {id, name, category, typeName}. Call before operating on user selection (color, delete, move).")]
        public static async Task<string> GetSelectedElements()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("get_selected_elements");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "get_available_family_types", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("List loadable family types. Returns {familyName, typeName, typeId} grouped by category. Optional: filter by category (e.g. 'Walls', 'Doors', 'Pipes' — NOT 'OST_Walls'). Feed typeId into create_point_based_element.")]
        public static async Task<string> GetAvailableFamilyTypes(string category = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("get_available_family_types", new { category });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "ai_element_filter", Destructive = false, Idempotent = true), System.ComponentModel.Description("Filter elements by category + parameter. Numeric values in mm (auto-converted). category uses human name ('Pipes', NOT 'OST_Pipes'). Operators: equals/contains/startswith/greaterthan/lessthan. select=true highlights results. Example: category='Pipes', parameterName='Diameter', parameterValue='200', operator='greaterthan', select=true.")]
        public static async Task<string> AiElementFilter(string category, string parameterName = "", string parameterValue = "", string @operator = "equals", int limit = 100, bool select = false)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("ai_element_filter", new { category, parameterName, parameterValue, @operator, limit, select });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "analyze_model_statistics", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Count elements grouped by category (Walls, Doors, Pipes, etc.). Call to understand project scope before detailed queries.")]
        public static async Task<string> AnalyzeModelStatistics()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("analyze_model_statistics");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "get_material_quantities", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Sum material quantities (area m², volume m³) by category. Required: category — human name ('Walls', 'Floors' — NOT 'OST_Walls').")]
        public static async Task<string> GetMaterialQuantities(string category)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("get_material_quantities", new { category });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("create")]
    public class CreateTools
    {
        [McpServerTool(Name = "create_line_based_element", Destructive = false), System.ComponentModel.Description("Create a line-based element (wall). Params: elementType, startX/Y, endX/Y (mm), level (name), typeId (optional), height (mm, default 3000).")]
        public static async Task<string> CreateLineBasedElement(string elementType, double startX, double startY, double endX, double endY, string level = "", long? typeId = null, double height = 3000)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("create_line_based_element", new { elementType, startX, startY, endX, endY, level, typeId, height });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "create_point_based_element", Destructive = false), System.ComponentModel.Description("Create a point-based element (door, window, furniture). Params: typeId (from get_available_family_types), x/y/z (mm), level (name).")]
        public static async Task<string> CreatePointBasedElement(long typeId, double x, double y, double z = 0, string level = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("create_point_based_element", new { typeId, x, y, z, level });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "create_surface_based_element", Destructive = false), System.ComponentModel.Description("Create a surface-based element (floor, ceiling). Params: elementType, points (JSON array of {x,y} in mm, min 3), level (name), typeId (optional). Example points: [{\"x\":0,\"y\":0},{\"x\":6000,\"y\":0},{\"x\":6000,\"y\":4000},{\"x\":0,\"y\":4000}].")]
        public static async Task<string> CreateSurfaceBasedElement(string elementType, string points, string level = "", long? typeId = null)
        {
            try
            {
                var parsedPoints = JArray.Parse(points);
                var result = await ToolGateway.SendToRevit("create_surface_based_element", new { elementType, points = parsedPoints, level, typeId });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "create_level", Destructive = false), System.ComponentModel.Description("Create a level at specified elevation. Params: elevation (mm), name (optional).")]
        public static async Task<string> CreateLevel(double elevation, string name = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("create_level", new { elevation, name });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "create_grid", Destructive = false), System.ComponentModel.Description("Create a grid line. Params: startX/Y, endX/Y (mm), name (optional).")]
        public static async Task<string> CreateGrid(double startX, double startY, double endX, double endY, string name = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("create_grid", new { startX, startY, endX, endY, name });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "create_room", Destructive = false), System.ComponentModel.Description("Create and place a room. Params: x/y (mm), level (name), name (optional), number (optional).")]
        public static async Task<string> CreateRoom(double x, double y, string level = "", string name = "", string number = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("create_room", new { x, y, level, name, number });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("modify")]
    public class ModifyTools
    {
        [McpServerTool(Name = "operate_element", Destructive = false), System.ComponentModel.Description("Select/hide/isolate/color elements in current view. operation: select (highlight), hide, unhide, isolate (hide everything else), setcolor (RGB override). elementIds: JSON int array e.g. '[12345, 67890]'. For setcolor: r/g/b 0-255 (default red 255,0,0).")]
        public static async Task<string> OperateElement(string operation, string elementIds, byte r = 255, byte g = 0, byte b = 0)
        {
            try
            {
                var parsedIds = JArray.Parse(elementIds).ToObject<long[]>();
                var result = await ToolGateway.SendToRevit("operate_element", new { operation, elementIds = parsedIds, r, g, b });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "color_elements", Destructive = false, Idempotent = true), System.ComponentModel.Description("Color-code elements by parameter value in current view. Auto-assigns distinct colors per unique value. category uses human name ('Walls', NOT 'OST_Walls'). Example: category='Pipes', parameterName='System Type' → each system type gets a different color.")]
        public static async Task<string> ColorElements(string category, string parameterName)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("color_elements", new { category, parameterName });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("delete")]
    public class DeleteTools
    {
        [McpServerTool(Name = "delete_element", Idempotent = true), System.ComponentModel.Description("Delete elements by ID. DESTRUCTIVE — cannot be undone via MCP. elementIds: JSON int array e.g. '[12345, 67890]'. Fetch IDs from get_selected_elements or ai_element_filter first.")]
        public static async Task<string> DeleteElement(string elementIds)
        {
            try
            {
                var parsedIds = JArray.Parse(elementIds).ToObject<long[]>();
                var result = await ToolGateway.SendToRevit("delete_element", new { elementIds = parsedIds });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("view")]
    public class ViewTools
    {
        [McpServerTool(Name = "create_view", Destructive = false), System.ComponentModel.Description("Create a view (floorplan or 3d). Params: viewType ('floorplan' or '3d'), level (name, required for floorplan), name (optional).")]
        public static async Task<string> CreateView(string viewType, string level = "", string name = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("create_view", new { viewType, level, name });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "place_view_on_sheet", Destructive = false), System.ComponentModel.Description("Place a view on a sheet. Auto-creates sheet if sheetId omitted. Params: viewId (required), sheetId (optional), sheetNumber (optional), sheetName (optional).")]
        public static async Task<string> PlaceViewOnSheet(long viewId, long? sheetId = null, string sheetNumber = "", string sheetName = "MCP Generated Sheet")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("place_view_on_sheet", new { viewId, sheetId, sheetNumber, sheetName });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "analyze_sheet_layout", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Analyze a sheet's title block + viewport layout in mm. Provide sheetNumber (e.g. 'ISO-005') or sheetId; if neither, uses active view when it is a sheet. Returns title block size, viewport centers, widths, heights, scales.")]
        public static async Task<string> AnalyzeSheetLayout(string sheetNumber = "", long? sheetId = null)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("analyze_sheet_layout", new { sheetNumber, sheetId });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("export")]
    public class ExportTools
    {
        [McpServerTool(Name = "export_room_data", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Export all rooms. Returns array of {name, number, area (m²), perimeter, level, department, volume (m³)}. For space analysis and reporting.")]
        public static async Task<string> ExportRoomData()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("export_room_data");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("annotation")]
    public class AnnotationTools
    {
        [McpServerTool(Name = "tag_all_walls", Destructive = false, Idempotent = true), System.ComponentModel.Description("Tag all walls in current view at midpoint. Skips already-tagged walls. Returns count of new tags.")]
        public static async Task<string> TagAllWalls()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("tag_all_walls");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "tag_all_rooms", Destructive = false, Idempotent = true), System.ComponentModel.Description("Tag all rooms in current view at location point. Skips already-tagged rooms. Returns count of new tags.")]
        public static async Task<string> TagAllRooms()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("tag_all_rooms");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("mep")]
    public class MepTools
    {
        [McpServerTool(Name = "detect_system_elements", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Walk an MEP system from a seed element. Traverses connectors to find all pipes, fittings, accessories, equipment in the same system. Returns IDs grouped by category + bounding box in mm. Fetch seed via get_selected_elements.")]
        public static async Task<string> DetectSystemElements(long elementId)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("detect_system_elements", new { elementId });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("toolbaker")]
    public class ToolbakerTools
    {
        [McpServerTool(Name = "send_code_to_revit"), System.ComponentModel.Description("Compile + run C# inside Revit. LAST RESORT — prefer typed tools (ai_element_filter, detect_system_elements, etc.) first. Can crash Revit or corrupt data. Variables: doc (Document), uidoc (UIDocument), app (UIApplication). Write body only, auto-wrapped in static Run(UIApplication). Must end with 'return ...;'. Namespaces: System, System.Linq, System.Collections.Generic, Autodesk.Revit.DB, Autodesk.Revit.UI. Common patterns: FilteredElementCollector for queries, Transaction for mutations, UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters), uidoc.Selection.SetElementIds(), OverrideGraphicSettings.")]
        public static async Task<string> SendCodeToRevit(string code)
        {
            try
            {
                var result = await ToolGateway.SendToRevit("send_code_to_revit", new { code });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "list_baked_tools", ReadOnly = true, Idempotent = true), System.ComponentModel.Description(
            "List all baked tools with name, description, usage count, creation date. " +
            "Call before run_baked_tool to discover available tools.")]
        public static async Task<string> ListBakedTools()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("list_baked_tools");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "run_baked_tool"), System.ComponentModel.Description(
            "Run a baked tool by name. Call list_baked_tools first to discover. " +
            "Params: name (baked tool name), params (object, tool-specific).")]
        public static async Task<string> RunBakedTool(string name, object @params = null)
        {
            var revitVersionBeforeConnect = ToolGateway.CurrentRevitVersion ?? AuthToken.Target ?? "unknown";
            try
            {
                var normalizedParams = NormalizeRunBakedToolParams(@params);
                var result = await ToolGateway.SendToRevit("run_baked_tool", new { name, @params = normalizedParams });
                var revitVersion = ToolGateway.CurrentRevitVersion ?? revitVersionBeforeConnect;
                RecordBakedToolRun(name, revitVersion, success: true, error: null);
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                var revitVersion = ToolGateway.CurrentRevitVersion ?? revitVersionBeforeConnect;
                RecordBakedToolRun(name, revitVersion, success: false, error: ex.Message);
                return $"Error: {ex.Message}";
            }
        }

        public static JObject NormalizeRunBakedToolParams(object @params)
        {
            if (@params == null)
                return new JObject();

            if (@params is JObject obj)
                return obj;

            if (@params is JToken token)
            {
                if (token is JObject tokenObj)
                    return tokenObj;
                throw new ArgumentException("params must be a JSON object.");
            }

            if (!(@params is JsonElement element))
            {
                var converted = JToken.FromObject(@params);
                if (converted is JObject convertedObj)
                    return convertedObj;
                throw new ArgumentException("params must be a JSON object.");
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                    return new JObject();
                case JsonValueKind.Object:
                    return JObject.Parse(element.GetRawText());
                default:
                    throw new ArgumentException("params must be a JSON object.");
            }
        }

        private static void RecordBakedToolRun(string name, string revitVersion, bool success, string error)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            try
            {
                using var db = new BakeDb(new BakePaths());
                db.Migrate();
                db.TryRecordRegistryRun(name, revitVersion, success, error, DateTimeOffset.UtcNow);
            }
            catch
            {
                // Server owns durable bake.db writes, but run_baked_tool should not fail
                // just because lifecycle stats could not be updated.
            }
        }
    }

    [McpServerToolType, Toolset("toolbaker")]
    public class AdaptiveBakeTools
    {
        [McpServerTool(Name = "list_bake_suggestions", ReadOnly = true, Idempotent = true), System.ComponentModel.Description(
            "List adaptive ToolBaker suggestions. Returns suggestions with id, title, source, score, state, output choices, and creation time.")]
        public static string ListBakeSuggestions()
        {
            try
            {
                var paths = new BakePaths();
                using var db = new BakeDb(paths);
                db.Migrate();
                return ListBakeSuggestionsHandler.Handle(db, ToolGateway.UsageLogger);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "accept_bake_suggestion"), System.ComponentModel.Description(
            "Accept an adaptive ToolBaker suggestion by id. Validates name, schema, and output choice, then prepares a bake request without native tool promotion.")]
        public static async Task<string> AcceptBakeSuggestion(string id, string name, string output_choice = "mcp_only", string params_schema = null)
        {
            try
            {
                var paths = new BakePaths();
                using var db = new BakeDb(paths);
                db.Migrate();
                return await AcceptBakeSuggestionHandler.HandleAsync(
                    db,
                    id,
                    name,
                    output_choice,
                    params_schema,
                    pluginApply: request => ToolGateway.SendToRevit("apply_bake", request));
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "dismiss_bake_suggestion"), System.ComponentModel.Description(
            "Dismiss an adaptive ToolBaker suggestion. action must be snooze_30d, never, or never_with_gap_signal.")]
        public static string DismissBakeSuggestion(string id, string action)
        {
            try
            {
                var paths = new BakePaths();
                using var db = new BakeDb(paths);
                db.Migrate();
                var revitVersion = ToolGateway.CurrentRevitVersion ?? AuthToken.Target ?? "unknown";
                return DismissBakeSuggestionHandler.Handle(
                    db,
                    id,
                    action,
                    currentRevitVersion: revitVersion,
                    auditLog: new ToolBakerAuditLog(paths.AuditJsonl));
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }

    [McpServerToolType, Toolset("meta")]
    public class MetaTools
    {
        [McpServerTool(Name = "show_message", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Show a Revit TaskDialog. For connection tests or user notifications. Both 'message' and 'title' are optional — omit for default greeting.")]
        public static async Task<string> ShowMessage(string message = null, string title = null)
        {
            try
            {
                object parameters = null;
                if (!string.IsNullOrWhiteSpace(message) || !string.IsNullOrWhiteSpace(title))
                {
                    parameters = new { message, title };
                }
                var result = await ToolGateway.SendToRevit("show_message", parameters);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "switch_target"), System.ComponentModel.Description(
            "Switch active Revit connection to a specific version when multiple Revits are running. " +
            "version: 'R22'|'R23'|'R24'|'R25'|'R26'|'R27' to pin, or 'auto' to clear pin and re-enable auto-detect. " +
            "Immediately closes the current Server↔Plugin connection (cancels in-flight requests) and updates the target. " +
            "The next tool call transparently reconnects against the new target — no user confirmation required. " +
            "Returns: {ok, previousTarget, newTarget, verified (if verify=true)}. " +
            "verify=true (default): immediately attempts get_current_view_info against the new target to confirm connectivity; " +
            "set verify=false to skip when the new target's document isn't in a view yet (e.g., Revit just launched).")]
        public static async Task<string> SwitchTarget(string version, bool verify = true)
        {
            try
            {
                var previousTarget = AuthToken.Target;
                string newTarget = null;
                if (!string.IsNullOrWhiteSpace(version) && !version.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    var upper = version.Trim().ToUpperInvariant();
                    if (Array.IndexOf(AuthToken.AllVersions, upper) < 0)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            ok = false,
                            error = $"Invalid version '{version}'. Allowed: {string.Join(",", AuthToken.AllVersions)} or 'auto'."
                        });
                    }
                    newTarget = upper;
                }

                ToolGateway.Reconnect(newTarget);

                if (!verify)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        ok = true,
                        previousTarget = previousTarget ?? "auto",
                        newTarget = newTarget ?? "auto",
                        verified = false,
                        note = "Target updated. Next tool call will connect to new target."
                    });
                }

                try
                {
                    var probe = await ToolGateway.SendToRevit("get_current_view_info");
                    return JsonConvert.SerializeObject(new
                    {
                        ok = true,
                        previousTarget = previousTarget ?? "auto",
                        newTarget = newTarget ?? "auto",
                        verified = true,
                        activeView = probe.Value<string>("viewName")
                    });
                }
                catch (Exception verifyEx)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        ok = true,
                        previousTarget = previousTarget ?? "auto",
                        newTarget = newTarget ?? "auto",
                        verified = false,
                        verifyError = verifyEx.Message,
                        note = "Target set, but verify failed. The next tool call may still succeed."
                    });
                }
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "batch_execute"), System.ComponentModel.Description(
            "Run multiple MCP commands atomically inside one Revit TransactionGroup (single undo on success). " +
            "Input: commands — JSON array of {command, params}, e.g. " +
            "'[{\"command\":\"create_level\",\"params\":{\"elevation\":3000}}, " +
            "{\"command\":\"create_grid\",\"params\":{\"startX\":0,\"startY\":0,\"endX\":5000,\"endY\":0}}]'. " +
            "On any failure the whole group rolls back unless continueOnError=true. " +
            "Returns: {results: [{index, ok, data|error}], rolledBack}.")]
        public static async Task<string> BatchExecute(string commands, bool continueOnError = false)
        {
            try
            {
                var parsed = JArray.Parse(commands);
                var result = await ToolGateway.SendToRevit("batch_execute", new { commands = parsed, continueOnError });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "analyze_usage_patterns", ReadOnly = true, Idempotent = true), System.ComponentModel.Description(
            "Analyze MCP tool usage. Returns session stats (call counts, success rates, top tools, flags) " +
            "plus historical data from journal files. " +
            "Params: days (int, default 1) — days of history to include. " +
            "Use to spot most-used tools, frequent failures, repeated patterns.")]
        public static string AnalyzeUsagePatterns(int days = 1)
        {
            try
            {
                var session = ToolGateway.Session;
                if (session == null) return JsonConvert.SerializeObject(new { error = "No active session" });

                var report = session.GetPatternReport();

                var journal = session.Journal;
                var historicalTools = new Dictionary<string, int>();
                var historicalErrors = new Dictionary<string, int>();
                int historicalTotal = 0;

                var dates = journal.ListDates();
                var cutoff = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");

                foreach (var date in dates)
                {
                    if (string.Compare(date, cutoff, StringComparison.Ordinal) < 0) continue;
                    var entries = journal.ReadDay(date);
                    foreach (var entry in entries)
                    {
                        historicalTotal++;
                        if (!historicalTools.ContainsKey(entry.Tool)) historicalTools[entry.Tool] = 0;
                        historicalTools[entry.Tool]++;
                        if (!entry.Success)
                        {
                            if (!historicalErrors.ContainsKey(entry.Tool)) historicalErrors[entry.Tool] = 0;
                            historicalErrors[entry.Tool]++;
                        }
                    }
                }

                var result = new
                {
                    session = new
                    {
                        total_calls = report.TotalCalls,
                        total_errors = report.TotalErrors,
                        top_tools = report.TopTools.Select(t => new { t.Tool, t.CallCount, t.ErrorCount, error_rate = t.ErrorRate.ToString("P0") }),
                        error_prone = report.ErrorProne.Select(t => new { t.Tool, t.CallCount, t.ErrorCount, error_rate = t.ErrorRate.ToString("P0") }),
                        flags = report.Flags
                    },
                    history = new
                    {
                        days_included = days,
                        total_calls = historicalTotal,
                        top_tools = historicalTools.OrderByDescending(kv => kv.Value).Take(10)
                            .Select(kv => new { tool = kv.Key, count = kv.Value }),
                        error_tools = historicalErrors.OrderByDescending(kv => kv.Value).Take(5)
                            .Select(kv => new { tool = kv.Key, errors = kv.Value })
                    }
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    [McpServerToolType, Toolset("lint")]
    public class LintTools
    {
        [McpServerTool(Name = "analyze_view_naming_patterns", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Infer dominant view-naming pattern from project. Returns patterns with coverage + outliers. Zero args. Use before suggest_view_name_corrections.")]
        public static async Task<string> AnalyzeViewNamingPatterns()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("analyze_view_naming_patterns");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "suggest_view_name_corrections", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Propose corrected view names for outliers. Optional profile=<id> uses firm-profile library rule; omit to use project-inferred dominant pattern. Returns suggestions array with id/current/suggested/reason.")]
        public static async Task<string> SuggestViewNameCorrections(string profile = "")
        {
            try
            {
                var result = await ToolGateway.SendToRevit("suggest_view_name_corrections", new { profile });
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        [McpServerTool(Name = "detect_firm_profile", ReadOnly = true, Idempotent = true), System.ComponentModel.Description("Fingerprint project naming (views + sheets + levels), match against firm-profile library. Returns project_pattern (always) + library_match (null if library empty or no match).")]
        public static async Task<string> DetectFirmProfile()
        {
            try
            {
                var result = await ToolGateway.SendToRevit("detect_firm_profile");
                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }
    }
}
