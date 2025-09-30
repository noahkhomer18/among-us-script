using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.VoiceChatIntegration
{
    [BepInPlugin("com.yourname.voicechatintegration", "Voice Chat Integration", "1.0.0")]
    public class VoiceChatIntegrationPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configProximityVoice;
        private static ConfigEntry<float> configVoiceRange;
        private static ConfigEntry<bool> configDeadCanHear;
        private static ConfigEntry<bool> configDeadCanSpeak;
        private static ConfigEntry<bool> configMeetingVoice;
        private static ConfigEntry<float> configVoiceVolume;
        private static ConfigEntry<bool> configPushToTalk;
        private static ConfigEntry<KeyCode> configPushToTalkKey;
        
        private static Dictionary<byte, VoicePlayer> voicePlayers = new Dictionary<byte, VoicePlayer>();
        private static Dictionary<byte, float> playerDistances = new Dictionary<byte, float>();
        private static bool isVoiceEnabled = false;
        private static AudioSource voiceAudioSource;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable voice chat integration");
            configProximityVoice = Config.Bind("Voice", "ProximityVoice", true, "Enable proximity-based voice chat");
            configVoiceRange = Config.Bind("Voice", "VoiceRange", 5f, "Range for proximity voice chat");
            configDeadCanHear = Config.Bind("Voice", "DeadCanHear", false, "Dead players can hear living players");
            configDeadCanSpeak = Config.Bind("Voice", "DeadCanSpeak", false, "Dead players can speak");
            configMeetingVoice = Config.Bind("Voice", "MeetingVoice", true, "Enable voice chat during meetings");
            configVoiceVolume = Config.Bind("Audio", "VoiceVolume", 1f, "Voice chat volume");
            configPushToTalk = Config.Bind("Controls", "PushToTalk", true, "Enable push-to-talk");
            configPushToTalkKey = Config.Bind("Controls", "PushToTalkKey", KeyCode.V, "Push-to-talk key");
            
            var harmony = new Harmony("com.yourname.voicechatintegration");
            harmony.PatchAll();
            
            InitializeVoiceSystem();
            CommonUtilities.LogMessage("VoiceChatIntegration", "Voice Chat Integration loaded successfully!");
        }

        private static void InitializeVoiceSystem()
        {
            // Initialize voice audio source
            voiceAudioSource = new GameObject("VoiceAudioSource").AddComponent<AudioSource>();
            voiceAudioSource.volume = configVoiceVolume.Value;
            voiceAudioSource.spatialBlend = 1f; // 3D audio
            
            // Initialize voice players
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.Disconnected)
                {
                    InitializeVoicePlayer(player);
                }
            }
            
            isVoiceEnabled = true;
            CommonUtilities.LogMessage("VoiceChatIntegration", "Voice system initialized");
        }

        private static void InitializeVoicePlayer(PlayerControl player)
        {
            var voicePlayer = new VoicePlayer
            {
                PlayerId = player.PlayerId,
                PlayerName = player.Data.PlayerName,
                IsDead = player.Data.IsDead,
                IsMuted = false,
                IsSpeaking = false,
                VoiceLevel = 1f,
                LastPosition = player.transform.position
            };
            
            voicePlayers[player.PlayerId] = voicePlayer;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class VoiceUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !isVoiceEnabled) return;
                
                UpdateVoicePlayer(__instance);
                UpdateProximityVoice(__instance);
            }
        }

        private static void UpdateVoicePlayer(PlayerControl player)
        {
            if (!voicePlayers.ContainsKey(player.PlayerId)) return;
            
            var voicePlayer = voicePlayers[player.PlayerId];
            voicePlayer.IsDead = player.Data.IsDead;
            voicePlayer.LastPosition = player.transform.position;
            
            // Update speaking status based on push-to-talk
            if (configPushToTalk.Value)
            {
                voicePlayer.IsSpeaking = Input.GetKey(configPushToTalkKey.Value);
            }
        }

        private static void UpdateProximityVoice(PlayerControl localPlayer)
        {
            if (!configProximityVoice.Value) return;
            
            var localVoicePlayer = voicePlayers[localPlayer.PlayerId];
            if (localVoicePlayer == null) return;
            
            // Calculate distances to other players
            foreach (var otherPlayer in PlayerControl.AllPlayerControls)
            {
                if (otherPlayer.PlayerId == localPlayer.PlayerId) continue;
                if (otherPlayer.Data.Disconnected) continue;
                
                float distance = Vector3.Distance(localPlayer.transform.position, otherPlayer.transform.position);
                playerDistances[otherPlayer.PlayerId] = distance;
                
                // Determine if other player should be audible
                bool shouldHear = ShouldHearPlayer(localPlayer, otherPlayer, distance);
                UpdatePlayerVoiceVolume(otherPlayer.PlayerId, shouldHear, distance);
            }
        }

        private static bool ShouldHearPlayer(PlayerControl listener, PlayerControl speaker, float distance)
        {
            // Check if within voice range
            if (distance > configVoiceRange.Value) return false;
            
            // Check if speaker is speaking
            if (!voicePlayers.ContainsKey(speaker.PlayerId)) return false;
            var speakerVoice = voicePlayers[speaker.PlayerId];
            if (!speakerVoice.IsSpeaking) return false;
            
            // Check if speaker is muted
            if (speakerVoice.IsMuted) return false;
            
            // Check dead player restrictions
            if (listener.Data.IsDead && !configDeadCanHear.Value) return false;
            if (speaker.Data.IsDead && !configDeadCanSpeak.Value) return false;
            
            return true;
        }

        private static void UpdatePlayerVoiceVolume(byte playerId, bool shouldHear, float distance)
        {
            if (!voicePlayers.ContainsKey(playerId)) return;
            
            var voicePlayer = voicePlayers[playerId];
            
            if (shouldHear)
            {
                // Calculate volume based on distance
                float volume = Mathf.Clamp01(1f - (distance / configVoiceRange.Value));
                voicePlayer.VoiceLevel = volume * configVoiceVolume.Value;
            }
            else
            {
                voicePlayer.VoiceLevel = 0f;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !configMeetingVoice.Value) return;
                
                EnableMeetingVoice();
            }
        }

        private static void EnableMeetingVoice()
        {
            // Enable voice chat for all players during meetings
            foreach (var voicePlayer in voicePlayers.Values)
            {
                voicePlayer.VoiceLevel = configVoiceVolume.Value;
            }
            
            CommonUtilities.SendChatMessage("Voice chat enabled for meeting!");
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
        public static class MeetingEndPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                DisableMeetingVoice();
            }
        }

        private static void DisableMeetingVoice()
        {
            // Reset voice levels to proximity-based
            foreach (var voicePlayer in voicePlayers.Values)
            {
                voicePlayer.VoiceLevel = 0f;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class PlayerDeathPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value) return;
                
                UpdateDeadPlayerVoice(target);
            }
        }

        private static void UpdateDeadPlayerVoice(PlayerControl deadPlayer)
        {
            if (!voicePlayers.ContainsKey(deadPlayer.PlayerId)) return;
            
            var voicePlayer = voicePlayers[deadPlayer.PlayerId];
            voicePlayer.IsDead = true;
            
            // Apply dead player voice restrictions
            if (!configDeadCanSpeak.Value)
            {
                voicePlayer.IsMuted = true;
            }
            
            CommonUtilities.SendChatMessage($"{deadPlayer.Data.PlayerName} is now {(configDeadCanSpeak.Value ? "ghost" : "muted")}");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for voice chat commands
                if (chatText == "/voice" || chatText == "/vc")
                {
                    ShowVoiceStatus(__instance);
                    return false;
                }
                else if (chatText == "/mute" || chatText == "/unmute")
                {
                    ToggleMute(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/mute "))
                {
                    string targetName = chatText.Substring(6);
                    MutePlayer(__instance, targetName);
                    return false;
                }
                else if (chatText == "/voicehelp")
                {
                    ShowVoiceHelp(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowVoiceStatus(PlayerControl player)
        {
            if (!voicePlayers.ContainsKey(player.PlayerId))
            {
                CommonUtilities.SendChatMessage("Voice system not initialized");
                return;
            }
            
            var voicePlayer = voicePlayers[player.PlayerId];
            string status = voicePlayer.IsMuted ? "Muted" : "Unmuted";
            string speaking = voicePlayer.IsSpeaking ? "Speaking" : "Not speaking";
            
            CommonUtilities.SendChatMessage($"Voice Status: {status} | {speaking}");
            CommonUtilities.SendChatMessage($"Voice Level: {voicePlayer.VoiceLevel:F2}");
        }

        private static void ToggleMute(PlayerControl player)
        {
            if (!voicePlayers.ContainsKey(player.PlayerId)) return;
            
            var voicePlayer = voicePlayers[player.PlayerId];
            voicePlayer.IsMuted = !voicePlayer.IsMuted;
            
            string status = voicePlayer.IsMuted ? "muted" : "unmuted";
            CommonUtilities.SendChatMessage($"You are now {status}");
        }

        private static void MutePlayer(PlayerControl player, string targetName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can mute other players!");
                return;
            }
            
            var targetPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => 
                p.Data.PlayerName.ToLower().Contains(targetName.ToLower()));
            
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found");
                return;
            }
            
            if (!voicePlayers.ContainsKey(targetPlayer.PlayerId)) return;
            
            var voicePlayer = voicePlayers[targetPlayer.PlayerId];
            voicePlayer.IsMuted = !voicePlayer.IsMuted;
            
            string status = voicePlayer.IsMuted ? "muted" : "unmuted";
            CommonUtilities.SendChatMessage($"{targetPlayer.Data.PlayerName} is now {status}");
        }

        private static void ShowVoiceHelp(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Voice Chat Commands ===");
            CommonUtilities.SendChatMessage("/voice - Show voice status");
            CommonUtilities.SendChatMessage("/mute - Toggle your mute");
            CommonUtilities.SendChatMessage("/mute <player> - Mute/unmute player (host only)");
            CommonUtilities.SendChatMessage("Push-to-talk: Hold V key to speak");
        }

        private void Update()
        {
            if (!configEnabled.Value || !isVoiceEnabled) return;
            
            // Update voice system
            UpdateVoiceSystem();
        }

        private static void UpdateVoiceSystem()
        {
            // Update voice audio processing
            foreach (var voicePlayer in voicePlayers.Values)
            {
                if (voicePlayer.IsSpeaking && !voicePlayer.IsMuted)
                {
                    // Process voice audio
                    ProcessVoiceAudio(voicePlayer);
                }
            }
        }

        private static void ProcessVoiceAudio(VoicePlayer voicePlayer)
        {
            // This would integrate with actual voice chat system
            // For now, we'll just log the voice activity
            if (voicePlayer.VoiceLevel > 0.1f)
            {
                // Voice is being transmitted
            }
        }
    }

    [System.Serializable]
    public class VoicePlayer
    {
        public byte PlayerId;
        public string PlayerName;
        public bool IsDead;
        public bool IsMuted;
        public bool IsSpeaking;
        public float VoiceLevel;
        public Vector3 LastPosition;
    }
}
