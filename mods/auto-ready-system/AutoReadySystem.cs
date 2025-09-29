using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.AutoReadySystem
{
    [BepInPlugin("com.yourname.autoreadysystem", "Auto-Ready System", "1.0.0")]
    public class AutoReadySystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configAutoReadyEnabled;
        private static ConfigEntry<int> configMinPlayers;
        private static ConfigEntry<float> configReadyDelay;
        private static ConfigEntry<bool> configReadyNotification;
        private static ConfigEntry<bool> configSmartReady;
        
        private static Dictionary<byte, bool> playerReadyStates = new Dictionary<byte, bool>();
        private static float lastReadyCheck = 0f;
        private static bool gameStarted = false;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the auto-ready system");
            configAutoReadyEnabled = Config.Bind("AutoReady", "AutoReadyEnabled", true, "Enable automatic ready-up");
            configMinPlayers = Config.Bind("Lobby", "MinPlayers", 4, "Minimum players required for auto-ready");
            configReadyDelay = Config.Bind("Timing", "ReadyDelay", 2f, "Delay before auto-ready in seconds");
            configReadyNotification = Config.Bind("Notifications", "ReadyNotification", true, "Show ready notifications");
            configSmartReady = Config.Bind("Smart", "SmartReady", true, "Enable smart ready (only when lobby is full)");
            
            var harmony = new Harmony("com.yourname.autoreadysystem");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("AutoReadySystem", "Auto-Ready System loaded successfully!");
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
        public static class GameStartManagerUpdatePatch
        {
            public static void Postfix(GameStartManager __instance)
            {
                if (!configEnabled.Value || !configAutoReadyEnabled.Value) return;
                
                // Check if we should auto-ready
                if (ShouldAutoReady())
                {
                    AutoReadyPlayer();
                }
                
                // Update ready states
                UpdateReadyStates();
            }
        }

        private static bool ShouldAutoReady()
        {
            if (gameStarted) return false;
            
            // Check if lobby has enough players
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < configMinPlayers.Value) return false;
            
            // Check if smart ready is enabled and lobby is full
            if (configSmartReady.Value)
            {
                if (alivePlayers.Count < 10) return false; // Not full lobby
            }
            
            // Check if enough time has passed since last check
            if (Time.time - lastReadyCheck < configReadyDelay.Value) return false;
            
            return true;
        }

        private static void AutoReadyPlayer()
        {
            if (PlayerControl.LocalPlayer == null) return;
            
            // Check if player is already ready
            if (playerReadyStates.ContainsKey(PlayerControl.LocalPlayer.PlayerId) && 
                playerReadyStates[PlayerControl.LocalPlayer.PlayerId]) return;
            
            // Auto-ready the player
            PlayerControl.LocalPlayer.RpcSetReady(true);
            playerReadyStates[PlayerControl.LocalPlayer.PlayerId] = true;
            lastReadyCheck = Time.time;
            
            if (configReadyNotification.Value)
            {
                CommonUtilities.SendChatMessage("Auto-ready activated!");
            }
            
            CommonUtilities.LogMessage("AutoReadySystem", "Player auto-ready activated");
        }

        private static void UpdateReadyStates()
        {
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            int readyCount = 0;
            
            foreach (var player in alivePlayers)
            {
                bool isReady = player.Data.IsReady;
                playerReadyStates[player.PlayerId] = isReady;
                
                if (isReady) readyCount++;
            }
            
            // Show ready status
            if (configReadyNotification.Value && alivePlayers.Count > 0)
            {
                float readyPercentage = (float)readyCount / alivePlayers.Count * 100f;
                if (readyPercentage >= 80f) // Show notification when 80%+ ready
                {
                    CommonUtilities.SendChatMessage($"Ready status: {readyCount}/{alivePlayers.Count} ({readyPercentage:F0}%)");
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                gameStarted = true;
                playerReadyStates.Clear();
                
                if (configReadyNotification.Value)
                {
                    CommonUtilities.SendChatMessage("Game started! Auto-ready system active for next game.");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetReady))]
        public static class ReadyStatePatch
        {
            public static void Postfix(PlayerControl __instance, bool ready)
            {
                if (!configEnabled.Value) return;
                
                playerReadyStates[__instance.PlayerId] = ready;
                
                if (configReadyNotification.Value)
                {
                    string status = ready ? "ready" : "not ready";
                    CommonUtilities.SendChatMessage($"{__instance.Data.PlayerName} is {status}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for ready commands
                if (chatText == "/ready" || chatText == "/r")
                {
                    ForceReady(__instance);
                    return false;
                }
                else if (chatText == "/unready" || chatText == "/ur")
                {
                    ForceUnready(__instance);
                    return false;
                }
                else if (chatText == "/readycount" || chatText == "/rc")
                {
                    ShowReadyCount(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ForceReady(PlayerControl player)
        {
            if (player.AmOwner)
            {
                player.RpcSetReady(true);
                CommonUtilities.SendChatMessage("Forced ready!");
            }
            else
            {
                CommonUtilities.SendChatMessage("Only the host can force ready!");
            }
        }

        private static void ForceUnready(PlayerControl player)
        {
            if (player.AmOwner)
            {
                player.RpcSetReady(false);
                CommonUtilities.SendChatMessage("Forced unready!");
            }
            else
            {
                CommonUtilities.SendChatMessage("Only the host can force unready!");
            }
        }

        private static void ShowReadyCount(PlayerControl player)
        {
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            int readyCount = alivePlayers.Count(p => p.Data.IsReady);
            float readyPercentage = alivePlayers.Count > 0 ? (float)readyCount / alivePlayers.Count * 100f : 0f;
            
            CommonUtilities.SendChatMessage($"Ready: {readyCount}/{alivePlayers.Count} ({readyPercentage:F0}%)");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnDestroy))]
        public static class PlayerDestroyPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                // Clean up player ready state
                if (playerReadyStates.ContainsKey(__instance.PlayerId))
                {
                    playerReadyStates.Remove(__instance.PlayerId);
                }
            }
        }

        private void Update()
        {
            if (!configEnabled.Value) return;
            
            // Reset game started flag when back in lobby
            if (CommonUtilities.IsInLobby() && gameStarted)
            {
                gameStarted = false;
            }
        }
    }
}
