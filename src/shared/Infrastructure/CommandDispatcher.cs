using System.Collections.Generic;
using Bimwright.Rvt.Plugin.ToolBaker;

namespace Bimwright.Rvt.Plugin
{
    public class CommandDispatcher
    {
        private readonly Dictionary<string, IRevitCommand> _commands = new Dictionary<string, IRevitCommand>();
        private readonly Dictionary<string, IRevitCommand> _bakedCommands = new Dictionary<string, IRevitCommand>();
        private readonly BakedToolRuntimeCache _runtimeCache;

        public CommandDispatcher(BakedToolRuntimeCache runtimeCache = null)
        {
            _runtimeCache = runtimeCache;
            Register(new Handlers.ShowMessageHandler());
            Register(new Handlers.GetCurrentViewHandler());
            Register(new Handlers.GetSelectedElementsHandler());
            Register(new Handlers.GetFamilyTypesHandler());
            Register(new Handlers.AiElementFilterHandler());
            Register(new Handlers.AnalyzeModelStatisticsHandler());
            Register(new Handlers.GetMaterialQuantitiesHandler());
            // Phase 3: Create
            Register(new Handlers.CreateLineBasedElementHandler());
            Register(new Handlers.CreatePointBasedElementHandler());
            Register(new Handlers.CreateSurfaceBasedElementHandler());
            Register(new Handlers.CreateLevelHandler());
            Register(new Handlers.CreateGridHandler());
            Register(new Handlers.CreateRoomHandler());
            // Phase 4: Modify & Delete
            Register(new Handlers.OperateElementHandler());
            Register(new Handlers.ColorElementsHandler());
            Register(new Handlers.DeleteElementHandler());
            // Phase 5: Export & Tags
            Register(new Handlers.ExportRoomDataHandler());
            Register(new Handlers.TagAllWallsHandler());
            Register(new Handlers.TagAllRoomsHandler());
            // Phase 6: Dynamic Code
            Register(new Handlers.SendCodeToRevitHandler());
            // Phase 7: Views & Sheets
            Register(new Handlers.CreateViewHandler());
            Register(new Handlers.PlaceViewOnSheetHandler());
            // Phase 8+: MEP System Analysis
            Register(new Handlers.DetectSystemElementsHandler());
            // Phase 10: New tools (post-analysis)
            Register(new Handlers.AnalyzeSheetLayoutHandler());
            // MCP Prompts support
            Register(new Handlers.GetModelOverviewHandler());
            // Phase 11: Lint toolset (L-05 + L-13)
            Register(new Handlers.AnalyzeViewNamingPatternsHandler());
            Register(new Handlers.SuggestViewNameCorrectionsHandler());
            Register(new Handlers.DetectFirmProfileHandler());
            // A6 batch execution — needs dispatcher ref to look up sub-commands
            Register(new Handlers.BatchExecuteHandler(this));
            // ToolBaker runtime access.
            Register(new Handlers.ApplyBakeSuggestionHandler(runtimeCache));
            Register(new Handlers.ListBakedToolsHandler());
            Register(new Handlers.RunBakedToolHandler());
        }

        public void Register(IRevitCommand command)
        {
            _commands[command.Name] = command;
        }

        public bool RegisterBaked(IRevitCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Name))
                return false;

            if (_commands.ContainsKey(command.Name))
                return false;
            if (_bakedCommands.ContainsKey(command.Name))
                return false;

            _bakedCommands[command.Name] = command;
            return true;
        }

        public IRevitCommand GetCommand(string name)
        {
            _commands.TryGetValue(name, out var command);
            return command;
        }

        public IRevitCommand GetBakedCommand(string name)
        {
            _bakedCommands.TryGetValue(name, out var command);
            return command;
        }

        public bool IsBakedCommand(string name)
        {
            return !string.IsNullOrEmpty(name) && _bakedCommands.ContainsKey(name);
        }

        /// <summary>Load all baked tools from registry and register them.</summary>
        public void LoadBakedTools(BakedToolRegistry registry)
        {
            foreach (var meta in registry.GetAll())
            {
                if (string.Equals(meta.LifecycleState, "archived", System.StringComparison.Ordinal))
                    continue;

                var source = registry.GetSource(meta.Name);
                if (source == null) continue;
                try
                {
                    IRevitCommand command;
                    string error = null;
                    if (BakedToolRuntimeSource.HasMarker(source))
                    {
                        if (!BakedToolRuntimeSource.IsAllowedForSuggestionSource(meta.Source))
                        {
                            System.Diagnostics.Debug.WriteLine($"[Bimwright] Skipped baked tool '{meta.Name}': runtime source marker is not allowed for source '{meta.Source}'.");
                            continue;
                        }

                        if (!BakedToolRuntimeCommandFactory.TryCreate(
                            meta.Name,
                            meta.Description,
                            meta.ParametersSchema,
                            source,
                            this,
                            out command,
                            out error))
                        {
                            System.Diagnostics.Debug.WriteLine($"[Bimwright] Failed to load baked tool '{meta.Name}': {error}");
                            continue;
                        }
                    }
                    else
                    {
                        command = ToolCompiler.CompileAndLoad(source, out error);
                    }

                    if (command != null)
                    {
                        if (!string.Equals(command.Name, meta.Name, System.StringComparison.Ordinal))
                        {
                            System.Diagnostics.Debug.WriteLine($"[Bimwright] Skipped baked tool '{meta.Name}': compiled command name '{command.Name}' did not match registry metadata.");
                            continue;
                        }

                        if (!RegisterBaked(command))
                        {
                            System.Diagnostics.Debug.WriteLine($"[Bimwright] Skipped baked tool '{meta.Name}': command name collides with an existing command.");
                        }
                        else
                        {
                            _runtimeCache?.RegisterOrUpdate(new BakedToolRuntimeEntry(
                                meta.Name,
                                meta.DisplayName,
                                meta.Description,
                                meta.ParametersSchema,
                                meta.OutputChoice,
                                command));
                        }
                    }
                    else
                        System.Diagnostics.Debug.WriteLine($"[Bimwright] Failed to load baked tool '{meta.Name}': {error}");
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Bimwright] Error loading baked tool '{meta.Name}': {ex.Message}");
                }
            }
        }
    }
}
