using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterPerformance
{
	// Token: 0x02000009 RID: 9
	public static class PatchUIAndMisc
	{
		// Token: 0x06000014 RID: 20 RVA: 0x00002719 File Offset: 0x00000919
		private static Camera GetMainCamera()
		{
			if (PatchUIAndMisc.s_cachedMainCamera == null || !PatchUIAndMisc.s_cachedMainCamera.isActiveAndEnabled)
			{
				PatchUIAndMisc.s_cachedMainCamera = Camera.main;
			}
			return PatchUIAndMisc.s_cachedMainCamera;
		}

		// Token: 0x0400002F RID: 47
		private static Camera s_cachedMainCamera;

		// Token: 0x04000030 RID: 48
		private static readonly AccessTools.FieldRef<UIPlayerUsernames, float> s_yOffsetField = AccessTools.FieldRefAccess<UIPlayerUsernames, float>("yOffset");

		// Token: 0x0200002A RID: 42
		[HarmonyPatch]
		public static class PatchUsernameWorldToScreen
		{
			// Token: 0x06000042 RID: 66 RVA: 0x00003605 File Offset: 0x00001805
			public static MethodBase TargetMethod()
			{
				return AccessTools.Method(typeof(UIPlayerUsernames), "UsernameWorldToScreen", null, null);
			}

			// Token: 0x06000043 RID: 67 RVA: 0x00003620 File Offset: 0x00001820
			[HarmonyPrefix]
			public static bool Prefix(UIPlayerUsernames __instance, VisualElement playerVisualElement, PlayerBody playerBody)
			{
				Camera mainCamera = PatchUIAndMisc.GetMainCamera();
				if (mainCamera == null)
				{
					return false;
				}
				float num = PatchUIAndMisc.s_yOffsetField.Invoke(__instance);
				float fadeThreshold = __instance.FadeThreshold;
				float maximumDistance = __instance.MaximumDistance;
				float fadeRange = __instance.FadeRange;
				VisualElement rootVisualElement = __instance.RootVisualElement;
				if (rootVisualElement == null || rootVisualElement.panel == null)
				{
					return false;
				}
				Vector3 position = mainCamera.transform.position;
				Vector3 position2 = playerBody.transform.position;
				float num2 = Vector3.Distance(position, position2);
				Vector3 vector = mainCamera.WorldToScreenPoint(position2 + Vector3.up * num);
				vector.y = (float)Screen.height - vector.y;
				Vector2 vector2 = RuntimePanelUtils.ScreenToPanel(rootVisualElement.panel, vector);
				if (vector.z < 0f)
				{
					playerVisualElement.style.display = DisplayStyle.None;
					return false;
				}
				float num3 = maximumDistance * fadeThreshold;
				float num4 = num3 + fadeRange;
				float num5 = Mathf.Clamp01(Utils.Map(num2, num3, num4, 1f, 0f));
				playerVisualElement.style.display = DisplayStyle.Flex;
				playerVisualElement.style.left = vector2.x;
				playerVisualElement.style.top = vector2.y;
				playerVisualElement.style.opacity = new StyleFloat(num5);
				return false;
			}
		}
	}
}
