#include "version.h"

#ifdef NET35
#define FUNC_ASSEMBLY "System.Core"
.assembly extern FUNC_ASSEMBLY
{
  .publickeytoken = CORE_PUBKEY_TOKEN
  .ver 3:5:0:0
}
#else
#define FUNC_ASSEMBLY "CORE_ASSEMBLY"
#endif

.class public abstract auto ansi sealed beforefieldinit MonoMod.ILHelpers
       extends [CORE_ASSEMBLY]System.Object
{
    .method public hidebysig static !!T TailCallDelegatePtr<T>(native int source) cil managed aggressiveinlining
    {
        .maxstack 8
        ldarg.0
        //tail. // The tail prefix on calli seems to be considered by many tools to be invalid.
        calli !!T()
        ret
    } // end of method Unsafe::Read

    .method public hidebysig static !!T TailCallFunc<T>(class [FUNC_ASSEMBLY]System.Func`1<!!T> func) cil managed aggressiveinlining
    {
        .maxstack 8
        ldarg.0
        tail.
        callvirt instance !0 class [FUNC_ASSEMBLY]System.Func`1<!!T>::Invoke()
        ret
    }
    
    .method public hidebysig static !!T& ObjectAsRef<T>(object obj) cil managed aggressiveinlining
    {
        .maxstack 1
        .locals (
            object pinned pin
#ifndef netcoreapp
            , !!T** refPtr
            , !!T& finalRef
#endif
        )

        // pin obj
        ldarg obj
        stloc pin

#ifdef netcoreapp
        // return ref *Unsafe.BitCast<object, T*>(pin);
        ldloc pin
        conv.u
#else
        // see docs/RuntimeIssueNotes.md - "`fixed` on strings in old Mono" for why this is necessary
        // T* ptr = *(T**)(&pin);
        ldloca pin
        conv.u
        stloc refPtr
        ldloc refPtr
        ldind.i
        // return Unsafe.AsRef<T>(ptr);
        // see the comments inside that function for why don't just immediately ret
        stloc finalRef
        ldloc finalRef
#endif

        ret
    }
}