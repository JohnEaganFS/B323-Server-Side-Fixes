using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace B323ServerSideFixes
{
    // Minimal mod entry implementing IPuckMod. Initializes Harmony patches on enable.
    public class B323ServerSideFixesInit : IPuckPlugin
    {
        private const string HarmonyId = "com.johneagan.puck.b323serversidefixes";

        private static readonly Harmony harmony = new Harmony(HarmonyId);
        private static readonly MethodInfo faceOffStartedPrefix = AccessTools.Method(typeof(B323ServerSideFixesInit), nameof(OnFaceOffStartedPrefix));
        private static bool patched;

        public bool OnEnable()
        {
            try
            {
                PatchReplayLifecycle();
                Debug.Log("B323 Server-Side Fixes mod enabled.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public bool OnDisable()
        {
            try
            {
                if (patched)
                {
                    harmony.UnpatchSelf();
                    patched = false;
                }
                Debug.Log("B323 Server-Side Fixes mod disabled.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        private static void PatchReplayLifecycle()
        {
            if (patched)
            {
                return;
            }

            PatchFaceOffStarted<PublicGameModeConfig>();
            PatchFaceOffStarted<CompetitiveGameModeConfig>();
            patched = true;
        }

        private static void PatchFaceOffStarted<TConfig>() where TConfig : StandardGameModeConfig, new()
        {
            MethodInfo original = AccessTools.Method(typeof(StandardGameMode<TConfig>), "OnFaceOffStarted");
            if (original == null)
            {
                throw new MissingMethodException(typeof(StandardGameMode<TConfig>).FullName, "OnFaceOffStarted");
            }

            harmony.Patch(original, prefix: new HarmonyMethod(faceOffStartedPrefix));
        }

        private static void OnFaceOffStartedPrefix(object __instance)
        {
            ReplayManager replayManager = GetReplayManager(__instance);
            if (replayManager == null)
            {
                Debug.LogWarning("B323 Server-Side Fixes: could not find ReplayManager before FaceOff replay reset.");
                return;
            }

            replayManager.Server_StopReplaying();
            replayManager.Server_StopRecording();
        }

        private static ReplayManager GetReplayManager(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo replayManagerField = AccessTools.Field(instance.GetType(), "ReplayManager");
            return replayManagerField?.GetValue(instance) as ReplayManager;
        }
    }
}
