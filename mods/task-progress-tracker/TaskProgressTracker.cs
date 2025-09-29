using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.TaskProgressTracker
{
    [BepInPlugin("com.yourname.taskprogresstracker", "Task Progress Tracker", "1.0.0")]
    public class TaskProgressTrackerPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configShowProgressBar;
        private static ConfigEntry<bool> configShowPercentage;
        private static ConfigEntry<bool> configShowTaskList;
        private static ConfigEntry<Color> configProgressBarColor;
        
        private static Dictionary<byte, int> playerTaskCounts = new Dictionary<byte, int>();
        private static Dictionary<byte, int> playerCompletedTasks = new Dictionary<byte, int>();
        private static GameObject progressUI;
        private static TMPro.TextMeshPro progressText;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the task progress tracker");
            configShowProgressBar = Config.Bind("UI", "ShowProgressBar", true, "Show visual progress bar");
            configShowPercentage = Config.Bind("UI", "ShowPercentage", true, "Show percentage completion");
            configShowTaskList = Config.Bind("UI", "ShowTaskList", true, "Show individual task list");
            configProgressBarColor = Config.Bind("UI", "ProgressBarColor", Color.green, "Color of the progress bar");
            
            var harmony = new Harmony("com.yourname.taskprogresstracker");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("TaskProgressTracker", "Task Progress Tracker loaded successfully!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                InitializeTaskTracking();
                CreateProgressUI();
            }
        }

        private static void InitializeTaskTracking()
        {
            playerTaskCounts.Clear();
            playerCompletedTasks.Clear();
            
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.IsDead && !player.Data.Disconnected)
                {
                    playerTaskCounts[player.PlayerId] = GetPlayerTaskCount(player);
                    playerCompletedTasks[player.PlayerId] = 0;
                }
            }
        }

        private static int GetPlayerTaskCount(PlayerControl player)
        {
            if (player.Data.Role.IsImpostor) return 0;
            
            var tasks = player.myTasks.ToArray();
            return tasks.Length;
        }

        private static void CreateProgressUI()
        {
            if (!configShowProgressBar.Value) return;
            
            // Create progress UI
            progressUI = new GameObject("TaskProgressUI");
            progressUI.transform.SetParent(HudManager.Instance.transform);
            
            // Create progress text
            progressText = progressUI.AddComponent<TMPro.TextMeshPro>();
            progressText.text = "Task Progress: 0%";
            progressText.fontSize = 2f;
            progressText.color = Color.white;
            progressText.transform.localPosition = new Vector3(0, 2f, 0);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletePatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value) return;
                
                if (playerCompletedTasks.ContainsKey(__instance.PlayerId))
                {
                    playerCompletedTasks[__instance.PlayerId]++;
                    UpdateProgressDisplay();
                }
            }
        }

        private static void UpdateProgressDisplay()
        {
            if (progressText == null) return;
            
            var aliveCrewmates = CommonUtilities.GetAlivePlayers()
                .Where(p => !p.Data.Role.IsImpostor).ToList();
            
            if (aliveCrewmates.Count == 0) return;
            
            int totalTasks = aliveCrewmates.Sum(p => playerTaskCounts.ContainsKey(p.PlayerId) ? playerTaskCounts[p.PlayerId] : 0);
            int completedTasks = aliveCrewmates.Sum(p => playerCompletedTasks.ContainsKey(p.PlayerId) ? playerCompletedTasks[p.PlayerId] : 0);
            
            if (totalTasks > 0)
            {
                float percentage = (float)completedTasks / totalTasks * 100f;
                progressText.text = $"Task Progress: {percentage:F1}% ({completedTasks}/{totalTasks})";
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class PlayerDeathPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value) return;
                
                // Update progress when player dies
                UpdateProgressDisplay();
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Show task progress in meeting
                ShowMeetingProgress();
            }
        }

        private static void ShowMeetingProgress()
        {
            var aliveCrewmates = CommonUtilities.GetAlivePlayers()
                .Where(p => !p.Data.Role.IsImpostor).ToList();
            
            if (aliveCrewmates.Count == 0) return;
            
            int totalTasks = aliveCrewmates.Sum(p => playerTaskCounts.ContainsKey(p.PlayerId) ? playerTaskCounts[p.PlayerId] : 0);
            int completedTasks = aliveCrewmates.Sum(p => playerCompletedTasks.ContainsKey(p.PlayerId) ? playerCompletedTasks[p.PlayerId] : 0);
            
            if (totalTasks > 0)
            {
                float percentage = (float)completedTasks / totalTasks * 100f;
                CommonUtilities.SendChatMessage($"Task Progress: {percentage:F1}% ({completedTasks}/{totalTasks})");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for task progress commands
                if (chatText == "/tasks" || chatText == "/progress")
                {
                    ShowPlayerTaskProgress(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowPlayerTaskProgress(PlayerControl player)
        {
            if (player.Data.Role.IsImpostor)
            {
                CommonUtilities.SendChatMessage("Impostors don't have tasks!");
                return;
            }
            
            if (playerCompletedTasks.ContainsKey(player.PlayerId) && playerTaskCounts.ContainsKey(player.PlayerId))
            {
                int completed = playerCompletedTasks[player.PlayerId];
                int total = playerTaskCounts[player.PlayerId];
                float percentage = total > 0 ? (float)completed / total * 100f : 0f;
                
                CommonUtilities.SendChatMessage($"Your tasks: {completed}/{total} ({percentage:F1}%)");
            }
        }
    }
}
