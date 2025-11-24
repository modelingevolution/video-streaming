//
// Created by pi on 30/11/24.
//

#ifndef HAILOPROCESSORSTATS_H
#define HAILOPROCESSORSTATS_H
#include "StageStats.h"

struct HailoProcessorStats {
    HailoProcessorStats(int writeThreadCount, int readInterferenceThreadCount, int postProcessingThreadCount, int callbackProcessingThreadCount, int totalCpuCount);
    StageStats writeProcessing;
    StageStats readInterferenceProcessing;
    StageStats postProcessing;
    StageStats callbackProcessing;
    StageStats totalProcessing;
    unsigned long InFlight() const;
    unsigned long Dropped() const;
    void Print();
    void Print2();

};



#endif //HAILOPROCESSORSTATS_H
