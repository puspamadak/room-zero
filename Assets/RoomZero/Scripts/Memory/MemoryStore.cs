// Local-first persistence (spec: "Persist local-first for demo reliability").
// File: Application.persistentDataPath/roomzero_profile.json
//   Quest: /sdcard/Android/data/<package>/files/roomzero_profile.json
//   (inspect with: adb shell cat /sdcard/Android/data/<package>/files/roomzero_profile.json)
// Save never throws into gameplay: failures are logged and the session stays in memory.
// Owner: #7 Persistence + Player Model.

using System;
using System.IO;
using UnityEngine;

namespace RoomZero.Memory
{
    public class MemoryStore
    {
        public const string FileName = "roomzero_profile.json";
        readonly string path;

        public PlayerProfile Profile { get; private set; }
        public bool LastSaveFailed { get; private set; }
        public string FilePath => path;

        public MemoryStore(string directory = null)
        {
            path = Path.Combine(directory ?? Application.persistentDataPath, FileName);
        }

        public PlayerProfile Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var p = JsonUtility.FromJson<PlayerProfile>(json);
                    Profile = Migrate(p);
                    ZeroLog.Info("Memory", $"loaded profile: {Profile.stats.missions} missions, last choice={(Profile.HasLastSession ? Profile.lastSession.choice : "none")}");
                    return Profile;
                }
            }
            catch (Exception e)
            {
                // Corrupt file: keep a copy for debugging, start fresh.
                ZeroLog.Warn("Memory", $"load failed: {e.Message}", -1, "fresh_profile");
                TryBackupCorrupt();
            }
            Profile = new PlayerProfile();
            ZeroLog.Info("Memory", "new profile");
            return Profile;
        }

        public bool Save(PlayerProfile profile)
        {
            Profile = profile;
            try
            {
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(profile, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);         // atomic-ish: never leave a half-written profile
                LastSaveFailed = false;
                ZeroLog.Info("Memory", $"saved -> {path}");
                return true;
            }
            catch (Exception e)
            {
                LastSaveFailed = true;
                ZeroLog.Warn("Memory", $"save failed: {e.Message}", -1, "kept_in_memory");
                return false;
            }
        }

        public void Reset()
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception e) { ZeroLog.Warn("Memory", e.Message); }
            Profile = new PlayerProfile();
            ZeroLog.Info("Memory", "profile reset");
        }

        /// Version migrations live here. Unknown future versions are not trusted.
        static PlayerProfile Migrate(PlayerProfile p)
        {
            if (p == null) return new PlayerProfile();
            if (p.schemaVersion > Schema.Version)
            {
                ZeroLog.Warn("Memory", $"profile schema {p.schemaVersion} is newer than app {Schema.Version}", -1, "fresh_profile");
                return new PlayerProfile();
            }
            // if (p.schemaVersion == 1) { ... upgrade to 2 ... }
            if (p.stats == null) p.stats = new ProfileStats();
            if (p.preferences == null) p.preferences = new ProfilePreferences();
            if (p.history == null) p.history = new System.Collections.Generic.List<SessionSummary>();
            p.schemaVersion = Schema.Version;
            return p;
        }

        void TryBackupCorrupt()
        {
            try { if (File.Exists(path)) File.Copy(path, path + ".corrupt", true); } catch { /* best effort */ }
        }
    }
}
