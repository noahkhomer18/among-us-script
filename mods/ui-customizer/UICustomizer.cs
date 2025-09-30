using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.UICustomizer
{
    [BepInPlugin("com.yourname.uicustomizer", "UI Customizer", "1.0.0")]
    public class UICustomizerPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<Color> configHUDColor;
        private static ConfigEntry<Color> configChatColor;
        private static ConfigEntry<Color> configVoteColor;
        private static ConfigEntry<bool> configCustomFonts;
        private static ConfigEntry<float> configUIScale;
        private static ConfigEntry<bool> configCustomBackgrounds;
        private static ConfigEntry<string> configTheme;
        
        private static Dictionary<string, Color> colorPresets = new Dictionary<string, Color>();
        private static Dictionary<string, string> themes = new Dictionary<string, string>();
        private static GameObject customUI;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable UI customization");
            configHUDColor = Config.Bind("Colors", "HUDColor", Color.white, "HUD color");
            configChatColor = Config.Bind("Colors", "ChatColor", Color.white, "Chat color");
            configVoteColor = Config.Bind("Colors", "VoteColor", Color.yellow, "Voting color");
            configCustomFonts = Config.Bind("Fonts", "CustomFonts", true, "Enable custom fonts");
            configUIScale = Config.Bind("UI", "UIScale", 1f, "UI scale factor");
            configCustomBackgrounds = Config.Bind("Backgrounds", "CustomBackgrounds", true, "Enable custom backgrounds");
            configTheme = Config.Bind("Theme", "Theme", "default", "UI theme");
            
            var harmony = new Harmony("com.yourname.uicustomizer");
            harmony.PatchAll();
            
            InitializeUICustomizer();
            CommonUtilities.LogMessage("UICustomizer", "UI Customizer loaded successfully!");
        }

        private static void InitializeUICustomizer()
        {
            InitializeColorPresets();
            InitializeThemes();
            CreateCustomUI();
        }

        private static void InitializeColorPresets()
        {
            colorPresets["Red"] = Color.red;
            colorPresets["Blue"] = Color.blue;
            colorPresets["Green"] = Color.green;
            colorPresets["Yellow"] = Color.yellow;
            colorPresets["Purple"] = Color.magenta;
            colorPresets["Orange"] = new Color(1f, 0.5f, 0f);
            colorPresets["Pink"] = new Color(1f, 0.75f, 0.8f);
            colorPresets["Cyan"] = Color.cyan;
        }

        private static void InitializeThemes()
        {
            themes["default"] = "Default Among Us theme";
            themes["dark"] = "Dark theme with dark backgrounds";
            themes["neon"] = "Neon theme with bright colors";
            themes["minimal"] = "Minimal theme with clean design";
            themes["retro"] = "Retro theme with vintage colors";
        }

        private static void CreateCustomUI()
        {
            if (!configEnabled.Value) return;
            
            customUI = new GameObject("CustomUI");
            customUI.transform.SetParent(HudManager.Instance.transform);
            
            // Apply initial theme
            ApplyTheme(configTheme.Value);
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
        public static class HUDStartPatch
        {
            public static void Postfix(HudManager __instance)
            {
                if (!configEnabled.Value) return;
                
                CustomizeHUD(__instance);
            }
        }

        private static void CustomizeHUD(HudManager hudManager)
        {
            // Customize HUD elements
            if (hudManager.Chat != null)
            {
                CustomizeChat(hudManager.Chat);
            }
            
            if (hudManager.TaskPanel != null)
            {
                CustomizeTaskPanel(hudManager.TaskPanel);
            }
            
            if (hudManager.KillButton != null)
            {
                CustomizeKillButton(hudManager.KillButton);
            }
            
            // Apply UI scale
            ApplyUIScale();
        }

        private static void CustomizeChat(ChatController chat)
        {
            // Customize chat appearance
            var chatText = chat.GetComponentInChildren<TMPro.TextMeshPro>();
            if (chatText != null)
            {
                chatText.color = configChatColor.Value;
                if (configCustomFonts.Value)
                {
                    // Apply custom font
                }
            }
        }

        private static void CustomizeTaskPanel(TaskPanel taskPanel)
        {
            // Customize task panel
            var taskTexts = taskPanel.GetComponentsInChildren<TMPro.TextMeshPro>();
            foreach (var text in taskTexts)
            {
                text.color = configHUDColor.Value;
            }
        }

        private static void CustomizeKillButton(KillButton killButton)
        {
            // Customize kill button
            var buttonRenderer = killButton.GetComponent<SpriteRenderer>();
            if (buttonRenderer != null)
            {
                buttonRenderer.color = configHUDColor.Value;
            }
        }

        private static void ApplyUIScale()
        {
            float scale = configUIScale.Value;
            if (HudManager.Instance != null)
            {
                HudManager.Instance.transform.localScale = Vector3.one * scale;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix(MeetingHud __instance)
            {
                if (!configEnabled.Value) return;
                
                CustomizeMeetingHUD(__instance);
            }
        }

        private static void CustomizeMeetingHUD(MeetingHud meetingHud)
        {
            // Customize meeting HUD
            var voteButtons = meetingHud.GetComponentsInChildren<PassiveButton>();
            foreach (var button in voteButtons)
            {
                var buttonRenderer = button.GetComponent<SpriteRenderer>();
                if (buttonRenderer != null)
                {
                    buttonRenderer.color = configVoteColor.Value;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for UI customization commands
                if (chatText.StartsWith("/color "))
                {
                    string colorName = chatText.Substring(7);
                    SetColor(__instance, colorName);
                    return false;
                }
                else if (chatText.StartsWith("/theme "))
                {
                    string themeName = chatText.Substring(7);
                    SetTheme(__instance, themeName);
                    return false;
                }
                else if (chatText.StartsWith("/scale "))
                {
                    string scaleValue = chatText.Substring(7);
                    SetUIScale(__instance, scaleValue);
                    return false;
                }
                else if (chatText == "/ui" || chatText == "/customize")
                {
                    ShowUICustomization(__instance);
                    return false;
                }
                else if (chatText == "/themes")
                {
                    ShowAvailableThemes(__instance);
                    return false;
                }
                else if (chatText == "/colors")
                {
                    ShowAvailableColors(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void SetColor(PlayerControl player, string colorName)
        {
            if (!colorPresets.ContainsKey(colorName))
            {
                CommonUtilities.SendChatMessage($"Color '{colorName}' not found. Use /colors to see available colors.");
                return;
            }
            
            Color newColor = colorPresets[colorName];
            configHUDColor.Value = newColor;
            
            // Apply color immediately
            CustomizeHUD(HudManager.Instance);
            
            CommonUtilities.SendChatMessage($"HUD color changed to {colorName}");
        }

        private static void SetTheme(PlayerControl player, string themeName)
        {
            if (!themes.ContainsKey(themeName))
            {
                CommonUtilities.SendChatMessage($"Theme '{themeName}' not found. Use /themes to see available themes.");
                return;
            }
            
            ApplyTheme(themeName);
            configTheme.Value = themeName;
            
            CommonUtilities.SendChatMessage($"Theme changed to {themeName}");
        }

        private static void ApplyTheme(string themeName)
        {
            switch (themeName)
            {
                case "dark":
                    configHUDColor.Value = Color.white;
                    configChatColor.Value = Color.white;
                    configVoteColor.Value = Color.yellow;
                    break;
                case "neon":
                    configHUDColor.Value = Color.cyan;
                    configChatColor.Value = Color.magenta;
                    configVoteColor.Value = Color.green;
                    break;
                case "minimal":
                    configHUDColor.Value = Color.gray;
                    configChatColor.Value = Color.black;
                    configVoteColor.Value = Color.blue;
                    break;
                case "retro":
                    configHUDColor.Value = new Color(1f, 0.8f, 0f);
                    configChatColor.Value = new Color(0.8f, 0.4f, 0f);
                    configVoteColor.Value = new Color(0.6f, 0.2f, 0.8f);
                    break;
                default:
                    configHUDColor.Value = Color.white;
                    configChatColor.Value = Color.white;
                    configVoteColor.Value = Color.yellow;
                    break;
            }
            
            // Apply theme immediately
            CustomizeHUD(HudManager.Instance);
        }

        private static void SetUIScale(PlayerControl player, string scaleValue)
        {
            if (float.TryParse(scaleValue, out float scale))
            {
                if (scale >= 0.5f && scale <= 2f)
                {
                    configUIScale.Value = scale;
                    ApplyUIScale();
                    CommonUtilities.SendChatMessage($"UI scale set to {scale}");
                }
                else
                {
                    CommonUtilities.SendChatMessage("Scale must be between 0.5 and 2.0");
                }
            }
            else
            {
                CommonUtilities.SendChatMessage("Invalid scale value. Use a number between 0.5 and 2.0");
            }
        }

        private static void ShowUICustomization(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== UI Customization ===");
            CommonUtilities.SendChatMessage($"Current theme: {configTheme.Value}");
            CommonUtilities.SendChatMessage($"UI scale: {configUIScale.Value}");
            CommonUtilities.SendChatMessage($"HUD color: {configHUDColor.Value}");
            CommonUtilities.SendChatMessage($"Chat color: {configChatColor.Value}");
            CommonUtilities.SendChatMessage($"Vote color: {configVoteColor.Value}");
        }

        private static void ShowAvailableThemes(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Available Themes ===");
            foreach (var theme in themes)
            {
                CommonUtilities.SendChatMessage($"{theme.Key}: {theme.Value}");
            }
        }

        private static void ShowAvailableColors(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Available Colors ===");
            foreach (var color in colorPresets)
            {
                CommonUtilities.SendChatMessage($"{color.Key}");
            }
        }
    }
}
