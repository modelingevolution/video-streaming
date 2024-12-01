//
// Created by pi on 01/12/24.
//

#ifndef HAILOPROCESSORSTATSDTO_H
#define HAILOPROCESSORSTATSDTO_H
#include <cstdint>

// forward declaration.
struct HailoProcessorStats;

#pragma pack(push, 1)
struct HailoProcessorStatsDto {
    struct StageStatsDto {
        uint64_t processed;
        uint64_t dropped;
        uint64_t lastIteration;
        uint64_t behind;
        int64_t totalProcessingTime; // Nanoseconds for C#
        int threadCount;
    } writeProcessing, readInterferenceProcessing, postProcessing, callbackProcessing, totalProcessing;

    uint64_t inFlight;
    uint64_t droppedTotal;

    void UpdateFrom(const HailoProcessorStats& stats);
};
#pragma pack(pop)


#endif //HAILOPROCESSORSTATSDTO_H
