using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.TranslationSystem
{
    [BepInPlugin("com.yourname.translationsystem", "Translation System", "1.0.0")]
    public class TranslationSystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<string> configDefaultLanguage;
        private static ConfigEntry<bool> configAutoDetectLanguage;
        private static ConfigEntry<bool> configShowLanguageIndicator;
        private static ConfigEntry<bool> configTranslateChat;
        private static ConfigEntry<bool> configTranslateUI;
        private static ConfigEntry<float> configTranslationConfidence;
        private static ConfigEntry<KeyCode> configToggleLanguageKey;
        private static ConfigEntry<KeyCode> configTranslateMessageKey;
        
        private static Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();
        private static string currentLanguage = "en";
        private static List<string> availableLanguages = new List<string>();
        private static bool isTranslating = false;

        private void Awake()
        {
            // Configuration
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the translation system");
            configDefaultLanguage = Config.Bind("General", "DefaultLanguage", "en", "Default language code (en, es, fr, de, etc.)");
            configAutoDetectLanguage = Config.Bind("General", "AutoDetectLanguage", true, "Automatically detect player language");
            configShowLanguageIndicator = Config.Bind("UI", "ShowLanguageIndicator", true, "Show current language indicator");
            configTranslateChat = Config.Bind("Features", "TranslateChat", true, "Enable chat message translation");
            configTranslateUI = Config.Bind("Features", "TranslateUI", true, "Enable UI element translation");
            configTranslationConfidence = Config.Bind("Features", "TranslationConfidence", 0.8f, "Minimum confidence for auto-translation (0.0-1.0)");
            configToggleLanguageKey = Config.Bind("Controls", "ToggleLanguageKey", KeyCode.T, "Key to toggle between languages");
            configTranslateMessageKey = Config.Bind("Controls", "TranslateMessageKey", KeyCode.Y, "Key to translate selected message");

            if (configEnabled.Value)
            {
                LoadTranslations();
                var harmony = new Harmony(Info.Metadata.GUID);
                harmony.PatchAll();
                CommonUtilities.LogMessage("TranslationSystem", "Translation System loaded successfully");
            }
        }

        private static void LoadTranslations()
        {
            string translationsPath = Path.Combine(Application.dataPath, "Translations");
            
            if (!Directory.Exists(translationsPath))
            {
                Directory.CreateDirectory(translationsPath);
                CreateDefaultTranslationFiles(translationsPath);
            }

            // Load all translation files
            string[] translationFiles = Directory.GetFiles(translationsPath, "*.json");
            
            foreach (string file in translationFiles)
            {
                try
                {
                    string languageCode = Path.GetFileNameWithoutExtension(file);
                    string jsonContent = File.ReadAllText(file, Encoding.UTF8);
                    
                    var languageTranslations = ParseTranslationJson(jsonContent);
                    translations[languageCode] = languageTranslations;
                    availableLanguages.Add(languageCode);
                    
                    CommonUtilities.LogMessage("TranslationSystem", $"Loaded translations for language: {languageCode}");
                }
                catch (Exception ex)
                {
                    CommonUtilities.LogMessage("TranslationSystem", $"Failed to load translation file {file}: {ex.Message}");
                }
            }

            // Set default language
            currentLanguage = configDefaultLanguage.Value;
            if (!availableLanguages.Contains(currentLanguage))
            {
                currentLanguage = "en";
            }
        }

        private static void CreateDefaultTranslationFiles(string translationsPath)
        {
            // English (default)
            CreateTranslationFile(translationsPath, "en", GetEnglishTranslations());
            
            // Spanish
            CreateTranslationFile(translationsPath, "es", GetSpanishTranslations());
            
            // French
            CreateTranslationFile(translationsPath, "fr", GetFrenchTranslations());
            
            // German
            CreateTranslationFile(translationsPath, "de", GetGermanTranslations());
            
            // Japanese
            CreateTranslationFile(translationsPath, "ja", GetJapaneseTranslations());
        }

        private static void CreateTranslationFile(string path, string languageCode, Dictionary<string, string> translations)
        {
            string filePath = Path.Combine(path, $"{languageCode}.json");
            string jsonContent = ConvertToJson(translations);
            File.WriteAllText(filePath, jsonContent, Encoding.UTF8);
        }

        private static Dictionary<string, string> ParseTranslationJson(string json)
        {
            var result = new Dictionary<string, string>();
            
            // Simple JSON parsing for translation files
            // In a real implementation, you'd use a proper JSON library
            string[] lines = json.Split('\n');
            foreach (string line in lines)
            {
                if (line.Contains("\"") && line.Contains(":"))
                {
                    string[] parts = line.Split(':');
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim().Trim('"', ' ', '\t');
                        string value = parts[1].Trim().Trim('"', ' ', '\t', ',');
                        result[key] = value;
                    }
                }
            }
            
            return result;
        }

        private static string ConvertToJson(Dictionary<string, string> translations)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            
            int count = 0;
            foreach (var kvp in translations)
            {
                json.AppendLine($"  \"{kvp.Key}\": \"{kvp.Value}\"{(count < translations.Count - 1 ? "," : "")}");
                count++;
            }
            
            json.AppendLine("}");
            return json.ToString();
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatTranslationPatch
        {
            public static void Postfix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !configTranslateChat.Value) return;
                
                // Translate incoming chat messages
                string translatedText = TranslateText(chatText, currentLanguage);
                if (translatedText != chatText)
                {
                    CommonUtilities.LogMessage("TranslationSystem", $"Translated: {chatText} -> {translatedText}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class PlayerUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value) return;
                
                HandleTranslationInput();
                UpdateLanguageIndicator();
            }
        }

        private static void HandleTranslationInput()
        {
            // Toggle language
            if (Input.GetKeyDown(configToggleLanguageKey.Value))
            {
                ToggleLanguage();
            }
            
            // Translate selected message
            if (Input.GetKeyDown(configTranslateMessageKey.Value))
            {
                TranslateSelectedMessage();
            }
        }

        private static void ToggleLanguage()
        {
            int currentIndex = availableLanguages.IndexOf(currentLanguage);
            int nextIndex = (currentIndex + 1) % availableLanguages.Count;
            currentLanguage = availableLanguages[nextIndex];
            
            CommonUtilities.SendChatMessage($"Language changed to: {GetLanguageName(currentLanguage)}");
            CommonUtilities.LogMessage("TranslationSystem", $"Language toggled to: {currentLanguage}");
        }

        private static void TranslateSelectedMessage()
        {
            // This would integrate with the chat system to translate the last message
            CommonUtilities.SendChatMessage("Translation feature activated");
        }

        private static string TranslateText(string text, string targetLanguage)
        {
            if (translations.ContainsKey(targetLanguage) && translations[targetLanguage].ContainsKey(text))
            {
                return translations[targetLanguage][text];
            }
            
            // Simple word-by-word translation for common terms
            return TranslateCommonTerms(text, targetLanguage);
        }

        private static string TranslateCommonTerms(string text, string targetLanguage)
        {
            var commonTerms = GetCommonTerms();
            
            if (commonTerms.ContainsKey(targetLanguage))
            {
                string result = text;
                foreach (var term in commonTerms[targetLanguage])
                {
                    result = result.Replace(term.Key, term.Value);
                }
                return result;
            }
            
            return text;
        }

        private static void UpdateLanguageIndicator()
        {
            if (!configShowLanguageIndicator.Value) return;
            
            // Display current language in UI
            // This would integrate with the game's UI system
        }

        private static string GetLanguageName(string languageCode)
        {
            var languageNames = new Dictionary<string, string>
            {
                {"en", "English"},
                {"es", "Español"},
                {"fr", "Français"},
                {"de", "Deutsch"},
                {"ja", "日本語"},
                {"ko", "한국어"},
                {"zh", "中文"},
                {"ru", "Русский"},
                {"pt", "Português"},
                {"it", "Italiano"}
            };
            
            return languageNames.ContainsKey(languageCode) ? languageNames[languageCode] : languageCode.ToUpper();
        }

        private static Dictionary<string, string> GetEnglishTranslations()
        {
            return new Dictionary<string, string>
            {
                {"sus", "suspicious"},
                {"vent", "vent"},
                {"task", "task"},
                {"kill", "kill"},
                {"report", "report"},
                {"emergency", "emergency"},
                {"meeting", "meeting"},
                {"vote", "vote"},
                {"skip", "skip"},
                {"impostor", "impostor"},
                {"crewmate", "crewmate"}
            };
        }

        private static Dictionary<string, string> GetSpanishTranslations()
        {
            return new Dictionary<string, string>
            {
                {"sus", "sospechoso"},
                {"vent", "ventilación"},
                {"task", "tarea"},
                {"kill", "matar"},
                {"report", "reportar"},
                {"emergency", "emergencia"},
                {"meeting", "reunión"},
                {"vote", "votar"},
                {"skip", "saltar"},
                {"impostor", "impostor"},
                {"crewmate", "tripulante"}
            };
        }

        private static Dictionary<string, string> GetFrenchTranslations()
        {
            return new Dictionary<string, string>
            {
                {"sus", "suspect"},
                {"vent", "bouche d'aération"},
                {"task", "tâche"},
                {"kill", "tuer"},
                {"report", "signaler"},
                {"emergency", "urgence"},
                {"meeting", "réunion"},
                {"vote", "voter"},
                {"skip", "passer"},
                {"impostor", "imposteur"},
                {"crewmate", "membre d'équipage"}
            };
        }

        private static Dictionary<string, string> GetGermanTranslations()
        {
            return new Dictionary<string, string>
            {
                {"sus", "verdächtig"},
                {"vent", "Lüftung"},
                {"task", "Aufgabe"},
                {"kill", "töten"},
                {"report", "melden"},
                {"emergency", "Notfall"},
                {"meeting", "Besprechung"},
                {"vote", "abstimmen"},
                {"skip", "überspringen"},
                {"impostor", "Betrüger"},
                {"crewmate", "Besatzungsmitglied"}
            };
        }

        private static Dictionary<string, string> GetJapaneseTranslations()
        {
            return new Dictionary<string, string>
            {
                {"sus", "怪しい"},
                {"vent", "ベント"},
                {"task", "タスク"},
                {"kill", "キル"},
                {"report", "報告"},
                {"emergency", "緊急"},
                {"meeting", "会議"},
                {"vote", "投票"},
                {"skip", "スキップ"},
                {"impostor", "インポスター"},
                {"crewmate", "クルーメイト"}
            };
        }

        private static Dictionary<string, Dictionary<string, string>> GetCommonTerms()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "es", new Dictionary<string, string>
                    {
                        {"red", "rojo"},
                        {"blue", "azul"},
                        {"green", "verde"},
                        {"yellow", "amarillo"},
                        {"orange", "naranja"},
                        {"purple", "morado"},
                        {"pink", "rosa"},
                        {"black", "negro"},
                        {"white", "blanco"},
                        {"brown", "marrón"}
                    }
                },
                {
                    "fr", new Dictionary<string, string>
                    {
                        {"red", "rouge"},
                        {"blue", "bleu"},
                        {"green", "vert"},
                        {"yellow", "jaune"},
                        {"orange", "orange"},
                        {"purple", "violet"},
                        {"pink", "rose"},
                        {"black", "noir"},
                        {"white", "blanc"},
                        {"brown", "marron"}
                    }
                }
            };
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Handle translation commands
                if (chatText.StartsWith("/lang"))
                {
                    HandleLanguageCommand(chatText);
                    return false;
                }
                
                if (chatText.StartsWith("/translate"))
                {
                    HandleTranslateCommand(chatText);
                    return false;
                }
                
                return true;
            }
        }

        private static void HandleLanguageCommand(string command)
        {
            string[] parts = command.Split(' ');
            if (parts.Length > 1)
            {
                string targetLanguage = parts[1].ToLower();
                if (availableLanguages.Contains(targetLanguage))
                {
                    currentLanguage = targetLanguage;
                    CommonUtilities.SendChatMessage($"Language set to: {GetLanguageName(currentLanguage)}");
                }
                else
                {
                    CommonUtilities.SendChatMessage($"Available languages: {string.Join(", ", availableLanguages)}");
                }
            }
            else
            {
                CommonUtilities.SendChatMessage($"Current language: {GetLanguageName(currentLanguage)}");
                CommonUtilities.SendChatMessage($"Available languages: {string.Join(", ", availableLanguages)}");
            }
        }

        private static void HandleTranslateCommand(string command)
        {
            string[] parts = command.Split(' ', 2);
            if (parts.Length > 1)
            {
                string textToTranslate = parts[1];
                string translatedText = TranslateText(textToTranslate, currentLanguage);
                CommonUtilities.SendChatMessage($"Translated: {translatedText}");
            }
        }
    }
}
