#include <unordered_map>
#include <vector>
#include <cstddef>
#include <iostream>
#include <memory>  // For std::shared_ptr

#pragma once
template <typename T>
class ArrayMemoryPool;

template <typename T>
class ArrayOwner {
public:
    ArrayOwner(T* data, size_t size, ArrayMemoryPool<T>& pool)
        : data_(data), size_(size), pool_(pool), refCount_(new int(1)) {
    }

    // Copy constructor increases reference count
    ArrayOwner(const ArrayOwner& other)
        : data_(other.data_), size_(other.size_), pool_(other.pool_), refCount_(other.refCount_) {
        ++(*refCount_);
    }

    // Assignment operator
    ArrayOwner& operator=(const ArrayOwner& other) {
        if (this != &other) {
            Release();
            data_ = other.data_;
            size_ = other.size_;
            pool_ = other.pool_;
            refCount_ = other.refCount_;
            ++(*refCount_);
        }
        return *this;
    }

    // Destructor: returns the array if no references remain
    ~ArrayOwner() {
        Release();
    }

    // Access underlying data
    T* Data() const { return data_; }
    size_t Size() const { return size_; }
    T& operator[](size_t index) { return data_[index]; }

private:
    T* data_;
    size_t size_;
    ArrayMemoryPool<T>& pool_;
    int* refCount_;

    void Release() {
        if (--(*refCount_) == 0) {
            delete refCount_;
            pool_.ReturnToPool(data_, size_);
        }
    }
};

template <typename T>
class ArrayMemoryPool {
public:
    ~ArrayMemoryPool() {
        for (auto& bucket : pool_) {
            for (auto array : bucket.second) {
                delete[] array;
            }
        }
    }

    // Rent: allocate an array and return an ArrayOwner
    ArrayOwner<T> Rent(size_t size) {
        size_t alignedSize = AlignSize(size);
        if (!pool_[alignedSize].empty()) {
            T* array = pool_[alignedSize].back();
            pool_[alignedSize].pop_back();
            return ArrayOwner<T>(array, alignedSize, *this);
        }
        return ArrayOwner<T>(new T[alignedSize], alignedSize, *this);
    }

private:
    std::unordered_map<size_t, std::vector<T*>> pool_;  // Buckets storing reusable arrays

    friend class ArrayOwner<T>;  // Allow ArrayOwner to access private members

    // Align the size to the nearest multiple of 256 bytes
    size_t AlignSize(size_t size) {
        const size_t alignment = 256 / sizeof(T);
        return ((size + alignment - 1) / alignment) * alignment;
    }

    // Return an array to the pool (called by ArrayOwner)
    void ReturnToPool(T* array, size_t size) {
        pool_[size].push_back(array);
    }
};
