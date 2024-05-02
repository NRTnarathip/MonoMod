using MonoMod.Core.Platforms.Systems;
using MonoMod.Utils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

#if NET6_USE_RUNTIME_INTROSPECTION
using System.Reflection;
using System.Runtime.CompilerServices;
#endif
using static MonoMod.Core.Interop.CoreCLR;

namespace MonoMod.Core.Platforms.Runtimes
{
    internal class Core60Runtime : Core50Runtime
    {
        public Core60Runtime(ISystem system) : base(system) { }

        // src/coreclr/inc/jiteeversionguid.h line 46
        // 5ed35c58-857b-48dd-a818-7c0136dc9f73
        private static readonly Guid JitVersionGuid = new Guid(
            0x5ed35c58,
            0x857b,
            0x48dd,
            0xa8, 0x18, 0x7c, 0x01, 0x36, 0xdc, 0x9f, 0x73
        );

        protected override Guid ExpectedJitVersion => JitVersionGuid;

        protected override InvokeCompileMethodPtr InvokeCompileMethodPtr => V60.InvokeCompileMethodPtr;

        protected override Delegate CastCompileHookToRealType(Delegate del)
            => del.CastDelegate<V60.CompileMethodDelegate>();
        
        private Delegate? jitPostHook;
        
        protected unsafe override void InstallJitHook(IntPtr jit)
        {
            if (PlatformDetection.Architecture == ArchitectureKind.Arm64 && PlatformDetection.OS == OSKind.OSX)
            {
                CheckVersionGuid(jit);

                // Get the real compile method vtable slot
                var compileMethodSlot = GetVTableEntry(jit, VtableIndexICorJitCompilerCompileMethod);
                
                var macosNativeHelper = ((MacOSSystem) System).NativeHelperInstance;
                
                var jitHookDelegateHolder = new JitHookDelegateHolder(this);
                var postHookDelegate = ((Delegate) jitHookDelegateHolder.CompileMethodPostHook).CastDelegate<V60.CompileMethodPostHookDelegate>();
                jitPostHook = postHookDelegate; // stash it away so that it stays alive forever
                
                var postHookPtr = Marshal.GetFunctionPointerForDelegate(postHookDelegate);
                
                macosNativeHelper.InitializeJitHook(*compileMethodSlot, postHookPtr);
                
                var ourCompileMethodPtr = macosNativeHelper.JitCompileHookFunc;
                
                // and now we can install our method pointer as a JIT hook
                Span<byte> ptrData = stackalloc byte[sizeof(IntPtr)];
                MemoryMarshal.Write(ptrData, ref ourCompileMethodPtr);

                System.PatchData(PatchTargetKind.ReadOnly, (IntPtr)compileMethodSlot, ptrData, default);
            }
            else
            {
                base.InstallJitHook(jit);
            }
        }
        
        private sealed class JitHookDelegateHolder
        {
            public readonly Core60Runtime Runtime;
            public readonly JitHookHelpersHolder JitHookHelpers;
            
            private static bool installedAllocHook;
            
            public JitHookDelegateHolder(Core60Runtime runtime)
            {
                Runtime = runtime;
                JitHookHelpers = runtime.JitHookHelpers;
            }
            
            [SuppressMessage("Design", "CA1031:Do not catch general exception types",
                Justification = "We want to swallow exceptions here to prevent them from bubbling out of the JIT")]
            public unsafe void CompileMethodPostHook(IntPtr corJitInfo, V21.CORINFO_METHOD_INFO* methodInfo, byte** nativeEntry, uint* nativeSizeOfCode, byte* hotCodeRW)
            {
                var lastError = MarshalEx.GetLastPInvokeError();
                
                try
                {
                    if (!installedAllocHook) {
                        var allocMemSlot = GetVTableEntry(corJitInfo, V60.VtableIndexICorJitInfoAllocMem);
                        
                        var macosNativeHelper = ((MacOSSystem) Runtime.System).NativeHelperInstance;
                        macosNativeHelper.SetOriginalJitMemAlloc(*allocMemSlot);
                        
                        var ourAllocMemPtr = macosNativeHelper.JitMemAllocHookFunc;
                        
                        Span<byte> ptrData = stackalloc byte[sizeof(IntPtr)];
                        MemoryMarshal.Write(ptrData, ref ourAllocMemPtr);

                        Runtime.System.PatchData(PatchTargetKind.ReadOnly, (IntPtr)allocMemSlot, ptrData, default);
                        
                        installedAllocHook = true;
                    }
                    
                    if (hotCodeRW == null) return;

                    // This is the top level JIT entry point, do our custom stuff
                    RuntimeTypeHandle[]? genericClassArgs = null;
                    RuntimeTypeHandle[]? genericMethodArgs = null;

                    if (methodInfo->args.sigInst.classInst != null)
                    {
                        genericClassArgs = new RuntimeTypeHandle[methodInfo->args.sigInst.classInstCount];
                        for (var i = 0; i < genericClassArgs.Length; i++)
                        {
                            genericClassArgs[i] = JitHookHelpers.GetTypeFromNativeHandle(methodInfo->args.sigInst.classInst[i]).TypeHandle;
                        }
                    }
                    if (methodInfo->args.sigInst.methInst != null)
                    {
                        genericMethodArgs = new RuntimeTypeHandle[methodInfo->args.sigInst.methInstCount];
                        for (var i = 0; i < genericMethodArgs.Length; i++)
                        {
                            genericMethodArgs[i] = JitHookHelpers.GetTypeFromNativeHandle(methodInfo->args.sigInst.methInst[i]).TypeHandle;
                        }
                    }

                    var declaringType = JitHookHelpers.GetDeclaringTypeOfMethodHandle(methodInfo->ftn).TypeHandle;
                    var method = JitHookHelpers.CreateHandleForHandlePointer(methodInfo->ftn);

                    Runtime.OnMethodCompiledCore(declaringType, method, genericClassArgs, genericMethodArgs, (IntPtr)(*nativeEntry), (IntPtr) hotCodeRW, *nativeSizeOfCode);
                }
                catch
                {
                    // eat the exception so we don't accidentally bubble up to native code
                }
                finally
                {
                    MarshalEx.SetLastPInvokeError(lastError);
                }
            }
        }

#if NET6_USE_RUNTIME_INTROSPECTION
        public override RuntimeFeature Features 
            => base.Features & ~RuntimeFeature.RequiresBodyThunkWalking;

        private unsafe IntPtr GetMethodBodyPtr(MethodBase method, RuntimeMethodHandle handle) {
            var md = (V60.MethodDesc*) handle.Value;

            md = V60.MethodDesc.FindTightlyBoundWrappedMethodDesc(md);

            return (IntPtr) md->GetNativeCode();
        }

        public override unsafe IntPtr GetMethodEntryPoint(MethodBase method) {
            method = GetIdentifiable(method);
            var handle = GetMethodHandle(method);

            GetPtr:
            var ptr = GetMethodBodyPtr(method, handle);
            if (ptr == IntPtr.Zero) { // the method hasn't been JITted yet
                // TODO: call PlatformTriple.Prepare instead to handle generic methods
                RuntimeHelpers.PrepareMethod(handle);
                goto GetPtr;
            }

            return ptr;
        }
#endif
    }
}
