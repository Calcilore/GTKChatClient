using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChatAppClient; 

public static class Prefs {
    private static Dictionary<string, string> prefs;

    public static string GetString(string key) {
        if (prefs == null) {
            // Load prefs from disk
            Load();
        }

        return !prefs.ContainsKey(key) ? null : prefs[key];
    }

    public static string GetString(string key, string defaultValue) => GetString(key) ?? defaultValue;
    
    public static void SetString(string key, string value) {
        if (prefs == null) {
            // Load prefs from disk
            Load();
        }

        prefs[key] = value;
    }
    
    // Load prefs function
    private static void Load() {
        if (!File.Exists("prefs.json")) {
            prefs = new Dictionary<string, string>();
            return;
        }
        
        // Load prefs from disk
        prefs = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("prefs.json"));
    }
    
    // Save prefs function
    public static void Save() {
        if (prefs == null) {
            // Nothing to save
            return;
        }

        // Save prefs to disk
        File.WriteAllText("prefs.json", JsonSerializer.Serialize(prefs));
    }
}