#if ENGINE
using System.Diagnostics;
#endif
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using static System.Reflection.Emit.OpCodes;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using static HarmonyLib.AccessTools;
using static HarmonyLib.CodeInstruction;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;

namespace CameraTweaks;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin {
	public const string ModName = "CameraTweaks";
	public const string ModGUID = "LGPLv3.MCPO." + ModName;
	public const string ModVersion = "1.0.0";
	public static Plugin Instance { get; private set; } = null!;
	private readonly Harmony _harmony = new(ModGUID);

	internal static ConfigEntry<bool> Apply;
	internal static ConfigEntry<bool> CameraScrollEnabled;
	internal static bool InternalCameraScrollEnabled;
	internal static ConfigEntry<bool> CameraZoomEnabled;
	internal static bool InternalCameraZoomEnabled;
	internal static ConfigEntry<bool> DisablePerfectPixelFilter;
	internal static bool InternalDisablePerfectPixelFilter;
	internal static ConfigEntry<bool> DisableScreenCrack;
	internal static bool InternalDisableScreenCrack;
	internal static ConfigEntry<bool> DisableBloodSplat;
	internal static bool InternalDisableBloodSplat;
	internal static ConfigEntry<bool> ChangeableCamEffectMult;
	internal static bool InternalChangeableCamEffectMult;

	internal static ConfigEntry<float> DefaultCameraScroll;
	internal static ConfigEntry<float> MinCameraScroll;
	internal static ConfigEntry<float> MaxCameraScroll;
	internal static ConfigEntry<float> DefaultCameraZoom;
	internal static ConfigEntry<float> MinCameraZoom;
	internal static ConfigEntry<float> MaxCameraZoom;
	internal static ConfigEntry<float> CamEffectMult;

	internal static ConfigEntry<bool> MouseScrollEnabled;
	internal static ConfigEntry<KeyCode> MouseScrollModifier;
	internal static ConfigEntry<float> MouseScrollMultiplier;
	internal static ConfigEntry<bool> MouseZoomEnabled;
	internal static ConfigEntry<KeyCode> MouseZoomModifier;
	internal static ConfigEntry<float> MouseZoomMultiplier;
	internal static ConfigEntry<KeyCode>[] modarr;

	internal static ConfigEntry<bool> KeyboardEnabled;
	internal static ConfigEntry<KeyCode> KeyPlus;
	internal static ConfigEntry<KeyCode> KeyMinus;
	internal static ConfigEntry<float> KeyValue;
	internal static ConfigEntry<KeyCode> KeyZoomPlus;
	internal static ConfigEntry<KeyCode> KeyZoomMinus;
	internal static ConfigEntry<float> KeyZoomValue;

	internal static ConfigEntry<KeyCode> KeyZoomReset;
	internal static ConfigEntry<KeyCode> KeyReset;
	internal static ConfigEntry<KeyCode> KeyTempReset;

	internal static Camera cam = null;
	private static float CameraScroll;
	private static float? CameraScrolltemp = null;

	public void Awake() {
#if ENGINE
		Debugger.Break();
#endif
		Instance = this;

		Apply = Config.Bind("Patches", "Apply", false, "Applies everything in this section, for use in the config manager");
		CameraScrollEnabled = Config.Bind("Patches", "CameraScroll", true, "Enables Camera Scrolling");
		InternalCameraScrollEnabled = CameraScrollEnabled.Value;
		CameraZoomEnabled = Config.Bind("Patches", "CameraZoom", true, "Enables zooming out the camera");
		InternalCameraZoomEnabled = CameraZoomEnabled.Value;
		DisablePerfectPixelFilter = Config.Bind("Patches", "DisablePerfectPixelFilter", false, "Disables the pixel alignment filter");
		InternalDisablePerfectPixelFilter = DisablePerfectPixelFilter.Value;
		DisableScreenCrack = Config.Bind("Patches", "DisableScreenCrack", false, "Disables the chance a bullet crack can appear on screen when shot");
		InternalDisableScreenCrack = DisableScreenCrack.Value;
		DisableBloodSplat = Config.Bind("Patches", "DisableBloodSplat", false, "Disables the chance a blood splatter can appear on screen when dismembering a limb");
		InternalDisableBloodSplat = DisableBloodSplat.Value;
		ChangeableCamEffectMult = Config.Bind("Patches", "ChangeableCamEffectMult", false, "Instead of reading vanilla effect multipler for the camera, use 'CamEffectMult' from this mod");
		InternalChangeableCamEffectMult = ChangeableCamEffectMult.Value;

		DefaultCameraScroll = Config.Bind("CameraScroll.Camera", "Default", 10f, "The default scroll value");
		MinCameraScroll = Config.Bind("CameraScroll.Camera", "Min", 0f, "The minimum scroll value");
		MaxCameraScroll = Config.Bind("CameraScroll.Camera", "Max", float.MaxValue, "The maximum scroll value");
		DefaultCameraZoom = Config.Bind("CameraZoom.Camera", "Default", 6.4f, "The default scroll value");
		MinCameraZoom = Config.Bind("CameraZoom.Camera", "Min", 1f, "The minimum scroll value");
		MaxCameraZoom = Config.Bind("CameraZoom.Camera", "Max", float.MaxValue, "The maximum scroll value");
		CamEffectMult = Config.Bind("Camera.Effect", "Mult", 1f, "The effect multipler for the camera");

		MouseScrollEnabled = Config.Bind("CameraScroll.Mouse", "Enabled", true, "If camera scrolling is affected by the mouse wheel");
		MouseScrollModifier = Config.Bind("CameraScroll.Mouse", "KeyModifier", KeyCode.LeftControl, "Key to hold down for wheel to affect scrolling, set to None to disable modifier requirement");
		MouseScrollMultiplier = Config.Bind("CameraScroll.Mouse", "Multiplier", 1f, "Multiply scrolling by this amount");
		MouseZoomEnabled = Config.Bind("CameraZoom.Mouse", "Enabled", true, "If camera scrolling is affected by the mouse wheel");
		MouseZoomModifier = Config.Bind("CameraZoom.Mouse", "KeyModifier", KeyCode.LeftShift, "Key to hold down for wheel to affect scrolling, set to None to disable modifier requirement");
		MouseZoomMultiplier = Config.Bind("CameraZoom.Mouse", "Multiplier", 1f, "Multiply scrolling by this amount");
		modarr = [MouseScrollModifier, MouseZoomModifier];

		KeyboardEnabled = Config.Bind("CameraScroll.Keyboard", "Enabled", true, "If camera scrolling is affected by keycodes");
		KeyPlus = Config.Bind("CameraScroll.Keyboard", "KeyPlus", KeyCode.Equals, "Scrolls the camera farther");
		KeyMinus = Config.Bind("CameraScroll.Keyboard", "KeyMinus", KeyCode.Minus, "Scrolls the camera closer");
		KeyValue = Config.Bind("CameraScroll.Keyboard", "KeyValue", 10f, "How much to scroll the camera by");
		KeyZoomPlus = Config.Bind("CameraZoom.Keyboard", "KeyPlus", KeyCode.RightBracket, "Zooms the camera closer");
		KeyZoomMinus = Config.Bind("CameraZoom.Keyboard", "KeyMinus", KeyCode.LeftBracket, "Zooms the camera farther");
		KeyZoomValue = Config.Bind("CameraZoom.Keyboard", "KeyValue", 2f, "How much to zoom the camera by");

		KeyZoomReset = Config.Bind("CameraZoom.Keybindings", "KeyReset", KeyCode.Mouse2, "Reset camera zoom values to default");
		KeyReset = Config.Bind("CameraScroll.Keybindings", "KeyReset", KeyCode.Mouse3, "Reset camera scroll values to default");
		KeyTempReset = Config.Bind("CameraScroll.Keybindings", "KeyTempReset", KeyCode.Mouse4, "Toggles default and modified scroll");

		if(InternalCameraScrollEnabled) {
			_harmony.Patch(Method(typeof(CameraFollow), nameof(CameraFollow.LateUpdate)), transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.CameraScrollTrans)));
		}

		if(InternalCameraZoomEnabled && !InternalDisablePerfectPixelFilter) {
			_harmony.Patch(Method(typeof(PixelPerfectCamera), nameof(PixelPerfectCamera.OnBeginContextRendering)), transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.CameraZoomAndPixel)));
		}

		if(InternalDisablePerfectPixelFilter) {
			_harmony.Patch(Method(typeof(CameraFollow), nameof(CameraFollow.Awake)), prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.DisablePPC)));
		}

		if(InternalDisableScreenCrack) {
			_harmony.Patch(Method(typeof(WeaponScript), nameof(WeaponScript.Shoot)), transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.NoScreenCrack)));
		}

		if(InternalDisableBloodSplat) {
			_harmony.Patch(Method(typeof(DismemberManager), nameof(DismemberManager.Update)), transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.NoBloodSplat)));
		}

		if(InternalChangeableCamEffectMult) {
			_harmony.Patch(Method(typeof(CameraFollow), nameof(CameraFollow.AddOffset)), transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.ReplacecamEffectMult)));
			_harmony.Patch(Method(typeof(CameraFollow), nameof(CameraFollow.AddRot)), transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.ReplacecamEffectMult)));
		}

		_harmony.PatchAll();

		if(Camera.main != null) {
			cam = Camera.main;
		}
	}

	private void OnDestroy() {
		_harmony?.UnpatchSelf();
	}

	public void Update() {
		if(Apply.Value == true) {
			Apply.Value = false;

			MethodBase orig;
			MethodInfo patch;
			bool configval = CameraScrollEnabled.Value;
			if(configval != InternalCameraScrollEnabled) {
				orig = Method(typeof(CameraFollow), nameof(CameraFollow.LateUpdate));
				patch = Method(typeof(Patches), nameof(Patches.CameraScrollTrans));
				if(configval) {
					_harmony.Patch(orig, transpiler: new HarmonyMethod(patch));
				} else {
					_harmony.Unpatch(orig, patch);
				}
				InternalCameraScrollEnabled = configval;
			}

			configval = DisablePerfectPixelFilter.Value;
			if(configval != InternalDisablePerfectPixelFilter) {
				orig = Method(typeof(CameraFollow), nameof(CameraFollow.Awake));
				patch = Method(typeof(Patches), nameof(Patches.DisablePPC));
				if(configval) {
					_harmony.Patch(orig, prefix: new HarmonyMethod(patch));
				} else {
					_harmony.Unpatch(orig, patch);
				}
				InternalDisablePerfectPixelFilter = configval;
			}

			configval = CameraZoomEnabled.Value;
			if(!InternalDisablePerfectPixelFilter && configval != InternalCameraZoomEnabled) {
				orig = Method(typeof(PixelPerfectCamera), nameof(PixelPerfectCamera.OnBeginContextRendering));
				patch = Method(typeof(Patches), nameof(Patches.CameraZoomAndPixel));
				if(configval) {
					_harmony.Patch(orig, transpiler: new HarmonyMethod(patch));
				} else {
					_harmony.Unpatch(orig, patch);
				}
				InternalCameraZoomEnabled = configval;
 			}

			configval = DisableScreenCrack.Value;
			if(configval != InternalDisableScreenCrack) {
				orig = Method(typeof(WeaponScript), nameof(WeaponScript.Shoot));
				patch = Method(typeof(Patches), nameof(Patches.NoScreenCrack));
				if(configval) {
					_harmony.Patch(orig, transpiler: new HarmonyMethod(patch));
				} else {
					_harmony.Unpatch(orig, patch);
				}
				InternalDisableScreenCrack = DisableScreenCrack.Value;
			}

			configval = DisableBloodSplat.Value;
			if(configval != InternalDisableBloodSplat) {
				orig = Method(typeof(DismemberManager), nameof(DismemberManager.Update));
				patch = Method(typeof(Patches), nameof(Patches.NoBloodSplat));
				if(configval) {
					_harmony.Patch(orig, transpiler: new HarmonyMethod(patch));
				} else {
					_harmony.Unpatch(orig, patch);
				}
				InternalDisableBloodSplat = configval;
			}

			configval = ChangeableCamEffectMult.Value;
			if(configval != InternalChangeableCamEffectMult) {
				orig = Method(typeof(CameraFollow), nameof(CameraFollow.AddOffset));
				patch = Method(typeof(Patches), nameof(Patches.ReplacecamEffectMult));
				MethodBase orig2 = Method(typeof(CameraFollow), nameof(CameraFollow.AddOffset));
				MethodInfo patch2 = Method(typeof(Patches), nameof(Patches.ReplacecamEffectMult));
				if(configval) {
					_harmony.Patch(orig, transpiler: new HarmonyMethod(patch));
					_harmony.Patch(orig2, transpiler: new HarmonyMethod(patch2));
				} else {
					_harmony.Unpatch(orig, patch);
					_harmony.Unpatch(orig2, patch2);
				}
				InternalChangeableCamEffectMult = configval;
			}
		}
	}

	internal enum Modifier {
		Scroll,
		Zoom
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static internal bool ModifierHandling(Modifier mod, ref bool consumed) {
		if(consumed) {
			return false;
		}

		KeyCode? keyconfig = null;
		switch(mod) {
			case Modifier.Scroll: {
				keyconfig = MouseScrollModifier.Value;
			break;
			} case Modifier.Zoom: {
				keyconfig = MouseZoomModifier.Value;
			break;
			} default: {
				return false;
			}
		}

		if(keyconfig != null) {
			if(keyconfig == KeyCode.None) {
				for(int i = 0; i < modarr.Length; i++) {
					if(i == (int)mod) {
						continue;
					}

					if(Input.GetKey(modarr[i].Value)) {
						return false;
					}
				}
				return true;
			} else {
				if(Input.GetKey((KeyCode)keyconfig)) {
					consumed = true;
					return true;
				}
			}
		}
		return false;
	}

	static class AutoPatches {
		[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.Update))]
		static class FCameraU {
			static internal void Postfix() {
				bool consumed = false;
				if(InternalCameraScrollEnabled) {
					if(MouseScrollEnabled.Value && ModifierHandling(Modifier.Scroll, ref consumed)) {
						CameraScroll += Input.mouseScrollDelta.y * MouseScrollMultiplier.Value;
						if(CameraScroll < MinCameraScroll.Value) {
							CameraScroll = MinCameraScroll.Value;
						} else if(CameraScroll > MaxCameraScroll.Value) {
							CameraScroll = MaxCameraScroll.Value;
						}
					}

					if(KeyboardEnabled.Value) {
						if(Input.GetKeyDown(KeyPlus.Value)) {
							CameraScroll += KeyValue.Value;
							if(CameraScroll > MaxCameraScroll.Value) {
								CameraScroll = MaxCameraScroll.Value;
							}
						} else if(Input.GetKeyDown(KeyMinus.Value)) {
							CameraScroll -= KeyValue.Value;
							if(CameraScroll < MinCameraScroll.Value) {
								CameraScroll = MinCameraScroll.Value;
							}
						}
					}

					if(Input.GetKeyDown(KeyReset.Value)) {
						CameraScroll = DefaultCameraScroll.Value;
						CameraScrolltemp = null;
					}

					if(Input.GetKeyDown(KeyTempReset.Value)) {
						if(CameraScrolltemp == null) {
							CameraScrolltemp = CameraScroll;
							CameraScroll = DefaultCameraScroll.Value;
						} else {
							CameraScroll = (float)CameraScrolltemp;
							CameraScrolltemp = null;
						}
					}
				}

				if(CameraZoomEnabled.Value) {
					if(MouseZoomEnabled.Value && ModifierHandling(Modifier.Zoom, ref consumed)) {
						cam.orthographicSize -= Input.mouseScrollDelta.y * MouseZoomMultiplier.Value;
						if(cam.orthographicSize < MinCameraZoom.Value) {
							cam.orthographicSize = MinCameraZoom.Value;
						} else if(cam.orthographicSize > MaxCameraZoom.Value) {
							cam.orthographicSize = MaxCameraZoom.Value;
						}
					}

					if(KeyboardEnabled.Value) {
						if(Input.GetKeyDown(KeyZoomPlus.Value)) {
							cam.orthographicSize += KeyZoomValue.Value;
							if(cam.orthographicSize > MaxCameraZoom.Value) {
								cam.orthographicSize = MaxCameraZoom.Value;
							}
						} else if(Input.GetKeyDown(KeyZoomMinus.Value)) {
							cam.orthographicSize -= KeyZoomValue.Value;
							if(cam.orthographicSize < MinCameraZoom.Value) {
								cam.orthographicSize = MinCameraZoom.Value;
							}
						}
					}

					if(Input.GetKeyDown(KeyZoomReset.Value)) {
						cam.orthographicSize = DefaultCameraZoom.Value;
					}
				}
			}
		}

		[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.Awake))]
		static class FCameraA {
			static internal void Postfix(CameraFollow __instance) {
				cam = __instance.GetComponent<Camera>();
				if(CameraZoomEnabled.Value) {
					cam.orthographicSize = DefaultCameraZoom.Value;
				}

				if(InternalCameraScrollEnabled) {
					CameraScroll = DefaultCameraScroll.Value;
					CameraScrolltemp = null;
				}
			}
		}
	}

	static class Patches {
		static internal IEnumerable<CodeInstruction> CameraScrollTrans(IEnumerable<CodeInstruction> instructions) {
			int startidx = -1;
			int endidx = -1;
			List<CodeInstruction> codes = new(instructions);
			int stcode = -1;
			List<int> ldidxs = [];
			for(int i = 0; i < codes.Count; i++) {
				CodeInstruction cur = codes[i];
				if(startidx == -1) {
					if(cur.opcode == Ldc_R4 && (float)cur.operand == 10) {
						startidx = i;
					}
				} else {
					if(cur.opcode == Ldc_R4 && (float)cur.operand == 20) {
						endidx = i + 1;
						CodeInstruction stloc = codes[endidx];
						if(stloc.opcode.Value >= Stloc_0.Value && stloc.opcode.Value <= Stloc_3.Value) {
							stcode = stloc.opcode.Value - Stloc_0.Value;
						}
						break;
					}
				}
			}

			if(endidx != -1 && stcode != -1) {
				int ldcode = Ldloc_0.Value + stcode;
				for(int i = 0; i < codes.Count; i++) {
					if(codes[i].opcode.Value == ldcode) {
						ldidxs.Add(i);
					}
				}

				CodeInstruction myfield = LoadField(typeof(Plugin), nameof(Plugin.CameraScroll));
				if(ldidxs.Count != 0) {
					for(int i = 0; i < ldidxs.Count; i++) {
						int idx = ldidxs[i];
						codes.RemoveAt(idx);
						codes.Insert(idx, myfield);
					}
					codes.RemoveRange(startidx, endidx - startidx + 1);
				}
			}

			return (IEnumerable<CodeInstruction>)codes;
		}

		static internal IEnumerable<CodeInstruction> CameraZoomAndPixel(IEnumerable<CodeInstruction> instructions) {
			int startidx = -1;
			int endidx = -1;
			List<CodeInstruction> codes = new(instructions);
			FieldInfo CineComp = Field(typeof(PixelPerfectCamera), nameof(PixelPerfectCamera.m_CinemachineCompatibilityMode));
			MethodInfo setOrtho = Method(typeof(Camera), "set_" + nameof(Camera.orthographicSize));
			for(int i = 0; i < codes.Count; i++) {
				if(startidx == -1) {
					if(codes[i].opcode == Ldarg_0 && codes[i + 1].LoadsField(CineComp)) {
						startidx = i;
					}
				} else {
					if(codes[i].Calls(setOrtho)) {
						endidx = i;
					}
				}
			}

			if(endidx != -1) {
				List<Label> afterendlabs = codes[endidx + 1].labels;
				foreach(Label lab in codes[startidx].labels) {
					afterendlabs.Add(lab);
				}
				codes.RemoveRange(startidx, (endidx - startidx) + 1);
			}

			return (IEnumerable<CodeInstruction>)codes;
		}

		static internal void DisablePPC(CameraFollow __instance) {
			__instance.GetComponent<PixelPerfectCamera>().enabled = false;
		}

		public static IEnumerable<CodeInstruction> NoScreenCrack(IEnumerable<CodeInstruction> instructions) {
			int startidx = -1;
			List<CodeInstruction> codes = new(instructions);
			MethodInfo crak = Method(typeof(CameraFollow), nameof(CameraFollow.CreateScreenCrack));
			for(int i = 0; i < codes.Count; i++) {
				if(startidx == -1) {
					if(codes[i].opcode == Ldloc_S && codes[i + 1].opcode == Ldc_R4 && (float)codes[i + 1].operand == 20) {
						startidx = i;
					}
				} else {
					if(codes[i].Calls(crak)) {
						codes.RemoveRange(startidx, (i - startidx) + 1);
						startidx = -1;
					}
				}
			}

			return (IEnumerable<CodeInstruction>)codes;
		}

		public static IEnumerable<CodeInstruction> NoBloodSplat(IEnumerable<CodeInstruction> instructions) {
			int startidx = -1;
			List<CodeInstruction> codes = new(instructions);
			FieldInfo cam = Field(typeof(CameraFollow), nameof(CameraFollow.cam));
			MethodInfo spalt = Method(typeof(CameraFollow), nameof(CameraFollow.CreateBloodSplat));
			for(int i = 0; i < codes.Count; i++) {
				if(codes[i].LoadsField(cam)) {
					startidx = i;
				} else if(codes[i].Calls(spalt)) {
					codes.RemoveRange(startidx, (i - startidx) + 1);
					break;
				}
			}

			return (IEnumerable<CodeInstruction>)codes;
		}

		public static IEnumerable<CodeInstruction> ReplacecamEffectMult(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new(instructions);
			FieldInfo cammult = Field(typeof(CameraFollow), nameof(CameraFollow.camEffectMult));
			for(int i = 0; i < codes.Count; i++) {
				if(codes[i].opcode == Ldarg_0 && codes[i + 1].LoadsField(cammult)) {
					codes.RemoveRange(i, 2);
					codes.InsertRange(i, [LoadField(typeof(Plugin), nameof(Plugin.CamEffectMult)), Call(typeof(ConfigEntry<float>), "get_" + nameof(ConfigEntry<float>.Value))]);
					break;
				}
			}
			return (IEnumerable<CodeInstruction>)codes;
		}
	}
}
