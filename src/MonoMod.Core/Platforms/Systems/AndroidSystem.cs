using Microsoft.Win32.SafeHandles;
using MonoMod.Core.Interop;
using MonoMod.Core.Platforms.Memory;
using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoMod.Core.Platforms.Systems;
public sealed class AndroidSystem : ISystem, IInitialize<IArchitecture>
{
    sealed class MmapPagedMemoryAllocator : PagedMemoryAllocator
    {
        public MmapPagedMemoryAllocator(nint pageSize)
            : base(pageSize)
        {
        }

        [SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
        [SuppressMessage("Design", "CA1064:Exceptions should be public",
            Justification = "This is used exclusively internally as jank control flow because I'm lazy")]
        private sealed class SyscallNotImplementedException : Exception { }

        private static int PageProbePipeReadFD, PageProbePipeWriteFD;

        [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations",
            Justification = "If the exception is thrown, the application is in an unrecoverable state. Methods on this type will not behave well.")]
        [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline",
            Justification = "There is no good way to inline the initialization here, and we want to make sure that the cctor runs before anything is done with the type.")]
        static unsafe MmapPagedMemoryAllocator()
        {
            // Open a temporary pipe for page probes
            // This pipe gets leaked, but eh
            var pipefd = stackalloc int[2];
            if (Unix.Pipe2(pipefd, Unix.PipeFlags.CloseOnExec) == -1)
            {
                throw new Win32Exception(Unix.Errno, "Failed to create pipe for page probes");
            }

            PageProbePipeReadFD = pipefd[0];
            PageProbePipeWriteFD = pipefd[1];
        }

        public static unsafe bool PageAllocated(nint page)
        {
            byte garbage;
            // TODO: Mincore isn't implemented in WSL, and always gives ENOSYS
            if (Unix.Mincore(page, 1, &garbage) == -1)
            {
                var lastError = Unix.Errno;
                if (lastError == 12)
                {  // ENOMEM, page is unallocated
                    return false;
                }
                if (lastError == 38)
                { // ENOSYS, function not implemented
                  // TODO: possibly implement /proc/self/maps parsing as a fallback
                    throw new SyscallNotImplementedException();
                }
                throw new NotImplementedException($"Got unimplemented errno for mincore(2); errno = {lastError}");
            }
            return true;
        }

        public static unsafe bool PageReadable(nint page)
        {
            // Try to write into a pipe using the page as the source buffer
            if (Unix.Write(PageProbePipeWriteFD, page, 1) == -1)
            {
                var lastError = Unix.Errno;
                if (lastError == 14)
                {  // EFAULT, buf is not readable
                    return false;
                }
                throw new NotImplementedException($"Got unimplemented errno for write(2); errno = {lastError}");
            }

            // Success - clean up the pipe
            byte garbage;
            if (Unix.Read(PageProbePipeReadFD, new IntPtr(&garbage), 1) == -1)
            {
                throw new Win32Exception("Failed to clean up page probe pipe after successful page probe");
            }

            return true;
        }

        private bool canTestPageAllocation = true;

        protected override bool TryAllocateNewPage(AllocationRequest request, [MaybeNullWhen(false)] out IAllocatedMemory allocated)
        {
            var prot = request.Executable ? Unix.Protection.Execute : Unix.Protection.None;
            prot |= Unix.Protection.Read | Unix.Protection.Write;

            // mmap the page we found
            var mmapPtr = Unix.Mmap(IntPtr.Zero, (nuint)PageSize, prot, Unix.MmapFlags.Private | Unix.MmapFlags.Anonymous, -1, 0);
            if (mmapPtr is 0 or -1)
            {
                // fuck
                var errno = Unix.Errno;
                MMDbgLog.Error($"Error creating allocation: {errno} {new Win32Exception(errno).Message}");
                allocated = null;
                return false;
            }

            // create a Page object for the newly mapped memory, even before deciding whether we succeeded or not
            var page = new Page(this, mmapPtr, (uint)PageSize, request.Executable);
            InsertAllocatedPage(page);

            // for simplicity, we'll try to allocate out of the page before checking bounds
            if (!page.TryAllocate((uint)request.Size, (uint)request.Alignment, out var pageAlloc))
            {
                // huh???
                RegisterForCleanup(page);
                allocated = null;
                return false;
            }

            // we got an allocation!
            allocated = pageAlloc;
            return true;
        }

        protected override bool TryAllocateNewPage(
            PositionedAllocationRequest request,
            nint targetPage, nint lowPageBound, nint highPageBound,
            [MaybeNullWhen(false)] out IAllocatedMemory allocated
        )
        {
            if (!canTestPageAllocation)
            {
                allocated = null;
                return false;
            }

            var prot = request.Base.Executable ? Unix.Protection.Execute : Unix.Protection.None;
            prot |= Unix.Protection.Read | Unix.Protection.Write;

            // number of pages needed to satisfy length requirements
            var numPages = request.Base.Size / PageSize + 1;

            // find the nearest unallocated page within our bounds
            var low = targetPage - PageSize;
            var high = targetPage;
            nint ptr = -1;

            try
            {
                while (low >= lowPageBound || high <= highPageBound)
                {

                    // check above the target page first
                    if (high <= highPageBound)
                    {
                        for (nint i = 0; i < numPages; i++)
                        {
                            if (PageAllocated(high + PageSize * i))
                            {
                                high += PageSize;
                                goto FailHigh;
                            }
                        }
                        // all pages are unallocated, we're done
                        ptr = high;
                        break;
                    }
                    FailHigh:
                    if (low >= lowPageBound)
                    {
                        for (nint i = 0; i < numPages; i++)
                        {
                            if (PageAllocated(low + PageSize * i))
                            {
                                low -= PageSize;
                                goto FailLow;
                            }
                        }
                        // all pages are unallocated, we're done
                        ptr = low;
                        break;
                    }
                    FailLow:
                    { }
                }
            }
            catch (SyscallNotImplementedException)
            {
                canTestPageAllocation = false;
                allocated = null;
                return false;
            }

            // unable to find a page within bounds
            if (ptr == -1)
            {
                allocated = null;
                return false;
            }

            // mmap the page we found
            var mmapPtr = Unix.Mmap(ptr, (nuint)PageSize, prot, Unix.MmapFlags.Private | Unix.MmapFlags.Anonymous | Unix.MmapFlags.FixedNoReplace, -1, 0);
            if (mmapPtr is 0 or -1)
            {
                // fuck
                allocated = null;
                return false;
            }

            // create a Page object for the newly mapped memory, even before deciding whether we succeeded or not
            var page = new Page(this, mmapPtr, (uint)PageSize, request.Base.Executable);
            InsertAllocatedPage(page);

            // for simplicity, we'll try to allocate out of the page before checking bounds
            if (!page.TryAllocate((uint)request.Base.Size, (uint)request.Base.Alignment, out var pageAlloc))
            {
                // huh???
                RegisterForCleanup(page);
                allocated = null;
                return false;
            }

            if ((nint)pageAlloc.BaseAddress < request.LowBound || (nint)pageAlloc.BaseAddress + pageAlloc.Size >= request.HighBound)
            {
                // the allocation didn't land in bounds, fail out
                pageAlloc.Dispose(); // because this is the only allocation in the page, this auto-registers it for cleanup
                allocated = null;
                return false;
            }

            // we got an allocation!
            allocated = pageAlloc;
            return true;
        }

        protected override bool TryFreePage(Page page, [NotNullWhen(false)] out string? errorMsg)
        {
            var res = Unix.Munmap(page.BaseAddr, page.Size);
            if (res != 0)
            {
                errorMsg = new Win32Exception(Unix.Errno).Message;
                return false;
            }
            errorMsg = null;
            return true;
        }
    }

    public OSKind Target => OSKind.Android;

    public SystemFeature Features => SystemFeature.RWXPages | SystemFeature.RXPages;

    readonly Abi defaultAbi;
    public Abi? DefaultAbi => defaultAbi;

    private readonly MmapPagedMemoryAllocator allocator;
    public IMemoryAllocator MemoryAllocator => allocator;

    public INativeExceptionHelper? NativeExceptionHelper => throw new NotImplementedException();
    public nint PageSize { get; private set; }
    IArchitecture? architecture = null;
    public AndroidSystem()
    {
        PageSize = (nint)Unix.Sysconf((Unix.SysconfName)0x27);
        allocator = new MmapPagedMemoryAllocator(PageSize);

        if (PlatformDetection.Architecture == ArchitectureKind.Arm64)
        {
            defaultAbi = new Abi(
               new[] { SpecialArgumentKind.ThisPointer, SpecialArgumentKind.UserArguments },
               ArmABI.ClassifyArm64, true
            );
        }
        else if (PlatformDetection.Architecture == ArchitectureKind.x86_64)
        {
            defaultAbi = new Abi(
                new[] { SpecialArgumentKind.ReturnBuffer, SpecialArgumentKind.ThisPointer, SpecialArgumentKind.UserArguments },
                SystemVABI.ClassifyAMD64, true
            );
        }
        else
        {
            throw new NotImplementedException();
        }
    }
    public void Initialize(IArchitecture arch)
    {
        architecture = arch;
    }

    public IEnumerable<string?> EnumerateLoadedModuleFiles()
    {
        return Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(m => m.FileName)!;
    }

    public nint GetSizeOfReadableMemory(IntPtr start, nint guess)
    {
        var currentPage = allocator.RoundDownToPageBoundary(start);
        if (!MmapPagedMemoryAllocator.PageReadable(currentPage))
        {
            return 0;
        }
        currentPage += PageSize;

        var known = currentPage - start;

        while (known < guess)
        {
            if (!MmapPagedMemoryAllocator.PageReadable(currentPage))
            {
                return known;
            }
            known += PageSize;
            currentPage += PageSize;
        }

        return known;
    }

    [DllImport("libAndroidNativeHelper.so", CallingConvention = CallingConvention.Cdecl)]
    public extern static bool FlushICache(IntPtr start, IntPtr end);

    public unsafe void PatchData(PatchTargetKind patchKind, IntPtr patchTarget, ReadOnlySpan<byte> data, Span<byte> backup)
    {
        var target = new Span<byte>((void*)patchTarget, data.Length);
        // now we copy target to backup, then data to target, then flush the instruction cache
        _ = target.TryCopyTo(backup);
        data.CopyTo(target);

        //flush icache
        IntPtr flushEnd = new IntPtr(patchTarget.ToInt64() + data.Length);
        IntPtr flushStart = patchTarget;
        //bool isFlushICacheDone = FlushICache(flushStart, flushEnd);
    }
}
