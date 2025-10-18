using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.TheGuesser
{
    [BepInPlugin("com.yourname.theguesser", "The Guesser", "1.0.0")]
    public class TheGuesserPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configShowFakeTaskProgress;
        private static ConfigEntry<float> configTaskCompletionThreshold;
        
        private static Dictionary<byte, bool> playerTaskCompletion = new Dictionary<byte, bool>();
        private static byte fakeCrewmateId = 0;
        private static bool gameStarted = false;
        private static bool gameEnded = false;
        private static int totalTasksCompleted = 0;
        private static int totalTasksNeeded = 0;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable The Guesser mod");
            configShowFakeTaskProgress = Config.Bind("Gameplay", "ShowFakeTaskProgress", false, "Show fake crewmate's task progress to others");
            configTaskCompletionThreshold = Config.Bind("Gameplay", "TaskCompletionThreshold", 0.8f, "Percentage of tasks needed to complete to win");
            
            var harmony = new Harmony("com.yourname.theguesser");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("TheGuesser", "The Guesser mod loaded successfully!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                InitializeGuesserGame();
            }
        }

        private static void InitializeGuesserGame()
        {
            gameStarted = true;
            gameEnded = false;
            playerTaskCompletion.Clear();
            totalTasksCompleted = 0;
            
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < 3) return; // Need at least 3 players
            
            // Select one random player as the fake crewmate
            var shuffledPlayers = alivePlayers.OrderBy(x => UnityEngine.Random.value).ToList();
            fakeCrewmateId = shuffledPlayers[0].PlayerId;
            
            // Calculate total tasks needed
            totalTasksNeeded = alivePlayers.Count * 3; // Assume 3 tasks per player
            
            CommonUtilities.SendChatMessage("🎮 THE GUESSER GAME BEGINS!");
            CommonUtilities.SendChatMessage("🔍 There are NO imposters - everyone is crewmate!");
            CommonUtilities.SendChatMessage("⚠️ But ONE crewmate is FAKE and cannot do tasks!");
            CommonUtilities.SendChatMessage("🎯 Complete your tasks and find the fake crewmate!");
            CommonUtilities.SendChatMessage("💀 If you vote out the wrong person, you LOSE!");
            
            // Notify the fake crewmate privately
            foreach (var player in alivePlayers)
            {
                if (player.PlayerId == fakeCrewmateId)
                {
                    CommonUtilities.SendChatMessage("🎭 YOU are the fake crewmate! Pretend to do tasks!");
                }
                else
                {
                    CommonUtilities.SendChatMessage("✅ You are a real crewmate! Complete your tasks!");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletionPatch
        {
            public static bool Prefix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !gameStarted || gameEnded) return true;
                
                // Fake crewmate cannot complete tasks
                if (__instance.PlayerId == fakeCrewmateId)
                {
                    CommonUtilities.SendChatMessage($"❌ {__instance.Data.PlayerName} tried to complete a task but failed!");
                    return false; // Block task completion
                }
                
                // Real crewmate completes task
                totalTasksCompleted++;
                playerTaskCompletion[__instance.PlayerId] = true;
                
                CommonUtilities.SendChatMessage($"✅ {__instance.Data.PlayerName} completed a task! ({totalTasksCompleted}/{totalTasksNeeded})");
                
                // Check if crewmates have completed enough tasks
                CheckTaskCompletion();
                
                return true;
            }
        }

        private static void CheckTaskCompletion()
        {
            float completionPercentage = (float)totalTasksCompleted / totalTasksNeeded;
            
            if (completionPercentage >= configTaskCompletionThreshold.Value)
            {
                // Crewmates completed enough tasks - they win!
                EndGame(true);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
        public static class VotingCompletePatch
        {
            public static void Postfix(MeetingHud __instance, byte[] states, byte[] votes, byte exiledPlayerId, bool tie)
            {
                if (!configEnabled.Value || !gameStarted || gameEnded) return;
                
                if (exiledPlayerId != 255) // Someone was voted out
                {
                    var exiledPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == exiledPlayerId);
                    if (exiledPlayer != null)
                    {
                        if (exiledPlayerId == fakeCrewmateId)
                        {
                            // Fake crewmate was voted out - crewmates win!
                            CommonUtilities.SendChatMessage("🎉 CREWMATES WIN! The fake crewmate was found!");
                            EndGame(true);
                        }
                        else
                        {
                            // Real crewmate was voted out - crewmates lose!
                            CommonUtilities.SendChatMessage("💀 CREWMATES LOSE! You voted out a real crewmate!");
                            EndGame(false);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static bool Prefix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !gameStarted || gameEnded) return false;
                
                // No murders in Guesser mode
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
        public static class ReportPatch
        {
            public static bool Prefix(PlayerControl __instance, GameData.PlayerInfo target)
            {
                if (!configEnabled.Value || !gameStarted || gameEnded) return false;
                
                // No body reports in Guesser mode
                return false;
            }
        }

        private static void EndGame(bool crewmatesWin)
        {
            gameEnded = true;
            
            if (crewmatesWin)
            {
                CommonUtilities.SendChatMessage("🏆 GAME OVER - CREWMATES WIN!");
                CommonUtilities.SendChatMessage("🎯 You successfully identified the fake crewmate!");
            }
            else
            {
                CommonUtilities.SendChatMessage("💀 GAME OVER - CREWMATES LOSE!");
                CommonUtilities.SendChatMessage("😈 The fake crewmate fooled you all!");
            }
            
            // Reset for next game
            gameStarted = false;
            gameEnded = false;
            playerTaskCompletion.Clear();
            totalTasksCompleted = 0;
            fakeCrewmateId = 0;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class FixedUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !gameStarted || gameEnded) return;
                
                // Show fake task progress if enabled
                if (configShowFakeTaskProgress.Value && __instance.PlayerId == fakeCrewmateId)
                {
                    // Fake crewmate shows fake task progress
                    // This would require more complex UI manipulation
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.EndGame))]
        public static class GameEndPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Reset when game ends
                gameStarted = false;
                gameEnded = false;
                playerTaskCompletion.Clear();
                totalTasksCompleted = 0;
                fakeCrewmateId = 0;
            }
        }

        // Chat commands
        [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
        public static class ChatCommandPatch
        {
            public static void Postfix(ChatController __instance, PlayerControl sourcePlayer, string chatText)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                if (chatText.StartsWith("/guesser"))
                {
                    if (sourcePlayer.PlayerId == fakeCrewmateId)
                    {
                        CommonUtilities.SendChatMessage("🎭 You are the fake crewmate! Don't reveal yourself!");
                    }
                    else
                    {
                        CommonUtilities.SendChatMessage("✅ You are a real crewmate! Complete your tasks!");
                    }
                }
                else if (chatText.StartsWith("/tasks"))
                {
                    CommonUtilities.SendChatMessage($"📊 Tasks completed: {totalTasksCompleted}/{totalTasksNeeded}");
                    CommonUtilities.SendChatMessage($"📈 Progress: {(float)totalTasksCompleted/totalTasksNeeded*100:F1}%");
                }
            }
        }
    }
}
