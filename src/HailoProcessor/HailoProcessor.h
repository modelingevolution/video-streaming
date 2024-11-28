#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "common.h"
#include "Export.h"
#include "Frame.h"
#include "Notifier.h"
#include <barrier>
#define HAILO

#ifdef HAILO
#include <hailo/hailort.h>
#include <hailo/hailort_common.hpp>
#include <hailo/vdevice.hpp>
#include <hailo/vstream.hpp>
#include <hailo/infer_model.hpp>
#include <chrono>
#include <hailo/quantization.hpp>
#include "OutTensor.h"
#include "Channel.hpp"
#include "Allocator.h"
#include "ArrayPool.h"
#include "xtensor/xadapt.hpp"
#include "xtensor/xarray.hpp"


using namespace std;
using namespace cv;
using namespace hailort;
using namespace std::literals::chrono_literals;

typedef void (*CallbackWithContext)(SegmentationResult* value, void* context);

class HailoException : public std::exception
{
public:
	HailoException(const hailo_status st);
	HailoException(const string& str);
	HailoException(const HailoException& c);
	hailo_status GetStatus();
	virtual const char* what() const noexcept override;
private:
	const hailo_status _status;
	const char* _msg;
};
class StopWatch {
public:
	StopWatch();
	std::chrono::milliseconds GetMs();
private:
	std::chrono::high_resolution_clock::time_point start;
};
class AiProcessorStats {
public:
	class Stopper {
	public:
		Stopper(std::chrono::nanoseconds& duration);
		~Stopper();

	private:
		std::chrono::high_resolution_clock::time_point start;
		std::chrono::nanoseconds& duration;
	};

	AiProcessorStats();
	~AiProcessorStats() = default;

	Stopper MeasurePreprocessing();
	Stopper MeasureInterference();
	Stopper MeasurePostProcessing();

	AiProcessorStats operator+(const AiProcessorStats& other) const;
	AiProcessorStats& operator++();

	std::chrono::milliseconds PreProcessingAvg() const;
	std::chrono::milliseconds InterferenceAvg() const;
	std::chrono::milliseconds PostProcessingAvg() const;

private:
	unsigned long _frames;
	std::chrono::nanoseconds _preprocessingTotal;
	std::chrono::nanoseconds _interferenceTotal;
	std::chrono::nanoseconds _postProcessingTotal;

	std::chrono::nanoseconds _lastPreprocessing;
	std::chrono::nanoseconds _lastInterference;
	std::chrono::nanoseconds _lastPostProcessing;
};

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
	void Write(const cv::Mat &frame);
	void Write(const YuvFrame &frame);
	void Write(const YuvFrame &frame, const cv::Rect &roi);
	void StartAsync();
	void StartAsync(CallbackWithContext callback, void * context);

	float ConfidenceThreshold();
	void ConfidenceThreshold(float value);
	void Deallocate();
	void Stop();

private:
	static std::shared_ptr<ConfiguredNetworkGroup> ConfigureNetworkGroup(VDevice &vdevice, const std::string &yolov_hef);
	static std::shared_ptr<FeatureData<uint8>> CreateFeature(const hailo_vstream_info_t &vstream_info, size_t frameSize);

	void OnRead(int nr);
	void PostProcess();
	void OnCallback();
	std::vector<std::shared_ptr<FeatureData<uint8>>> _features;
	std::vector<std::thread> _threads;
	volatile bool _isRunning;
	std::atomic_int _readOutputCounter;
	Notifier _readOpNotifier;
	AiProcessorStats _stats;
	unique_ptr<VDevice> _dev;
	pair<vector<InputVStream>, vector<OutputVStream>> _vstreams;
	InputVStream* _input_vstream;

	Channel<SegmentationResult*> _callbackArgs;
	CallbackWithContext _callback;
	void *_context;

	HailoAsyncProcessor(std::unique_ptr<VDevice> &dev, std::shared_ptr<ConfiguredNetworkGroup> networkGroup);
};

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
	AiProcessorStats _stats;
	
};
#endif