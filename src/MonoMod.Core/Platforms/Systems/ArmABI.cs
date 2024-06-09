using System;

namespace MonoMod.Core.Platforms.Systems
{
    internal static class ArmABI
	{
		public static TypeClassification ClassifyArm64(Type type, bool isReturn) {
			// This obviously wrong. However, currently the only place that ClassifyType is used is in PlatformTriple.GetRealDetourTarget
			// to detect if a function has a return buffer. On arm64, the return buffer is always passed through x8, not as a parameter, so no ABI fix is ever needed.
			// For now just always return InRegister to stop PlatformTriple.GetRealDetourTarget from generating abi fixup glue.
			// TODO: Do this properly.
			return TypeClassification.InRegister;
		}
	}
}