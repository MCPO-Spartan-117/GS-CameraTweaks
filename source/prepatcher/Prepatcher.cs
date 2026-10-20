using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using static Mono.Cecil.Cil.OpCodes;
using static Mono.Cecil.MethodAttributes;

namespace CameraTweaksPre {
	public class Prepatcher {
		public static IEnumerable<string> TargetDLLs { get; } = ["Assembly-CSharp.dll"];

		public static void Patch(AssemblyDefinition assembly) {
#if DEBUG
			Debugger.Break();
#endif
			TypeDefinition playerclass = assembly.MainModule.Types.First(t => t.Name == "CameraFollow");
			if(playerclass != null) {
				MethodDefinition onDestroyMeth = new("OnDestroy", Public | HideBySig, assembly.MainModule.TypeSystem.Void);
				onDestroyMeth.Body.GetILProcessor().Emit(Ret);
				playerclass.Methods.Add(onDestroyMeth);
			}
#if DEBUG
			Debugger.Break();
#endif
		}
	}
}
