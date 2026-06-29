using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OneClickClose.Core.Helpers
{
    /// <summary>
    /// Shared JSON file read/write utilities and set helpers used by AppConfig and UserPreferencesStore.
    /// </summary>
    public static class JsonFileStore
    {
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions { WriteIndented = true };

        public static T ReadJson<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = JsonSerializer.Serialize(value, DefaultOptions);
            File.WriteAllText(path, json, new UTF8Encoding(true));
        }

        public static HashSet<string> MakeSet(IEnumerable<string> names)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    set.Add(name.Trim());
                }
            }
            return set;
        }

        public static string[] AddToArray(string[] existing, string value)
        {
            HashSet<string> set = MakeSet(existing);
            if (!string.IsNullOrWhiteSpace(value))
            {
                set.Add(value.Trim());
            }
            return set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
