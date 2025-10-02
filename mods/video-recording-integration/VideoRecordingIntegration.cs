using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.VideoRecordingIntegration
{
    [BepInPlugin("com.yourname.videorecordingintegration", "Video Recording Integration", "1.0.0")]
    public class VideoRecordingIntegrationPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<KeyCode> configRecordKey;
        private static ConfigEntry<KeyCode> configStopKey;
        private static ConfigEntry<bool> configAutoRecord;
        private static ConfigEntry<bool> configRecordOnGameStart;
        private static ConfigEntry<bool> configRecordOnKill;
        private static ConfigEntry<bool> configRecordOnVote;
        private static ConfigEntry<string> configVideoDirectory;
        private static ConfigEntry<string> configVideoFormat;
        private static ConfigEntry<int> configVideoQuality;
        private static ConfigEntry<int> configVideoFPS;
        private static ConfigEntry<int> configVideoBitrate;
        private static ConfigEntry<bool> configIncludeAudio;
        private static ConfigEntry<bool> configIncludeUI;
        private static ConfigEntry<bool> configIncludeChat;
        private static ConfigEntry<bool> configWatermark;
        private static ConfigEntry<string> configWatermarkText;
        private static ConfigEntry<int> configMaxRecordingTime;
        
        private static List<VideoRecording> recordings = new List<VideoRecording>();
        private static VideoRecording currentRecording;
        private static bool isRecording = false;
        private static bool isPaused = false;
        private static GameObject videoUI;
        private static int recordingCounter = 0;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable video recording integration");
            configRecordKey = Config.Bind("Controls", "RecordKey", KeyCode.F9, "Key to start recording");
            configStopKey = Config.Bind("Controls", "StopKey", KeyCode.F10, "Key to stop recording");
            configAutoRecord = Config.Bind("Auto", "AutoRecord", false, "Automatically start recording games");
            configRecordOnGameStart = Config.Bind("Auto", "RecordOnGameStart", true, "Start recording when game starts");
            configRecordOnKill = Config.Bind("Auto", "RecordOnKill", true, "Start recording when player is killed");
            configRecordOnVote = Config.Bind("Auto", "RecordOnVote", true, "Start recording during voting");
            configVideoDirectory = Config.Bind("Storage", "VideoDirectory", "Videos", "Directory to store video files");
            configVideoFormat = Config.Bind("Storage", "VideoFormat", "MP4", "Video format (MP4/AVI/MOV)");
            configVideoQuality = Config.Bind("Quality", "VideoQuality", 80, "Video quality (1-100)");
            configVideoFPS = Config.Bind("Quality", "VideoFPS", 30, "Video FPS");
            configVideoBitrate = Config.Bind("Quality", "VideoBitrate", 5000, "Video bitrate (kbps)");
            configIncludeAudio = Config.Bind("Audio", "IncludeAudio", true, "Include audio in recordings");
            configIncludeUI = Config.Bind("Display", "IncludeUI", false, "Include UI elements in recordings");
            configIncludeChat = Config.Bind("Display", "IncludeChat", true, "Include chat in recordings");
            configWatermark = Config.Bind("Display", "Watermark", true, "Add watermark to recordings");
            configWatermarkText = Config.Bind("Display", "WatermarkText", "Among Us Mod", "Watermark text");
            configMaxRecordingTime = Config.Bind("Limits", "MaxRecordingTime", 30, "Maximum recording time in minutes");
            
            var harmony = new Harmony("com.yourname.videorecordingintegration");
            harmony.PatchAll();
            
            InitializeVideoRecording();
            CommonUtilities.LogMessage("VideoRecordingIntegration", "Video Recording Integration loaded successfully!");
        }

        private static void InitializeVideoRecording()
        {
            // Create video directory if it doesn't exist
            string videoPath = Path.Combine(Application.persistentDataPath, configVideoDirectory.Value);
            if (!Directory.Exists(videoPath))
            {
                Directory.CreateDirectory(videoPath);
            }
            
            CreateVideoUI();
            CommonUtilities.LogMessage("VideoRecordingIntegration", "Video recording system initialized");
        }

        private static void CreateVideoUI()
        {
            videoUI = new GameObject("VideoUI");
            videoUI.transform.SetParent(HudManager.Instance.transform);
            videoUI.SetActive(false);
        }

        private void Update()
        {
            if (!configEnabled.Value) return;
            
            // Check for recording key press
            if (Input.GetKeyDown(configRecordKey.Value))
            {
                if (!isRecording)
                {
                    StartRecording("Manual");
                }
                else
                {
                    PauseRecording();
                }
            }
            
            // Check for stop key press
            if (Input.GetKeyDown(configStopKey.Value) && isRecording)
            {
                StopRecording();
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !configAutoRecord.Value || !configRecordOnGameStart.Value) return;
                
                StartRecording("GameStart");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class KillRecordingPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !configAutoRecord.Value || !configRecordOnKill.Value) return;
                
                if (!isRecording)
                {
                    StartRecording($"Kill_{target.Data.PlayerName}");
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class VoteRecordingPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !configAutoRecord.Value || !configRecordOnVote.Value) return;
                
                if (!isRecording)
                {
                    StartRecording("Voting_Started");
                }
            }
        }

        private static void StartRecording(string eventType)
        {
            try
            {
                if (isRecording) return;
                
                recordingCounter++;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"recording_{timestamp}_{eventType}_{recordingCounter}";
                string filePath = Path.Combine(Application.persistentDataPath, configVideoDirectory.Value, fileName);
                
                currentRecording = new VideoRecording
                {
                    FileName = fileName,
                    FilePath = filePath + "." + configVideoFormat.Value.ToLower(),
                    EventType = eventType,
                    StartTime = DateTime.Now,
                    Quality = configVideoQuality.Value,
                    FPS = configVideoFPS.Value,
                    Bitrate = configVideoBitrate.Value,
                    IncludeAudio = configIncludeAudio.Value,
                    IncludeUI = configIncludeUI.Value,
                    IncludeChat = configIncludeChat.Value,
                    Watermark = configWatermark.Value,
                    WatermarkText = configWatermarkText.Value
                };
                
                isRecording = true;
                isPaused = false;
                
                // Initialize video recording
                InitializeVideoCapture();
                
                CommonUtilities.SendChatMessage($"🎥 Recording started: {fileName}");
                CommonUtilities.LogMessage("VideoRecordingIntegration", $"Recording started: {fileName}");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("VideoRecordingIntegration", $"Error starting recording: {e.Message}");
            }
        }

        private static void InitializeVideoCapture()
        {
            // Initialize video capture system
            // This would integrate with actual video recording libraries
            CommonUtilities.LogMessage("VideoRecordingIntegration", "Video capture initialized");
        }

        private static void PauseRecording()
        {
            if (!isRecording) return;
            
            isPaused = !isPaused;
            string status = isPaused ? "paused" : "resumed";
            CommonUtilities.SendChatMessage($"🎥 Recording {status}");
        }

        private static void StopRecording()
        {
            if (!isRecording) return;
            
            try
            {
                currentRecording.EndTime = DateTime.Now;
                currentRecording.Duration = (currentRecording.EndTime - currentRecording.StartTime).TotalMinutes;
                
                // Finalize video recording
                FinalizeVideoCapture();
                
                recordings.Add(currentRecording);
                
                CommonUtilities.SendChatMessage($"🎥 Recording stopped: {currentRecording.FileName}");
                CommonUtilities.LogMessage("VideoRecordingIntegration", $"Recording stopped: {currentRecording.FileName}");
                
                isRecording = false;
                isPaused = false;
                currentRecording = null;
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("VideoRecordingIntegration", $"Error stopping recording: {e.Message}");
            }
        }

        private static void FinalizeVideoCapture()
        {
            // Finalize video capture and save file
            // This would integrate with actual video recording libraries
            CommonUtilities.LogMessage("VideoRecordingIntegration", "Video capture finalized");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for video recording commands
                if (chatText == "/record" || chatText == "/rec")
                {
                    if (!isRecording)
                    {
                        StartRecording("Chat_Command");
                    }
                    else
                    {
                        PauseRecording();
                    }
                    return false;
                }
                else if (chatText == "/stop" || chatText == "/stoprec")
                {
                    StopRecording();
                    return false;
                }
                else if (chatText == "/recordings" || chatText == "/videos")
                {
                    ShowRecordingList(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/rec "))
                {
                    string eventType = chatText.Substring(5);
                    StartRecording(eventType);
                    return false;
                }
                else if (chatText == "/recstatus")
                {
                    ShowRecordingStatus(__instance);
                    return false;
                }
                else if (chatText == "/rechelp")
                {
                    ShowRecordingHelp(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowRecordingList(PlayerControl player)
        {
            if (recordings.Count == 0)
            {
                CommonUtilities.SendChatMessage("No recordings found!");
                return;
            }
            
            CommonUtilities.SendChatMessage($"=== Recent Recordings ({recordings.Count}) ===");
            var recentRecordings = recordings.TakeLast(10).ToList();
            
            for (int i = 0; i < recentRecordings.Count; i++)
            {
                var recording = recentRecordings[i];
                CommonUtilities.SendChatMessage($"{i + 1}. {recording.FileName} ({recording.Duration:F1}m)");
            }
        }

        private static void ShowRecordingStatus(PlayerControl player)
        {
            if (!isRecording)
            {
                CommonUtilities.SendChatMessage("Not currently recording");
                return;
            }
            
            var duration = (DateTime.Now - currentRecording.StartTime).TotalMinutes;
            string status = isPaused ? "Paused" : "Recording";
            
            CommonUtilities.SendChatMessage($"=== Recording Status ===");
            CommonUtilities.SendChatMessage($"Status: {status}");
            CommonUtilities.SendChatMessage($"Duration: {duration:F1} minutes");
            CommonUtilities.SendChatMessage($"Event: {currentRecording.EventType}");
            CommonUtilities.SendChatMessage($"Quality: {currentRecording.Quality}%");
        }

        private static void ShowRecordingHelp(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Video Recording Commands ===");
            CommonUtilities.SendChatMessage($"{configRecordKey.Value} - Start/Pause recording");
            CommonUtilities.SendChatMessage($"{configStopKey.Value} - Stop recording");
            CommonUtilities.SendChatMessage("/record - Start/Pause recording");
            CommonUtilities.SendChatMessage("/stop - Stop recording");
            CommonUtilities.SendChatMessage("/recordings - Show recording list");
            CommonUtilities.SendChatMessage("/rec <event> - Start recording with custom event name");
            CommonUtilities.SendChatMessage("/recstatus - Show recording status");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class RecordingUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !isRecording || isPaused) return;
                
                // Check for maximum recording time
                if (currentRecording != null)
                {
                    var duration = (DateTime.Now - currentRecording.StartTime).TotalMinutes;
                    if (duration >= configMaxRecordingTime.Value)
                    {
                        CommonUtilities.SendChatMessage("🎥 Maximum recording time reached, stopping automatically");
                        StopRecording();
                    }
                }
                
                // Update recording progress
                UpdateRecordingProgress();
            }
        }

        private static void UpdateRecordingProgress()
        {
            // Update recording progress and handle any recording-specific updates
            if (currentRecording != null)
            {
                // This would handle real-time recording updates
            }
        }

        public static List<VideoRecording> GetRecordings()
        {
            return recordings;
        }

        public static VideoRecording GetCurrentRecording()
        {
            return currentRecording;
        }

        public static bool IsRecording()
        {
            return isRecording;
        }

        public static bool IsPaused()
        {
            return isPaused;
        }

        public static void ClearRecordings()
        {
            recordings.Clear();
            CommonUtilities.SendChatMessage("Recording history cleared!");
        }
    }

    [System.Serializable]
    public class VideoRecording
    {
        public string FileName;
        public string FilePath;
        public string EventType;
        public DateTime StartTime;
        public DateTime EndTime;
        public double Duration;
        public int Quality;
        public int FPS;
        public int Bitrate;
        public bool IncludeAudio;
        public bool IncludeUI;
        public bool IncludeChat;
        public bool Watermark;
        public string WatermarkText;
    }
}
