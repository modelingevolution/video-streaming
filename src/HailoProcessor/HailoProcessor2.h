#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "common.h"
#include "Export.h"
#include "Frame.h"
#include "Notifier.h"
#include "StopWatch.h"
#include <barrier>
#include <mutex>
#include <hailo/hailort.h>
#include <hailo/hailort_common.hpp>
#include <hailo/vdevice.hpp>
#include <hailo/vstream.hpp>
#include <hailo/infer_model.hpp>
#include <chrono>
#include <hailo/quantization.hpp>
#include "xtensor/xadapt.hpp"
#include "xtensor/xarray.hpp"

#include "OutTensor.h"
#include "Allocator.h"
#include "ArrayPool.h"
#include "ArrayOperations.h"
#include "HailoProcessorStats.h"
#include "StageStats.h"
#include "HailoException.h"

using namespace std;
using namespace cv;
using namespace hailort;
using namespace std::literals::chrono_literals;

class HailoProcessor {
public:
    static unique_ptr<HailoProcessor> Load(const string& fileName);

    HailoProcessor(unique_ptr<VDevice>& dev, shared_ptr<InferModel>& infer_model,
        shared_ptr<ConfiguredInferModel>& configured_infer_model,
        const ConfiguredInferModel::Bindings& bindings, size_t input_frame_size);
    unique_ptr<SegmentationResult> ProcessFrame(const YuvFrame& frame, const Rect& roi, const Size& dstSize);

    float ConfidenceThreshold() const;
    void ConfidenceThreshold(float value);

    cv::Size GetInputSize() const;

    void StartAsync(CallbackWithContext callback, void * context);

private:
    unique_ptr<VDevice> _dev;
    shared_ptr<InferModel> _model;
    shared_ptr<ConfiguredInferModel> _configured_infer_model;
    ConfiguredInferModel::Bindings _bindings;
    size_t _input_frame_size;
    shared_ptr<PageAlignedMemoryPool> _allocator = make_shared<PageAlignedMemoryPool>();
    float _confidenceThreshold = 0.8f;
    ArrayMemoryPool<float> _floatPool;
    ArrayMemoryPool<uint8> _bytePool;


};