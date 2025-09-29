using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.AntiTrollTools
{
    [BepInPlugin("com.yourname.antitrolltools", "Anti-Troll Tools", "1.0.0")]
    public class AntiTrollToolsPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configChatFilterEnabled;
        private static ConfigEntry<bool> configAFKDetectionEnabled;
        private static ConfigEntry<float> configAFKTimeLimit;
        private static ConfigEntry<bool> configSpamProtectionEnabled;
        private static ConfigEntry<int> configMaxMessagesPerMinute;
        
        private static Dictionary<byte, float> lastActivityTime = new Dictionary<byte, float>();
        private static Dictionary<byte, List<float>> playerMessageTimes = new Dictionary<byte, List<float>>();
        private static List<string> bannedWords = new List<string> { "hack", "cheat", "mod", "troll", "noob", "kill yourself" };

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable anti-troll tools");
            configChatFilterEnabled = Config.Bind("Chat", "ChatFilterEnabled", true, "Enable chat filtering");
            configAFKDetectionEnabled = Config.Bind("AFK", "AFKDetectionEnabled", true, "Enable AFK detection");
            configAFKTimeLimit = Config.Bind("AFK", "AFKTimeLimit", 120f, "AFK time limit in seconds");
            configSpamProtectionEnabled = Config.Bind("Spam", "SpamProtectionEnabled", true, "Enable spam protection");
            configMaxMessagesPerMinute = Config.Bind("Spam", "MaxMessagesPerMinute", 10, "Maximum messages per minute per player");
            
            var harmony = new Harmony("com.yourname.antitrolltools");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("AntiTrollTools", "Anti-Troll Tools loaded successfully!");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatFilterPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !configChatFilterEnabled.Value) return true;

                // Check for spam
                if (configSpamProtectionEnabled.Value && IsSpamming(__instance.PlayerId))
                {
                    CommonUtilities.SendChatMessage($"{__instance.Data.PlayerName} is spamming and has been muted!");
                    return false;
                }

                // Check for banned words
                if (ContainsBannedWords(chatText))
                {
                    CommonUtilities.SendChatMessage($"{__instance.Data.PlayerName} used inappropriate language!");
                    return false;
                }

                // Record message time for spam detection
                if (configSpamProtectionEnabled.Value)
                {
                    RecordMessageTime(__instance.PlayerId);
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class ActivityTrackerPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !configAFKDetectionEnabled.Value) return;

                // Update last activity time
                lastActivityTime[__instance.PlayerId] = Time.time;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatActivityPatch
        {
            public static void Postfix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !configAFKDetectionEnabled.Value) return;

                // Update activity time when player chats
                lastActivityTime[__instance.PlayerId] = Time.time;
            }
        }

        private static bool IsSpamming(byte playerId)
        {
            if (!playerMessageTimes.ContainsKey(playerId))
            {
                playerMessageTimes[playerId] = new List<float>();
            }

            var messageTimes = playerMessageTimes[playerId];
            float currentTime = Time.time;
            
            // Remove messages older than 1 minute
            messageTimes.RemoveAll(time => currentTime - time > 60f);
            
            // Check if player is sending too many messages
            return messageTimes.Count >= configMaxMessagesPerMinute.Value;
        }

        private static void RecordMessageTime(byte playerId)
        {
            if (!playerMessageTimes.ContainsKey(playerId))
            {
                playerMessageTimes[playerId] = new List<float>();
            }
            
            playerMessageTimes[playerId].Add(Time.time);
        }

        private static bool ContainsBannedWords(string message)
        {
            string lowerMessage = message.ToLower();
            return bannedWords.Any(word => lowerMessage.Contains(word));
        }

        private void Update()
        {
            if (!configEnabled.Value || !configAFKDetectionEnabled.Value) return;

            // Check for AFK players
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            foreach (var player in alivePlayers)
            {
                if (lastActivityTime.ContainsKey(player.PlayerId))
                {
                    float timeSinceActivity = Time.time - lastActivityTime[player.PlayerId];
                    if (timeSinceActivity > configAFKTimeLimit.Value)
                    {
                        // Kick AFK player
                        CommonUtilities.SendChatMessage($"{player.Data.PlayerName} has been kicked for being AFK!");
                        if (AmongUsClient.Instance.AmHost)
                        {
                            AmongUsClient.Instance.KickPlayer(player.PlayerId, false);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                // Reset spam tracking when meeting starts
                playerMessageTimes.Clear();
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                // Reset all tracking when game starts
                lastActivityTime.Clear();
                playerMessageTimes.Clear();
            }
        }
    }
}
