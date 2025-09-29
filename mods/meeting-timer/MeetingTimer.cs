using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.MeetingTimer
{
    [BepInPlugin("com.yourname.meetingtimer", "Meeting Timer", "1.0.0")]
    public class MeetingTimerPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<float> configDiscussionTime;
        private static ConfigEntry<float> configVotingTime;
        private static ConfigEntry<bool> configShowTimer;
        private static ConfigEntry<bool> configAutoSkipVoting;
        private static ConfigEntry<Color> configTimerColor;
        
        private static float discussionStartTime;
        private static float votingStartTime;
        private static bool isDiscussionPhase = false;
        private static bool isVotingPhase = false;
        private static GameObject timerUI;
        private static TMPro.TextMeshPro timerText;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the meeting timer");
            configDiscussionTime = Config.Bind("Timing", "DiscussionTime", 60f, "Discussion time in seconds");
            configVotingTime = Config.Bind("Timing", "VotingTime", 30f, "Voting time in seconds");
            configShowTimer = Config.Bind("UI", "ShowTimer", true, "Show visual timer");
            configAutoSkipVoting = Config.Bind("UI", "AutoSkipVoting", false, "Auto-skip voting if no votes");
            configTimerColor = Config.Bind("UI", "TimerColor", Color.yellow, "Color of the timer text");
            
            var harmony = new Harmony("com.yourname.meetingtimer");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("MeetingTimer", "Meeting Timer loaded successfully!");
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                StartDiscussionPhase();
                CreateTimerUI();
            }
        }

        private static void StartDiscussionPhase()
        {
            isDiscussionPhase = true;
            isVotingPhase = false;
            discussionStartTime = Time.time;
            
            CommonUtilities.SendChatMessage($"Discussion phase started! {configDiscussionTime.Value} seconds remaining.");
        }

        private static void CreateTimerUI()
        {
            if (!configShowTimer.Value) return;
            
            // Create timer UI
            timerUI = new GameObject("MeetingTimerUI");
            timerUI.transform.SetParent(HudManager.Instance.transform);
            
            // Create timer text
            timerText = timerUI.AddComponent<TMPro.TextMeshPro>();
            timerText.text = "Discussion: 60s";
            timerText.fontSize = 3f;
            timerText.color = configTimerColor.Value;
            timerText.transform.localPosition = new Vector3(0, 3f, 0);
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
        public static class MeetingUpdatePatch
        {
            public static void Postfix(MeetingHud __instance)
            {
                if (!configEnabled.Value) return;
                
                UpdateTimer();
                CheckPhaseTransitions();
            }
        }

        private static void UpdateTimer()
        {
            if (timerText == null) return;
            
            float remainingTime = 0f;
            string phaseText = "";
            
            if (isDiscussionPhase)
            {
                remainingTime = configDiscussionTime.Value - (Time.time - discussionStartTime);
                phaseText = "Discussion";
            }
            else if (isVotingPhase)
            {
                remainingTime = configVotingTime.Value - (Time.time - votingStartTime);
                phaseText = "Voting";
            }
            
            if (remainingTime > 0)
            {
                timerText.text = $"{phaseText}: {remainingTime:F0}s";
                timerText.color = remainingTime < 10f ? Color.red : configTimerColor.Value;
            }
            else
            {
                timerText.text = $"{phaseText}: 0s";
                timerText.color = Color.red;
            }
        }

        private static void CheckPhaseTransitions()
        {
            if (isDiscussionPhase)
            {
                float discussionElapsed = Time.time - discussionStartTime;
                if (discussionElapsed >= configDiscussionTime.Value)
                {
                    StartVotingPhase();
                }
            }
            else if (isVotingPhase)
            {
                float votingElapsed = Time.time - votingStartTime;
                if (votingElapsed >= configVotingTime.Value)
                {
                    EndMeeting();
                }
            }
        }

        private static void StartVotingPhase()
        {
            isDiscussionPhase = false;
            isVotingPhase = true;
            votingStartTime = Time.time;
            
            CommonUtilities.SendChatMessage($"Voting phase started! {configVotingTime.Value} seconds remaining.");
        }

        private static void EndMeeting()
        {
            isDiscussionPhase = false;
            isVotingPhase = false;
            
            CommonUtilities.SendChatMessage("Time's up! Meeting ended.");
            
            // Force end meeting
            if (MeetingHud.Instance != null)
            {
                MeetingHud.Instance.RpcClose();
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
        public static class VotingCompletePatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Voting completed early
                isVotingPhase = false;
                CommonUtilities.SendChatMessage("Voting completed!");
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
        public static class MeetingClosePatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Clean up timer
                isDiscussionPhase = false;
                isVotingPhase = false;
                
                if (timerUI != null)
                {
                    UnityEngine.Object.Destroy(timerUI);
                    timerUI = null;
                    timerText = null;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for timer commands
                if (chatText == "/timer" || chatText == "/time")
                {
                    ShowTimeRemaining(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowTimeRemaining(PlayerControl player)
        {
            if (!isDiscussionPhase && !isVotingPhase)
            {
                CommonUtilities.SendChatMessage("No meeting in progress!");
                return;
            }
            
            float remainingTime = 0f;
            string phaseText = "";
            
            if (isDiscussionPhase)
            {
                remainingTime = configDiscussionTime.Value - (Time.time - discussionStartTime);
                phaseText = "Discussion";
            }
            else if (isVotingPhase)
            {
                remainingTime = configVotingTime.Value - (Time.time - votingStartTime);
                phaseText = "Voting";
            }
            
            if (remainingTime > 0)
            {
                CommonUtilities.SendChatMessage($"{phaseText} phase: {remainingTime:F0} seconds remaining");
            }
            else
            {
                CommonUtilities.SendChatMessage($"{phaseText} phase: Time's up!");
            }
        }
    }
}
