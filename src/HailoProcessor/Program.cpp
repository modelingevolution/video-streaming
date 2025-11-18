#include "hailo/hailort.hpp"
#include <hailo/vdevice.hpp>
#include "Export.h"
#include <iostream>
#include <chrono>
#include <mutex>
#include <future>
#include <random>
#include <string>
#include <filesystem>
#include "common.h"
#include "common/hailo_objects.hpp"
#include "Frame.h"
#include "HailoProcessorStatsDto.h"

using namespace std;
using namespace hailort;

constexpr bool QUANTIZED = true;
constexpr hailo_format_type_t FORMAT_TYPE = HAILO_FORMAT_TYPE_AUTO;

//std::string yolov_hef  = "yolov8n-seg.hef";
//std::string input_path = "nba.jpg";

void OnResult(SegmentationResult* res, void *ptr) {
    //std::cout << "OnResult, context" << ptr << endl;

    for(int i = 0; i < res->Count(); i++) {
        Segment seg = res->Get(i);
        //std::cout << "Found " << seg.Label << " with confidence: " << seg.Confidence << endl;
        cv::Mat image(seg.Resolution.height, seg.Resolution.width, CV_8UC1);
        for (int r = 0; r < seg.Resolution.height; r++) {
            for (int c = 0; c < seg.Resolution.width; c++) {
                auto v = seg.At(c, r) * 255.0f;
                unsigned char color = static_cast<unsigned char>(v);
                image.at<unsigned char>(r, c) = color;
            }
        }
        std::string fn = std::to_string(i)+"." + seg.Label + ".jpg";
        cv::imwrite(fn, image);
    }
}
void print_exports() {

    cout << "sizeof(FrameIdentifier): " << sizeof(FrameIdentifier) << endl;
    cout << "sizeof(HailoProcessorStatsDto): " << sizeof(HailoProcessorStatsDto) << endl;
    cout << "sizeof(StageStatsDto): " << sizeof(HailoProcessorStatsDto::StageStatsDto) << endl;
    cout << "sizeof(Size/OpenCV): " << sizeof(cv::Size) << endl;
    cout << "sizeof(Rect/OpenCV): " << sizeof(cv::Rect) << endl;
    cout << "sizeof(Rect2f/OpenCV): " << sizeof(cv::Rect2f) << endl;
    cout << "sizeof(Point/OpenCV): " << sizeof(cv::Point) << endl;
}

bool ParseArguments(int argc, char **argv, std::string &yolov_hef, std::string &input_path) {
    if (argc < 3) {
        std::cerr << "Error: At least two argument is required." << std::endl;
        return false;
    }
    yolov_hef = argv[1];
    input_path = argv[2];
    if(!std::filesystem::exists(yolov_hef)) {
        std::cerr << "HEF file does not exists." << endl;
        return false;
    }
    if(!std::filesystem::exists(input_path)) {
        std::cerr << "Jpg file does not exists." << endl;
        return false;
    }
    return true;
}
int main2() {
    // Using int for simplicity, but this could be any type T
    Channel<int> channel(3, DiscardPolicy::Oldest);

    // Counter for discarded items
    std::vector<int> discardedItems;

    // Connect to the discard signal
    auto disconnect = channel.connectDropped([&discardedItems](const int& item) {
        std::cout << "Discarded item: " << item << std::endl;
        discardedItems.push_back(item);
    });

    // Test case 1: Write more items than capacity, oldest should be discarded
    bool success;
    success = channel.TryWrite(1);  // Should write successfully
    success = channel.TryWrite(2);  // Should write successfully
    success = channel.TryWrite(3);  // Should write successfully
    success = channel.TryWrite(4);  // Should discard 1, write 4

    // Check if the correct item was discarded
    if(discardedItems.size() != 1 || discardedItems[0] != 1) {
        std::cerr << "Discard policy failed for oldest item." << std::endl;
        return 1;
    }

    // Test case 2: Read items, check order
    int item;
    success = channel.TryRead(item, std::chrono::milliseconds(100));  // Should read 2
    if (!success || item != 2) {
        std::cerr << "Failed to read expected item: 2" << std::endl;
        return 1;
    }

    success = channel.TryRead(item, std::chrono::milliseconds(100));  // Should read 3
    if (!success || item != 3) {
        std::cerr << "Failed to read expected item: 3" << std::endl;
        return 1;
    }

    success = channel.TryRead(item, std::chrono::milliseconds(100));  // Should read 4
    if (!success || item != 4) {
        std::cerr << "Failed to read expected item: 4" << std::endl;
        return 1;
    }

    // After reading all, channel should be empty
    if (channel.Pending() != 0) {
        std::cerr << "Channel length is not zero after reading all items." << std::endl;
        return 1;
    }

    // Additional test case: Write again, ensuring discard works after reads
    discardedItems.clear();
    success = channel.TryWrite(5);  // Should write successfully
    success = channel.TryWrite(6);  // Should write successfully
    success = channel.TryWrite(7);  // Should write successfully
    success = channel.TryWrite(8);  // Should discard 5, write 8

    // Check if the correct item was discarded again
    if(discardedItems.size() != 1 || discardedItems[0] != 5) {
        std::cerr << "Discard policy failed for oldest item in second test." << std::endl;
        return 1;
    }

    std::cout << "All tests passed successfully." << std::endl;
    return 0;
}
int main(int argc, char** argv)
{
    print_exports();

    std::string yolov_hef;
    std::string input_path;
    if (!ParseArguments(argc, argv, yolov_hef, input_path))
        return 1;

    cout << "HAILO TESTING..." << endl;

    auto frame = YuvFrame::LoadFile(input_path.c_str()).release();
    cout << "File " << input_path << ": " << GREEN << "loaded" << RESET << endl;

    auto p = hailo_processor_load_hef(yolov_hef.c_str());
    cout << "Hef file " << yolov_hef << " ";
    cout << GREEN << "loaded" << RESET << endl;

    hailo_processor_start_async(p, &OnResult, nullptr);

    // Function to encapsulate the loop for async execution
    auto writer_loop = [p, frame]() {
        for(int i = 0; i < 240; i++) {
            std::this_thread::sleep_for(std::chrono::milliseconds(30));
            StopWatch sw;
            FrameIdentifier id(1,0);
            hailo_processor_write_frame(p, frame->GetData(), id.CameraId, id.FrameId, frame->Width(), frame->Height(), 0, 0, frame->Width(), frame->Height(), 0);
        }
    };
    std::future<void> futureWriter = std::async(std::launch::async, writer_loop);

    for(int i = 0 ; i < 60; i++) {
        p->Stats().Print2();
        std::this_thread::sleep_for(120ms);
    }
    cout << "Pres enter to stop the process...";
    string inLine;
    cin >> inLine;

    p->Stats().Print();
    cout << "Stopping threads..." << endl;
    hailo_processor_stop(p);
    cout << "Deallocating resources..." << endl;
    p->Deallocate();
    cout << "Done" << endl;
    string line;
    cin >> line;
    return 0;
}
