using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.RelaxedCrew
{
    [BepInPlugin("com.yourname.relaxedcrew", "Relaxed Crew", "1.0.0")]
    public class RelaxedCrewPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<float> configEmergencyInterval;
        private static ConfigEntry<bool> configAutoEmergency;
        private static ConfigEntry<bool> configVoteOutCompletesTasks;
        private static ConfigEntry<int> configTasksPerPlayer;
        
        private static bool gameStarted = false;
        private static float lastEmergencyTime = 0f;
        private static float nextEmergencyTime = 0f;
        private static Dictionary<byte, int> playerTaskCount = new Dictionary<byte, int>();
        private static Dictionary<byte, bool> playerVotedOut = new Dictionary<byte, bool>();
        private static int totalTasksCompleted = 0;
        private static int totalTasksNeeded = 0;
        private static bool emergencyButtonCooldown = false;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable Relaxed Crew mod");
            configEmergencyInterval = Config.Bind("Gameplay", "EmergencyInterval", 90f, "Emergency button interval in seconds (1:30 = 90)");
            configAutoEmergency = Config.Bind("Gameplay", "AutoEmergency", true, "Automatically trigger emergency button at intervals");
            configVoteOutCompletesTasks = Config.Bind("Gameplay", "VoteOutCompletesTasks", true, "Voting out a player completes their tasks");
            configTasksPerPlayer = Config.Bind("Gameplay", "TasksPerPlayer", 3, "Number of tasks each player must complete");
            
            var harmony = new Harmony("com.yourname.relaxedcrew");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("RelaxedCrew", "Relaxed Crew mod loaded successfully!");
        }

        private void Update()
        {
            if (!configEnabled.Value || !gameStarted) return;
            
            // Check for automatic emergency button
            if (configAutoEmergency.Value && Time.time >= nextEmergencyTime && !emergencyButtonCooldown)
            {
                TriggerEmergencyMeeting();
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                InitializeRelaxedGame();
            }
        }

        private static void InitializeRelaxedGame()
        {
            gameStarted = true;
            playerTaskCount.Clear();
            playerVotedOut.Clear();
            totalTasksCompleted = 0;
            emergencyButtonCooldown = false;
            
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < 2) return;
            
            // Calculate total tasks needed
            totalTasksNeeded = alivePlayers.Count * configTasksPerPlayer.Value;
            
            // Initialize task counts for each player
            foreach (var player in alivePlayers)
            {
                playerTaskCount[player.PlayerId] = 0;
                playerVotedOut[player.PlayerId] = false;
            }
            
            // Set first emergency time
            nextEmergencyTime = Time.time + configEmergencyInterval.Value;
            
            CommonUtilities.SendChatMessage("🌅 RELAXED CREW GAME BEGINS!");
            CommonUtilities.SendChatMessage("👥 Everyone is a crewmate - no imposters!");
            CommonUtilities.SendChatMessage("✅ Complete all tasks to win!");
            CommonUtilities.SendChatMessage("⏰ Emergency button will appear every 1:30 for discussion!");
            CommonUtilities.SendChatMessage("💡 Strategy: Vote out players to complete their tasks faster!");
            CommonUtilities.SendChatMessage("⚠️ But be careful - you need everyone's cooperation!");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletionPatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                // Track task completion
                if (playerTaskCount.ContainsKey(__instance.PlayerId))
                {
                    playerTaskCount[__instance.PlayerId]++;
                    totalTasksCompleted++;
                    
                    CommonUtilities.SendChatMessage($"✅ {__instance.Data.PlayerName} completed a task! ({totalTasksCompleted}/{totalTasksNeeded})");
                    
                    // Check if all tasks are completed
                    CheckTaskCompletion();
                }
            }
        }

        private static void CheckTaskCompletion()
        {
            if (totalTasksCompleted >= totalTasksNeeded)
            {
                // All tasks completed - crewmates win!
                EndGame(true);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
        public static class VotingCompletePatch
        {
            public static void Postfix(MeetingHud __instance, byte[] states, byte[] votes, byte exiledPlayerId, bool tie)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                if (exiledPlayerId != 255 && configVoteOutCompletesTasks.Value) // Someone was voted out
                {
                    var exiledPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == exiledPlayerId);
                    if (exiledPlayer != null && !playerVotedOut[exiledPlayerId])
                    {
                        // Complete the voted out player's remaining tasks
                        int remainingTasks = configTasksPerPlayer.Value - playerTaskCount[exiledPlayerId];
                        if (remainingTasks > 0)
                        {
                            totalTasksCompleted += remainingTasks;
                            playerVotedOut[exiledPlayerId] = true;
                            
                            CommonUtilities.SendChatMessage($"📋 {exiledPlayer.Data.PlayerName} was voted out!");
                            CommonUtilities.SendChatMessage($"✅ Their {remainingTasks} remaining tasks are now completed!");
                            CommonUtilities.SendChatMessage($"📊 Total progress: {totalTasksCompleted}/{totalTasksNeeded}");
                            
                            // Check if all tasks are now completed
                            CheckTaskCompletion();
                        }
                    }
                }
                
                // Reset emergency button cooldown
                emergencyButtonCooldown = false;
                nextEmergencyTime = Time.time + configEmergencyInterval.Value;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static bool Prefix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !gameStarted) return false;
                
                // No murders in Relaxed Crew mode
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
        public static class ReportPatch
        {
            public static bool Prefix(PlayerControl __instance, GameData.PlayerInfo target)
            {
                if (!configEnabled.Value || !gameStarted) return false;
                
                // No body reports in Relaxed Crew mode
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
        public static class EmergencyButtonPatch
        {
            public static bool Prefix(PlayerControl __instance, GameData.PlayerInfo target)
            {
                if (!configEnabled.Value || !gameStarted) return true;
                
                // Allow emergency button usage
                emergencyButtonCooldown = true;
                nextEmergencyTime = Time.time + configEmergencyInterval.Value;
                
                CommonUtilities.SendChatMessage("🚨 Emergency meeting called!");
                CommonUtilities.SendChatMessage("💬 Use this time to discuss strategy and coordination!");
                
                return true;
            }
        }

        private static void TriggerEmergencyMeeting()
        {
            if (emergencyButtonCooldown) return;
            
            CommonUtilities.SendChatMessage("⏰ AUTOMATIC EMERGENCY MEETING!");
            CommonUtilities.SendChatMessage("💬 Time to discuss strategy and progress!");
            CommonUtilities.SendChatMessage("📊 Current progress: " + GetProgressMessage());
            
            // Trigger emergency meeting
            if (AmongUsClient.Instance.AmHost)
            {
                // This would require more complex implementation to actually trigger the meeting
                // For now, we'll just send the message
            }
            
            emergencyButtonCooldown = true;
        }

        private static string GetProgressMessage()
        {
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            var progressInfo = new List<string>();
            
            foreach (var player in alivePlayers)
            {
                if (playerTaskCount.ContainsKey(player.PlayerId))
                {
                    int tasksDone = playerTaskCount[player.PlayerId];
                    int tasksNeeded = configTasksPerPlayer.Value;
                    progressInfo.Add($"{player.Data.PlayerName}: {tasksDone}/{tasksNeeded}");
                }
            }
            
            return string.Join(", ", progressInfo);
        }

        private static void EndGame(bool crewmatesWin)
        {
            gameStarted = false;
            
            if (crewmatesWin)
            {
                CommonUtilities.SendChatMessage("🏆 GAME OVER - CREWMATES WIN!");
                CommonUtilities.SendChatMessage("🎉 All tasks completed! Great teamwork!");
            }
            else
            {
                CommonUtilities.SendChatMessage("💀 GAME OVER - CREWMATES LOSE!");
                CommonUtilities.SendChatMessage("😞 Not enough tasks completed!");
            }
            
            // Reset for next game
            playerTaskCount.Clear();
            playerVotedOut.Clear();
            totalTasksCompleted = 0;
            emergencyButtonCooldown = false;
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.EndGame))]
        public static class GameEndPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Reset when game ends
                gameStarted = false;
                playerTaskCount.Clear();
                playerVotedOut.Clear();
                totalTasksCompleted = 0;
                emergencyButtonCooldown = false;
            }
        }

        // Chat commands
        [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
        public static class ChatCommandPatch
        {
            public static void Postfix(ChatController __instance, PlayerControl sourcePlayer, string chatText)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                if (chatText.StartsWith("/relaxed"))
                {
                    CommonUtilities.SendChatMessage("🌅 Relaxed Crew Mode Active!");
                    CommonUtilities.SendChatMessage("👥 Everyone is crewmate - complete tasks to win!");
                    CommonUtilities.SendChatMessage("⏰ Emergency meetings every 1:30 for discussion!");
                }
                else if (chatText.StartsWith("/progress"))
                {
                    CommonUtilities.SendChatMessage($"📊 Overall Progress: {totalTasksCompleted}/{totalTasksNeeded}");
                    CommonUtilities.SendChatMessage($"📈 Individual Progress: {GetProgressMessage()}");
                }
                else if (chatText.StartsWith("/nextemergency"))
                {
                    float timeLeft = nextEmergencyTime - Time.time;
                    if (timeLeft > 0)
                    {
                        CommonUtilities.SendChatMessage($"⏰ Next emergency meeting in {timeLeft:F0} seconds");
                    }
                    else
                    {
                        CommonUtilities.SendChatMessage("🚨 Emergency meeting available now!");
                    }
                }
                else if (chatText.StartsWith("/strategy"))
                {
                    CommonUtilities.SendChatMessage("💡 Strategy Tips:");
                    CommonUtilities.SendChatMessage("• Vote out players to complete their tasks faster");
                    CommonUtilities.SendChatMessage("• Use emergency meetings to coordinate");
                    CommonUtilities.SendChatMessage("• Balance task completion vs. player count");
                }
            }
        }
    }
}
