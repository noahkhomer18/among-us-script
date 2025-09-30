using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.LobbySettingsPresets
{
    [BepInPlugin("com.yourname.lobbysettingspresets", "Lobby Settings Presets", "1.0.0")]
    public class LobbySettingsPresetsPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configAutoSave;
        private static ConfigEntry<string> configPresetsDirectory;
        private static ConfigEntry<int> configMaxPresets;
        private static ConfigEntry<bool> configSharePresets;
        
        private static Dictionary<string, LobbyPreset> savedPresets = new Dictionary<string, LobbyPreset>();
        private static string presetsDirectory;
        private static LobbyPreset currentPreset;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable lobby settings presets");
            configAutoSave = Config.Bind("AutoSave", "AutoSave", true, "Automatically save current settings");
            configPresetsDirectory = Config.Bind("Storage", "PresetsDirectory", "LobbyPresets", "Directory for preset files");
            configMaxPresets = Config.Bind("Limits", "MaxPresets", 20, "Maximum number of presets to keep");
            configSharePresets = Config.Bind("Sharing", "SharePresets", true, "Allow sharing presets with other players");
            
            var harmony = new Harmony("com.yourname.lobbysettingspresets");
            harmony.PatchAll();
            
            InitializePresetSystem();
            CommonUtilities.LogMessage("LobbySettingsPresets", "Lobby Settings Presets loaded successfully!");
        }

        private static void InitializePresetSystem()
        {
            presetsDirectory = Path.Combine(Application.persistentDataPath, configPresetsDirectory.Value);
            
            if (!Directory.Exists(presetsDirectory))
            {
                Directory.CreateDirectory(presetsDirectory);
            }
            
            LoadDefaultPresets();
            LoadSavedPresets();
        }

        private static void LoadDefaultPresets()
        {
            // Classic Among Us preset
            var classicPreset = new LobbyPreset
            {
                Name = "Classic",
                Description = "Classic Among Us settings",
                PlayerSpeed = 1f,
                CrewmateVision = 1f,
                ImpostorVision = 1.5f,
                KillCooldown = 30f,
                KillDistance = 1,
                TaskBarUpdates = 1,
                CommonTasks = 1,
                LongTasks = 1,
                ShortTasks = 2,
                EmergencyMeetings = 1,
                EmergencyCooldown = 15f,
                DiscussionTime = 15f,
                VotingTime = 120f,
                ConfirmEjects = true,
                VisualTasks = true,
                AnonymousVotes = false
            };
            
            savedPresets["classic"] = classicPreset;
            
            // Speedrun preset
            var speedrunPreset = new LobbyPreset
            {
                Name = "Speedrun",
                Description = "Fast-paced speedrun settings",
                PlayerSpeed = 1.5f,
                CrewmateVision = 0.75f,
                ImpostorVision = 1.25f,
                KillCooldown = 10f,
                KillDistance = 2,
                TaskBarUpdates = 0,
                CommonTasks = 0,
                LongTasks = 0,
                ShortTasks = 1,
                EmergencyMeetings = 1,
                EmergencyCooldown = 5f,
                DiscussionTime = 5f,
                VotingTime = 15f,
                ConfirmEjects = false,
                VisualTasks = false,
                AnonymousVotes = true
            };
            
            savedPresets["speedrun"] = speedrunPreset;
            
            // Hardcore preset
            var hardcorePreset = new LobbyPreset
            {
                Name = "Hardcore",
                Description = "Hardcore difficulty settings",
                PlayerSpeed = 0.75f,
                CrewmateVision = 0.5f,
                ImpostorVision = 1f,
                KillCooldown = 60f,
                KillDistance = 0,
                TaskBarUpdates = 1,
                CommonTasks = 2,
                LongTasks = 2,
                ShortTasks = 3,
                EmergencyMeetings = 1,
                EmergencyCooldown = 30f,
                DiscussionTime = 30f,
                VotingTime = 180f,
                ConfirmEjects = true,
                VisualTasks = false,
                AnonymousVotes = false
            };
            
            savedPresets["hardcore"] = hardcorePreset;
        }

        private static void LoadSavedPresets()
        {
            try
            {
                var presetFiles = Directory.GetFiles(presetsDirectory, "*.json");
                int loadedCount = 0;
                
                foreach (var presetFile in presetFiles)
                {
                    if (loadedCount >= configMaxPresets.Value) break;
                    
                    string jsonData = File.ReadAllText(presetFile);
                    var preset = JsonUtility.FromJson<LobbyPreset>(jsonData);
                    savedPresets[preset.Name.ToLower()] = preset;
                    loadedCount++;
                }
                
                CommonUtilities.LogMessage("LobbySettingsPresets", $"Loaded {loadedCount} saved presets");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("LobbySettingsPresets", $"Error loading presets: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
        public static class LobbyUpdatePatch
        {
            public static void Postfix(GameStartManager __instance)
            {
                if (!configEnabled.Value || !configAutoSave.Value) return;
                
                // Auto-save current settings periodically
                if (Time.time % 30f < 0.1f) // Every 30 seconds
                {
                    SaveCurrentSettings();
                }
            }
        }

        private static void SaveCurrentSettings()
        {
            if (GameStartManager.Instance == null) return;
            
            var currentSettings = GetCurrentLobbySettings();
            if (currentSettings != null)
            {
                currentPreset = currentSettings;
            }
        }

        private static LobbyPreset GetCurrentLobbySettings()
        {
            var gameOptions = GameOptionsManager.Instance.gameOptions;
            if (gameOptions == null) return null;
            
            return new LobbyPreset
            {
                Name = "Current",
                Description = "Current lobby settings",
                PlayerSpeed = gameOptions.PlayerSpeedMod,
                CrewmateVision = gameOptions.CrewLightMod,
                ImpostorVision = gameOptions.ImpostorLightMod,
                KillCooldown = gameOptions.KillCooldown,
                KillDistance = (int)gameOptions.KillDistance,
                TaskBarUpdates = (int)gameOptions.TaskBarMode,
                CommonTasks = gameOptions.NumCommonTasks,
                LongTasks = gameOptions.NumLongTasks,
                ShortTasks = gameOptions.NumShortTasks,
                EmergencyMeetings = gameOptions.NumEmergencyMeetings,
                EmergencyCooldown = gameOptions.EmergencyCooldown,
                DiscussionTime = gameOptions.DiscussionTime,
                VotingTime = gameOptions.VotingTime,
                ConfirmEjects = gameOptions.ConfirmImpostor,
                VisualTasks = gameOptions.VisualTasks,
                AnonymousVotes = gameOptions.AnonymousVotes
            };
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for preset commands
                if (chatText == "/presets" || chatText == "/listpresets")
                {
                    ShowAvailablePresets(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/loadpreset "))
                {
                    string presetName = chatText.Substring(12);
                    LoadPreset(__instance, presetName);
                    return false;
                }
                else if (chatText.StartsWith("/savepreset "))
                {
                    string presetName = chatText.Substring(12);
                    SavePreset(__instance, presetName);
                    return false;
                }
                else if (chatText.StartsWith("/deletepreset "))
                {
                    string presetName = chatText.Substring(14);
                    DeletePreset(__instance, presetName);
                    return false;
                }
                else if (chatText == "/currentsettings")
                {
                    ShowCurrentSettings(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/sharepreset "))
                {
                    string presetName = chatText.Substring(13);
                    SharePreset(__instance, presetName);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowAvailablePresets(PlayerControl player)
        {
            CommonUtilities.SendChatMessage($"=== Available Presets ({savedPresets.Count}) ===");
            
            foreach (var preset in savedPresets.Values)
            {
                CommonUtilities.SendChatMessage($"{preset.Name}: {preset.Description}");
            }
        }

        private static void LoadPreset(PlayerControl player, string presetName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can load presets!");
                return;
            }
            
            var preset = savedPresets.Values.FirstOrDefault(p => 
                p.Name.ToLower() == presetName.ToLower());
            
            if (preset == null)
            {
                CommonUtilities.SendChatMessage($"Preset '{presetName}' not found!");
                return;
            }
            
            ApplyPreset(preset);
            CommonUtilities.SendChatMessage($"Loaded preset: {preset.Name}");
        }

        private static void ApplyPreset(LobbyPreset preset)
        {
            var gameOptions = GameOptionsManager.Instance.gameOptions;
            if (gameOptions == null) return;
            
            // Apply preset settings
            gameOptions.PlayerSpeedMod = preset.PlayerSpeed;
            gameOptions.CrewLightMod = preset.CrewmateVision;
            gameOptions.ImpostorLightMod = preset.ImpostorVision;
            gameOptions.KillCooldown = preset.KillCooldown;
            gameOptions.KillDistance = (KillDistances)preset.KillDistance;
            gameOptions.TaskBarMode = (TaskBarMode)preset.TaskBarUpdates;
            gameOptions.NumCommonTasks = preset.CommonTasks;
            gameOptions.NumLongTasks = preset.LongTasks;
            gameOptions.NumShortTasks = preset.ShortTasks;
            gameOptions.NumEmergencyMeetings = preset.EmergencyMeetings;
            gameOptions.EmergencyCooldown = preset.EmergencyCooldown;
            gameOptions.DiscussionTime = preset.DiscussionTime;
            gameOptions.VotingTime = preset.VotingTime;
            gameOptions.ConfirmImpostor = preset.ConfirmEjects;
            gameOptions.VisualTasks = preset.VisualTasks;
            gameOptions.AnonymousVotes = preset.AnonymousVotes;
            
            // Update UI
            if (GameStartManager.Instance != null)
            {
                GameStartManager.Instance.Update();
            }
        }

        private static void SavePreset(PlayerControl player, string presetName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can save presets!");
                return;
            }
            
            var currentSettings = GetCurrentLobbySettings();
            if (currentSettings == null)
            {
                CommonUtilities.SendChatMessage("Unable to get current settings!");
                return;
            }
            
            currentSettings.Name = presetName;
            currentSettings.Description = $"Custom preset saved by {player.Data.PlayerName}";
            
            savedPresets[presetName.ToLower()] = currentSettings;
            SavePresetToFile(currentSettings);
            
            CommonUtilities.SendChatMessage($"Saved preset: {presetName}");
        }

        private static void SavePresetToFile(LobbyPreset preset)
        {
            try
            {
                string jsonData = JsonUtility.ToJson(preset, true);
                string fileName = Path.Combine(presetsDirectory, $"{preset.Name}.json");
                File.WriteAllText(fileName, jsonData);
                
                CommonUtilities.LogMessage("LobbySettingsPresets", $"Saved preset: {preset.Name}");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("LobbySettingsPresets", $"Error saving preset: {e.Message}");
            }
        }

        private static void DeletePreset(PlayerControl player, string presetName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can delete presets!");
                return;
            }
            
            if (!savedPresets.ContainsKey(presetName.ToLower()))
            {
                CommonUtilities.SendChatMessage($"Preset '{presetName}' not found!");
                return;
            }
            
            savedPresets.Remove(presetName.ToLower());
            
            // Delete file
            string fileName = Path.Combine(presetsDirectory, $"{presetName}.json");
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            
            CommonUtilities.SendChatMessage($"Deleted preset: {presetName}");
        }

        private static void ShowCurrentSettings(PlayerControl player)
        {
            var currentSettings = GetCurrentLobbySettings();
            if (currentSettings == null)
            {
                CommonUtilities.SendChatMessage("Unable to get current settings!");
                return;
            }
            
            CommonUtilities.SendChatMessage("=== Current Lobby Settings ===");
            CommonUtilities.SendChatMessage($"Player Speed: {currentSettings.PlayerSpeed}");
            CommonUtilities.SendChatMessage($"Crewmate Vision: {currentSettings.CrewmateVision}");
            CommonUtilities.SendChatMessage($"Impostor Vision: {currentSettings.ImpostorVision}");
            CommonUtilities.SendChatMessage($"Kill Cooldown: {currentSettings.KillCooldown}s");
            CommonUtilities.SendChatMessage($"Kill Distance: {currentSettings.KillDistance}");
            CommonUtilities.SendChatMessage($"Tasks: {currentSettings.CommonTasks}C {currentSettings.LongTasks}L {currentSettings.ShortTasks}S");
        }

        private static void SharePreset(PlayerControl player, string presetName)
        {
            if (!configSharePresets.Value)
            {
                CommonUtilities.SendChatMessage("Preset sharing is disabled!");
                return;
            }
            
            var preset = savedPresets.Values.FirstOrDefault(p => 
                p.Name.ToLower() == presetName.ToLower());
            
            if (preset == null)
            {
                CommonUtilities.SendChatMessage($"Preset '{presetName}' not found!");
                return;
            }
            
            // Share preset data via chat
            string presetData = JsonUtility.ToJson(preset);
            CommonUtilities.SendChatMessage($"=== Preset: {preset.Name} ===");
            CommonUtilities.SendChatMessage($"Description: {preset.Description}");
            CommonUtilities.SendChatMessage($"Data: {presetData}");
        }
    }

    [System.Serializable]
    public class LobbyPreset
    {
        public string Name;
        public string Description;
        public float PlayerSpeed;
        public float CrewmateVision;
        public float ImpostorVision;
        public float KillCooldown;
        public int KillDistance;
        public int TaskBarUpdates;
        public int CommonTasks;
        public int LongTasks;
        public int ShortTasks;
        public int EmergencyMeetings;
        public float EmergencyCooldown;
        public float DiscussionTime;
        public float VotingTime;
        public bool ConfirmEjects;
        public bool VisualTasks;
        public bool AnonymousVotes;
    }
}
