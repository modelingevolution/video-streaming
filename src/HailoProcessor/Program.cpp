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
            hailo_processor_write_frame(p, frame->GetData(), id, frame->Width(), frame->Height(), 0, 0, frame->Width(), frame->Height());
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
