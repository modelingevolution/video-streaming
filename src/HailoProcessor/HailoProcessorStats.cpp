//
// Created by pi on 30/11/24.
//

#include "HailoProcessorStats.h"
#include <iostream>
#include <string>
#include <iostream>
#include <iomanip>


HailoProcessorStats::HailoProcessorStats(int writeThreadCount, int readInterferenceThreadCount,
    int postProcessingThreadCount, int callbackProcessingThreadCount, int totalCpuCount)
        : writeProcessing(writeThreadCount), readInterferenceProcessing(readInterferenceThreadCount), postProcessing(postProcessingThreadCount), callbackProcessing(callbackProcessingThreadCount), totalProcessing(totalCpuCount)
{
    readInterferenceProcessing.SetPrvStage(&writeProcessing);
    postProcessing.SetPrvStage(&readInterferenceProcessing);
    callbackProcessing.SetPrvStage(&postProcessing);
    totalProcessing.SetPrvStage(&readInterferenceProcessing);
}

unsigned long HailoProcessorStats::InFlight() {
     return this->totalProcessing.LastIteration() - this->writeProcessing.LastIteration();
}

unsigned long HailoProcessorStats::Dropped() {
    return writeProcessing.Dropped() + readInterferenceProcessing.Dropped() + postProcessing.Dropped() + callbackProcessing.Dropped();
}
void HailoProcessorStats::Print2() {
    std::cout << "| Stage                    | Processed | Dropped | Behind | Threads | Est.FPS | Avg. Time (ms) |\n";
    std::cout << "|--------------------------|-----------|---------|--------|---------|---------|----------------|\n";

    auto printStage = [this](const char* name, const StageStats& stats, unsigned long* dropped = nullptr) {
        std::cout << "| " << std::left << std::setw(24) << name << " | ";
        std::cout << std::right << std::setw(9) << stats.Processed() << " | ";

        if(dropped == nullptr)
            std::cout << std::right << std::setw(7) << stats.Dropped() << " | ";
        else
            std::cout << std::right << std::setw(7) << *dropped << " | ";

        std::cout << std::right << std::setw(6) << stats.Behind() << " | "; // New column for Behind
        std::cout << std::right << std::setw(7) << stats.ThreadCount() << " | ";
        float fps = stats.Fps();
        std::chrono::milliseconds avgTime = stats.FrameProcessingTime();
        std::cout << std::right << std::setw(7) << std::fixed << std::setprecision(2) << fps << " | ";
        std::cout << std::right << std::setw(14) << avgTime.count() << " |" << std::endl;
    };

    auto dropped = Dropped();
    printStage("Write Processing", writeProcessing);
    printStage("Read Interference", readInterferenceProcessing);
    printStage("Post Processing", postProcessing);
    printStage("Callback Processing", callbackProcessing);
    // Assuming totalProcessing is a cumulative StageStats object
    printStage("Total Processing", totalProcessing, &dropped);
}
void HailoProcessorStats::Print() {
    std::cout << "| Stage                    | Processed | Dropped | Threads | Est.FPS | Avg. Time (ms) |\n";
    std::cout << "|--------------------------|-----------|---------|---------|---------|----------------|\n";

    auto printStage = [this](const char* name, const StageStats& stats, unsigned long* dropped = nullptr) {
        std::cout << "| " << std::left << std::setw(24) << name << " | ";
        std::cout << std::right << std::setw(9) << stats.Processed() << " | ";

        if(dropped == nullptr)
            std::cout << std::right << std::setw(7) << stats.Dropped() << " | ";
        else
            std::cout << std::right << std::setw(7) << *dropped << " | ";

        std::cout << std::right << std::setw(7) << stats.ThreadCount() << " | ";
        float fps = stats.Fps();
        std::chrono::milliseconds avgTime = stats.FrameProcessingTime();
        std::cout << std::right << std::setw(7) << std::fixed << std::setprecision(2) << fps << " | ";
        std::cout << std::right << std::setw(14) << avgTime.count() << " |" << std::endl;
    };

    auto dropped = Dropped();
    printStage("Write Processing", writeProcessing);
    printStage("Read Interference", readInterferenceProcessing);
    printStage("Post Processing", postProcessing);
    printStage("Callback Processing", callbackProcessing);
    // Assuming there's a totalProcessing member in HailoProcessorStats for cumulative stats
    printStage("Total Processing", totalProcessing, &dropped);
}
