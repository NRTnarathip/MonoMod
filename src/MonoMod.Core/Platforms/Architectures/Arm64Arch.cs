using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;

namespace MonoMod.Core.Platforms.Architectures
{
    internal sealed class Arm64Arch : IArchitecture
    {
        public ArchitectureKind Target => ArchitectureKind.Arm64;

        public ArchitectureFeature Features => ArchitectureFeature.None;

        public BytePatternCollection KnownMethodThunks => new BytePatternCollection(
            new BytePattern(new(AddressKind.Rel32, 5), mustMatchAtStart: true, 0x88, 0x77, 0x99) // Just put something random here to make the errors go away
        );

        public IAltEntryFactory AltEntryFactory => throw new NotImplementedException();

        private sealed class Abs64Kind : DetourKindBase
        {
            public static readonly Abs64Kind Instance = new();
            
            public override int Size => 4 + 4 + 8;

            public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle)
            {
                Unsafe.WriteUnaligned(ref buffer[0], (uint) 0x58000049); // ldr x9, 0x8
                Unsafe.WriteUnaligned(ref buffer[4], (uint) 0xd61f0120); // br x9
                
                Unsafe.WriteUnaligned(ref buffer[8], (long) to);
                
                allocHandle = null;
                return Size;
            }

            public override bool TryGetRetargetInfo(NativeDetourInfo orig, IntPtr to, int maxSize, out NativeDetourInfo retargetInfo)
            {
                // we can always trivially retarget an abs64 detour (change the absolute constant)
                retargetInfo = orig with { To = to };
                return true;
            }
            
            public override int DoRetarget(NativeDetourInfo origInfo, IntPtr to, Span<byte> buffer, object? data,
                out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
            {
                needsRepatch = true;
                disposeOldAlloc = true;
                
                return GetBytes(origInfo.From, to, buffer, data, out allocationHandle);
            }
        }

        private readonly ISystem system;

        public Arm64Arch(ISystem system)
        {
            this.system = system;
        }

        public NativeDetourInfo ComputeDetourInfo(IntPtr from, IntPtr target, int maxSizeHint = -1)
        {
            if (maxSizeHint < 0)
            {
                maxSizeHint = int.MaxValue;
            }
            
            if (maxSizeHint < Abs64Kind.Instance.Size)
            {
                MMDbgLog.Warning($"Size too small for all known detour kinds; defaulting to Abs64. provided size: {maxSizeHint}");
            }
            
            return new(from, target, Abs64Kind.Instance, null);
        }
        
        public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocHandle)
        {
            return DetourKindBase.GetDetourBytes(info, buffer, out allocHandle);
        }

        public NativeDetourInfo ComputeRetargetInfo(NativeDetourInfo detour, IntPtr to, int maxSizeHint = -1)
        {
            if (DetourKindBase.TryFindRetargetInfo(detour, to, maxSizeHint, out var retarget))
            {
                // the detour knows how to retarget itself, we'll use that
                return retarget;
            }
            else
            {
                // the detour doesn't know how to retarget itself, lets just compute a new detour to our new target
                return ComputeDetourInfo(detour.From, to, maxSizeHint);
            }
        }

        public int GetRetargetBytes(NativeDetourInfo original, NativeDetourInfo retarget, Span<byte> buffer,
            out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
        {
            return DetourKindBase.DoRetarget(original, retarget, buffer, out allocationHandle, out needsRepatch, out disposeOldAlloc);
        }

        public ReadOnlyMemory<IAllocatedMemory> CreateNativeVtableProxyStubs(IntPtr vtableBase, int vtableSize)
        {
            throw new NotImplementedException();
        }

        public IAllocatedMemory CreateSpecialEntryStub(IntPtr target, IntPtr argument)
        {
            throw new NotImplementedException();
        }
    }
}