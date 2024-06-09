#include <cstring>
#include <cstdint>
#include <pthread.h>
#include <iostream>
#include <thread>

// Disable write protection, copy the data, renable write protection
// This has to be done in native code because while write protection is disabled
// we can't execute managed code
extern "C" void copy_to_jit(void* from, void* to, size_t length)
{
	pthread_jit_write_protect_np(0);
	
	std::memcpy(to, from, length);
	
	pthread_jit_write_protect_np(1);
}

// https://github.com/dotnet/runtime/blob/241a6e8daab2929a69189eb2457efcaa975104c1/src/coreclr/inc/corjit.h#L145-L161
struct AllocMemArgs
{
    // Input arguments
    uint32_t hotCodeSize;
    uint32_t coldCodeSize;
    uint32_t roDataSize;
    uint32_t xcptnsCount;
    uint32_t flag;

    // Output arguments
    void* hotCodeBlock;
    void* hotCodeBlockRW;
    void* coldCodeBlock;
    void* coldCodeBlockRW;
    void* roDataBlock;
    void* roDataBlockRW;
};

typedef int (*JitCompileFunc)(void* jit, void* corJitInfo, void* methodInfo, unsigned flags, uint8_t** entryAddress, uint32_t* nativeSizeOfCode);

typedef void (*JitCompilePostHookFunc)(void* corJitInfo, void* methodInfo, uint8_t** entryAddress, uint32_t* nativeSizeOfCode, void* hotCodeRW);

typedef void (*JitInfoAllocMemFunc)(void* corJitInfo, AllocMemArgs* args);

struct JitCompileHookParams
{
	JitCompileFunc original_jit_func;
	JitCompilePostHookFunc post_hook;
	JitInfoAllocMemFunc original_alloc_mem;
};

volatile JitCompileHookParams GLOBAL_JIT_HOOK_PARAMS;

static thread_local int HOOK_ENTRANCY = 0;
static thread_local AllocMemArgs LAST_JIT_MEM_ALLOC_PARAMS;

struct HookEntrancyTicket
{
	HookEntrancyTicket()
	{
		HOOK_ENTRANCY++;
	}
	
	~HookEntrancyTicket()
	{
		HOOK_ENTRANCY--;
	}
};

extern "C" int jit_compile_hook(void* jit, void* corJitInfo, void* methodInfo, unsigned flags, uint8_t** entryAddress, uint32_t* nativeSizeOfCode)
{
	HookEntrancyTicket hook_entrancy_ticket;
	
	*entryAddress = nullptr;
	*nativeSizeOfCode = 0;
	LAST_JIT_MEM_ALLOC_PARAMS.hotCodeBlockRW = nullptr;
	
	int result = GLOBAL_JIT_HOOK_PARAMS.original_jit_func(jit, corJitInfo, methodInfo, flags, entryAddress, nativeSizeOfCode);
	
	if (HOOK_ENTRANCY == 1)
	{
		// Some invocations of CILJit::compileMethod already have write protection turned off, and some don't.
		// We can't execute managed code while write protection is off, so run the managed hook in another thread to sidestep the issue.
		
		auto hot_code_rw = LAST_JIT_MEM_ALLOC_PARAMS.hotCodeBlockRW;
		
		std::thread hook_thread([=]()
		{
			HOOK_ENTRANCY = 1; // HOOK_ENTRANCY is thread local, so set it to 1 here so that we don't recurse.
			
			GLOBAL_JIT_HOOK_PARAMS.post_hook(corJitInfo, methodInfo, entryAddress, nativeSizeOfCode, hot_code_rw);
		});
		
		hook_thread.join();
	}
	
	return result;
}

extern "C" void jit_info_alloc_mem_hook(void* corJitInfo, AllocMemArgs* args)
{
	GLOBAL_JIT_HOOK_PARAMS.original_alloc_mem(corJitInfo, args);
	
	if (HOOK_ENTRANCY == 1)
	{
		LAST_JIT_MEM_ALLOC_PARAMS = *args;
	}
}
