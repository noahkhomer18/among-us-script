using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.StreamOverlayIntegration
{
    [BepInPlugin("com.yourname.streamoverlayintegration", "Stream Overlay Integration", "1.0.0")]
    public class StreamOverlayIntegrationPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configShowPlayerList;
        private static ConfigEntry<bool> configShowGameInfo;
        private static ConfigEntry<bool> configShowChat;
        private static ConfigEntry<bool> configShowVoting;
        private static ConfigEntry<bool> configShowTasks;
        private static ConfigEntry<bool> configShowStatistics;
        private static ConfigEntry<bool> configShowAlerts;
        private static ConfigEntry<bool> configShowWebcam;
        private static ConfigEntry<bool> configShowDonations;
        private static ConfigEntry<bool> configShowFollowers;
        private static ConfigEntry<string> configOverlayStyle;
        private static ConfigEntry<string> configOverlayPosition;
        private static ConfigEntry<int> configOverlayOpacity;
        private static ConfigEntry<bool> configAutoHide;
        private static ConfigEntry<int> configAutoHideDelay;
        private static ConfigEntry<string> configStreamingPlatform;
        private static ConfigEntry<string> configOBSWebSocketURL;
        private static ConfigEntry<string> configOBSWebSocketPassword;
        private static ConfigEntry<bool> configDiscordIntegration;
        private static ConfigEntry<string> configDiscordWebhookURL;
        private static ConfigEntry<bool> configTwitchIntegration;
        private static ConfigEntry<string> configTwitchChannel;
        private static ConfigEntry<string> configYouTubeIntegration;
        private static ConfigEntry<string> configYouTubeChannel;
        
        private static GameObject overlayUI;
        private static Dictionary<string, OverlayElement> overlayElements = new Dictionary<string, OverlayElement>();
        private static List<StreamAlert> streamAlerts = new List<StreamAlert>();
        private static List<StreamEvent> streamEvents = new List<StreamEvent>();
        private static bool isStreaming = false;
        private static string currentStreamTitle = "";
        private static int viewerCount = 0;
        private static int followerCount = 0;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable stream overlay integration");
            configShowPlayerList = Config.Bind("Display", "ShowPlayerList", true, "Show player list on overlay");
            configShowGameInfo = Config.Bind("Display", "ShowGameInfo", true, "Show game information on overlay");
            configShowChat = Config.Bind("Display", "ShowChat", true, "Show chat on overlay");
            configShowVoting = Config.Bind("Display", "ShowVoting", true, "Show voting information on overlay");
            configShowTasks = Config.Bind("Display", "ShowTasks", true, "Show task progress on overlay");
            configShowStatistics = Config.Bind("Display", "ShowStatistics", true, "Show player statistics on overlay");
            configShowAlerts = Config.Bind("Display", "ShowAlerts", true, "Show stream alerts on overlay");
            configShowWebcam = Config.Bind("Display", "ShowWebcam", false, "Show webcam on overlay");
            configShowDonations = Config.Bind("Display", "ShowDonations", true, "Show donation alerts on overlay");
            configShowFollowers = Config.Bind("Display", "ShowFollowers", true, "Show follower alerts on overlay");
            configOverlayStyle = Config.Bind("Style", "OverlayStyle", "Modern", "Overlay style (Modern/Classic/Minimal)");
            configOverlayPosition = Config.Bind("Style", "OverlayPosition", "TopRight", "Overlay position (TopLeft/TopRight/BottomLeft/BottomRight)");
            configOverlayOpacity = Config.Bind("Style", "OverlayOpacity", 90, "Overlay opacity (1-100)");
            configAutoHide = Config.Bind("Behavior", "AutoHide", true, "Automatically hide overlay elements");
            configAutoHideDelay = Config.Bind("Behavior", "AutoHideDelay", 5, "Auto-hide delay in seconds");
            configStreamingPlatform = Config.Bind("Platform", "StreamingPlatform", "Twitch", "Streaming platform (Twitch/YouTube/Facebook)");
            configOBSWebSocketURL = Config.Bind("OBS", "WebSocketURL", "ws://localhost:4455", "OBS WebSocket URL");
            configOBSWebSocketPassword = Config.Bind("OBS", "WebSocketPassword", "", "OBS WebSocket password");
            configDiscordIntegration = Config.Bind("Discord", "DiscordIntegration", false, "Enable Discord integration");
            configDiscordWebhookURL = Config.Bind("Discord", "DiscordWebhookURL", "", "Discord webhook URL");
            configTwitchIntegration = Config.Bind("Twitch", "TwitchIntegration", true, "Enable Twitch integration");
            configTwitchChannel = Config.Bind("Twitch", "TwitchChannel", "", "Twitch channel name");
            configYouTubeIntegration = Config.Bind("YouTube", "YouTubeIntegration", false, "Enable YouTube integration");
            configYouTubeChannel = Config.Bind("YouTube", "YouTubeChannel", "", "YouTube channel name");
            
            var harmony = new Harmony("com.yourname.streamoverlayintegration");
            harmony.PatchAll();
            
            InitializeStreamOverlay();
            CommonUtilities.LogMessage("StreamOverlayIntegration", "Stream Overlay Integration loaded successfully!");
        }

        private static void InitializeStreamOverlay()
        {
            CreateOverlayUI();
            InitializeOverlayElements();
            CommonUtilities.LogMessage("StreamOverlayIntegration", "Stream overlay system initialized");
        }

        private static void CreateOverlayUI()
        {
            overlayUI = new GameObject("StreamOverlayUI");
            overlayUI.transform.SetParent(HudManager.Instance.transform);
            overlayUI.SetActive(false);
        }

        private static void InitializeOverlayElements()
        {
            // Initialize overlay elements
            overlayElements["PlayerList"] = new OverlayElement("PlayerList", "Player List", true);
            overlayElements["GameInfo"] = new OverlayElement("GameInfo", "Game Information", true);
            overlayElements["Chat"] = new OverlayElement("Chat", "Chat Messages", true);
            overlayElements["Voting"] = new OverlayElement("Voting", "Voting Status", true);
            overlayElements["Tasks"] = new OverlayElement("Tasks", "Task Progress", true);
            overlayElements["Statistics"] = new OverlayElement("Statistics", "Player Statistics", true);
            overlayElements["Alerts"] = new OverlayElement("Alerts", "Stream Alerts", true);
            overlayElements["Webcam"] = new OverlayElement("Webcam", "Webcam Feed", false);
            overlayElements["Donations"] = new OverlayElement("Donations", "Donation Alerts", true);
            overlayElements["Followers"] = new OverlayElement("Followers", "Follower Alerts", true);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                StartStreaming();
                UpdateGameInfo();
            }
        }

        private static void StartStreaming()
        {
            isStreaming = true;
            currentStreamTitle = $"Among Us Game - {DateTime.Now:HH:mm}";
            
            // Initialize streaming platform integration
            InitializeStreamingPlatform();
            
            CommonUtilities.SendChatMessage("📺 Stream overlay activated!");
            CommonUtilities.LogMessage("StreamOverlayIntegration", "Streaming started");
        }

        private static void InitializeStreamingPlatform()
        {
            switch (configStreamingPlatform.Value.ToLower())
            {
                case "twitch":
                    InitializeTwitchIntegration();
                    break;
                case "youtube":
                    InitializeYouTubeIntegration();
                    break;
                case "facebook":
                    InitializeFacebookIntegration();
                    break;
            }
        }

        private static void InitializeTwitchIntegration()
        {
            if (!configTwitchIntegration.Value) return;
            
            // Initialize Twitch integration
            CommonUtilities.LogMessage("StreamOverlayIntegration", "Twitch integration initialized");
        }

        private static void InitializeYouTubeIntegration()
        {
            if (!configYouTubeIntegration.Value) return;
            
            // Initialize YouTube integration
            CommonUtilities.LogMessage("StreamOverlayIntegration", "YouTube integration initialized");
        }

        private static void InitializeFacebookIntegration()
        {
            // Initialize Facebook integration
            CommonUtilities.LogMessage("StreamOverlayIntegration", "Facebook integration initialized");
        }

        private static void UpdateGameInfo()
        {
            var gameInfo = new GameInfo
            {
                PlayerCount = PlayerControl.AllPlayerControls.Count(p => !p.Data.Disconnected),
                ImpostorCount = PlayerControl.AllPlayerControls.Count(p => p.Data.Role.IsImpostor),
                GameState = CommonUtilities.GetGameState().ToString(),
                MapName = "Unknown", // Would get actual map name
                TimeElapsed = Time.time
            };
            
            UpdateOverlayElement("GameInfo", gameInfo.ToString());
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class OverlayUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !isStreaming) return;
                
                UpdatePlayerList();
                UpdateTaskProgress();
                UpdateStatistics();
            }
        }

        private static void UpdatePlayerList()
        {
            if (!configShowPlayerList.Value) return;
            
            var playerList = new List<PlayerInfo>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.Disconnected)
                {
                    playerList.Add(new PlayerInfo
                    {
                        Name = player.Data.PlayerName,
                        IsAlive = !player.Data.IsDead,
                        IsImpostor = player.Data.Role.IsImpostor,
                        Color = player.Data.ColorId
                    });
                }
            }
            
            UpdateOverlayElement("PlayerList", string.Join("\n", playerList.Select(p => p.ToString())));
        }

        private static void UpdateTaskProgress()
        {
            if (!configShowTasks.Value) return;
            
            var taskProgress = new List<TaskProgress>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.Disconnected && !player.Data.Role.IsImpostor)
                {
                    taskProgress.Add(new TaskProgress
                    {
                        PlayerName = player.Data.PlayerName,
                        TasksCompleted = 0, // Would get actual task count
                        TotalTasks = 0 // Would get actual total tasks
                    });
                }
            }
            
            UpdateOverlayElement("Tasks", string.Join("\n", taskProgress.Select(t => t.ToString())));
        }

        private static void UpdateStatistics()
        {
            if (!configShowStatistics.Value) return;
            
            var statistics = new GameStatistics
            {
                TotalKills = 0, // Would get from statistics tracker
                TotalTasks = 0, // Would get from statistics tracker
                GameTime = Time.time,
                PlayerCount = PlayerControl.AllPlayerControls.Count(p => !p.Data.Disconnected)
            };
            
            UpdateOverlayElement("Statistics", statistics.ToString());
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class KillAlertPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !configShowAlerts.Value) return;
                
                ShowKillAlert(__instance, target);
            }
        }

        private static void ShowKillAlert(PlayerControl killer, PlayerControl victim)
        {
            var alert = new StreamAlert
            {
                Type = "Kill",
                Title = "Player Eliminated!",
                Message = $"{victim.Data.PlayerName} was eliminated by {killer.Data.PlayerName}",
                Timestamp = DateTime.Now,
                Duration = 5f
            };
            
            streamAlerts.Add(alert);
            UpdateOverlayElement("Alerts", alert.ToString());
            
            // Auto-hide after duration
            if (configAutoHide.Value)
            {
                HideAlertAfterDelay(alert, configAutoHideDelay.Value);
            }
        }

        private static void HideAlertAfterDelay(StreamAlert alert, int delay)
        {
            // Hide alert after delay
            CommonUtilities.LogMessage("StreamOverlayIntegration", $"Alert will hide in {delay} seconds");
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class VotingStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !configShowVoting.Value) return;
                
                ShowVotingInfo();
            }
        }

        private static void ShowVotingInfo()
        {
            var votingInfo = new VotingInfo
            {
                Phase = "Discussion",
                TimeRemaining = 60f, // Would get actual time
                PlayersAlive = PlayerControl.AllPlayerControls.Count(p => !p.Data.IsDead && !p.Data.Disconnected)
            };
            
            UpdateOverlayElement("Voting", votingInfo.ToString());
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatOverlayPatch
        {
            public static void Postfix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !configShowChat.Value) return;
                
                ShowChatMessage(__instance, chatText);
            }
        }

        private static void ShowChatMessage(PlayerControl player, string message)
        {
            var chatMessage = new ChatMessage
            {
                PlayerName = player.Data.PlayerName,
                Message = message,
                IsDead = player.Data.IsDead,
                Timestamp = DateTime.Now
            };
            
            UpdateOverlayElement("Chat", chatMessage.ToString());
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for stream overlay commands
                if (chatText == "/overlay" || chatText == "/stream")
                {
                    ToggleOverlay(__instance);
                    return false;
                }
                else if (chatText == "/overlayinfo")
                {
                    ShowOverlayInfo(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/overlay "))
                {
                    string elementName = chatText.Substring(9);
                    ToggleOverlayElement(__instance, elementName);
                    return false;
                }
                else if (chatText == "/streamalert")
                {
                    string message = "Test stream alert!";
                    ShowStreamAlert(__instance, message);
                    return false;
                }
                else if (chatText == "/overlayhelp")
                {
                    ShowOverlayHelp(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ToggleOverlay(PlayerControl player)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can toggle the overlay!");
                return;
            }
            
            overlayUI.SetActive(!overlayUI.activeSelf);
            string status = overlayUI.activeSelf ? "shown" : "hidden";
            CommonUtilities.SendChatMessage($"Stream overlay {status}");
        }

        private static void ShowOverlayInfo(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Stream Overlay Info ===");
            CommonUtilities.SendChatMessage($"Platform: {configStreamingPlatform.Value}");
            CommonUtilities.SendChatMessage($"Viewers: {viewerCount}");
            CommonUtilities.SendChatMessage($"Followers: {followerCount}");
            CommonUtilities.SendChatMessage($"Streaming: {(isStreaming ? "Yes" : "No")}");
        }

        private static void ToggleOverlayElement(PlayerControl player, string elementName)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can toggle overlay elements!");
                return;
            }
            
            if (overlayElements.ContainsKey(elementName))
            {
                overlayElements[elementName].IsVisible = !overlayElements[elementName].IsVisible;
                string status = overlayElements[elementName].IsVisible ? "shown" : "hidden";
                CommonUtilities.SendChatMessage($"Overlay element '{elementName}' {status}");
            }
            else
            {
                CommonUtilities.SendChatMessage($"Unknown overlay element: {elementName}");
            }
        }

        private static void ShowStreamAlert(PlayerControl player, string message)
        {
            var alert = new StreamAlert
            {
                Type = "Custom",
                Title = "Custom Alert",
                Message = message,
                Timestamp = DateTime.Now,
                Duration = 3f
            };
            
            streamAlerts.Add(alert);
            UpdateOverlayElement("Alerts", alert.ToString());
        }

        private static void ShowOverlayHelp(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Stream Overlay Commands ===");
            CommonUtilities.SendChatMessage("/overlay - Toggle overlay (host only)");
            CommonUtilities.SendChatMessage("/overlayinfo - Show overlay information");
            CommonUtilities.SendChatMessage("/overlay <element> - Toggle overlay element (host only)");
            CommonUtilities.SendChatMessage("/streamalert - Show test alert");
        }

        private static void UpdateOverlayElement(string elementName, string content)
        {
            if (overlayElements.ContainsKey(elementName))
            {
                overlayElements[elementName].Content = content;
                overlayElements[elementName].LastUpdated = DateTime.Now;
            }
        }

        public static void ShowDonationAlert(string donorName, float amount, string message)
        {
            if (!configShowDonations.Value) return;
            
            var alert = new StreamAlert
            {
                Type = "Donation",
                Title = "New Donation!",
                Message = $"{donorName} donated ${amount:F2}: {message}",
                Timestamp = DateTime.Now,
                Duration = 10f
            };
            
            streamAlerts.Add(alert);
            UpdateOverlayElement("Donations", alert.ToString());
        }

        public static void ShowFollowerAlert(string followerName)
        {
            if (!configShowFollowers.Value) return;
            
            var alert = new StreamAlert
            {
                Type = "Follower",
                Title = "New Follower!",
                Message = $"{followerName} started following!",
                Timestamp = DateTime.Now,
                Duration = 5f
            };
            
            streamAlerts.Add(alert);
            UpdateOverlayElement("Followers", alert.ToString());
        }

        public static void UpdateViewerCount(int count)
        {
            viewerCount = count;
            UpdateOverlayElement("GameInfo", $"Viewers: {count}");
        }

        public static void UpdateFollowerCount(int count)
        {
            followerCount = count;
        }
    }

    [System.Serializable]
    public class OverlayElement
    {
        public string Name;
        public string DisplayName;
        public bool IsVisible;
        public string Content;
        public DateTime LastUpdated;

        public OverlayElement(string name, string displayName, bool isVisible)
        {
            Name = name;
            DisplayName = displayName;
            IsVisible = isVisible;
            Content = "";
            LastUpdated = DateTime.Now;
        }
    }

    [System.Serializable]
    public class StreamAlert
    {
        public string Type;
        public string Title;
        public string Message;
        public DateTime Timestamp;
        public float Duration;

        public override string ToString()
        {
            return $"{Title}: {Message}";
        }
    }

    [System.Serializable]
    public class StreamEvent
    {
        public string EventType;
        public string EventData;
        public DateTime Timestamp;
    }

    [System.Serializable]
    public class PlayerInfo
    {
        public string Name;
        public bool IsAlive;
        public bool IsImpostor;
        public int Color;

        public override string ToString()
        {
            string status = IsAlive ? "Alive" : "Dead";
            string role = IsImpostor ? "Impostor" : "Crewmate";
            return $"{Name} ({status}) - {role}";
        }
    }

    [System.Serializable]
    public class GameInfo
    {
        public int PlayerCount;
        public int ImpostorCount;
        public string GameState;
        public string MapName;
        public float TimeElapsed;

        public override string ToString()
        {
            return $"Players: {PlayerCount} | Impostors: {ImpostorCount} | State: {GameState}";
        }
    }

    [System.Serializable]
    public class TaskProgress
    {
        public string PlayerName;
        public int TasksCompleted;
        public int TotalTasks;

        public override string ToString()
        {
            return $"{PlayerName}: {TasksCompleted}/{TotalTasks} tasks";
        }
    }

    [System.Serializable]
    public class GameStatistics
    {
        public int TotalKills;
        public int TotalTasks;
        public float GameTime;
        public int PlayerCount;

        public override string ToString()
        {
            return $"Kills: {TotalKills} | Tasks: {TotalTasks} | Time: {GameTime:F1}s";
        }
    }

    [System.Serializable]
    public class VotingInfo
    {
        public string Phase;
        public float TimeRemaining;
        public int PlayersAlive;

        public override string ToString()
        {
            return $"{Phase} | Time: {TimeRemaining:F1}s | Players: {PlayersAlive}";
        }
    }

    [System.Serializable]
    public class ChatMessage
    {
        public string PlayerName;
        public string Message;
        public bool IsDead;
        public DateTime Timestamp;

        public override string ToString()
        {
            string status = IsDead ? "[DEAD]" : "";
            return $"{status}{PlayerName}: {Message}";
        }
    }
}
