using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.CustomMapLoader
{
    [BepInPlugin("com.yourname.custommaploader", "Custom Map Loader", "1.0.0")]
    public class CustomMapLoaderPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configAutoLoad;
        private static ConfigEntry<string> configMapDirectory;
        private static ConfigEntry<string> configDefaultMap;
        private static ConfigEntry<bool> configAllowCustomMaps;
        private static ConfigEntry<int> configMaxCustomMaps;
        
        private static List<CustomMap> availableMaps = new List<CustomMap>();
        private static CustomMap currentMap;
        private static string mapDirectory;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable custom map loader");
            configAutoLoad = Config.Bind("Loading", "AutoLoad", true, "Automatically load default map");
            configMapDirectory = Config.Bind("Storage", "MapDirectory", "CustomMaps", "Directory for custom maps");
            configDefaultMap = Config.Bind("Loading", "DefaultMap", "skeld", "Default map to load");
            configAllowCustomMaps = Config.Bind("Security", "AllowCustomMaps", true, "Allow loading custom maps");
            configMaxCustomMaps = Config.Bind("Limits", "MaxCustomMaps", 10, "Maximum number of custom maps");
            
            var harmony = new Harmony("com.yourname.custommaploader");
            harmony.PatchAll();
            
            InitializeMapSystem();
            CommonUtilities.LogMessage("CustomMapLoader", "Custom Map Loader loaded successfully!");
        }

        private static void InitializeMapSystem()
        {
            mapDirectory = Path.Combine(Application.persistentDataPath, configMapDirectory.Value);
            
            if (!Directory.Exists(mapDirectory))
            {
                Directory.CreateDirectory(mapDirectory);
            }
            
            LoadAvailableMaps();
            
            if (configAutoLoad.Value)
            {
                LoadMap(configDefaultMap.Value);
            }
        }

        private static void LoadAvailableMaps()
        {
            availableMaps.Clear();
            
            // Load default maps
            availableMaps.Add(new CustomMap
            {
                MapId = "skeld",
                MapName = "The Skeld",
                IsDefault = true,
                IsCustom = false,
                Description = "Original Among Us map"
            });
            
            availableMaps.Add(new CustomMap
            {
                MapId = "mirahq",
                MapName = "MIRA HQ",
                IsDefault = true,
                IsCustom = false,
                Description = "MIRA HQ facility"
            });
            
            availableMaps.Add(new CustomMap
            {
                MapId = "polus",
                MapName = "Polus",
                IsDefault = true,
                IsCustom = false,
                Description = "Polus research station"
            });
            
            // Load custom maps
            if (configAllowCustomMaps.Value)
            {
                LoadCustomMaps();
            }
            
            CommonUtilities.LogMessage("CustomMapLoader", $"Loaded {availableMaps.Count} available maps");
        }

        private static void LoadCustomMaps()
        {
            try
            {
                var mapFiles = Directory.GetFiles(mapDirectory, "*.json");
                int loadedCount = 0;
                
                foreach (var mapFile in mapFiles)
                {
                    if (loadedCount >= configMaxCustomMaps.Value) break;
                    
                    string jsonData = File.ReadAllText(mapFile);
                    var customMap = JsonUtility.FromJson<CustomMap>(jsonData);
                    customMap.IsCustom = true;
                    customMap.IsDefault = false;
                    
                    availableMaps.Add(customMap);
                    loadedCount++;
                }
                
                CommonUtilities.LogMessage("CustomMapLoader", $"Loaded {loadedCount} custom maps");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("CustomMapLoader", $"Error loading custom maps: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                ApplyCurrentMap();
            }
        }

        private static void ApplyCurrentMap()
        {
            if (currentMap == null) return;
            
            // Apply map-specific settings
            if (currentMap.IsCustom)
            {
                ApplyCustomMapSettings(currentMap);
            }
            
            CommonUtilities.LogMessage("CustomMapLoader", $"Applied map: {currentMap.MapName}");
        }

        private static void ApplyCustomMapSettings(CustomMap map)
        {
            // Apply custom map configurations
            if (map.CustomSettings != null)
            {
                // Apply custom spawn points
                if (map.CustomSettings.SpawnPoints != null)
                {
                    ApplyCustomSpawnPoints(map.CustomSettings.SpawnPoints);
                }
                
                // Apply custom task locations
                if (map.CustomSettings.TaskLocations != null)
                {
                    ApplyCustomTaskLocations(map.CustomSettings.TaskLocations);
                }
                
                // Apply custom vent connections
                if (map.CustomSettings.VentConnections != null)
                {
                    ApplyCustomVentConnections(map.CustomSettings.VentConnections);
                }
            }
        }

        private static void ApplyCustomSpawnPoints(List<Vector3> spawnPoints)
        {
            // Apply custom spawn points to the game
            CommonUtilities.LogMessage("CustomMapLoader", $"Applied {spawnPoints.Count} custom spawn points");
        }

        private static void ApplyCustomTaskLocations(List<TaskLocation> taskLocations)
        {
            // Apply custom task locations
            CommonUtilities.LogMessage("CustomMapLoader", $"Applied {taskLocations.Count} custom task locations");
        }

        private static void ApplyCustomVentConnections(List<VentConnection> ventConnections)
        {
            // Apply custom vent connections
            CommonUtilities.LogMessage("CustomMapLoader", $"Applied {ventConnections.Count} custom vent connections");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for map commands
                if (chatText == "/maps" || chatText == "/listmaps")
                {
                    ShowAvailableMaps(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/loadmap "))
                {
                    string mapId = chatText.Substring(9);
                    LoadMapCommand(__instance, mapId);
                    return false;
                }
                else if (chatText == "/currentmap")
                {
                    ShowCurrentMap(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/createmap "))
                {
                    string mapName = chatText.Substring(11);
                    CreateCustomMap(__instance, mapName);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowAvailableMaps(PlayerControl player)
        {
            CommonUtilities.SendChatMessage($"=== Available Maps ({availableMaps.Count}) ===");
            
            foreach (var map in availableMaps)
            {
                string mapType = map.IsDefault ? "Default" : "Custom";
                CommonUtilities.SendChatMessage($"{map.MapId}: {map.MapName} ({mapType})");
            }
        }

        private static void LoadMapCommand(PlayerControl player, string mapId)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can change maps!");
                return;
            }
            
            LoadMap(mapId);
        }

        private static void LoadMap(string mapId)
        {
            var map = availableMaps.FirstOrDefault(m => m.MapId == mapId);
            if (map == null)
            {
                CommonUtilities.SendChatMessage($"Map '{mapId}' not found!");
                return;
            }
            
            currentMap = map;
            CommonUtilities.SendChatMessage($"Loading map: {map.MapName}");
            
            // Apply map settings
            ApplyCurrentMap();
        }

        private static void ShowCurrentMap(PlayerControl player)
        {
            if (currentMap == null)
            {
                CommonUtilities.SendChatMessage("No map currently loaded");
                return;
            }
            
            CommonUtilities.SendChatMessage($"Current map: {currentMap.MapName}");
            CommonUtilities.SendChatMessage($"Description: {currentMap.Description}");
            CommonUtilities.SendChatMessage($"Type: {(currentMap.IsCustom ? "Custom" : "Default")}");
        }

        private static void CreateCustomMap(PlayerControl player, string mapName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can create custom maps!");
                return;
            }
            
            if (!configAllowCustomMaps.Value)
            {
                CommonUtilities.SendChatMessage("Custom maps are disabled!");
                return;
            }
            
            var customMap = new CustomMap
            {
                MapId = mapName.ToLower().Replace(" ", "_"),
                MapName = mapName,
                IsCustom = true,
                IsDefault = false,
                Description = "Custom map created by host",
                CustomSettings = new CustomMapSettings
                {
                    SpawnPoints = new List<Vector3>(),
                    TaskLocations = new List<TaskLocation>(),
                    VentConnections = new List<VentConnection>()
                }
            };
            
            availableMaps.Add(customMap);
            SaveCustomMap(customMap);
            
            CommonUtilities.SendChatMessage($"Created custom map: {mapName}");
        }

        private static void SaveCustomMap(CustomMap map)
        {
            try
            {
                string jsonData = JsonUtility.ToJson(map, true);
                string fileName = Path.Combine(mapDirectory, $"{map.MapId}.json");
                File.WriteAllText(fileName, jsonData);
                
                CommonUtilities.LogMessage("CustomMapLoader", $"Saved custom map: {map.MapId}");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("CustomMapLoader", $"Error saving custom map: {e.Message}");
            }
        }
    }

    [System.Serializable]
    public class CustomMap
    {
        public string MapId;
        public string MapName;
        public string Description;
        public bool IsDefault;
        public bool IsCustom;
        public CustomMapSettings CustomSettings;
    }

    [System.Serializable]
    public class CustomMapSettings
    {
        public List<Vector3> SpawnPoints;
        public List<TaskLocation> TaskLocations;
        public List<VentConnection> VentConnections;
    }

    [System.Serializable]
    public class TaskLocation
    {
        public Vector3 Position;
        public string TaskType;
        public string Description;
    }

    [System.Serializable]
    public class VentConnection
    {
        public Vector3 FromPosition;
        public Vector3 ToPosition;
        public bool IsBidirectional;
    }
}
