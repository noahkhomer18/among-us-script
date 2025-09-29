using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.DeathAnimationCustomizer
{
    [BepInPlugin("com.yourname.deathanimationcustomizer", "Death Animation Customizer", "1.0.0")]
    public class DeathAnimationCustomizerPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configCustomEffects;
        private static ConfigEntry<bool> configCustomSounds;
        private static ConfigEntry<bool> configCustomParticles;
        private static ConfigEntry<Color> configDeathColor;
        private static ConfigEntry<float> configEffectDuration;
        private static ConfigEntry<string> configDeathSound;
        
        private static Dictionary<byte, DeathEffect> playerDeathEffects = new Dictionary<byte, DeathEffect>();
        private static List<AudioClip> customDeathSounds = new List<AudioClip>();
        private static List<GameObject> deathParticles = new List<GameObject>();

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable death animation customization");
            configCustomEffects = Config.Bind("Effects", "CustomEffects", true, "Enable custom death effects");
            configCustomSounds = Config.Bind("Audio", "CustomSounds", true, "Enable custom death sounds");
            configCustomParticles = Config.Bind("Particles", "CustomParticles", true, "Enable custom death particles");
            configDeathColor = Config.Bind("Visual", "DeathColor", Color.red, "Color of death effects");
            configEffectDuration = Config.Bind("Timing", "EffectDuration", 3f, "Duration of death effects in seconds");
            configDeathSound = Config.Bind("Audio", "DeathSound", "default", "Custom death sound to play");
            
            var harmony = new Harmony("com.yourname.deathanimationcustomizer");
            harmony.PatchAll();
            
            LoadCustomAssets();
            CommonUtilities.LogMessage("DeathAnimationCustomizer", "Death Animation Customizer loaded successfully!");
        }

        private static void LoadCustomAssets()
        {
            // Load custom death sounds
            if (configCustomSounds.Value)
            {
                // This would load custom audio clips from a resources folder
                // For now, we'll use placeholder logic
                CommonUtilities.LogMessage("DeathAnimationCustomizer", "Loading custom death sounds...");
            }
            
            // Load custom particle effects
            if (configCustomParticles.Value)
            {
                // This would load custom particle systems
                CommonUtilities.LogMessage("DeathAnimationCustomizer", "Loading custom particle effects...");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static void Postfix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value) return;
                
                // Create custom death effect
                CreateDeathEffect(target);
                
                // Play custom death sound
                PlayDeathSound(target);
                
                // Create death particles
                CreateDeathParticles(target);
            }
        }

        private static void CreateDeathEffect(PlayerControl target)
        {
            if (!configCustomEffects.Value) return;
            
            // Create death effect object
            GameObject deathEffect = new GameObject($"DeathEffect_{target.PlayerId}");
            deathEffect.transform.position = target.transform.position;
            
            // Add visual effect component
            var effect = deathEffect.AddComponent<DeathEffect>();
            effect.Initialize(target, configEffectDuration.Value);
            
            // Store effect for cleanup
            playerDeathEffects[target.PlayerId] = effect;
            
            CommonUtilities.LogMessage("DeathAnimationCustomizer", $"Created death effect for {target.Data.PlayerName}");
        }

        private static void PlayDeathSound(PlayerControl target)
        {
            if (!configCustomSounds.Value) return;
            
            // Play custom death sound
            if (customDeathSounds.Count > 0)
            {
                var randomSound = customDeathSounds[UnityEngine.Random.Range(0, customDeathSounds.Count)];
                AudioSource.PlayClipAtPoint(randomSound, target.transform.position);
            }
            else
            {
                // Play default enhanced sound
                AudioSource.PlayClipAtPoint(Resources.Load<AudioClip>("Sounds/death"), target.transform.position);
            }
            
            CommonUtilities.LogMessage("DeathAnimationCustomizer", $"Played death sound for {target.Data.PlayerName}");
        }

        private static void CreateDeathParticles(PlayerControl target)
        {
            if (!configCustomParticles.Value) return;
            
            // Create particle system
            GameObject particleSystem = new GameObject($"DeathParticles_{target.PlayerId}");
            particleSystem.transform.position = target.transform.position;
            
            var particles = particleSystem.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startColor = configDeathColor.Value;
            main.startLifetime = configEffectDuration.Value;
            main.startSpeed = 5f;
            main.maxParticles = 100;
            
            var emission = particles.emission;
            emission.rateOverTime = 50f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 100)
            });
            
            // Auto-destroy after duration
            UnityEngine.Object.Destroy(particleSystem, configEffectDuration.Value);
            
            CommonUtilities.LogMessage("DeathAnimationCustomizer", $"Created death particles for {target.Data.PlayerName}");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Revive))]
        public static class RevivePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value) return;
                
                // Clean up death effects when player is revived
                if (playerDeathEffects.ContainsKey(__instance.PlayerId))
                {
                    var effect = playerDeathEffects[__instance.PlayerId];
                    if (effect != null)
                    {
                        UnityEngine.Object.Destroy(effect.gameObject);
                    }
                    playerDeathEffects.Remove(__instance.PlayerId);
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                // Clear all death effects when new game starts
                foreach (var effect in playerDeathEffects.Values)
                {
                    if (effect != null)
                    {
                        UnityEngine.Object.Destroy(effect.gameObject);
                    }
                }
                playerDeathEffects.Clear();
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for death effect commands
                if (chatText.StartsWith("/deatheffect "))
                {
                    string effectName = chatText.Substring(12);
                    SetPlayerDeathEffect(__instance, effectName);
                    return false;
                }
                
                return true;
            }
        }

        private static void SetPlayerDeathEffect(PlayerControl player, string effectName)
        {
            // This would set a custom death effect for the player
            CommonUtilities.SendChatMessage($"Death effect '{effectName}' set for {player.Data.PlayerName}");
        }
    }

    public class DeathEffect : MonoBehaviour
    {
        private PlayerControl targetPlayer;
        private float duration;
        private float startTime;
        private Color effectColor;
        
        public void Initialize(PlayerControl player, float effectDuration)
        {
            targetPlayer = player;
            duration = effectDuration;
            startTime = Time.time;
            effectColor = Color.red;
            
            // Start the death effect animation
            StartCoroutine(DeathEffectCoroutine());
        }
        
        private System.Collections.IEnumerator DeathEffectCoroutine()
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                // Create pulsing effect
                float scale = 1f + Mathf.Sin(elapsed * 10f) * 0.2f;
                transform.localScale = Vector3.one * scale;
                
                // Fade out over time
                float alpha = 1f - (elapsed / duration);
                effectColor.a = alpha;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Destroy the effect
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
