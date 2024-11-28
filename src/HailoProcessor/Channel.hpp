#include <boost/lockfree/queue.hpp>
#include <iostream>
#include <thread>

enum class DiscardPolicy {
    Oldest,
    Newest
};

template<typename T>
class Channel {
public:
    using DiscardCallback = std::function<void(const T&)>;

    Channel(size_t capacity, DiscardPolicy policy, DiscardCallback callback = nullptr)
         : _capacity(capacity), _policy(policy), _queue(capacity), _sem(0), _onDiscard(callback) {}


    // Non-blocking write, returns true if the write was successful
    bool TryWrite(const T& value) {
        if (_queue.push(value)) {
            _sem.release();  // Signal a waiting consumer
            return true;  // Successfully pushed
        }

        // Handle overflow based on policy
        if (_policy == DiscardPolicy::Oldest) {
            T discarded;
            if (_queue.pop(discarded)) {
                if (_queue.push(value)) {
                    _sem.release();
                    if (_onDiscard) _onDiscard(discarded);
                    return true;
                }
            }
        }
        else if (_policy == DiscardPolicy::Newest) {
            if (_onDiscard) _onDiscard(value);
        }
        return false;
    }

    // Blocking read, waits until an item is available
    T Read() {
        _sem.acquire();  // Wait until an item is available
        T value;
        while (!_queue.pop(value)) ;
        return value;
    }
    template<typename _Rep, typename _Period>
    bool TryRead(T &value, const std::chrono::duration<_Rep, _Period>& __rtime) {
        if(_sem.try_acquire_for(__rtime))
            return _queue.pop(value);

        return false;
    }

private:
    size_t _capacity;
    DiscardPolicy _policy;
    boost::lockfree::queue<T> _queue;  // Lock-free concurrent bounded queue
    std::counting_semaphore<> _sem;
    DiscardCallback _onDiscard;  // Callback for when an item is discarded
};
