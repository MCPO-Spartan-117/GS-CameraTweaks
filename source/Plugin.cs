using System.Runtime.CompilerServices;
using System.Collections.Generic;
using static System.Reflection.Emit.OpCodes;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using static HarmonyLib.CodeInstruction;
using UnityEngine;

namespace CameraTweaks;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin {
	public const string ModName = "CameraTweaks";
	public const string ModGUID = "LGPLv3.MCPO." + ModName;
	public const string ModVersion = "1.0.0";
	public static Plugin Instance { get; private set; } = null!;
	private readonly Harmony _harmony = new(ModGUID);

	internal static ConfigEntry<float> DefaultCameraScroll;
	internal static ConfigEntry<float> MinCameraScroll;
	internal static ConfigEntry<float> MaxCameraScroll;

	internal static ConfigEntry<bool> MouseScrollEnabled;
	internal static ConfigEntry<KeyCode> MouseScrollModifier;
	internal static ConfigEntry<float> MouseScrollMultiplier;

	internal static ConfigEntry<bool> KeyboardEnabled;
	internal static ConfigEntry<KeyCode> KeyPlus;
	internal static ConfigEntry<KeyCode> KeyMinus;
	internal static ConfigEntry<float> KeyValue;

	internal static ConfigEntry<KeyCode> KeyReset;
	internal static ConfigEntry<KeyCode> KeyTempReset;

	private static float CameraScroll;
	private static float? CameraScrolltemp = null;

	public void Awake() {
		Instance = this;

		DefaultCameraScroll = Config.Bind("CameraScroll.Camera", "Default", 10f, "The default scroll value");
		CameraScroll = DefaultCameraScroll.Value;
		MinCameraScroll = Config.Bind("CameraScroll.Camera", "Min", 0f, "The minimum scroll value");
		MaxCameraScroll = Config.Bind("CameraScroll.Camera", "Max", float.MaxValue, "The maximum scroll value");

		MouseScrollEnabled = Config.Bind("CameraScroll.Mouse", "Enabled", true, "If camera scrolling is affected by the mouse wheel");
		MouseScrollModifier = Config.Bind("CameraScroll.Mouse", "KeyModifier", KeyCode.LeftControl, "Key to hold down for wheel to affect scrolling, set to None to disable modifier requirement");
		MouseScrollMultiplier = Config.Bind("CameraScroll.Mouse", "Multiplier", 1f, "Multiply scrolling by this amount");

		KeyboardEnabled = Config.Bind("CameraScroll.Keyboard", "Enabled", true, "If camera scrolling is affected by keycodes");
		KeyPlus = Config.Bind("CameraScroll.Keyboard", "KeyPlus", KeyCode.Equals, "Scrolls the camera farther");
		KeyMinus = Config.Bind("CameraScroll.Keyboard", "KeyMinus", KeyCode.Minus, "Scrolls the camera closer");
		KeyValue = Config.Bind("CameraScroll.Keyboard", "KeyValue", 10f, "How much to scroll the camera by");

		KeyReset = Config.Bind("CameraScroll.Keybindings", "KeyReset", KeyCode.Mouse3, "Reset camera scroll values to default");
		KeyTempReset = Config.Bind("CameraScroll.Keybindings", "KeyTempReset", KeyCode.Mouse4, "Toggles default and modified scroll");

		_harmony.PatchAll();
	}

	public void Update() {
		if(MouseScrollEnabled.Value && ModifierHandling()) {
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ModifierHandling() {
		KeyCode keyconfig = MouseScrollModifier.Value;
		if(keyconfig == KeyCode.None) {
			return true;
		} else {
			return Input.GetKey(keyconfig);
		}
	}


	[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.LateUpdate))]
	static class CameraScrollPatch {
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
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
	}
}
