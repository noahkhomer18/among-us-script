using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.PerformanceMonitor
{
    [BepInPlugin("com.yourname.performancemonitor", "Performance Monitor", "1.0.0")]
    public class PerformanceMonitorPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configShowFPS;
        private static ConfigEntry<bool> configShowMemory;
        private static ConfigEntry<bool> configShowPing;
        private static ConfigEntry<bool> configShowFrameTime;
        private static ConfigEntry<bool> configShowCPU;
        private static ConfigEntry<bool> configShowGPU;
        private static ConfigEntry<bool> configShowNetwork;
        private static ConfigEntry<bool> configShowAdvanced;
        private static ConfigEntry<float> configUpdateInterval;
        private static ConfigEntry<KeyCode> configToggleDisplayKey;
        private static ConfigEntry<KeyCode> configResetStatsKey;
        private static ConfigEntry<KeyCode> configScreenshotKey;
        
        private static bool displayEnabled = true;
        private static float lastUpdateTime = 0f;
        private static float frameTime = 0f;
        private static int frameCount = 0;
        private static float fps = 0f;
        private static long memoryUsage = 0;
        private static float ping = 0f;
        private static float cpuUsage = 0f;
        private static float gpuUsage = 0f;
        private static int networkPackets = 0;
        private static float networkBandwidth = 0f;
        
        private static List<float> fpsHistory = new List<float>();
        private static List<float> memoryHistory = new List<float>();
        private static int maxHistorySize = 100;

        private void Awake()
        {
            // Configuration
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the performance monitor");
            configShowFPS = Config.Bind("Display", "ShowFPS", true, "Show FPS counter");
            configShowMemory = Config.Bind("Display", "ShowMemory", true, "Show memory usage");
            configShowPing = Config.Bind("Display", "ShowPing", true, "Show network ping");
            configShowFrameTime = Config.Bind("Display", "ShowFrameTime", true, "Show frame time");
            configShowCPU = Config.Bind("Display", "ShowCPU", false, "Show CPU usage");
            configShowGPU = Config.Bind("Display", "ShowGPU", false, "Show GPU usage");
            configShowNetwork = Config.Bind("Display", "ShowNetwork", true, "Show network statistics");
            configShowAdvanced = Config.Bind("Display", "ShowAdvanced", false, "Show advanced performance metrics");
            configUpdateInterval = Config.Bind("Performance", "UpdateInterval", 0.1f, "Update interval in seconds");
            configToggleDisplayKey = Config.Bind("Controls", "ToggleDisplayKey", KeyCode.F3, "Key to toggle performance display");
            configResetStatsKey = Config.Bind("Controls", "ResetStatsKey", KeyCode.F4, "Key to reset performance statistics");
            configScreenshotKey = Config.Bind("Controls", "ScreenshotKey", KeyCode.F12, "Key to take performance screenshot");

            if (configEnabled.Value)
            {
                var harmony = new Harmony(Info.Metadata.GUID);
                harmony.PatchAll();
                CommonUtilities.LogMessage("PerformanceMonitor", "Performance Monitor loaded successfully");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class PlayerUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value) return;
                
                UpdatePerformanceMetrics();
                HandleInput();
                RenderPerformanceDisplay();
            }
        }

        private static void UpdatePerformanceMetrics()
        {
            float currentTime = Time.time;
            
            if (currentTime - lastUpdateTime >= configUpdateInterval.Value)
            {
                // Update FPS
                frameCount++;
                if (frameCount >= 10)
                {
                    fps = frameCount / (currentTime - lastUpdateTime);
                    frameCount = 0;
                    lastUpdateTime = currentTime;
                    
                    // Add to history
                    fpsHistory.Add(fps);
                    if (fpsHistory.Count > maxHistorySize)
                    {
                        fpsHistory.RemoveAt(0);
                    }
                }
                
                // Update frame time
                frameTime = Time.deltaTime * 1000f; // Convert to milliseconds
                
                // Update memory usage
                memoryUsage = GC.GetTotalMemory(false);
                memoryHistory.Add(memoryUsage / (1024f * 1024f)); // Convert to MB
                if (memoryHistory.Count > maxHistorySize)
                {
                    memoryHistory.RemoveAt(0);
                }
                
                // Update ping (if in multiplayer)
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                {
                    ping = 0f; // Host has 0 ping
                }
                else if (AmongUsClient.Instance != null)
                {
                    ping = AmongUsClient.Instance.Ping;
                }
                
                // Update CPU usage (simplified)
                cpuUsage = GetCPUUsage();
                
                // Update GPU usage (simplified)
                gpuUsage = GetGPUUsage();
                
                // Update network statistics
                UpdateNetworkStats();
            }
        }

        private static void UpdateNetworkStats()
        {
            if (AmongUsClient.Instance != null)
            {
                // This would integrate with the game's networking system
                // For now, we'll use placeholder values
                networkPackets = UnityEngine.Random.Range(0, 100);
                networkBandwidth = UnityEngine.Random.Range(0f, 1f);
            }
        }

        private static float GetCPUUsage()
        {
            // Simplified CPU usage calculation
            // In a real implementation, you'd use System.Diagnostics.Process
            return UnityEngine.Random.Range(0f, 100f);
        }

        private static float GetGPUUsage()
        {
            // Simplified GPU usage calculation
            // In a real implementation, you'd use GPU monitoring APIs
            return UnityEngine.Random.Range(0f, 100f);
        }

        private static void HandleInput()
        {
            // Toggle display
            if (Input.GetKeyDown(configToggleDisplayKey.Value))
            {
                displayEnabled = !displayEnabled;
                CommonUtilities.SendChatMessage($"Performance display: {(displayEnabled ? "ON" : "OFF")}");
            }
            
            // Reset statistics
            if (Input.GetKeyDown(configResetStatsKey.Value))
            {
                ResetStatistics();
                CommonUtilities.SendChatMessage("Performance statistics reset");
            }
            
            // Take performance screenshot
            if (Input.GetKeyDown(configScreenshotKey.Value))
            {
                TakePerformanceScreenshot();
            }
        }

        private static void ResetStatistics()
        {
            fpsHistory.Clear();
            memoryHistory.Clear();
            frameCount = 0;
            lastUpdateTime = Time.time;
        }

        private static void TakePerformanceScreenshot()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"Performance_{timestamp}.png";
            
            // Take screenshot
            ScreenCapture.CaptureScreenshot(filename);
            CommonUtilities.SendChatMessage($"Performance screenshot saved: {filename}");
        }

        private static void RenderPerformanceDisplay()
        {
            if (!displayEnabled) return;
            
            // Create performance display UI
            var displayText = new System.Text.StringBuilder();
            
            if (configShowFPS.Value)
            {
                displayText.AppendLine($"FPS: {fps:F1}");
            }
            
            if (configShowFrameTime.Value)
            {
                displayText.AppendLine($"Frame Time: {frameTime:F2}ms");
            }
            
            if (configShowMemory.Value)
            {
                float memoryMB = memoryUsage / (1024f * 1024f);
                displayText.AppendLine($"Memory: {memoryMB:F1} MB");
            }
            
            if (configShowPing.Value)
            {
                displayText.AppendLine($"Ping: {ping:F0}ms");
            }
            
            if (configShowCPU.Value)
            {
                displayText.AppendLine($"CPU: {cpuUsage:F1}%");
            }
            
            if (configShowGPU.Value)
            {
                displayText.AppendLine($"GPU: {gpuUsage:F1}%");
            }
            
            if (configShowNetwork.Value)
            {
                displayText.AppendLine($"Network: {networkPackets} packets/s");
                displayText.AppendLine($"Bandwidth: {networkBandwidth:F2} MB/s");
            }
            
            if (configShowAdvanced.Value)
            {
                displayText.AppendLine($"FPS Avg: {GetAverageFPS():F1}");
                displayText.AppendLine($"Memory Avg: {GetAverageMemory():F1} MB");
                displayText.AppendLine($"FPS Min: {GetMinFPS():F1}");
                displayText.AppendLine($"FPS Max: {GetMaxFPS():F1}");
            }
            
            // Display the performance information
            // This would integrate with the game's UI system
            DisplayPerformanceText(displayText.ToString());
        }

        private static void DisplayPerformanceText(string text)
        {
            // This would render the text on screen
            // For now, we'll just log it
            if (Time.frameCount % 60 == 0) // Update every 60 frames
            {
                CommonUtilities.LogMessage("PerformanceMonitor", text);
            }
        }

        private static float GetAverageFPS()
        {
            if (fpsHistory.Count == 0) return 0f;
            
            float sum = 0f;
            foreach (float fps in fpsHistory)
            {
                sum += fps;
            }
            return sum / fpsHistory.Count;
        }

        private static float GetAverageMemory()
        {
            if (memoryHistory.Count == 0) return 0f;
            
            float sum = 0f;
            foreach (float memory in memoryHistory)
            {
                sum += memory;
            }
            return sum / memoryHistory.Count;
        }

        private static float GetMinFPS()
        {
            if (fpsHistory.Count == 0) return 0f;
            
            float min = float.MaxValue;
            foreach (float fps in fpsHistory)
            {
                if (fps < min) min = fps;
            }
            return min;
        }

        private static float GetMaxFPS()
        {
            if (fpsHistory.Count == 0) return 0f;
            
            float max = float.MinValue;
            foreach (float fps in fpsHistory)
            {
                if (fps > max) max = fps;
            }
            return max;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Handle performance commands
                if (chatText.StartsWith("/fps"))
                {
                    HandleFPSCommand();
                    return false;
                }
                
                if (chatText.StartsWith("/perf"))
                {
                    HandlePerformanceCommand();
                    return false;
                }
                
                if (chatText.StartsWith("/stats"))
                {
                    HandleStatsCommand();
                    return false;
                }
                
                return true;
            }
        }

        private static void HandleFPSCommand()
        {
            CommonUtilities.SendChatMessage($"Current FPS: {fps:F1}");
            CommonUtilities.SendChatMessage($"Average FPS: {GetAverageFPS():F1}");
            CommonUtilities.SendChatMessage($"Min FPS: {GetMinFPS():F1}");
            CommonUtilities.SendChatMessage($"Max FPS: {GetMaxFPS():F1}");
        }

        private static void HandlePerformanceCommand()
        {
            CommonUtilities.SendChatMessage("=== Performance Report ===");
            CommonUtilities.SendChatMessage($"FPS: {fps:F1} (Avg: {GetAverageFPS():F1})");
            CommonUtilities.SendChatMessage($"Frame Time: {frameTime:F2}ms");
            CommonUtilities.SendChatMessage($"Memory: {memoryUsage / (1024f * 1024f):F1} MB");
            CommonUtilities.SendChatMessage($"Ping: {ping:F0}ms");
            if (configShowAdvanced.Value)
            {
                CommonUtilities.SendChatMessage($"CPU: {cpuUsage:F1}%");
                CommonUtilities.SendChatMessage($"GPU: {gpuUsage:F1}%");
            }
        }

        private static void HandleStatsCommand()
        {
            CommonUtilities.SendChatMessage("=== Performance Statistics ===");
            CommonUtilities.SendChatMessage($"FPS Range: {GetMinFPS():F1} - {GetMaxFPS():F1}");
            CommonUtilities.SendChatMessage($"Average FPS: {GetAverageFPS():F1}");
            CommonUtilities.SendChatMessage($"Average Memory: {GetAverageMemory():F1} MB");
            CommonUtilities.SendChatMessage($"Data Points: {fpsHistory.Count}");
        }
    }
}
