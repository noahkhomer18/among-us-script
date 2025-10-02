using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.ScreenshotSystem
{
    [BepInPlugin("com.yourname.screenshotsystem", "Screenshot System", "1.0.0")]
    public class ScreenshotSystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<KeyCode> configScreenshotKey;
        private static ConfigEntry<bool> configAutoScreenshot;
        private static ConfigEntry<bool> configScreenshotOnKill;
        private static ConfigEntry<bool> configScreenshotOnVote;
        private static ConfigEntry<bool> configScreenshotOnWin;
        private static ConfigEntry<string> configScreenshotDirectory;
        private static ConfigEntry<string> configScreenshotFormat;
        private static ConfigEntry<int> configScreenshotQuality;
        private static ConfigEntry<bool> configIncludeUI;
        private static ConfigEntry<bool> configIncludeChat;
        private static ConfigEntry<bool> configWatermark;
        private static ConfigEntry<string> configWatermarkText;
        
        private static List<ScreenshotData> screenshots = new List<ScreenshotData>();
        private static GameObject screenshotUI;
        private static bool isScreenshotMode = false;
        private static int screenshotCounter = 0;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable screenshot system");
            configScreenshotKey = Config.Bind("Controls", "ScreenshotKey", KeyCode.F12, "Key to take screenshots");
            configAutoScreenshot = Config.Bind("Auto", "AutoScreenshot", false, "Automatically take screenshots on events");
            configScreenshotOnKill = Config.Bind("Auto", "ScreenshotOnKill", true, "Take screenshot when player is killed");
            configScreenshotOnVote = Config.Bind("Auto", "ScreenshotOnVote", true, "Take screenshot during voting");
            configScreenshotOnWin = Config.Bind("Auto", "ScreenshotOnWin", true, "Take screenshot when game ends");
            configScreenshotDirectory = Config.Bind("Storage", "ScreenshotDirectory", "Screenshots", "Directory to store screenshots");
            configScreenshotFormat = Config.Bind("Storage", "ScreenshotFormat", "PNG", "Screenshot format (PNG/JPG)");
            configScreenshotQuality = Config.Bind("Storage", "ScreenshotQuality", 100, "Screenshot quality (1-100)");
            configIncludeUI = Config.Bind("Display", "IncludeUI", false, "Include UI elements in screenshots");
            configIncludeChat = Config.Bind("Display", "IncludeChat", true, "Include chat in screenshots");
            configWatermark = Config.Bind("Display", "Watermark", true, "Add watermark to screenshots");
            configWatermarkText = Config.Bind("Display", "WatermarkText", "Among Us Mod", "Watermark text");
            
            var harmony = new Harmony("com.yourname.screenshotsystem");
            harmony.PatchAll();
            
            InitializeScreenshotSystem();
            CommonUtilities.LogMessage("ScreenshotSystem", "Screenshot System loaded successfully!");
        }

        private static void InitializeScreenshotSystem()
        {
            // Create screenshot directory if it doesn't exist
            string screenshotPath = Path.Combine(Application.persistentDataPath, configScreenshotDirectory.Value);
            if (!Directory.Exists(screenshotPath))
            {
                Directory.CreateDirectory(screenshotPath);
            }
            
            CreateScreenshotUI();
            CommonUtilities.LogMessage("ScreenshotSystem", "Screenshot system initialized");
        }

        private static void CreateScreenshotUI()
        {
            screenshotUI = new GameObject("ScreenshotUI");
            screenshotUI.transform.SetParent(HudManager.Instance.transform);
            screenshotUI.SetActive(false);
        }

        private void Update()
        {
            if (!configEnabled.Value) return;
            
            // Check for screenshot key press
            if (Input.GetKeyDown(configScreenshotKey.Value))
            {
                TakeScreenshot("Manual");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class KillScreenshotPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value || !configAutoScreenshot.Value || !configScreenshotOnKill.Value) return;
                
                TakeScreenshot($"Kill_{target.Data.PlayerName}");
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class VoteScreenshotPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value || !configAutoScreenshot.Value || !configScreenshotOnVote.Value) return;
                
                TakeScreenshot("Voting_Started");
            }
        }

        [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
        public static class WinScreenshotPatch
        {
            public static void Postfix(EndGameManager __instance)
            {
                if (!configEnabled.Value || !configAutoScreenshot.Value || !configScreenshotOnWin.Value) return;
                
                string gameResult = __instance.GameOverReason.ToString();
                TakeScreenshot($"GameEnd_{gameResult}");
            }
        }

        private static void TakeScreenshot(string eventType)
        {
            try
            {
                screenshotCounter++;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"screenshot_{timestamp}_{eventType}_{screenshotCounter}";
                string filePath = Path.Combine(Application.persistentDataPath, configScreenshotDirectory.Value, fileName);
                
                // Hide UI if not including it
                bool uiWasActive = true;
                if (!configIncludeUI.Value)
                {
                    uiWasActive = HudManager.Instance.gameObject.activeSelf;
                    HudManager.Instance.gameObject.SetActive(false);
                }
                
                // Hide chat if not including it
                bool chatWasActive = true;
                if (!configIncludeChat.Value && HudManager.Instance.Chat != null)
                {
                    chatWasActive = HudManager.Instance.Chat.gameObject.activeSelf;
                    HudManager.Instance.Chat.gameObject.SetActive(false);
                }
                
                // Take the screenshot
                ScreenCapture.CaptureScreenshot(filePath + "." + configScreenshotFormat.Value.ToLower());
                
                // Restore UI elements
                if (!configIncludeUI.Value && uiWasActive)
                {
                    HudManager.Instance.gameObject.SetActive(true);
                }
                
                if (!configIncludeChat.Value && chatWasActive && HudManager.Instance.Chat != null)
                {
                    HudManager.Instance.Chat.gameObject.SetActive(true);
                }
                
                // Add watermark if enabled
                if (configWatermark.Value)
                {
                    AddWatermark(filePath + "." + configScreenshotFormat.Value.ToLower());
                }
                
                // Record screenshot data
                var screenshotData = new ScreenshotData
                {
                    FileName = fileName,
                    FilePath = filePath + "." + configScreenshotFormat.Value.ToLower(),
                    EventType = eventType,
                    Timestamp = DateTime.Now,
                    GameState = CommonUtilities.GetGameState().ToString(),
                    PlayerCount = PlayerControl.AllPlayerControls.Count(p => !p.Data.Disconnected)
                };
                
                screenshots.Add(screenshotData);
                
                CommonUtilities.SendChatMessage($"📸 Screenshot saved: {fileName}");
                CommonUtilities.LogMessage("ScreenshotSystem", $"Screenshot taken: {fileName}");
            }
            catch (Exception e)
            {
                CommonUtilities.LogMessage("ScreenshotSystem", $"Error taking screenshot: {e.Message}");
            }
        }

        private static void AddWatermark(string filePath)
        {
            // Watermark implementation would go here
            // This would require image processing libraries
            CommonUtilities.LogMessage("ScreenshotSystem", "Watermark added to screenshot");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for screenshot commands
                if (chatText == "/screenshot" || chatText == "/ss")
                {
                    TakeScreenshot("Chat_Command");
                    return false;
                }
                else if (chatText == "/screenshots" || chatText == "/sslist")
                {
                    ShowScreenshotList(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/ss "))
                {
                    string eventType = chatText.Substring(4);
                    TakeScreenshot(eventType);
                    return false;
                }
                else if (chatText == "/ssmode" || chatText == "/screenshotmode")
                {
                    ToggleScreenshotMode(__instance);
                    return false;
                }
                else if (chatText == "/sshelp")
                {
                    ShowScreenshotHelp(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowScreenshotList(PlayerControl player)
        {
            if (screenshots.Count == 0)
            {
                CommonUtilities.SendChatMessage("No screenshots found!");
                return;
            }
            
            CommonUtilities.SendChatMessage($"=== Recent Screenshots ({screenshots.Count}) ===");
            var recentScreenshots = screenshots.TakeLast(10).ToList();
            
            for (int i = 0; i < recentScreenshots.Count; i++)
            {
                var screenshot = recentScreenshots[i];
                CommonUtilities.SendChatMessage($"{i + 1}. {screenshot.FileName} ({screenshot.EventType})");
            }
        }

        private static void ToggleScreenshotMode(PlayerControl player)
        {
            if (!player.AmOwner)
            {
                CommonUtilities.SendChatMessage("Only the host can toggle screenshot mode!");
                return;
            }
            
            isScreenshotMode = !isScreenshotMode;
            string status = isScreenshotMode ? "enabled" : "disabled";
            CommonUtilities.SendChatMessage($"Screenshot mode {status}");
        }

        private static void ShowScreenshotHelp(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Screenshot Commands ===");
            CommonUtilities.SendChatMessage($"{configScreenshotKey.Value} - Take screenshot");
            CommonUtilities.SendChatMessage("/screenshot - Take screenshot");
            CommonUtilities.SendChatMessage("/screenshots - Show screenshot list");
            CommonUtilities.SendChatMessage("/ss <event> - Take screenshot with custom event name");
            CommonUtilities.SendChatMessage("/ssmode - Toggle screenshot mode (host only)");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class ScreenshotModePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !isScreenshotMode) return;
                
                // In screenshot mode, take screenshots more frequently
                if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
                {
                    TakeScreenshot("ScreenshotMode");
                }
            }
        }

        public static List<ScreenshotData> GetScreenshots()
        {
            return screenshots;
        }

        public static ScreenshotData GetLatestScreenshot()
        {
            return screenshots.LastOrDefault();
        }

        public static void ClearScreenshots()
        {
            screenshots.Clear();
            CommonUtilities.SendChatMessage("Screenshot history cleared!");
        }
    }

    [System.Serializable]
    public class ScreenshotData
    {
        public string FileName;
        public string FilePath;
        public string EventType;
        public DateTime Timestamp;
        public string GameState;
        public int PlayerCount;
    }
}
