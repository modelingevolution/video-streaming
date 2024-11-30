#include <boost/lockfree/queue.hpp>
#include <iostream>
#include <thread>
#include <boost/signals2.hpp>
#include <atomic>
#include <semaphore>
#include <functional>

enum class DiscardPolicy {
    Oldest,
    Newest
};

template<typename T>
class Channel {
public:
    using DiscardSignal = boost::signals2::signal<void(const T&)>;

    Channel(size_t capacity, DiscardPolicy policy)
         : _capacity(capacity), _policy(policy), _queue(capacity), _sem(0), _count(0) {}

    int Length() const {
        return _count.load(std::memory_order_relaxed);
    }

    // Non-blocking write, returns true if the write was successful
    bool TryWrite(const T& value) {
        if (_queue.push(value)) {
            AddCount();
            _sem.release();  // Signal a waiting consumer
            return true;  // Successfully pushed
        }

        // Handle overflow based on policy
        if (_policy == DiscardPolicy::Oldest) {
            T discarded;
            if (_queue.pop(discarded)) {
                SubCount();
                if (_queue.push(value)) {
                    AddCount();
                    _sem.release();
                    _onDiscard(discarded);
                    return true;
                }
            }
        }
        else if (_policy == DiscardPolicy::Newest) {
            _onDiscard(value);
        }
        return false;
    }

    // Blocking read, waits until an item is available
    T Read() {
        _sem.acquire();  // Wait until an item is available
        T value;
        while (!_queue.pop(value)) ;
        SubCount();
        return value;
    }

    template<typename _Rep, typename _Period>
    bool TryRead(T &value, const std::chrono::duration<_Rep, _Period>& __rtime) {
        if(_sem.try_acquire_for(__rtime)) {
            if(_queue.pop(value)) {
                SubCount();
                return true;
            }
            // If we couldn't pop but acquired the semaphore, release it back
            _sem.release();
        }
        return false;
    }

    // Method to connect to the discard signal
    boost::signals2::connection connectDropped(std::function<void(const T&)> slot) {
        return _onDiscard.connect(slot);
    }

private:
    size_t _capacity;
    DiscardSignal _onDiscard;
    DiscardPolicy _policy;
    boost::lockfree::queue<T> _queue;  // Lock-free concurrent bounded queue
    std::counting_semaphore<> _sem;
    std::atomic<int> _count;
     // Signal for when an item is discarded
    inline void AddCount() { _count.fetch_add(1, std::memory_order_relaxed); }
    inline void SubCount() { _count.fetch_sub(1, std::memory_order_relaxed); }
};