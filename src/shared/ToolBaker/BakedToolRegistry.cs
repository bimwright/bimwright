using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Bimwright.Rvt.Plugin.ToolBaker
{
    public class BakedToolMeta
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ParametersSchema { get; set; }
        public string CreatedUtc { get; set; }
        public int CallCount { get; set; }
    }

    public class BakedToolRegistry
    {
        private readonly string _dir;
        private readonly string _registryPath;
        private readonly Dictionary<string, BakedToolMeta> _tools = new Dictionary<string, BakedToolMeta>();
        private readonly object _lock = new object();

        public BakedToolRegistry()
        {
            _dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Bimwright", "baked");
            Directory.CreateDirectory(_dir);
            _registryPath = Path.Combine(_dir, "registry.json");
            Load();
        }

        public string BakedDir => _dir;

        public void Save(BakedToolMeta meta, string sourceCode)
        {
            lock (_lock)
            {
                _tools[meta.Name] = meta;
                File.WriteAllText(Path.Combine(_dir, meta.Name + ".cs"), sourceCode);
                WriteRegistryAtomic();
            }
        }

        public string GetSource(string name)
        {
            var path = Path.Combine(_dir, name + ".cs");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public BakedToolMeta GetMeta(string name)
        {
            lock (_lock)
            {
                _tools.TryGetValue(name, out var meta);
                return meta;
            }
        }

        public IEnumerable<BakedToolMeta> GetAll()
        {
            lock (_lock)
            {
                return new List<BakedToolMeta>(_tools.Values);
            }
        }

        public void IncrementCallCount(string name)
        {
            lock (_lock)
            {
                if (_tools.TryGetValue(name, out var meta))
                {
                    meta.CallCount++;
                    WriteRegistryAtomic();
                }
            }
        }

        public bool Remove(string name)
        {
            lock (_lock)
            {
                if (!_tools.Remove(name)) return false;
                var csPath = Path.Combine(_dir, name + ".cs");
                if (File.Exists(csPath)) File.Delete(csPath);
                WriteRegistryAtomic();
                return true;
            }
        }

        // Caller must hold _lock.
        private void WriteRegistryAtomic()
        {
            var json = JsonConvert.SerializeObject(_tools.Values, Formatting.Indented);
            var tmp = _registryPath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_registryPath))
                File.Replace(tmp, _registryPath, null);
            else
                File.Move(tmp, _registryPath);
        }

        private void Load()
        {
            if (!File.Exists(_registryPath)) return;
            try
            {
                var json = File.ReadAllText(_registryPath);
                var list = JsonConvert.DeserializeObject<List<BakedToolMeta>>(json);
                if (list == null) return;
                foreach (var meta in list)
                    _tools[meta.Name] = meta;
            }
            catch (Exception ex)
            {
                // Quarantine corrupt file + record error so silent-disappear is diagnosable.
                try
                {
                    var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    var quarantine = _registryPath + ".corrupt-" + stamp;
                    File.Copy(_registryPath, quarantine, overwrite: true);
                    var errLog = Path.Combine(_dir, "registry-load.error");
                    File.AppendAllText(errLog,
                        $"{DateTime.UtcNow:o} {ex.GetType().Name}: {ex.Message}\nQuarantined: {quarantine}\n");
                }
                catch { }
            }
        }
    }
}
