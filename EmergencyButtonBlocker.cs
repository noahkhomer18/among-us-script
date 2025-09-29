using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace EmergencyButtonBlocker
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class EmergencyButtonBlockerPlugin : BaseUnityPlugin
    {
        public const string PluginInfo.PLUGIN_GUID = "com.yourname.emergencybuttonblocker";
        public const string PluginInfo.PLUGIN_NAME = "Emergency Button Blocker";
        public const string PluginInfo.PLUGIN_VERSION = "1.0.0";

        private static ConfigEntry<bool> configEnabled;
        private static int gameRound = 0;
        private static bool firstMeetingCompleted = false;

        private void Awake()
        {
            // Load configuration
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the emergency button blocker");
            
            // Apply Harmony patches
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            Logger.LogInfo("Emergency Button Blocker loaded successfully!");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
        public static class EmergencyButtonPatch
        {
            public static bool Prefix(PlayerControl __instance, GameData.PlayerInfo target)
            {
                // Check if the plugin is enabled
                if (!configEnabled.Value)
                    return true;

                // Check if this is the first meeting
                if (!firstMeetingCompleted)
                {
                    // Log the attempt
                    Logger.LogInfo($"Emergency button call blocked in first round by {__instance.Data.PlayerName}");
                    
                    // Show message to player (optional)
                    if (__instance.AmOwner)
                    {
                        // You could add a popup message here if desired
                        Logger.LogInfo("Emergency button is disabled for the first round!");
                    }
                    
                    return false; // Block the call
                }

                return true; // Allow the call
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                // Mark that we've had at least one meeting
                if (!firstMeetingCompleted)
                {
                    firstMeetingCompleted = true;
                    Logger.LogInfo("First meeting completed - emergency button is now enabled");
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                // Reset for new game
                gameRound = 0;
                firstMeetingCompleted = false;
                Logger.LogInfo("New game started - emergency button blocked for first round");
            }
        }
    }
}
