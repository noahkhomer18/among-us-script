using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.BehaviorAnalytics
{
    [BepInPlugin("com.yourname.behavioranalytics", "Player Behavior Analytics", "1.0.0")]
    public class BehaviorAnalyticsPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configTrackMovement;
        private static ConfigEntry<bool> configTrackChat;
        private static ConfigEntry<bool> configTrackVoting;
        private static ConfigEntry<bool> configTrackTasks;
        private static ConfigEntry<float> configSuspiciousThreshold;
        private static ConfigEntry<bool> configAutoFlag;
        
        private static Dictionary<byte, PlayerBehavior> playerBehaviors = new Dictionary<byte, PlayerBehavior>();
        private static Dictionary<byte, List<SuspiciousActivity>> suspiciousActivities = new Dictionary<byte, List<SuspiciousActivity>>();
        private static List<byte> flaggedPlayers = new List<byte>();

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable behavior analytics");
            configTrackMovement = Config.Bind("Tracking", "TrackMovement", true, "Track player movement patterns");
            configTrackChat = Config.Bind("Tracking", "TrackChat", true, "Track chat behavior");
            configTrackVoting = Config.Bind("Tracking", "TrackVoting", true, "Track voting patterns");
            configTrackTasks = Config.Bind("Tracking", "TrackTasks", true, "Track task completion behavior");
            configSuspiciousThreshold = Config.Bind("Detection", "SuspiciousThreshold", 0.7f, "Threshold for flagging suspicious behavior");
            configAutoFlag = Config.Bind("Detection", "AutoFlag", true, "Automatically flag suspicious players");
            
            var harmony = new Harmony("com.yourname.behavioranalytics");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("BehaviorAnalytics", "Player Behavior Analytics loaded successfully!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                InitializePlayerTracking();
            }
        }

        private static void InitializePlayerTracking()
        {
            playerBehaviors.Clear();
            suspiciousActivities.Clear();
            flaggedPlayers.Clear();
            
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.Disconnected)
                {
                    playerBehaviors[player.PlayerId] = new PlayerBehavior
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.Data.PlayerName,
                        IsImpostor = player.Data.Role.IsImpostor,
                        StartTime = DateTime.Now,
                        MovementData = new List<MovementPoint>(),
                        ChatMessages = new List<ChatAnalysis>(),
                        VotingPattern = new List<VoteRecord>(),
                        TaskBehavior = new TaskBehaviorData()
                    };
                    
                    suspiciousActivities[player.PlayerId] = new List<SuspiciousActivity>();
                }
            }
            
            CommonUtilities.LogMessage("BehaviorAnalytics", "Initialized player behavior tracking");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class MovementTrackingPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || !configTrackMovement.Value) return;
                
                TrackPlayerMovement(__instance);
            }
        }

        private static void TrackPlayerMovement(PlayerControl player)
        {
            if (!playerBehaviors.ContainsKey(player.PlayerId)) return;
            
            var behavior = playerBehaviors[player.PlayerId];
            var currentPos = player.transform.position;
            
            // Record movement point
            behavior.MovementData.Add(new MovementPoint
            {
                Timestamp = DateTime.Now,
                Position = currentPos,
                Speed = player.GetComponent<Rigidbody2D>()?.velocity.magnitude ?? 0f
            });
            
            // Keep only recent movement data (last 5 minutes)
            var cutoffTime = DateTime.Now.AddMinutes(-5);
            behavior.MovementData.RemoveAll(m => m.Timestamp < cutoffTime);
            
            // Analyze movement patterns
            AnalyzeMovementPatterns(player);
        }

        private static void AnalyzeMovementPatterns(PlayerControl player)
        {
            var behavior = playerBehaviors[player.PlayerId];
            var recentMovements = behavior.MovementData.TakeLast(10).ToList();
            
            if (recentMovements.Count < 5) return;
            
            // Check for suspicious movement patterns
            float avgSpeed = recentMovements.Average(m => m.Speed);
            float speedVariance = CalculateVariance(recentMovements.Select(m => m.Speed));
            
            // Flag if player is moving too fast consistently
            if (avgSpeed > 5f && speedVariance < 0.5f)
            {
                FlagSuspiciousActivity(player, "Consistent high-speed movement", 0.8f);
            }
            
            // Check for teleportation-like behavior
            for (int i = 1; i < recentMovements.Count; i++)
            {
                float distance = Vector3.Distance(recentMovements[i-1].Position, recentMovements[i].Position);
                float timeDiff = (float)(recentMovements[i].Timestamp - recentMovements[i-1].Timestamp).TotalSeconds;
                
                if (distance > 10f && timeDiff < 0.1f)
                {
                    FlagSuspiciousActivity(player, "Possible teleportation detected", 0.9f);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatAnalysisPatch
        {
            public static void Postfix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value || !configTrackChat.Value) return;
                
                AnalyzeChatBehavior(__instance, chatText);
            }
        }

        private static void AnalyzeChatBehavior(PlayerControl player, string message)
        {
            if (!playerBehaviors.ContainsKey(player.PlayerId)) return;
            
            var behavior = playerBehaviors[player.PlayerId];
            var chatAnalysis = new ChatAnalysis
            {
                Timestamp = DateTime.Now,
                Message = message,
                Length = message.Length,
                WordCount = message.Split(' ').Length,
                ContainsCaps = message.Any(c => char.IsUpper(c)),
                ContainsNumbers = message.Any(c => char.IsDigit(c))
            };
            
            behavior.ChatMessages.Add(chatAnalysis);
            
            // Keep only recent chat data
            var cutoffTime = DateTime.Now.AddMinutes(-10);
            behavior.ChatMessages.RemoveAll(c => c.Timestamp < cutoffTime);
            
            // Analyze chat patterns
            AnalyzeChatPatterns(player);
        }

        private static void AnalyzeChatPatterns(PlayerControl player)
        {
            var behavior = playerBehaviors[player.PlayerId];
            var recentChats = behavior.ChatMessages.TakeLast(20).ToList();
            
            if (recentChats.Count < 5) return;
            
            // Check for spam
            var recentMessages = recentChats.Where(c => (DateTime.Now - c.Timestamp).TotalMinutes < 1).ToList();
            if (recentMessages.Count > 10)
            {
                FlagSuspiciousActivity(player, "Excessive chat activity (spam)", 0.6f);
            }
            
            // Check for repetitive messages
            var messageGroups = recentChats.GroupBy(c => c.Message.ToLower()).ToList();
            var repetitiveMessages = messageGroups.Where(g => g.Count() > 3).ToList();
            if (repetitiveMessages.Any())
            {
                FlagSuspiciousActivity(player, "Repetitive chat messages", 0.5f);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
        public static class VotingAnalysisPatch
        {
            public static void Postfix(MeetingHud __instance, byte srcPlayerId, byte suspectPlayerId)
            {
                if (!configEnabled.Value || !configTrackVoting.Value) return;
                
                AnalyzeVotingBehavior(srcPlayerId, suspectPlayerId);
            }
        }

        private static void AnalyzeVotingBehavior(byte voterId, byte targetId)
        {
            if (!playerBehaviors.ContainsKey(voterId)) return;
            
            var behavior = playerBehaviors[voterId];
            var voteRecord = new VoteRecord
            {
                Timestamp = DateTime.Now,
                VoterId = voterId,
                TargetId = targetId,
                IsSelfVote = voterId == targetId
            };
            
            behavior.VotingPattern.Add(voteRecord);
            
            // Analyze voting patterns
            var recentVotes = behavior.VotingPattern.TakeLast(10).ToList();
            
            // Check for suspicious voting patterns
            if (recentVotes.Count >= 3)
            {
                var selfVotes = recentVotes.Count(v => v.IsSelfVote);
                if (selfVotes > recentVotes.Count * 0.5f)
                {
                    FlagSuspiciousActivity(PlayerControl.AllPlayerControls.First(p => p.PlayerId == voterId), 
                        "Excessive self-voting", 0.7f);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
        public static class TaskBehaviorPatch
        {
            public static void Postfix(PlayerControl __instance, uint taskId)
            {
                if (!configEnabled.Value || !configTrackTasks.Value) return;
                
                AnalyzeTaskBehavior(__instance, taskId);
            }
        }

        private static void AnalyzeTaskBehavior(PlayerControl player, uint taskId)
        {
            if (!playerBehaviors.ContainsKey(player.PlayerId)) return;
            
            var behavior = playerBehaviors[player.PlayerId];
            behavior.TaskBehavior.TasksCompleted++;
            behavior.TaskBehavior.LastTaskTime = DateTime.Now;
            
            // Check for suspiciously fast task completion
            if (behavior.TaskBehavior.TasksCompleted > 1)
            {
                var timeSinceLastTask = (DateTime.Now - behavior.TaskBehavior.LastTaskTime).TotalSeconds;
                if (timeSinceLastTask < 2f)
                {
                    FlagSuspiciousActivity(player, "Suspiciously fast task completion", 0.6f);
                }
            }
        }

        private static void FlagSuspiciousActivity(PlayerControl player, string reason, float severity)
        {
            if (!playerBehaviors.ContainsKey(player.PlayerId)) return;
            
            var suspiciousActivity = new SuspiciousActivity
            {
                Timestamp = DateTime.Now,
                Reason = reason,
                Severity = severity,
                PlayerId = player.PlayerId
            };
            
            suspiciousActivities[player.PlayerId].Add(suspiciousActivity);
            
            // Calculate overall suspicious score
            float totalScore = suspiciousActivities[player.PlayerId].Sum(a => a.Severity);
            float avgScore = totalScore / suspiciousActivities[player.PlayerId].Count;
            
            if (avgScore >= configSuspiciousThreshold.Value && !flaggedPlayers.Contains(player.PlayerId))
            {
                flaggedPlayers.Add(player.PlayerId);
                
                if (configAutoFlag.Value)
                {
                    CommonUtilities.SendChatMessage($"⚠️ {player.Data.PlayerName} flagged for suspicious behavior: {reason}");
                }
            }
            
            CommonUtilities.LogMessage("BehaviorAnalytics", $"Flagged {player.Data.PlayerName}: {reason} (Severity: {severity:F2})");
        }

        private static float CalculateVariance(IEnumerable<float> values)
        {
            var valueList = values.ToList();
            if (valueList.Count < 2) return 0f;
            
            float mean = valueList.Average();
            float variance = valueList.Sum(v => (v - mean) * (v - mean)) / valueList.Count;
            return variance;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for analytics commands
                if (chatText == "/analytics" || chatText == "/behavior")
                {
                    ShowPlayerAnalytics(__instance);
                    return false;
                }
                else if (chatText == "/flagged" || chatText == "/suspicious")
                {
                    ShowFlaggedPlayers(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/analyze "))
                {
                    string targetName = chatText.Substring(9);
                    AnalyzeSpecificPlayer(__instance, targetName);
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowPlayerAnalytics(PlayerControl player)
        {
            if (!playerBehaviors.ContainsKey(player.PlayerId))
            {
                CommonUtilities.SendChatMessage("No analytics data available for you");
                return;
            }
            
            var behavior = playerBehaviors[player.PlayerId];
            var suspiciousCount = suspiciousActivities[player.PlayerId].Count;
            var isFlagged = flaggedPlayers.Contains(player.PlayerId);
            
            CommonUtilities.SendChatMessage($"=== {player.Data.PlayerName}'s Analytics ===");
            CommonUtilities.SendChatMessage($"Suspicious Activities: {suspiciousCount}");
            CommonUtilities.SendChatMessage($"Flagged: {(isFlagged ? "Yes" : "No")}");
            CommonUtilities.SendChatMessage($"Chat Messages: {behavior.ChatMessages.Count}");
            CommonUtilities.SendChatMessage($"Movement Points: {behavior.MovementData.Count}");
        }

        private static void ShowFlaggedPlayers(PlayerControl player)
        {
            if (flaggedPlayers.Count == 0)
            {
                CommonUtilities.SendChatMessage("No players currently flagged");
                return;
            }
            
            CommonUtilities.SendChatMessage($"=== Flagged Players ({flaggedPlayers.Count}) ===");
            foreach (var flaggedId in flaggedPlayers)
            {
                var flaggedPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => p.PlayerId == flaggedId);
                if (flaggedPlayer != null)
                {
                    var activities = suspiciousActivities[flaggedId];
                    CommonUtilities.SendChatMessage($"{flaggedPlayer.Data.PlayerName}: {activities.Count} suspicious activities");
                }
            }
        }

        private static void AnalyzeSpecificPlayer(PlayerControl player, string targetName)
        {
            var targetPlayer = PlayerControl.AllPlayerControls.FirstOrDefault(p => 
                p.Data.PlayerName.ToLower().Contains(targetName.ToLower()));
            
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found");
                return;
            }
            
            if (!playerBehaviors.ContainsKey(targetPlayer.PlayerId))
            {
                CommonUtilities.SendChatMessage("No analytics data available for this player");
                return;
            }
            
            var behavior = playerBehaviors[targetPlayer.PlayerId];
            var activities = suspiciousActivities[targetPlayer.PlayerId];
            
            CommonUtilities.SendChatMessage($"=== Analysis: {targetPlayer.Data.PlayerName} ===");
            CommonUtilities.SendChatMessage($"Suspicious Activities: {activities.Count}");
            CommonUtilities.SendChatMessage($"Flagged: {(flaggedPlayers.Contains(targetPlayer.PlayerId) ? "Yes" : "No")}");
            
            if (activities.Count > 0)
            {
                CommonUtilities.SendChatMessage("Recent activities:");
                foreach (var activity in activities.TakeLast(3))
                {
                    CommonUtilities.SendChatMessage($"- {activity.Reason} (Severity: {activity.Severity:F2})");
                }
            }
        }
    }

    [System.Serializable]
    public class PlayerBehavior
    {
        public byte PlayerId;
        public string PlayerName;
        public bool IsImpostor;
        public DateTime StartTime;
        public List<MovementPoint> MovementData;
        public List<ChatAnalysis> ChatMessages;
        public List<VoteRecord> VotingPattern;
        public TaskBehaviorData TaskBehavior;
    }

    [System.Serializable]
    public class MovementPoint
    {
        public DateTime Timestamp;
        public Vector3 Position;
        public float Speed;
    }

    [System.Serializable]
    public class ChatAnalysis
    {
        public DateTime Timestamp;
        public string Message;
        public int Length;
        public int WordCount;
        public bool ContainsCaps;
        public bool ContainsNumbers;
    }

    [System.Serializable]
    public class VoteRecord
    {
        public DateTime Timestamp;
        public byte VoterId;
        public byte TargetId;
        public bool IsSelfVote;
    }

    [System.Serializable]
    public class TaskBehaviorData
    {
        public int TasksCompleted;
        public DateTime LastTaskTime;
    }

    [System.Serializable]
    public class SuspiciousActivity
    {
        public DateTime Timestamp;
        public string Reason;
        public float Severity;
        public byte PlayerId;
    }
}
