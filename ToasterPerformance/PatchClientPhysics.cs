using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ToasterPerformance
{
	// Token: 0x02000002 RID: 2
	public static class PatchClientPhysics
	{
		// Token: 0x04000001 RID: 1
		public const bool ReplacePuckCheckSphere = true;

		// Token: 0x04000002 RID: 2
		public const bool ThrottleHoverOnClient = true;

		// Token: 0x04000003 RID: 3
		public const bool GateStickPositionerOnClient = true;

		// Token: 0x04000004 RID: 4
		private static readonly AccessTools.FieldRef<Puck, float> s_puckGroundedRadius = AccessTools.FieldRefAccess<Puck, float>("groundedCheckSphereRadius");

		// Token: 0x04000005 RID: 5
		private static readonly AccessTools.FieldRef<Puck, float> s_puckSpeed = AccessTools.FieldRefAccess<Puck, float>("Speed");

		// Token: 0x04000006 RID: 6
		private static readonly AccessTools.FieldRef<Puck, float> s_puckAngularSpeed = AccessTools.FieldRefAccess<Puck, float>("AngularSpeed");

		// Token: 0x04000007 RID: 7
		private static readonly AccessTools.FieldRef<Puck, bool> s_puckIsGrounded = AccessTools.FieldRefAccess<Puck, bool>("<IsGrounded>k__BackingField");

		// Token: 0x04000008 RID: 8
		private static readonly ConditionalWeakTable<Hover, PatchClientPhysics.HoverState> s_hoverState = new ConditionalWeakTable<Hover, PatchClientPhysics.HoverState>();

		// Token: 0x0200000B RID: 11
		[HarmonyPatch(typeof(Puck), "FixedUpdate")]
		public static class PatchPuckFixedUpdate
		{
			// Token: 0x0600001E RID: 30 RVA: 0x000028EC File Offset: 0x00000AEC
			[HarmonyPrefix]
			public static bool Prefix(Puck __instance)
			{
				NetworkManager singleton = NetworkManager.Singleton;
				if (singleton == null || singleton.IsServer)
				{
					return true;
				}
				Rigidbody rigidbody = __instance.Rigidbody;
				if (rigidbody == null)
				{
					return false;
				}
				PatchClientPhysics.s_puckSpeed.Invoke(__instance) = rigidbody.linearVelocity.magnitude;
				PatchClientPhysics.s_puckAngularSpeed.Invoke(__instance) = rigidbody.angularVelocity.magnitude;
				float num = PatchClientPhysics.s_puckGroundedRadius.Invoke(__instance);
				bool flag = __instance.transform.position.y < num;
				PatchClientPhysics.s_puckIsGrounded.Invoke(__instance) = flag;
				SphereCollider netSphereCollider = __instance.NetSphereCollider;
				if (netSphereCollider != null)
				{
					float predictedSpeed = __instance.PredictedSpeed;
					float num2 = flag ? 0f : Mathf.Clamp(predictedSpeed * 0.025f, 0.15f, 0.75f);
					if (netSphereCollider.radius < num2)
					{
						netSphereCollider.radius = num2;
					}
					else if (netSphereCollider.radius > num2)
					{
						netSphereCollider.radius = Mathf.Lerp(netSphereCollider.radius, num2, Time.fixedDeltaTime * 5f);
					}
				}
				return false;
			}
		}

		// Token: 0x0200000C RID: 12
		private sealed class HoverState
		{
			// Token: 0x04000035 RID: 53
			public int FrameParity;
		}

		// Token: 0x0200000D RID: 13
		[HarmonyPatch(typeof(Hover), "FixedUpdate")]
		public static class PatchHoverFixedUpdate
		{
			// Token: 0x06000020 RID: 32 RVA: 0x00002A10 File Offset: 0x00000C10
			[HarmonyPrefix]
			public static bool Prefix(Hover __instance)
			{
				NetworkManager singleton = NetworkManager.Singleton;
				if (singleton == null || singleton.IsServer)
				{
					return true;
				}
				PatchClientPhysics.HoverState value = PatchClientPhysics.s_hoverState.GetValue(__instance, (Hover _) => new PatchClientPhysics.HoverState());
				value.FrameParity++;
				return (value.FrameParity & 1) == 0;
			}
		}

		// Token: 0x0200000E RID: 14
		[HarmonyPatch(typeof(StickPositioner), "FixedUpdate")]
		public static class PatchStickPositionerFixedUpdate
		{
			// Token: 0x06000021 RID: 33 RVA: 0x00002A78 File Offset: 0x00000C78
			[HarmonyPrefix]
			public static bool Prefix()
			{
				NetworkManager singleton = NetworkManager.Singleton;
				return singleton == null || singleton.IsServer;
			}
		}
	}
}
