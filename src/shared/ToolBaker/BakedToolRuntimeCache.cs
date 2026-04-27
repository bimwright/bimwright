using System;
using System.Collections.Generic;
using System.Linq;

namespace Bimwright.Rvt.Plugin.ToolBaker
{
    public sealed class BakedToolRuntimeEntry
    {
        public BakedToolRuntimeEntry(
            string name,
            string displayName,
            string description,
            string parametersSchema,
            string outputChoice,
            object command)
        {
            Name = name;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
            Description = description ?? string.Empty;
            ParametersSchema = string.IsNullOrWhiteSpace(parametersSchema) ? "{}" : parametersSchema;
            OutputChoice = string.IsNullOrWhiteSpace(outputChoice) ? "mcp_only" : outputChoice;
            Command = command;
        }

        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ParametersSchema { get; }
        public string OutputChoice { get; }
        public object Command { get; }
        public int RibbonSlot { get; internal set; }

        public bool WantsRibbon =>
            OutputChoice.IndexOf("ribbon", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public sealed class BakedToolRuntimeCache
    {
        public const int MaxRibbonSlots = 12;

        private readonly object _lock = new object();
        private readonly Dictionary<string, BakedToolRuntimeEntry> _entries =
            new Dictionary<string, BakedToolRuntimeEntry>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _slotToTool =
            new Dictionary<int, string>();

        public int Count
        {
            get { lock (_lock) return _entries.Count; }
        }

        public int RibbonSlotCount
        {
            get { lock (_lock) return _slotToTool.Count; }
        }

        public bool HasRibbonOverflow
        {
            get { lock (_lock) return _entries.Values.Count(e => e.WantsRibbon) > MaxRibbonSlots; }
        }

        public BakedToolRuntimeEntry RegisterOrUpdate(BakedToolRuntimeEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                return null;

            lock (_lock)
            {
                var slot = 0;
                if (_entries.TryGetValue(entry.Name, out var existing))
                    slot = existing.RibbonSlot;

                if (entry.WantsRibbon && slot == 0)
                    slot = AssignRibbonSlot(entry.Name);
                else if (!entry.WantsRibbon && slot != 0)
                    _slotToTool.Remove(slot);

                entry.RibbonSlot = slot;
                _entries[entry.Name] = entry;
                if (slot != 0)
                    _slotToTool[slot] = entry.Name;

                return entry;
            }
        }

        public IReadOnlyList<BakedToolRuntimeEntry> GetAll()
        {
            lock (_lock)
            {
                return _entries.Values
                    .OrderBy(e => e.Name, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        public IReadOnlyList<BakedToolRuntimeEntry> GetRibbonEntries()
        {
            lock (_lock)
            {
                return _entries.Values
                    .Where(e => e.RibbonSlot > 0)
                    .OrderBy(e => e.RibbonSlot)
                    .ToArray();
            }
        }

        public string GetToolNameForSlot(int slot)
        {
            lock (_lock)
            {
                return _slotToTool.TryGetValue(slot, out var name) ? name : null;
            }
        }

        public BakedToolRuntimeEntry GetByName(string name)
        {
            lock (_lock)
            {
                return name != null && _entries.TryGetValue(name, out var entry) ? entry : null;
            }
        }

        public BakedToolRuntimeEntry GetBySlot(int slot)
        {
            var name = GetToolNameForSlot(slot);
            return name == null ? null : GetByName(name);
        }

        private int AssignRibbonSlot(string name)
        {
            for (var slot = 1; slot <= MaxRibbonSlots; slot++)
            {
                if (!_slotToTool.ContainsKey(slot))
                {
                    _slotToTool[slot] = name;
                    return slot;
                }
            }

            return 0;
        }
    }
}
