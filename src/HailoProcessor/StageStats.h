#ifndef STAGESTATS_H
#define STAGESTATS_H
#include <chrono>
#include <atomic>
#include <cstdint>

class StageStats {
public:
    StageStats(int threadCount = 1, StageStats* prvStage = nullptr);
    void FrameProcessed(const std::chrono::nanoseconds &duration, unsigned long iteration);
    void FrameDropped(unsigned long iteration);
    void SetThreadCount(int count);
    void SetPrvStage(StageStats *prv);
    // getters
    uint64_t Dropped() const;
    uint64_t Processed() const;
    uint64_t LastIteration() const;
    uint64_t Behind() const;
    int ThreadCount() const;
    std::chrono::nanoseconds Total() const;
    float Fps() const;
    std::chrono::milliseconds FrameProcessingTime() const;
private:
    StageStats* _prvStage;
    int _threadCount;
    std::atomic<uint64_t> _dropped{0};
    std::atomic<uint64_t> _processed{0};
    std::atomic<uint64_t> _lastIteration{0};
    std::atomic<std::chrono::nanoseconds::rep> _total{0};
};

#endif //STAGESTATS_H
