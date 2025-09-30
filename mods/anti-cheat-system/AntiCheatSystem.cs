using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.AntiCheatSystem
{
    [BepInPlugin("com.yourname.anticheatsystem", "Anti-Cheat System", "1.0.0")]
    public class AntiCheatSystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configDetectSpeedHack;
        private static ConfigEntry<bool> configDetectTeleport;
        private static ConfigEntry<bool> configDetectWallHack;
        private static ConfigEntry<bool> configDetectTaskHack;
        private static ConfigEntry<bool> configDetectKillHack;
        private static ConfigEntry<float> configMaxSpeed;
        private static ConfigEntry<float> configMaxDistance;
        private static ConfigEntry<int> configMaxViolations;
        private static ConfigEntry<bool> configAutoKick;
        
        private static Dictionary<byte, PlayerCheatData> playerCheatData = new Dictionary<byte, PlayerCheatData>();
        private static Dictionary<byte, List<CheatViolation>> cheatViolations = new Dictionary<byte, List<CheatViolation>>();
        private static List<byte> flaggedPlayers = new List<byte>();

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable anti-cheat system");
            configDetectSpeedHack = Config.Bind("Detection", "DetectSpeedHack", true, "Detect speed hacking");
            configDetectTeleport = Config.Bind("Detection", "DetectTeleport", true, "Detect teleportation");
            configDetectWallHack = Config.Bind("Detection", "DetectWallHack", true, "Detect wall hacking");
            configDetectTaskHack = Config.Bind("Detection", "DetectTaskHack", true, "Detect task completion hacking");
            configDetectKillHack = Config.Bind("Detection", "DetectKillHack", true, "Detect kill range hacking");
            configMaxSpeed = Config.Bind("Limits", "MaxSpeed", 5f, "Maximum allowed speed");
            configMaxDistance = Config.Bind("Limits", "MaxDistance", 10f, "Maximum allowed distance per frame");
            configMaxViolations = Config.Bind("Limits", "MaxViolations", 5, "Maximum violations before kick");
            configAutoKick = Config.Bind("Actions", "AutoKick", true, "Automatically kick cheaters");
            
            var harmony = new Harmony("com.yourname.anticheatsystem");
            harmony.PatchAll();
            
            InitializeAntiCheat();
            CommonUtilities.LogMessage("AntiCheatSystem", "Anti-Cheat System loaded successfully!");
        }

        private static void InitializeAntiCheat()
        {
            // Initialize cheat data for all players
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.Disconnected)
                {
                    InitializePlayerCheatData(player);
                }
            }
        }

        private static void InitializePlayerCheatData(PlayerControl player)
        {
            var cheatData = new PlayerCheatData
            {
                PlayerId = player.PlayerId,
                PlayerName = player.Data.PlayerName,
                LastPosition = player.transform.position,
                LastUpdateTime = DateTime.Now,
                ViolationCount = 0,
                IsFlagged = false
            };
            
            playerCheatData[player.PlayerId] = cheatData;
            cheatViolations[player.PlayerId] = new List<CheatViolation>();
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class MovementCheckPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value) return;
                
                CheckPlayerMovement(__instance);
            }
        }

        private static void CheckPlayerMovement(PlayerControl player)
        {
            if (!playerCheatData.ContainsKey(player.PlayerId)) return;
            
            var cheatData = playerCheatData[player.PlayerId];
            var currentPosition = player.transform.position;
            var currentTime = DateTime.Now;
            
            // Calculate movement data
            float distance = Vector3.Distance(cheatData.LastPosition, currentPosition);
            float timeDelta = (float)(currentTime - cheatData.LastUpdateTime).TotalSeconds;
            float speed = timeDelta > 0 ? distance / timeDelta : 0f;
            
            // Check for speed hacking
            if (configDetectSpeedHack.Value && speed > configMaxSpeed.Value)
            {
                FlagCheatViolation(player, "Speed Hack", $"Speed: {speed:F2} (Max: {configMaxSpeed.Value})", 0.9f);
            }
            
            // Check for teleportation
            if (configDetectTeleport.Value && distance > configMaxDistance.Value)
            {
                FlagCheatViolation(player, "Teleportation", $"Distance: {distance:F2} (Max: {configMaxDistance.Value})", 0.8f);
            }
            
            // Update player data
            cheatData.LastPosition = currentPosition;
            cheatData.LastUpdateTime = currentTime;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletionCheckPatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !configDetectTaskHack.Value) return;
                
                CheckTaskCompletion(__instance, taskId);
            }
        }

        private static void CheckTaskCompletion(PlayerControl player, uint taskId)
        {
            if (!playerCheatData.ContainsKey(player.PlayerId)) return;
            
            var cheatData = playerCheatData[player.PlayerId];
            var currentTime = DateTime.Now;
            
            // Check for suspiciously fast task completion
            if (cheatData.LastTaskTime != DateTime.MinValue)
            {
                float timeSinceLastTask = (float)(currentTime - cheatData.LastTaskTime).TotalSeconds;
                if (timeSinceLastTask < 1f) // Less than 1 second between tasks
                {
                    FlagCheatViolation(player, "Task Completion Hack", $"Tasks completed too quickly: {timeSinceLastTask:F2}s", 0.7f);
                }
            }
            
            cheatData.LastTaskTime = currentTime;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class KillRangeCheckPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !configDetectKillHack.Value) return;
                
                CheckKillRange(__instance, target);
            }
        }

        private static void CheckKillRange(PlayerControl killer, PlayerControl target)
        {
            float distance = Vector3.Distance(killer.transform.position, target.transform.position);
            float maxKillRange = 2f; // Standard kill range
            
            if (distance > maxKillRange)
            {
                FlagCheatViolation(killer, "Kill Range Hack", $"Kill distance: {distance:F2} (Max: {maxKillRange})", 0.8f);
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetHat))]
        public static class WallHackCheckPatch
        {
            public static void Postfix(PlayerControl __instance, string hatId)
            {
                if (!configEnabled.Value || !configDetectWallHack.Value) return;
                
                CheckWallHack(__instance);
            }
        }

        private static void CheckWallHack(PlayerControl player)
        {
            // Check if player is inside walls or objects
            var collider = player.GetComponent<Collider2D>();
            if (collider != null)
            {
                var bounds = collider.bounds;
                var center = bounds.center;
                
                // Check for collision with walls
                var hit = Physics2D.OverlapCircle(center, bounds.size.magnitude);
                if (hit != null && hit.gameObject.layer == LayerMask.NameToLayer("Default"))
                {
                    FlagCheatViolation(player, "Wall Hack", "Player detected inside walls", 0.6f);
                }
            }
        }

        private static void FlagCheatViolation(PlayerControl player, string cheatType, string details, float severity)
        {
            if (!playerCheatData.ContainsKey(player.PlayerId)) return;
            
            var cheatData = playerCheatData[player.PlayerId];
            var violation = new CheatViolation
            {
                Timestamp = DateTime.Now,
                CheatType = cheatType,
                Details = details,
                Severity = severity,
                PlayerId = player.PlayerId
            };
            
            cheatViolations[player.PlayerId].Add(violation);
            cheatData.ViolationCount++;
            
            // Check if player should be flagged
            if (cheatData.ViolationCount >= configMaxViolations.Value)
            {
                cheatData.IsFlagged = true;
                flaggedPlayers.Add(player.PlayerId);
                
                CommonUtilities.SendChatMessage($"⚠️ {player.Data.PlayerName} flagged for cheating: {cheatType}");
                
                if (configAutoKick.Value && AmongUsClient.Instance.AmHost)
                {
                    AmongUsClient.Instance.KickPlayer(player.PlayerId, false);
                    CommonUtilities.SendChatMessage($"{player.Data.PlayerName} was kicked for cheating!");
                }
            }
            
            CommonUtilities.LogMessage("AntiCheatSystem", $"Cheat detected: {player.Data.PlayerName} - {cheatType} ({severity:F2})");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for anti-cheat commands
                if (chatText == "/anticheat" || chatText == "/ac")
                {
                    ShowAntiCheatStatus(__instance);
                    return false;
                }
                else if (chatText == "/flagged" || chatText == "/cheaters")
                {
                    ShowFlaggedPlayers(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/check "))
                {
                    string targetName = chatText.Substring(7);
                    CheckSpecificPlayer(__instance, targetName);
                    return false;
                }
                else if (chatText == "/resetviolations")
                {
                    ResetViolations(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowAntiCheatStatus(PlayerControl player)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can view anti-cheat status!");
                return;
            }
            
            int totalViolations = cheatViolations.Values.Sum(v => v.Count);
            int flaggedCount = flaggedPlayers.Count;
            
            CommonUtilities.SendChatMessage("=== Anti-Cheat Status ===");
            CommonUtilities.SendChatMessage($"Total Violations: {totalViolations}");
            CommonUtilities.SendChatMessage($"Flagged Players: {flaggedCount}");
            CommonUtilities.SendChatMessage($"Detection Enabled: {configEnabled.Value}");
        }

        private static void ShowFlaggedPlayers(PlayerControl player)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can view flagged players!");
                return;
            }
            
            if (flaggedPlayers.Count == 0)
            {
                CommonUtilities.SendChatMessage("No players currently flagged");
                return;
            }
            
            CommonUtilities.SendChatMessage($"=== Flagged Players ({flaggedPlayers.Count}) ===");
            foreach (var flaggedId in flaggedPlayers)
            {
                var flaggedPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == flaggedId);
                if (flaggedPlayer != null)
                {
                    var violations = cheatViolations[flaggedId];
                    CommonUtilities.SendChatMessage($"{flaggedPlayer.Data.PlayerName}: {violations.Count} violations");
                }
            }
        }

        private static void CheckSpecificPlayer(PlayerControl player, string targetName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can check specific players!");
                return;
            }
            
            var targetPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => 
                p.Data.PlayerName.ToLower().Contains(targetName.ToLower()));
            
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found!");
                return;
            }
            
            if (!playerCheatData.ContainsKey(targetPlayer.PlayerId))
            {
                CommonUtilities.SendChatMessage("No cheat data available for this player");
                return;
            }
            
            var cheatData = playerCheatData[targetPlayer.PlayerId];
            var violations = cheatViolations[targetPlayer.PlayerId];
            
            CommonUtilities.SendChatMessage($"=== Cheat Check: {targetPlayer.Data.PlayerName} ===");
            CommonUtilities.SendChatMessage($"Violations: {cheatData.ViolationCount}");
            CommonUtilities.SendChatMessage($"Flagged: {(cheatData.IsFlagged ? "Yes" : "No")}");
            
            if (violations.Count > 0)
            {
                CommonUtilities.SendChatMessage("Recent violations:");
                foreach (var violation in violations.TakeLast(3))
                {
                    CommonUtilities.SendChatMessage($"- {violation.CheatType}: {violation.Details}");
                }
            }
        }

        private static void ResetViolations(PlayerControl player)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can reset violations!");
                return;
            }
            
            foreach (var cheatData in playerCheatData.Values)
            {
                cheatData.ViolationCount = 0;
                cheatData.IsFlagged = false;
            }
            
            cheatViolations.Clear();
            flaggedPlayers.Clear();
            
            CommonUtilities.SendChatMessage("All cheat violations have been reset!");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnDestroy))]
        public static class PlayerDestroyPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                // Clean up cheat data
                if (playerCheatData.ContainsKey(__instance.PlayerId))
                {
                    playerCheatData.Remove(__instance.PlayerId);
                }
                
                if (cheatViolations.ContainsKey(__instance.PlayerId))
                {
                    cheatViolations.Remove(__instance.PlayerId);
                }
                
                if (flaggedPlayers.Contains(__instance.PlayerId))
                {
                    flaggedPlayers.Remove(__instance.PlayerId);
                }
            }
        }
    }

    [System.Serializable]
    public class PlayerCheatData
    {
        public byte PlayerId;
        public string PlayerName;
        public Vector3 LastPosition;
        public DateTime LastUpdateTime;
        public DateTime LastTaskTime;
        public int ViolationCount;
        public bool IsFlagged;
    }

    [System.Serializable]
    public class CheatViolation
    {
        public DateTime Timestamp;
        public string CheatType;
        public string Details;
        public float Severity;
        public byte PlayerId;
    }
}
