//
// Created by pi on 01/12/24.
//

#include "HailoProcessorStatsDto.h"

#include "HailoProcessorStats.h"

inline void PopulateStageStatsDto(HailoProcessorStatsDto::StageStatsDto& dest, const StageStats& src) {
    dest.processed = src.Processed();
    dest.dropped = src.Dropped();
    dest.lastIteration = src.LastIteration();
    dest.behind = src.Behind();
    dest.threadCount = src.ThreadCount();
    dest.totalProcessingTime = src.Total().count();
}
void HailoProcessorStatsDto::UpdateFrom(const HailoProcessorStats& stats) {
    PopulateStageStatsDto(writeProcessing, stats.writeProcessing);
    PopulateStageStatsDto(readInterferenceProcessing, stats.readInterferenceProcessing);
    PopulateStageStatsDto(postProcessing, stats.postProcessing);
    PopulateStageStatsDto(callbackProcessing, stats.callbackProcessing);
    PopulateStageStatsDto(totalProcessing, stats.totalProcessing);

    inFlight = stats.InFlight();
    droppedTotal = stats.Dropped();
}