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

std::string yolov_hef  = "yolov8n-seg.hef";
std::string input_path = "nba.jpg";

void OnResult(SegmentationResult* res, void *ptr) {
    std::cout << "OnResult, context" << ptr << endl;

    for(int i = 0; i < res->Count(); i++) {
        Segment seg = res->Get(i);
        std::cout << "Found " << seg.Label << " with confidence: " << seg.Confidence << endl;
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
int main(int argc, char** argv)
{
    cout << "HAILO TESTING..." << endl;

    auto frame = YuvFrame::LoadFile(input_path.c_str());
    cout << "File " << input_path << ": " << GREEN << "loaded" << RESET << endl;

    auto p = hailo_processor_load_hef(yolov_hef.c_str());
    cout << "Hef file " << yolov_hef << " ";
    cout << GREEN << "loaded" << RESET << endl;

    hailo_processor_start_async(p, &OnResult, nullptr);

    for(int i = 0; i < 1; i++) {
        std::this_thread::sleep_for(500ms);
        {
            StopWatch sw;
            hailo_processor_write_frame(p, frame.get()->GetData(), frame->Width(), frame->Height(),0,0,frame->Width(), frame->Height());
            cout << "Frame written: " << i << " in " << sw.GetMs() << endl;
        }
    }
    std::this_thread::sleep_for(2s);
    //cout << "Writing data finished.\nPress key to deallocate resources." << endl;
    // string line;
    // cin >> line;
    cout << "Stopping threads..." << endl;
    hailo_processor_stop(p);
    cout << "Deallocating resources..." << endl;
    p->Deallocate();
    cout << "Done" << endl;
    string line;
    cin >> line;
    return 0;
}