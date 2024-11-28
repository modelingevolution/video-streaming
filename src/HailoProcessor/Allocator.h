#pragma once

#include <vector>
#include <stdlib.h>
#include <sys/mman.h>

// A very simple page-aligned memory heap.
// DO NOT use this if you are allocating more than a handful of buffers.
// All operations are O(n) where n is the number of buffers allocated.
// We only reuse buffers when the requested size matches 100%.
// So this is intended for frequent re-use where the buffer sizes are consistent.
class PageAlignedMemoryPool {
public:
	struct Buffer {
		void* P;
		size_t Size;
	};
	std::vector<Buffer> Used;
	std::vector<Buffer> Available;

	~PageAlignedMemoryPool() {
		for (auto b : Used)
			munmap(b.P, b.Size);
		for (auto b : Available)
			munmap(b.P, b.Size);
	}
	template <typename T>
	std::shared_ptr<T> Rent(size_t count) {
		size_t size = count * sizeof(T);
		void* memory = Rent(size);

		// Custom deleter to return memory to the pool
		auto deleter = [this](T* ptr) {
			this->Return(ptr);
			};

		return std::shared_ptr<T>(static_cast<T*>(memory), deleter);
	}
	void* Rent(size_t size) {
		for (size_t i = 0; i < Available.size(); i++) {
			if (Available[i].Size == size) {
				void* p = Available[i].P;
				Used.push_back(Available[i]);
				std::swap(Available[i], Available.back());
				Available.pop_back();
				return p;
			}
		}
		Buffer b;
		b.P = mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
		b.Size = size;
		Used.push_back(b);
		return b.P;
	}
	

	void Return(void* buf) {
		
		for (size_t i = 0; i < Used.size(); i++) {
			if (Used[i].P == buf) {
				//cout << "Returning bytes: " << Used[i].Size << endl;
				Available.push_back(Used[i]);
				std::swap(Used[i], Used.back());
				Used.pop_back();
				return;
			}
		}
	}
};