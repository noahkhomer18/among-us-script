using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.StatisticsTracker
{
    [BepInPlugin("com.yourname.statisticstracker", "Statistics Tracker", "1.0.0")]
    public class StatisticsTrackerPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configTrackWins;
        private static ConfigEntry<bool> configTrackKills;
        private static ConfigEntry<bool> configTrackTasks;
        private static ConfigEntry<bool> configTrackTime;
        private static ConfigEntry<bool> configShowStats;
        private static ConfigEntry<string> configStatsFile;
        
        private static Dictionary<byte, PlayerStats> playerStats = new Dictionary<byte, PlayerStats>();
        private static Dictionary<byte, GameSession> currentSessions = new Dictionary<byte, GameSession>();
        private static string statsFilePath;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable statistics tracking");
            configTrackWins = Config.Bind("Tracking", "TrackWins", true, "Track win/loss statistics");
            configTrackKills = Config.Bind("Tracking", "TrackKills", true, "Track kill statistics");
            configTrackTasks = Config.Bind("Tracking", "TrackTasks", true, "Track task completion");
            configTrackTime = Config.Bind("Tracking", "TrackTime", true, "Track play time");
            configShowStats = Config.Bind("UI", "ShowStats", true, "Show statistics in chat");
            configStatsFile = Config.Bind("Data", "StatsFile", "player_stats.json", "Statistics file name");
            
            var harmony = new Harmony("com.yourname.statisticstracker");
            harmony.PatchAll();
            
            InitializeStats();
            CommonUtilities.LogMessage("StatisticsTracker", "Statistics Tracker loaded successfully!");
        }

        private static void InitializeStats()
        {
            statsFilePath = Path.Combine(Application.persistentDataPath, configStatsFile.Value);
            
            // Load existing statistics
            if (File.Exists(statsFilePath))
            {
                try
                {
                    string jsonData = File.ReadAllText(statsFilePath);
                    // Deserialize player statistics
                    CommonUtilities.LogMessage("StatisticsTracker", "Loaded existing statistics");
                }
                catch (Exception e)
                {
                    CommonUtilities.LogMessage("StatisticsTracker", $"Error loading statistics: {e.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                StartNewGameSession();
            }
        }

        private static void StartNewGameSession()
        {
            currentSessions.Clear();
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            
            foreach (var player in alivePlayers)
            {
                var session = new GameSession
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.Data.PlayerName,
                    StartTime = DateTime.Now,
                    IsImpostor = player.Data.Role.IsImpostor,
                    TasksCompleted = 0,
                    Kills = 0,
                    Deaths = 0
                };
                
                currentSessions[player.PlayerId] = session;
            }
            
            CommonUtilities.LogMessage("StatisticsTracker", "Started new game session tracking");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletePatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !configTrackTasks.Value) return;
                
                if (currentSessions.ContainsKey(__instance.PlayerId))
                {
                    currentSessions[__instance.PlayerId].TasksCompleted++;
                }
                
                // Update permanent stats
                UpdatePlayerStats(__instance.PlayerId, stats => stats.TasksCompleted++);
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !configTrackKills.Value) return;
                
                // Track kill for killer
                if (currentSessions.ContainsKey(__instance.PlayerId))
                {
                    currentSessions[__instance.PlayerId].Kills++;
                }
                
                // Track death for victim
                if (currentSessions.ContainsKey(target.PlayerId))
                {
                    currentSessions[target.PlayerId].Deaths++;
                }
                
                // Update permanent stats
                UpdatePlayerStats(__instance.PlayerId, stats => stats.Kills++);
                UpdatePlayerStats(target.PlayerId, stats => stats.Deaths++);
            }
        }

        [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
        public static class GameEndPatch
        {
            public static void Postfix(EndGameManager __instance)
            {
                if (!configEnabled.Value) return;
                
                ProcessGameEnd(__instance);
            }
        }

        private static void ProcessGameEnd(EndGameManager endGameManager)
        {
            var gameResult = endGameManager.GameOverReason;
            bool impostorsWon = gameResult == GameOverReason.ImpostorByKill || 
                               gameResult == GameOverReason.ImpostorBySabotage ||
                               gameResult == GameOverReason.ImpostorByVote;
            
            foreach (var session in currentSessions.Values)
            {
                bool playerWon = (session.IsImpostor && impostorsWon) || 
                                (!session.IsImpostor && !impostorsWon);
                
                UpdatePlayerStats(session.PlayerId, stats =>
                {
                    if (playerWon) stats.Wins++;
                    else stats.Losses++;
                    
                    stats.TotalPlayTime += (DateTime.Now - session.StartTime).TotalMinutes;
                });
            }
            
            SaveStatistics();
            CommonUtilities.LogMessage("StatisticsTracker", "Game ended, statistics updated");
        }

        private static void UpdatePlayerStats(byte playerId, Action<PlayerStats> updateAction)
        {
            if (!playerStats.ContainsKey(playerId))
            {
                playerStats[playerId] = new PlayerStats
                {
                    PlayerId = playerId,
                    PlayerName = GetPlayerName(playerId)
                };
            }
            
            updateAction(playerStats[playerId]);
        }

        private static string GetPlayerName(byte playerId)
        {
            var player = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == playerId);
            return player?.Data.PlayerName ?? "Unknown";
        }

        private static void SaveStatistics()
        {
            try
            {
                // Serialize and save statistics
                string jsonData = JsonUtility.ToJson(new StatisticsData { PlayerStats = playerStats.Values.ToList() });
                File.WriteAllText(statsFilePath, jsonData);
                CommonUtilities.LogMessage("StatisticsTracker", "Statistics saved to file");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("StatisticsTracker", $"Error saving statistics: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for statistics commands
                if (chatText == "/stats" || chatText == "/statistics")
                {
                    ShowPlayerStats(__instance);
                    return false;
                }
                else if (chatText == "/leaderboard" || chatText == "/lb")
                {
                    ShowLeaderboard(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/stats "))
                {
                    string targetName = chatText.Substring(7);
                    ShowPlayerStatsByName(__instance, targetName);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowPlayerStats(PlayerControl player)
        {
            if (!playerStats.ContainsKey(player.PlayerId))
            {
                CommonUtilities.SendChatMessage("No statistics found for you yet!");
                return;
            }
            
            var stats = playerStats[player.PlayerId];
            CommonUtilities.SendChatMessage($"=== {stats.PlayerName}'s Statistics ===");
            CommonUtilities.SendChatMessage($"Wins: {stats.Wins} | Losses: {stats.Losses}");
            CommonUtilities.SendChatMessage($"Kills: {stats.Kills} | Deaths: {stats.Deaths}");
            CommonUtilities.SendChatMessage($"Tasks Completed: {stats.TasksCompleted}");
            CommonUtilities.SendChatMessage($"Play Time: {stats.TotalPlayTime:F1} minutes");
        }

        private static void ShowLeaderboard(PlayerControl player)
        {
            var topPlayers = playerStats.Values
                .OrderByDescending(s => s.Wins)
                .Take(5)
                .ToList();
            
            CommonUtilities.SendChatMessage("=== Leaderboard (Top 5) ===");
            for (int i = 0; i < topPlayers.Count; i++)
            {
                var stats = topPlayers[i];
                CommonUtilities.SendChatMessage($"{i + 1}. {stats.PlayerName}: {stats.Wins} wins");
            }
        }

        private static void ShowPlayerStatsByName(PlayerControl player, string targetName)
        {
            var targetStats = playerStats.Values
                .FirstOrDefault(s => s.PlayerName.ToLower().Contains(targetName.ToLower()));
            
            if (targetStats == null)
            {
                CommonUtilities.SendChatMessage($"No statistics found for player '{targetName}'");
                return;
            }
            
            CommonUtilities.SendChatMessage($"=== {targetStats.PlayerName}'s Statistics ===");
            CommonUtilities.SendChatMessage($"Wins: {targetStats.Wins} | Losses: {targetStats.Losses}");
            CommonUtilities.SendChatMessage($"Kills: {targetStats.Kills} | Deaths: {targetStats.Deaths}");
        }
    }

    [System.Serializable]
    public class PlayerStats
    {
        public byte PlayerId;
        public string PlayerName;
        public int Wins;
        public int Losses;
        public int Kills;
        public int Deaths;
        public int TasksCompleted;
        public double TotalPlayTime;
    }

    [System.Serializable]
    public class GameSession
    {
        public byte PlayerId;
        public string PlayerName;
        public DateTime StartTime;
        public bool IsImpostor;
        public int TasksCompleted;
        public int Kills;
        public int Deaths;
    }

    [System.Serializable]
    public class StatisticsData
    {
        public List<PlayerStats> PlayerStats;
    }
}
