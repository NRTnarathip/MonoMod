#include <cstring>
#include <cstdint>
#include <pthread.h>

extern "C" void copy_to_jit(void* from, void* to, size_t length) {
	pthread_jit_write_protect_np(0);
	
	std::memcpy(to, from, length);
	
	pthread_jit_write_protect_np(1);
}

typedef int (*JitCompileFunc)(void* jit, void* corJitInfo, void* methodInfo, unsigned flags, uint8_t** entryAddress, uint32_t* nativeSizeOfCode);

struct JitCompileHookParams {
	JitCompileFunc original_jit_func;
	JitCompileFunc hook_callback;
};

volatile JitCompileHookParams GLOBAL_JIT_HOOK_PARAMS;

static thread_local int HOOK_ENTRANCY = 0;

struct HookEntrancyTicket {
	HookEntrancyTicket() {
		HOOK_ENTRANCY++;
	}
	
	~HookEntrancyTicket() {
		HOOK_ENTRANCY--;
	}
};

extern "C" int jit_compile_hook(void* jit, void* corJitInfo, void* methodInfo, unsigned flags, uint8_t** entryAddress, uint32_t* nativeSizeOfCode) {
	HookEntrancyTicket hook_entrancy_ticket;
	
	*entryAddress = nullptr;
	*nativeSizeOfCode = 0;
	
	int result = GLOBAL_JIT_HOOK_PARAMS.original_jit_func(jit, corJitInfo, methodInfo, flags, entryAddress, nativeSizeOfCode);
	
	if (HOOK_ENTRANCY == 1) {
		pthread_jit_write_protect_np(1);
		
		GLOBAL_JIT_HOOK_PARAMS.hook_callback(jit, corJitInfo, methodInfo, flags, entryAddress, nativeSizeOfCode);
		
		pthread_jit_write_protect_np(0);
	}
	
	return result;
}

extern "C" int fake_jit_compile(void* jit, void* corJitInfo, void* methodInfo, unsigned flags, uint8_t** entryAddress, uint32_t* nativeSizeOfCode) {
	// Do nothing
	return 0;
}