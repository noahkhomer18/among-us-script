using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.SpeedRole
{
    [BepInPlugin("com.yourname.speedrole", "Speed Role", "1.0.0")]
    public class SpeedRolePlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<float> configSpeedMultiplier;
        private static ConfigEntry<bool> configSpeedPlayerCanKill;
        private static ConfigEntry<bool> configSpeedPlayerCanVent;
        private static ConfigEntry<bool> configShowSpeedIndicator;
        private static ConfigEntry<float> configSpeedCooldown;
        
        private static Dictionary<byte, float> originalSpeeds = new Dictionary<byte, float>();
        private static byte speedPlayerId = 0;
        private static bool gameStarted = false;
        private static float lastSpeedUse = 0f;
        private static bool speedActive = false;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable Speed Role mod");
            configSpeedMultiplier = Config.Bind("Speed", "SpeedMultiplier", 3.0f, "Speed multiplier for the speed player");
            configSpeedPlayerCanKill = Config.Bind("Speed", "SpeedPlayerCanKill", true, "Allow speed player to kill other players");
            configSpeedPlayerCanVent = Config.Bind("Speed", "SpeedPlayerCanVent", true, "Allow speed player to use vents");
            configShowSpeedIndicator = Config.Bind("UI", "ShowSpeedIndicator", true, "Show speed indicator to other players");
            configSpeedCooldown = Config.Bind("Speed", "SpeedCooldown", 5.0f, "Cooldown between speed activations in seconds");
            
            var harmony = new Harmony("com.yourname.speedrole");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("SpeedRole", "Speed Role mod loaded successfully!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                InitializeSpeedGame();
            }
        }

        private static void InitializeSpeedGame()
        {
            gameStarted = true;
            originalSpeeds.Clear();
            speedActive = false;
            lastSpeedUse = 0f;
            
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < 2) return;
            
            // Select one random player as the speed player
            var shuffledPlayers = alivePlayers.OrderBy(x => UnityEngine.Random.value).ToList();
            speedPlayerId = shuffledPlayers[0].PlayerId;
            
            // Store original speeds
            foreach (var player in alivePlayers)
            {
                originalSpeeds[player.PlayerId] = player.MyPhysics.Speed;
            }
            
            CommonUtilities.SendChatMessage("⚡ SPEED ROLE GAME BEGINS!");
            CommonUtilities.SendChatMessage("🏃 One player has FLASH SPEED abilities!");
            CommonUtilities.SendChatMessage("🐌 Everyone else moves at normal speed!");
            CommonUtilities.SendChatMessage("💨 Use your speed wisely - it has a cooldown!");
            
            // Notify the speed player privately
            foreach (var player in alivePlayers)
            {
                if (player.PlayerId == speedPlayerId)
                {
                    CommonUtilities.SendChatMessage("⚡ YOU are the speed player! Press SHIFT to activate speed!");
                    CommonUtilities.SendChatMessage("💨 You can move 3x faster than everyone else!");
                    if (configSpeedPlayerCanKill.Value)
                    {
                        CommonUtilities.SendChatMessage("🗡️ You can kill other players!");
                    }
                    if (configSpeedPlayerCanVent.Value)
                    {
                        CommonUtilities.SendChatMessage("🕳️ You can use vents!");
                    }
                }
                else
                {
                    CommonUtilities.SendChatMessage("🐌 You move at normal speed - watch out for the speed player!");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class SpeedUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                // Apply speed modifications
                if (__instance.PlayerId == speedPlayerId)
                {
                    // Speed player gets enhanced speed when active
                    if (speedActive)
                    {
                        __instance.MyPhysics.Speed = originalSpeeds[__instance.PlayerId] * configSpeedMultiplier.Value;
                    }
                    else
                    {
                        __instance.MyPhysics.Speed = originalSpeeds[__instance.PlayerId];
                    }
                }
                else
                {
                    // Other players maintain normal speed
                    if (originalSpeeds.ContainsKey(__instance.PlayerId))
                    {
                        __instance.MyPhysics.Speed = originalSpeeds[__instance.PlayerId];
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Update))]
        public static class InputUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !gameStarted || __instance.PlayerId != speedPlayerId) return;
                
                // Check for speed activation input (Shift key)
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                {
                    ToggleSpeed();
                }
            }
        }

        private static void ToggleSpeed()
        {
            if (Time.time - lastSpeedUse < configSpeedCooldown.Value)
            {
                CommonUtilities.SendChatMessage($"⏰ Speed cooldown active! Wait {configSpeedCooldown.Value - (Time.time - lastSpeedUse):F1} seconds");
                return;
            }
            
            speedActive = !speedActive;
            lastSpeedUse = Time.time;
            
            if (speedActive)
            {
                CommonUtilities.SendChatMessage("💨 SPEED ACTIVATED! You're moving at 3x speed!");
                if (configShowSpeedIndicator.Value)
                {
                    // Show speed indicator to other players
                    CommonUtilities.SendChatMessage("⚡ The speed player is now moving at high speed!");
                }
            }
            else
            {
                CommonUtilities.SendChatMessage("🐌 Speed deactivated - back to normal speed");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static bool Prefix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !gameStarted) return true;
                
                // Only speed player can kill if enabled
                if (__instance.PlayerId == speedPlayerId)
                {
                    if (!configSpeedPlayerCanKill.Value)
                    {
                        CommonUtilities.SendChatMessage("❌ Speed player cannot kill in this mode!");
                        return false;
                    }
                    
                    if (!speedActive)
                    {
                        CommonUtilities.SendChatMessage("💨 Activate speed first to kill!");
                        return false;
                    }
                    
                    CommonUtilities.SendChatMessage($"⚡ {__instance.Data.PlayerName} (Speed Player) eliminated {target.Data.PlayerName}!");
                    return true;
                }
                else
                {
                    // Other players cannot kill
                    CommonUtilities.SendChatMessage("❌ Only the speed player can eliminate others!");
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetHat))]
        public static class VentPatch
        {
            public static bool Prefix(PlayerControl __instance, string hatId)
            {
                if (!configEnabled.Value || !gameStarted) return true;
                
                // Check if this is a vent interaction
                if (__instance.PlayerId == speedPlayerId && !configSpeedPlayerCanVent.Value)
                {
                    CommonUtilities.SendChatMessage("❌ Speed player cannot use vents in this mode!");
                    return false;
                }
                
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletionPatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                // Speed player gets bonus for completing tasks while speed is active
                if (__instance.PlayerId == speedPlayerId && speedActive)
                {
                    CommonUtilities.SendChatMessage($"⚡ {__instance.Data.PlayerName} completed a task at high speed! Bonus points!");
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                // Deactivate speed during meetings
                if (speedActive)
                {
                    speedActive = false;
                    CommonUtilities.SendChatMessage("🐌 Speed deactivated during meeting");
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
        public static class VotingCompletePatch
        {
            public static void Postfix(MeetingHud __instance, byte[] states, byte[] votes, byte exiledPlayerId, bool tie)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                if (exiledPlayerId != 255) // Someone was voted out
                {
                    var exiledPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == exiledPlayerId);
                    if (exiledPlayer != null)
                    {
                        if (exiledPlayerId == speedPlayerId)
                        {
                            // Speed player was voted out - assign new speed player
                            AssignNewSpeedPlayer();
                        }
                    }
                }
            }
        }

        private static void AssignNewSpeedPlayer()
        {
            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < 2) return;
            
            // Select new speed player
            var shuffledPlayers = alivePlayers.OrderBy(x => UnityEngine.Random.value).ToList();
            speedPlayerId = shuffledPlayers[0].PlayerId;
            
            CommonUtilities.SendChatMessage($"⚡ {PlayerControl.AllPlayerControls.First(p => p.PlayerId == speedPlayerId).Data.PlayerName} is now the speed player!");
            CommonUtilities.SendChatMessage("💨 Press SHIFT to activate your speed abilities!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.EndGame))]
        public static class GameEndPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Reset when game ends
                gameStarted = false;
                originalSpeeds.Clear();
                speedActive = false;
                speedPlayerId = 0;
            }
        }

        // Chat commands
        [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
        public static class ChatCommandPatch
        {
            public static void Postfix(ChatController __instance, PlayerControl sourcePlayer, string chatText)
            {
                if (!configEnabled.Value || !gameStarted) return;
                
                if (chatText.StartsWith("/speed"))
                {
                    if (sourcePlayer.PlayerId == speedPlayerId)
                    {
                        CommonUtilities.SendChatMessage("⚡ You are the speed player! Press SHIFT to activate speed!");
                        CommonUtilities.SendChatMessage($"💨 Current speed status: {(speedActive ? "ACTIVE" : "INACTIVE")}");
                        if (Time.time - lastSpeedUse < configSpeedCooldown.Value)
                        {
                            CommonUtilities.SendChatMessage($"⏰ Cooldown: {configSpeedCooldown.Value - (Time.time - lastSpeedUse):F1} seconds");
                        }
                    }
                    else
                    {
                        CommonUtilities.SendChatMessage("🐌 You move at normal speed - watch out for the speed player!");
                    }
                }
                else if (chatText.StartsWith("/speedstatus"))
                {
                    var speedPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == speedPlayerId);
                    if (speedPlayer != null)
                    {
                        CommonUtilities.SendChatMessage($"⚡ Speed Player: {speedPlayer.Data.PlayerName}");
                        CommonUtilities.SendChatMessage($"💨 Speed Status: {(speedActive ? "ACTIVE" : "INACTIVE")}");
                    }
                }
                else if (chatText.StartsWith("/speedhelp"))
                {
                    CommonUtilities.SendChatMessage("⚡ Speed Role Commands:");
                    CommonUtilities.SendChatMessage("• /speed - Check your speed status");
                    CommonUtilities.SendChatMessage("• /speedstatus - Check speed player status");
                    CommonUtilities.SendChatMessage("• Press SHIFT to activate/deactivate speed");
                }
            }
        }
    }
}
