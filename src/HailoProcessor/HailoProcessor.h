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
#define HAILO

#ifdef HAILO
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
#include "Channel.hpp"
#include "Allocator.h"
#include "ArrayPool.h"
#include "HailoProcessorStats.h"
#include "StageStats.h"
#include "HailoException.h"

using namespace std;
using namespace cv;
using namespace hailort;
using namespace std::literals::chrono_literals;


class HailoError
{
public:
	HailoError();
	void SetLastError(const HailoException& ex);
	bool IsOk();
	HailoException& LastException() const;
private:
	bool _isSet = false;
	HailoException* _hailoException;
};

class HailoAsyncProcessor {
public:
	static unique_ptr<HailoAsyncProcessor> Load(const string& fileName);

	// this method would push mat object on the queue.

	void Write(const YuvFrame &frame, const FrameIdentifier &frameId);
	void Write(const YuvFrame &frame, const cv::Rect &roi, const FrameIdentifier &frameId, float threshold);
	void StartAsync(unsigned int postProcessThreadCount);
	void StartAsync(CallbackWithContext callback, void * context);

	float ConfidenceThreshold();
	void ConfidenceThreshold(float value);
	void Deallocate();
	void Stop();

	HailoProcessorStats& Stats();

private:

	static std::shared_ptr<ConfiguredNetworkGroup> ConfigureNetworkGroup(VDevice &vdevice, const std::string &yolov_hef);
	static std::shared_ptr<FeatureData<uint8>> CreateFeature(const hailo_vstream_info_t &vstream_info, size_t frameSize);
	void OnWrite(const cv::Mat &frame, FrameContext* frameInfo);
	void OnWrite(const cv::Mat &org_frame, size_t frame_size, FrameContext *frameId);

	void OnFrameDrop(FrameContext *ptr);
	void OnRead(int nr);
	void PostProcess();

	void OnFrameDrop_OnWrite(FrameContext *ptr);

	void OnFrameDrop_OnRead(FrameContext *ptr);

	void OnFrameDrop_OnPostProcess(FrameContext *ptr);

	void OnFrameDrop_OnCallback(FrameContext *ptr);

	void OnCallback();
	std::vector<std::shared_ptr<FeatureData<uint8>>> _features;
	std::vector<std::thread> _threads;
	volatile bool _isRunning;
	std::atomic_int _readOutputCounter;
	//std::atomic_int _readOutputCounter;
	std::mutex _writeMx;
	uint64_t _iteration;
	float _threshold = 0.8f;
	HailoProcessorStats _stats;
	unique_ptr<VDevice> _dev;
	pair<vector<InputVStream>, vector<OutputVStream>> _vstreams;
	InputVStream* _input_vstream;


	Channel<FrameContext*> _writeChannel;
	Channel<FrameContext*> _readChannel;
	Channel<FrameContext*> _postProcessingChannel;
	Channel<FrameContext*> _callbackChannel;

	CallbackWithContext _callback;
	void *_context;

	HailoAsyncProcessor(std::unique_ptr<VDevice> &dev, std::shared_ptr<ConfiguredNetworkGroup> networkGroup);
};


#endif