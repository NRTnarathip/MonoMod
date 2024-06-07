using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoMod.Core.Platforms.Architectures
{
    internal sealed class Arm64Arch : IArchitecture
    {
        public ArchitectureKind Target => ArchitectureKind.Arm64;

        public ArchitectureFeature Features => ArchitectureFeature.None;

        private BytePatternCollection? lazyKnownMethodThunks;
        public unsafe BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, &CreateKnownMethodThunks);

        public IAltEntryFactory AltEntryFactory => throw new NotImplementedException();
        
        private static BytePatternCollection CreateKnownMethodThunks()
        {
            const ulong An = 0xFFFFFFFFFFFF0000 | BytePattern.SAnyValue;
            const ulong Ad = 0xFFFFFFFFFFFF0000 | BytePattern.SAddressValue;
            // const byte Bn = BytePattern.BAnyValue;
            // const byte Bd = BytePattern.BAddressValue;
            
            ushort[] armInsts(params ulong[] pattern) {
                var smallPattern = new List<ushort>();
                
                foreach (var item in pattern)
                {
                    if (item == An) {
                        smallPattern.AddRange(Enumerable.Repeat(BytePattern.SAnyValue, 4));
                    } else if (item == Ad) {
                        smallPattern.AddRange(Enumerable.Repeat(BytePattern.SAddressValue, 4));
                    } else {
                        smallPattern.Add((ushort) ((item >> 0) & 0xFF));
                        smallPattern.Add((ushort) ((item >> 8) & 0xFF));
                        smallPattern.Add((ushort) ((item >> 16) & 0xFF));
                        smallPattern.Add((ushort) ((item >> 24) & 0xFF));
                    }
                }
                
                return smallPattern.ToArray();
            }

            // Adapted from https://github.com/MonoMod/MonoMod.Common/blob/7d2819f3b2309a3127f5ff7b1b9d91a0908eece0/RuntimeDetour/Platforms/Runtime/DetourRuntimeNETPlatform.cs#L153-L201
            if (PlatformDetection.Runtime is RuntimeKind.Framework or RuntimeKind.CoreCLR)
            {
                return new BytePatternCollection(
                    // .NET Core 6
                    
                    // StubPrecode
                    // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L567-L572
                    new(new(AddressKind.Abs64), mustMatchAtStart: true, armInsts(
                        0x10000089, // adr x9, #0x10
                        0xa940312a, // ldp x10, x12, [x9]
                        0xd61f0140, // br x10
                        An,
                        Ad, Ad
                    )),
                    
                    // NDirectImportPrecode
                    // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L628-L633
                    new(new(AddressKind.Abs64), mustMatchAtStart: true, armInsts(
                        0x1000008b, // adr x11, #0x10
                        0xa940316a, // ldp x10, x12, [x11]
                        0xd61f0140, // br x10
                        An,
                        Ad, Ad
                    )),
                    
                    // FixupPrecode
                    // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L666-L672
                    new(new(AddressKind.Abs64), mustMatchAtStart: true, armInsts(
                        0x1000000c, // adr x12, #0x00
                        0x5800006b, // ldr x11, #0x0c
                        0xd61f0160, // br x11
                        An,
                        Ad, Ad
                    )),
                    
                    // ThisPtrRetBufPrecode
                    // https://github.com/dotnet/runtime/blob/4da6b9a8d55913c0ea560d63590d35dc942425be/src/coreclr/vm/arm64/stubs.cpp#L641-L647
                    new(new(AddressKind.Abs64), mustMatchAtStart: true, armInsts(
                        0x91000010, // mov x16, x0
                        0x91000020, // mov x0, x1
                        0x91000201, // mov x1, x16
                        0x58000070, // ldr x16, #0x0c
                        0xd61f0200, // br x16
                        An,
                        Ad, Ad
                    )),
                    
                    // .NET Core 8
                    
                    // FixupPrecode main entry point
                    // https://github.com/dotnet/runtime/blob/17d88ed72da4b70821159169499a805b87739ee6/src/coreclr/vm/arm64/thunktemplates.S#L17-L23
                    new(new(AddressKind.Rel64 | AddressKind.Literal | AddressKind.Indirect, relativeOffset: 0, literalConstant: 0x4000), mustMatchAtStart: true, armInsts(
                        0x5802000b, // ldr	x11, 0x4000
                        0xd61f0160  // br	x11
                    )),
                    
                    // FixupPrecode precode fixup thunk
                    // https://github.com/dotnet/runtime/blob/17d88ed72da4b70821159169499a805b87739ee6/src/coreclr/vm/arm64/thunktemplates.S#L17-L23
                    new(new(AddressKind.PrecodeFixupThunkRel64 | AddressKind.Literal | AddressKind.Indirect, relativeOffset: 0, literalConstant: 0x4008), mustMatchAtStart: true, armInsts(
                        0x5802000c, // ldr	x12, 0x4008
                        0x5802002b, // ldr	x11, 0x4010
                        0xd61f0160  // br	x11
                    )),
                    
                    // CallCountingStub
                    // https://github.com/dotnet/runtime/blob/17d88ed72da4b70821159169499a805b87739ee6/src/coreclr/vm/arm64/thunktemplates.S#L25-L37
                    new(new(AddressKind.Rel64 | AddressKind.Literal | AddressKind.Indirect, relativeOffset: 0, literalConstant: 0x4008), mustMatchAtStart: true, armInsts(
                        0x58020009, // ldr	x9, 0x4000
                        0x7940012a, // ldrh	w10, [x9]
                        0x7100054a, // subs	w10, w10, #0x1
                        0x7900012a, // strh	w10, [x9]
                        0x54000060, // b.eq	0x1c  // b.none
                        0x5801ffa9, // ldr	x9, 0x4008
                        0xd61f0120  // br	x9
                    ))
                );
                
                
                // return new BytePatternCollection(
                //     // StubPrecode
                //     // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L567-L572
                //     new(new(AddressKind.Abs64), mustMatchAtStart: true,
                //         0x89, 0x00, 0x00, 0x10, // adr x9, #0x10
                //         0x2a, 0x31, 0x40, 0xa9, // ldp x10, x12, [x9]
                //         0x40, 0x01, 0x1f, 0xd6, // br x10
                //         An, An, An, An,
                //         Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad
                //     ),
                    
                //     // NDirectImportPrecode
                //     // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L628-L633
                //     new(new(AddressKind.Abs64), mustMatchAtStart: true,
                //         0x8b, 0x00, 0x00, 0x10, // adr x11, #0x10
                //         0x6a, 0x31, 0x40, 0xa9, // ldp x10, x12, [x11]
                //         0x40, 0x01, 0x1f, 0xd6, // br x10
                //         An, An, An, An,
                //         Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad
                //     ),
                    
                //     // FixupPrecode
                //     // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L666-L672
                //     new(new(AddressKind.Abs64), mustMatchAtStart: true,
                //         0x0c, 0x00, 0x00, 0x10, // adr x12, #0x00
                //         0x6b, 0x00, 0x00, 0x58, // ldr x11, #0x0c
                //         0x60, 0x01, 0x1f, 0xd6, // br x11
                //         An, An, An, An,
                //         Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad
                //     ),
                    
                //     // ThisPtrRetBufPrecode
                //     // https://github.com/dotnet/runtime/blob/4da6b9a8d55913c0ea560d63590d35dc942425be/src/coreclr/vm/arm64/stubs.cpp#L641-L647
                //     new(new(AddressKind.Abs64), mustMatchAtStart: true,
                //         0x10, 0x00, 0x00, 0x91, // mov x16, x0
                //         0x20, 0x00, 0x00, 0x91, // mov x0, x1
                //         0x01, 0x02, 0x00, 0x91, // mov x1, x16
                //         0x70, 0x00, 0x00, 0x58, // ldr x16, #0x0c
                //         0x00, 0x02, 0x1f, 0xd6, // br x16
                //         An, An, An, An,
                //         Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad
                //     )
                // );
            }
            else
            {
                // TODO: Mono
                return new();
            }
        }

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