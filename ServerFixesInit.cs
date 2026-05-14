using System;
using UnityEngine;
using HarmonyLib;

namespace B323ServerSideFixes
{
    // Minimal mod entry implementing IPuckMod. Initializes Harmony patches on enable.
    public class B323ServerSideFixesInit : IPuckPlugin
    {
        private const string HarmonyId = "com.johneagan.puck.b323serversidefixes";

        private static readonly Harmony harmony = new Harmony(HarmonyId);
        private static bool patched;

        public bool OnEnable()
        {
            try
            {
                PatchReplayLifecycle();
                Debug.Log("B323 Server-Side Fixes mod enabled. Hi Toter!");
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

            ReplayFaceOffLifecyclePatch.Apply(harmony);
            ReplayStickSimulationPatch.Apply(harmony);
            ReplayGhostObjectCleanupPatch.Apply(harmony);
            patched = true;
        }
    }
}
