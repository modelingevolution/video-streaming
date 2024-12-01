#include "StageStats.h"


StageStats::StageStats(int threadCount, StageStats *prvStage) : _total(0),_dropped(0), _processed(0) {
    _prvStage = prvStage;
    _threadCount = threadCount;
}

// Method to record when a frame is processed with its duration
void StageStats::FrameProcessed(const std::chrono::nanoseconds &duration, unsigned long iteration) {
    _processed.fetch_add(1, std::memory_order_relaxed);
    _total.fetch_add(duration.count(), std::memory_order_relaxed);
    _lastIteration.store(iteration);
}

// Method to record when a frame is dropped
void StageStats::FrameDropped(unsigned long iteration) {
    _dropped.fetch_add(1, std::memory_order_relaxed);
    _lastIteration.store(iteration);
}

void StageStats::SetThreadCount(int count) {
    this->_threadCount = count;
}

void StageStats::SetPrvStage(StageStats *prv) {
    this->_prvStage = prv;
}

// Get the number of frames dropped
uint64_t StageStats::Dropped() const {
    return _dropped.load(std::memory_order_relaxed);
}

// Get the number of frames processed
uint64_t StageStats::Processed() const {
    return _processed.load(std::memory_order_relaxed);
}

uint64_t StageStats::LastIteration() const {
    return this->_lastIteration.load(std::memory_order_relaxed);
}

uint64_t StageStats::Behind() const {
    if(_prvStage == nullptr) return 0;
    auto prv = _prvStage->LastIteration() ;
    auto c = this->LastIteration();
    if(prv > c) return prv - c;
    return 0;
}

int StageStats::ThreadCount() const {
    return this->_threadCount;
}

// Get the total time spent processing frames
std::chrono::nanoseconds StageStats::Total() const {
    return std::chrono::nanoseconds(_total.load(std::memory_order_relaxed));
}

// Calculate and return the Frames Per Second (FPS)
float StageStats::Fps() const {
    // To calculate FPS, we need to know how much time has passed.
    // For this example, we'll assume _total represents all time since started.
    // If _processed is 0, return 0 to avoid division by zero error.
    auto p = Processed();
    auto t= Total();
    auto result = p > 0 ? static_cast<float>(p) / (static_cast<float>(t.count()) * 1e-9f) : 0.0f;

    return result * _threadCount;
}

// Calculate and return the average frame processing time in milliseconds
std::chrono::milliseconds StageStats::FrameProcessingTime() const {
    auto p = Processed();
    auto t= Total();
    if (p == 0) {
        return std::chrono::milliseconds::zero();
    }
    // Convert nanoseconds to milliseconds, then calculate average
    return std::chrono::duration_cast<std::chrono::milliseconds>(t / p);
}