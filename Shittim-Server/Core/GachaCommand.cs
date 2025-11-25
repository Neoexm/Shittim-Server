using System.Text.Json;
using Schale.FlatData;

namespace Shittim_Server.Core
{
    public static class GachaCommand
    {
        private static Dictionary<long, double>? _customRates;
        private static long? _guaranteedCharacterId;
        private static DateTime _lastConfigCheck = DateTime.MinValue;
        private static readonly TimeSpan ConfigCacheTime = TimeSpan.FromSeconds(5);
        
        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "gacha_config.json");

        private class GachaConfig
        {
            public Dictionary<string, double>? custom_rates { get; set; }
            public long? guaranteed_character { get; set; }
        }

        private static void LoadConfigIfNeeded()
        {
            if (DateTime.UtcNow - _lastConfigCheck < ConfigCacheTime)
                return;

            _lastConfigCheck = DateTime.UtcNow;

            try
            {
                var configPath = Path.GetFullPath(ConfigPath);
                Console.WriteLine($"[GachaCommand] Loading config from: {configPath}");
                
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"[GachaCommand] Config file not found at: {configPath}");
                    _customRates = null;
                    _guaranteedCharacterId = null;
                    return;
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<GachaConfig>(json);

                if (config == null)
                {
                    _customRates = null;
                    _guaranteedCharacterId = null;
                    return;
                }

                if (config.custom_rates != null && config.custom_rates.Count > 0)
                {
                    _customRates = new Dictionary<long, double>();
                    
                    if (config.custom_rates.TryGetValue("ssr", out double ssrRate))
                    {
                        _customRates[3] = ssrRate;
                        Console.WriteLine($"[GachaCommand] Loaded SSR rate: {ssrRate}%");
                    }
                    
                    if (config.custom_rates.TryGetValue("sr", out double srRate))
                    {
                        _customRates[2] = srRate;
                        Console.WriteLine($"[GachaCommand] Loaded SR rate: {srRate}%");
                    }
                    
                    if (config.custom_rates.TryGetValue("r", out double rRate))
                    {
                        _customRates[1] = rRate;
                        Console.WriteLine($"[GachaCommand] Loaded R rate: {rRate}%");
                    }
                }
                else
                {
                    Console.WriteLine("[GachaCommand] No custom rates found in config");
                    _customRates = null;
                }

                if (config.guaranteed_character.HasValue && config.guaranteed_character.Value > 0)
                {
                    _guaranteedCharacterId = config.guaranteed_character.Value;
                }
                else
                {
                    _guaranteedCharacterId = null;
                }
            }
            catch
            {
                _customRates = null;
                _guaranteedCharacterId = null;
            }
        }

        public static Dictionary<long, double> GetCustomRates()
        {
            LoadConfigIfNeeded();
            return _customRates ?? new Dictionary<long, double>();
        }

        public static long? GetGuaranteedCharacterId()
        {
            LoadConfigIfNeeded();
            return _guaranteedCharacterId;
        }

        public static void ClearCache()
        {
            _lastConfigCheck = DateTime.MinValue;
            _customRates = null;
            _guaranteedCharacterId = null;
        }
    }
}
