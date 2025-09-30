using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.ReplaySystem
{
    [BepInPlugin("com.yourname.replaysystem", "Replay System", "1.0.0")]
    public class ReplaySystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configAutoRecord;
        private static ConfigEntry<bool> configRecordChat;
        private static ConfigEntry<bool> configRecordMovement;
        private static ConfigEntry<bool> configRecordActions;
        private static ConfigEntry<string> configReplayDirectory;
        private static ConfigEntry<int> configMaxReplays;
        
        private static ReplayData currentReplay;
        private static bool isRecording = false;
        private static bool isPlaying = false;
        private static List<ReplayData> savedReplays = new List<ReplayData>();
        private static string replayDirectory;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the replay system");
            configAutoRecord = Config.Bind("Recording", "AutoRecord", true, "Automatically start recording games");
            configRecordChat = Config.Bind("Recording", "RecordChat", true, "Record chat messages");
            configRecordMovement = Config.Bind("Recording", "RecordMovement", true, "Record player movement");
            configRecordActions = Config.Bind("Recording", "RecordActions", true, "Record player actions");
            configReplayDirectory = Config.Bind("Storage", "ReplayDirectory", "Replays", "Directory to store replay files");
            configMaxReplays = Config.Bind("Storage", "MaxReplays", 50, "Maximum number of replays to keep");
            
            var harmony = new Harmony("com.yourname.replaysystem");
            harmony.PatchAll();
            
            InitializeReplaySystem();
            CommonUtilities.LogMessage("ReplaySystem", "Replay System loaded successfully!");
        }

        private static void InitializeReplaySystem()
        {
            replayDirectory = Path.Combine(Application.persistentDataPath, configReplayDirectory.Value);
            
            if (!Directory.Exists(replayDirectory))
            {
                Directory.CreateDirectory(replayDirectory);
            }
            
            LoadSavedReplays();
        }

        private static void LoadSavedReplays()
        {
            try
            {
                var replayFiles = Directory.GetFiles(replayDirectory, "*.json");
                foreach (var file in replayFiles)
                {
                    string jsonData = File.ReadAllText(file);
                    var replay = JsonUtility.FromJson<ReplayData>(jsonData);
                    savedReplays.Add(replay);
                }
                
                CommonUtilities.LogMessage("ReplaySystem", $"Loaded {savedReplays.Count} saved replays");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("ReplaySystem", $"Error loading replays: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !configAutoRecord.Value) return;
                
                StartRecording();
            }
        }

        private static void StartRecording()
        {
            if (isRecording) return;
            
            currentReplay = new ReplayData
            {
                GameId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now,
                Players = new List<PlayerReplayData>(),
                Events = new List<ReplayEvent>(),
                ChatMessages = new List<ChatMessage>()
            };
            
            // Initialize player data
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.Disconnected)
                {
                    currentReplay.Players.Add(new PlayerReplayData
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.Data.PlayerName,
                        IsImpostor = player.Data.Role.IsImpostor,
                        StartPosition = player.transform.position
                    });
                }
            }
            
            isRecording = true;
            CommonUtilities.LogMessage("ReplaySystem", "Started recording game");
        }

        [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
        public static class GameEndPatch
        {
            public static void Postfix(EndGameManager __instance)
            {
                if (!configEnabled.Value || !isRecording) return;
                
                StopRecording(__instance);
            }
        }

        private static void StopRecording(EndGameManager endGameManager)
        {
            if (!isRecording) return;
            
            currentReplay.EndTime = DateTime.Now;
            currentReplay.GameResult = endGameManager.GameOverReason.ToString();
            currentReplay.Duration = (currentReplay.EndTime - currentReplay.StartTime).TotalSeconds;
            
            SaveReplay();
            isRecording = false;
            
            CommonUtilities.LogMessage("ReplaySystem", $"Stopped recording game (Duration: {currentReplay.Duration:F1}s)");
        }

        private static void SaveReplay()
        {
            try
            {
                // Clean up old replays if we exceed the limit
                if (savedReplays.Count >= configMaxReplays.Value)
                {
                    var oldestReplay = savedReplays.OrderBy(r => r.StartTime).First();
                    savedReplays.Remove(oldestReplay);
                    
                    string oldFile = Path.Combine(replayDirectory, $"{oldestReplay.GameId}.json");
                    if (File.Exists(oldFile))
                    {
                        File.Delete(oldFile);
                    }
                }
                
                // Save new replay
                savedReplays.Add(currentReplay);
                string jsonData = JsonUtility.ToJson(currentReplay, true);
                string fileName = Path.Combine(replayDirectory, $"{currentReplay.GameId}.json");
                File.WriteAllText(fileName, jsonData);
                
                CommonUtilities.LogMessage("ReplaySystem", $"Saved replay: {currentReplay.GameId}");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("ReplaySystem", $"Error saving replay: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static void Postfix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !isRecording || !configRecordChat.Value) return;
                
                currentReplay.ChatMessages.Add(new ChatMessage
                {
                    Timestamp = DateTime.Now,
                    PlayerId = __instance.PlayerId,
                    PlayerName = __instance.Data.PlayerName,
                    Message = chatText
                });
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !isRecording || !configRecordActions.Value) return;
                
                currentReplay.Events.Add(new ReplayEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = "Murder",
                    PlayerId = __instance.PlayerId,
                    TargetId = target.PlayerId,
                    Description = $"{__instance.Data.PlayerName} killed {target.Data.PlayerName}"
                });
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskCompletePatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !isRecording || !configRecordActions.Value) return;
                
                currentReplay.Events.Add(new ReplayEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = "TaskComplete",
                    PlayerId = __instance.PlayerId,
                    Description = $"{__instance.Data.PlayerName} completed a task"
                });
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for replay commands
                if (chatText == "/replays" || chatText == "/listreplays")
                {
                    ShowReplayList(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/playreplay "))
                {
                    string replayId = chatText.Substring(12);
                    PlayReplay(__instance, replayId);
                    return false;
                }
                else if (chatText == "/stopreplay")
                {
                    StopReplay(__instance);
                    return false;
                }
                else if (chatText == "/startrecording")
                {
                    StartRecording();
                    CommonUtilities.SendChatMessage("Started recording current game");
                    return false;
                }
                else if (chatText == "/stoprecording")
                {
                    if (isRecording)
                    {
                        isRecording = false;
                        CommonUtilities.SendChatMessage("Stopped recording");
                    }
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowReplayList(PlayerControl player)
        {
            if (savedReplays.Count == 0)
            {
                CommonUtilities.SendChatMessage("No replays available");
                return;
            }
            
            CommonUtilities.SendChatMessage($"=== Available Replays ({savedReplays.Count}) ===");
            var recentReplays = savedReplays.OrderByDescending(r => r.StartTime).Take(5);
            
            foreach (var replay in recentReplays)
            {
                string duration = TimeSpan.FromSeconds(replay.Duration).ToString(@"mm\:ss");
                CommonUtilities.SendChatMessage($"{replay.GameId}: {replay.StartTime:HH:mm} ({duration}) - {replay.GameResult}");
            }
        }

        private static void PlayReplay(PlayerControl player, string replayId)
        {
            var replay = savedReplays.FirstOrDefault(r => r.GameId == replayId);
            if (replay == null)
            {
                CommonUtilities.SendChatMessage($"Replay '{replayId}' not found");
                return;
            }
            
            if (isPlaying)
            {
                CommonUtilities.SendChatMessage("Already playing a replay. Use /stopreplay first.");
                return;
            }
            
            StartReplayPlayback(replay);
            CommonUtilities.SendChatMessage($"Playing replay: {replayId}");
        }

        private static void StartReplayPlayback(ReplayData replay)
        {
            isPlaying = true;
            // Replay playback logic would go here
            CommonUtilities.LogMessage("ReplaySystem", $"Started playback of replay: {replay.GameId}");
        }

        private static void StopReplay(PlayerControl player)
        {
            if (!isPlaying)
            {
                CommonUtilities.SendChatMessage("No replay is currently playing");
                return;
            }
            
            isPlaying = false;
            CommonUtilities.SendChatMessage("Stopped replay playback");
        }
    }

    [System.Serializable]
    public class ReplayData
    {
        public string GameId;
        public DateTime StartTime;
        public DateTime EndTime;
        public double Duration;
        public string GameResult;
        public List<PlayerReplayData> Players;
        public List<ReplayEvent> Events;
        public List<ChatMessage> ChatMessages;
    }

    [System.Serializable]
    public class PlayerReplayData
    {
        public byte PlayerId;
        public string PlayerName;
        public bool IsImpostor;
        public Vector3 StartPosition;
    }

    [System.Serializable]
    public class ReplayEvent
    {
        public DateTime Timestamp;
        public string EventType;
        public byte PlayerId;
        public byte TargetId;
        public string Description;
    }

    [System.Serializable]
    public class ChatMessage
    {
        public DateTime Timestamp;
        public byte PlayerId;
        public string PlayerName;
        public string Message;
    }
}
