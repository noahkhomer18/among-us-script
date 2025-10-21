using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.MedbayTracker
{
    [BepInPlugin("com.yourname.medbaytracker", "Medbay Tracker", "1.0.0")]
    public class MedbayTrackerPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configMedbayTrackerEnabled;
        private static ConfigEntry<int> configDeathPenalty;
        private static ConfigEntry<bool> configShowScanList;
        private static ConfigEntry<bool> configNotifyOnScan;
        
        private static Dictionary<byte, bool> medbayTrackerPlayers = new Dictionary<byte, bool>();
        private static Dictionary<byte, List<string>> scanHistory = new Dictionary<byte, List<string>>();
        private static List<string> currentGameScans = new List<string>();

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the medbay tracker system");
            configMedbayTrackerEnabled = Config.Bind("Roles", "MedbayTrackerEnabled", true, "Enable Medbay Tracker role");
            configDeathPenalty = Config.Bind("Penalty", "DeathPenalty", 2, "Number of people who die when medbay tracker dies");
            configShowScanList = Config.Bind("UI", "ShowScanList", true, "Show list of scanned players to medbay tracker");
            configNotifyOnScan = Config.Bind("Notifications", "NotifyOnScan", true, "Notify medbay tracker when someone scans");
            
            var harmony = new Harmony("com.yourname.medbaytracker");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("MedbayTracker", "Medbay Tracker loaded successfully!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                InitializeMedbayTracker();
            }
        }

        private static void InitializeMedbayTracker()
        {
            medbayTrackerPlayers.Clear();
            scanHistory.Clear();
            currentGameScans.Clear();

            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < 4) return; // Need at least 4 players for roles

            if (configMedbayTrackerEnabled.Value)
            {
                // Assign Medbay Tracker role to one random player
                var shuffledPlayers = alivePlayers.OrderBy(x => UnityEngine.Random.value).ToList();
                var medbayTracker = shuffledPlayers[0];
                
                medbayTrackerPlayers[medbayTracker.PlayerId] = true;
                scanHistory[medbayTracker.PlayerId] = new List<string>();
                
                CommonUtilities.SendChatMessage($"{medbayTracker.Data.PlayerName} is the Medbay Tracker!");
                CommonUtilities.LogMessage("MedbayTracker", $"Assigned Medbay Tracker to {medbayTracker.Data.PlayerName}");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletePatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value) return;

                // Check if this is a medbay scan task
                if (IsMedbayScanTask(__instance, taskId))
                {
                    HandleMedbayScan(__instance);
                }
            }
        }

        private static bool IsMedbayScanTask(PlayerControl player, uint taskId)
        {
            // Check if the task is a medbay scan
            // This would need to be adapted based on the actual task system
            // For now, we'll simulate this by checking if the player is in medbay
            var playerPosition = player.transform.position;
            
            // Check if player is in medbay area (coordinates may vary by map)
            // This is a simplified check - in reality, you'd need to check the actual task type
            return IsInMedbayArea(playerPosition);
        }

        private static bool IsInMedbayArea(Vector3 position)
        {
            // Simplified medbay area check - this would need to be adapted for different maps
            // For The Skeld: medbay is typically around x: -2 to 2, y: 2 to 6
            return position.x >= -2f && position.x <= 2f && position.y >= 2f && position.y <= 6f;
        }

        private static void HandleMedbayScan(PlayerControl scanner)
        {
            string scannerName = scanner.Data.PlayerName;
            currentGameScans.Add(scannerName);
            
            // Notify all medbay trackers about the scan
            foreach (var trackerId in medbayTrackerPlayers.Keys)
            {
                if (medbayTrackerPlayers[trackerId])
                {
                    var tracker = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == trackerId);
                    if (tracker != null && !tracker.Data.IsDead)
                    {
                        scanHistory[trackerId].Add(scannerName);
                        
                        if (configNotifyOnScan.Value)
                        {
                            CommonUtilities.SendChatMessage($"[Medbay Tracker] {scannerName} has scanned in medbay!");
                        }
                        
                        if (configShowScanList.Value)
                        {
                            ShowScanListToTracker(tracker);
                        }
                    }
                }
            }
            
            CommonUtilities.LogMessage("MedbayTracker", $"{scannerName} completed medbay scan");
        }

        private static void ShowScanListToTracker(PlayerControl tracker)
        {
            if (scanHistory.ContainsKey(tracker.PlayerId))
            {
                var scans = scanHistory[tracker.PlayerId];
                if (scans.Count > 0)
                {
                    string scanList = string.Join(", ", scans);
                    CommonUtilities.SendChatMessage($"[Medbay Tracker] Players who scanned: {scanList}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value) return;

                // Check if a medbay tracker was killed
                if (medbayTrackerPlayers.ContainsKey(target.PlayerId) && medbayTrackerPlayers[target.PlayerId])
                {
                    HandleMedbayTrackerDeath(target);
                }
            }
        }

        private static void HandleMedbayTrackerDeath(PlayerControl deadTracker)
        {
            CommonUtilities.SendChatMessage($"{deadTracker.Data.PlayerName} (Medbay Tracker) has died! The support system is compromised!");
            
            // Kill 2 random players as penalty
            var alivePlayers = CommonUtilities.GetAlivePlayers()
                .Where(p => p.PlayerId != deadTracker.PlayerId)
                .ToList();
            
            if (alivePlayers.Count >= configDeathPenalty.Value)
            {
                var playersToKill = alivePlayers
                    .OrderBy(x => UnityEngine.Random.value)
                    .Take(configDeathPenalty.Value)
                    .ToList();
                
                foreach (var player in playersToKill)
                {
                    player.MurderPlayer(player);
                    CommonUtilities.SendChatMessage($"{player.Data.PlayerName} died due to medbay tracker's death!");
                }
                
                CommonUtilities.SendChatMessage($"Due to the medbay tracker's death, {configDeathPenalty.Value} additional players have died!");
            }
            
            // Remove the dead tracker from our list
            medbayTrackerPlayers[deadTracker.PlayerId] = false;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for medbay tracker commands
                if (chatText == "/scans" || chatText == "/scanlist")
                {
                    if (medbayTrackerPlayers.ContainsKey(__instance.PlayerId) && medbayTrackerPlayers[__instance.PlayerId])
                    {
                        ShowScanListToTracker(__instance);
                        return false;
                    }
                    else
                    {
                        CommonUtilities.SendChatMessage("You are not a medbay tracker!");
                        return false;
                    }
                }
                else if (chatText == "/trackerinfo")
                {
                    ShowTrackerInfo(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowTrackerInfo(PlayerControl player)
        {
            if (medbayTrackerPlayers.ContainsKey(player.PlayerId) && medbayTrackerPlayers[player.PlayerId])
            {
                CommonUtilities.SendChatMessage("=== Medbay Tracker Info ===");
                CommonUtilities.SendChatMessage("You can see who has scanned in medbay this game.");
                CommonUtilities.SendChatMessage("If you die, 2 additional players will die as penalty.");
                CommonUtilities.SendChatMessage("Use /scans to see the list of scanned players.");
            }
            else
            {
                CommonUtilities.SendChatMessage("You are not a medbay tracker.");
            }
        }

        public static bool IsMedbayTracker(byte playerId)
        {
            return medbayTrackerPlayers.ContainsKey(playerId) && medbayTrackerPlayers[playerId];
        }

        public static List<string> GetScanHistory(byte playerId)
        {
            return scanHistory.ContainsKey(playerId) ? scanHistory[playerId] : new List<string>();
        }

        public static int GetTotalScans()
        {
            return currentGameScans.Count;
        }
    }
}
