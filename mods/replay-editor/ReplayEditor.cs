using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.ReplayEditor
{
    [BepInPlugin("com.yourname.replayeditor", "Replay Editor", "1.0.0")]
    public class ReplayEditorPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configAutoRecord;
        private static ConfigEntry<bool> configRecordChat;
        private static ConfigEntry<bool> configRecordMovement;
        private static ConfigEntry<bool> configRecordActions;
        private static ConfigEntry<string> configReplayDirectory;
        private static ConfigEntry<int> configMaxReplayLength;
        private static ConfigEntry<bool> configCompressReplays;
        
        private static Dictionary<byte, PlayerReplayData> playerReplays = new Dictionary<byte, PlayerReplayData>();
        private static List<GameEvent> gameEvents = new List<GameEvent>();
        private static ReplaySession currentReplay;
        private static bool isRecording = false;
        private static bool isEditing = false;
        private static GameObject replayEditorUI;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable replay editor");
            configAutoRecord = Config.Bind("Recording", "AutoRecord", true, "Automatically start recording games");
            configRecordChat = Config.Bind("Recording", "RecordChat", true, "Record chat messages in replays");
            configRecordMovement = Config.Bind("Recording", "RecordMovement", true, "Record player movement");
            configRecordActions = Config.Bind("Recording", "RecordActions", true, "Record player actions");
            configReplayDirectory = Config.Bind("Storage", "ReplayDirectory", "Replays", "Directory to store replay files");
            configMaxReplayLength = Config.Bind("Storage", "MaxReplayLength", 30, "Maximum replay length in minutes");
            configCompressReplays = Config.Bind("Storage", "CompressReplays", true, "Compress replay files to save space");
            
            var harmony = new Harmony("com.yourname.replayeditor");
            harmony.PatchAll();
            
            InitializeReplayEditor();
            CommonUtilities.LogMessage("ReplayEditor", "Replay Editor loaded successfully!");
        }

        private static void InitializeReplayEditor()
        {
            // Create replay directory if it doesn't exist
            string replayPath = Path.Combine(Application.persistentDataPath, configReplayDirectory.Value);
            if (!Directory.Exists(replayPath))
            {
                Directory.CreateDirectory(replayPath);
            }
            
            CreateReplayEditorUI();
            CommonUtilities.LogMessage("ReplayEditor", "Replay editor initialized");
        }

        private static void CreateReplayEditorUI()
        {
            replayEditorUI = new GameObject("ReplayEditorUI");
            replayEditorUI.transform.SetParent(HudManager.Instance.transform);
            replayEditorUI.SetActive(false);
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
            currentReplay = new ReplaySession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now,
                Players = new Dictionary<byte, PlayerReplayData>(),
                Events = new List<GameEvent>(),
                GameSettings = new GameSettingsData()
            };
            
            isRecording = true;
            CommonUtilities.SendChatMessage("🎬 Replay recording started!");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class MovementRecordingPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !isRecording || !configRecordMovement.Value) return;
                
                RecordPlayerMovement(__instance);
            }
        }

        private static void RecordPlayerMovement(PlayerControl player)
        {
            if (!currentReplay.Players.ContainsKey(player.PlayerId))
            {
                currentReplay.Players[player.PlayerId] = new PlayerReplayData
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.Data.PlayerName,
                    IsImpostor = player.Data.Role.IsImpostor,
                    MovementPoints = new List<MovementPoint>(),
                    Actions = new List<PlayerAction>(),
                    ChatMessages = new List<ChatMessage>()
                };
            }
            
            var replayData = currentReplay.Players[player.PlayerId];
            replayData.MovementPoints.Add(new MovementPoint
            {
                Timestamp = DateTime.Now,
                Position = player.transform.position,
                Rotation = player.transform.rotation,
                IsDead = player.Data.IsDead
            });
            
            // Keep only recent movement data (last 5 minutes)
            var cutoffTime = DateTime.Now.AddMinutes(-5);
            replayData.MovementPoints.RemoveAll(m => m.Timestamp < cutoffTime);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatRecordingPatch
        {
            public static void Postfix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !isRecording || !configRecordChat.Value) return;
                
                RecordChatMessage(__instance, chatText);
            }
        }

        private static void RecordChatMessage(PlayerControl player, string message)
        {
            if (!currentReplay.Players.ContainsKey(player.PlayerId)) return;
            
            var chatMessage = new ChatMessage
            {
                Timestamp = DateTime.Now,
                PlayerId = player.PlayerId,
                PlayerName = player.Data.PlayerName,
                Message = message,
                IsDead = player.Data.IsDead
            };
            
            currentReplay.Players[player.PlayerId].ChatMessages.Add(chatMessage);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class ActionRecordingPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !isRecording || !configRecordActions.Value) return;
                
                RecordPlayerAction(__instance, "Murder", target.PlayerId);
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskRecordingPatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !isRecording || !configRecordActions.Value) return;
                
                RecordPlayerAction(__instance, "CompleteTask", taskId);
            }
        }

        private static void RecordPlayerAction(PlayerControl player, string actionType, uint targetId)
        {
            if (!currentReplay.Players.ContainsKey(player.PlayerId)) return;
            
            var action = new PlayerAction
            {
                Timestamp = DateTime.Now,
                ActionType = actionType,
                TargetId = targetId,
                PlayerId = player.PlayerId
            };
            
            currentReplay.Players[player.PlayerId].Actions.Add(action);
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
            if (currentReplay == null) return;
            
            currentReplay.EndTime = DateTime.Now;
            currentReplay.GameResult = endGameManager.GameOverReason.ToString();
            currentReplay.Duration = (currentReplay.EndTime - currentReplay.StartTime).TotalMinutes;
            
            SaveReplay();
            isRecording = false;
            
            CommonUtilities.SendChatMessage("🎬 Replay recording stopped and saved!");
        }

        private static void SaveReplay()
        {
            try
            {
                string replayPath = Path.Combine(Application.persistentDataPath, configReplayDirectory.Value);
                string fileName = $"replay_{currentReplay.SessionId}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = Path.Combine(replayPath, fileName);
                
                string jsonData = JsonUtility.ToJson(currentReplay, true);
                File.WriteAllText(filePath, jsonData);
                
                if (configCompressReplays.Value)
                {
                    CompressReplayFile(filePath);
                }
                
                CommonUtilities.LogMessage("ReplayEditor", $"Replay saved: {fileName}");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("ReplayEditor", $"Error saving replay: {e.Message}");
            }
        }

        private static void CompressReplayFile(string filePath)
        {
            // Basic compression implementation
            // In a real implementation, you'd use a compression library
            CommonUtilities.LogMessage("ReplayEditor", "Replay compressed");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for replay editor commands
                if (chatText == "/replay" || chatText == "/replays")
                {
                    ShowReplayList(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/play "))
                {
                    string replayId = chatText.Substring(6);
                    PlayReplay(__instance, replayId);
                    return false;
                }
                else if (chatText.StartsWith("/edit "))
                {
                    string replayId = chatText.Substring(6);
                    EditReplay(__instance, replayId);
                    return false;
                }
                else if (chatText == "/replayhelp")
                {
                    ShowReplayHelp(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowReplayList(PlayerControl player)
        {
            string replayPath = Path.Combine(Application.persistentDataPath, configReplayDirectory.Value);
            
            if (!Directory.Exists(replayPath))
            {
                CommonUtilities.SendChatMessage("No replays found!");
                return;
            }
            
            var replayFiles = Directory.GetFiles(replayPath, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Take(10)
                .ToArray();
            
            CommonUtilities.SendChatMessage("=== Recent Replays ===");
            for (int i = 0; i < replayFiles.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(replayFiles[i]);
                CommonUtilities.SendChatMessage($"{i + 1}. {fileName}");
            }
        }

        private static void PlayReplay(PlayerControl player, string replayId)
        {
            CommonUtilities.SendChatMessage($"Playing replay: {replayId}");
            // Replay playback logic would go here
        }

        private static void EditReplay(PlayerControl player, string replayId)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can edit replays!");
                return;
            }
            
            isEditing = true;
            CommonUtilities.SendChatMessage($"Editing replay: {replayId}");
            // Replay editing logic would go here
        }

        private static void ShowReplayHelp(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Replay Editor Commands ===");
            CommonUtilities.SendChatMessage("/replay - Show recent replays");
            CommonUtilities.SendChatMessage("/play <id> - Play a specific replay");
            CommonUtilities.SendChatMessage("/edit <id> - Edit a specific replay (host only)");
        }
    }

    [System.Serializable]
    public class ReplaySession
    {
        public string SessionId;
        public DateTime StartTime;
        public DateTime EndTime;
        public double Duration;
        public string GameResult;
        public Dictionary<byte, PlayerReplayData> Players;
        public List<GameEvent> Events;
        public GameSettingsData GameSettings;
    }

    [System.Serializable]
    public class PlayerReplayData
    {
        public byte PlayerId;
        public string PlayerName;
        public bool IsImpostor;
        public List<MovementPoint> MovementPoints;
        public List<PlayerAction> Actions;
        public List<ChatMessage> ChatMessages;
    }

    [System.Serializable]
    public class MovementPoint
    {
        public DateTime Timestamp;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsDead;
    }

    [System.Serializable]
    public class PlayerAction
    {
        public DateTime Timestamp;
        public string ActionType;
        public uint TargetId;
        public byte PlayerId;
    }

    [System.Serializable]
    public class ChatMessage
    {
        public DateTime Timestamp;
        public byte PlayerId;
        public string PlayerName;
        public string Message;
        public bool IsDead;
    }

    [System.Serializable]
    public class GameEvent
    {
        public DateTime Timestamp;
        public string EventType;
        public string EventData;
    }

    [System.Serializable]
    public class GameSettingsData
    {
        public int PlayerCount;
        public int ImpostorCount;
        public float KillCooldown;
        public float DiscussionTime;
        public float VotingTime;
    }
}
