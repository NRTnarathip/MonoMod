#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET
#define UNSAFE_IN_ILHELPERS
#endif

extern alias ilhelpers;

// Sometimes these global usings are unused. That's fine.
#pragma warning disable IDE0005

// Global usings
global using ilhelpers::MonoMod;

#if UNSAFE_IN_ILHELPERS && !NET6_0_OR_GREATER
global using Unsafe = ilhelpers::System.Runtime.CompilerServices.Unsafe;
#else
global using Unsafe = System.Runtime.CompilerServices.Unsafe;
#endif

#pragma warning restore IDE0005

#if UNSAFE_IN_ILHELPERS
// SRCS.Unsafe is defined in ILHelpers, so we want to define UnsafeRaw + a type-forwarder

#if NET6_0_OR_GREATER
using ILImpl = System.Runtime.CompilerServices.Unsafe;
#else
using ILImpl = ilhelpers::System.Runtime.CompilerServices.Unsafe;
#endif

using System;
using System.Runtime.CompilerServices;

[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(ILImpl))]

namespace MonoMod.Backports.ILHelpers;

[CLSCompliant(false)]
public static unsafe class UnsafeRaw
#else
// SRCS.Unsafe is defined here, so we want to define Unsafe

using MonoMod.Backports;

using ILImpl = ilhelpers::MonoMod.Backports.ILHelpers.UnsafeRaw;

namespace System.Runtime.CompilerServices;

[CLSCompliant(false)]
public static unsafe class Unsafe
#endif
{
    #region Direct forwarders
#nullable disable
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static T Read<T>(void* source) => ILImpl.Read<T>(source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static T ReadUnaligned<T>(void* source) => ILImpl.ReadUnaligned<T>(source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static T ReadUnaligned<T>(ref byte source) => ILImpl.ReadUnaligned<T>(ref source);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void Write<T>(void* destination, T value) => ILImpl.Write(destination, value);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void WriteUnaligned<T>(void* destination, T value) => ILImpl.WriteUnaligned(destination, value);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void WriteUnaligned<T>(ref byte destination, T value) => ILImpl.WriteUnaligned(ref destination, value);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void Copy<T>(void* destination, ref T source) => ILImpl.Copy(destination, ref source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void Copy<T>(ref T destination, void* source) => ILImpl.Copy(ref destination, source);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void* AsPointer<T>(ref T value) => ILImpl.AsPointer(ref value);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void SkipInit<T>(out T value) => ILImpl.SkipInit(out value);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void CopyBlock(void* destination, void* source, uint byteCount) => ILImpl.CopyBlock(destination, source, byteCount);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void CopyBlock(ref byte destination, ref byte source, uint byteCount) => ILImpl.CopyBlock(ref destination, ref source, byteCount);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void CopyBlockUnaligned(void* destination, void* source, uint byteCount) => ILImpl.CopyBlockUnaligned(destination, source, byteCount);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount) => ILImpl.CopyBlockUnaligned(ref destination, ref source, byteCount);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void InitBlock(void* startAddress, byte value, uint byteCount) => ILImpl.InitBlock(startAddress, value, byteCount);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void InitBlock(ref byte startAddress, byte value, uint byteCount) => ILImpl.InitBlock(ref startAddress, value, byteCount);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void InitBlockUnaligned(void* startAddress, byte value, uint byteCount) => ILImpl.InitBlockUnaligned(startAddress, value, byteCount);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount) => ILImpl.InitBlockUnaligned(ref startAddress, value, byteCount);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static T As<T>(object o) where T : class => ILImpl.As<T>(o);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T AsRef<T>(void* source) => ref ILImpl.AsRef<T>(source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T AsRef<T>(in T source) => ref ILImpl.AsRef(in source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref TTo As<TFrom, TTo>(ref TFrom source) => ref ILImpl.As<TFrom, TTo>(ref source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Unbox<T>(object box) where T : struct => ref ILImpl.Unbox<T>(box);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T AddByteOffset<T>(ref T source, nint byteOffset) => ref ILImpl.AddByteOffset(ref source, byteOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T AddByteOffset<T>(ref T source, nuint byteOffset) => ref ILImpl.AddByteOffset(ref source, byteOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T SubtractByteOffset<T>(ref T source, nint byteOffset) => ref ILImpl.SubtractByteOffset(ref source, byteOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T SubtractByteOffset<T>(ref T source, nuint byteOffset) => ref ILImpl.SubtractByteOffset(ref source, byteOffset);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static nint ByteOffset<T>(ref T origin, ref T target) => ILImpl.ByteOffset(ref origin, ref target);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static bool AreSame<T>(ref T left, ref T right) => ILImpl.AreSame(ref left, ref right);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static bool IsAddressGreaterThan<T>(ref T left, ref T right) => ILImpl.IsAddressGreaterThan(ref left, ref right);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static bool IsAddressLessThan<T>(ref T left, ref T right) => ILImpl.IsAddressLessThan(ref left, ref right);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static bool IsNullRef<T>(ref T source) => ILImpl.IsNullRef(ref source);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T NullRef<T>() => ref ILImpl.NullRef<T>();
#nullable enable
    #endregion

#if !UNSAFE_IN_ILHELPERS
    // See docs/RuntimeIssueNotes.md. Until 2015, Mono returned incorrect values for the sizeof opcode when applied to a type parameter.
    // To deal with this, we need to compute type size in another way, and return it as appropriate, specializing all of the below accordingly.
    private static class PerTypeValues<T>
    {
        public static readonly nint TypeSize = ComputeTypeSize();

        private static nint ComputeTypeSize()
        {
            var array = new T[2];
            return ILImpl.ByteOffset(ref array[0], ref array[1]);
        }
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static int SizeOf<T>() => (int)PerTypeValues<T>.TypeSize;

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Add<T>(ref T source, int elementOffset) => ref ILImpl.AddByteOffset(ref source, (nint)elementOffset * PerTypeValues<T>.TypeSize);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void* Add<T>(void* source, int elementOffset) => (byte*)source + (elementOffset * PerTypeValues<T>.TypeSize);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Add<T>(ref T source, nint elementOffset) => ref ILImpl.AddByteOffset(ref source, elementOffset * PerTypeValues<T>.TypeSize);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Add<T>(ref T source, nuint elementOffset) => ref ILImpl.AddByteOffset(ref source, elementOffset * (nuint)PerTypeValues<T>.TypeSize);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Subtract<T>(ref T source, int elementOffset) => ref ILImpl.SubtractByteOffset(ref source, (nint)elementOffset * PerTypeValues<T>.TypeSize);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void* Subtract<T>(void* source, int elementOffset) => (byte*)source - (elementOffset * PerTypeValues<T>.TypeSize);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Subtract<T>(ref T source, nint elementOffset) => ref ILImpl.SubtractByteOffset(ref source, elementOffset * PerTypeValues<T>.TypeSize);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Subtract<T>(ref T source, nuint elementOffset) => ref ILImpl.SubtractByteOffset(ref source, elementOffset * (nuint)PerTypeValues<T>.TypeSize);

#else

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static int SizeOf<T>() => ILImpl.SizeOf<T>();

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Add<T>(ref T source, int elementOffset) => ref ILImpl.Add(ref source, elementOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void* Add<T>(void* source, int elementOffset) => ILImpl.Add<T>(source, elementOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Add<T>(ref T source, nint elementOffset) => ref ILImpl.Add(ref source, elementOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Add<T>(ref T source, nuint elementOffset) => ref ILImpl.Add(ref source, elementOffset);

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Subtract<T>(ref T source, int elementOffset) => ref ILImpl.Subtract(ref source, elementOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static void* Subtract<T>(void* source, int elementOffset) => ILImpl.Subtract<T>(source, elementOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Subtract<T>(ref T source, nint elementOffset) => ref ILImpl.Subtract(ref source, elementOffset);
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining), NonVersionable]
    public static ref T Subtract<T>(ref T source, nuint elementOffset) => ref ILImpl.Subtract(ref source, elementOffset);

#endif
}