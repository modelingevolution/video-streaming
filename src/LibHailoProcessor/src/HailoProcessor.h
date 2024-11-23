#include <string>
#include <opencv2/opencv.hpp>
#include "Frame.h"

#define HAILO

#ifdef HAILO
#include <hailo/hailort.h>
#include <hailo/hailort_common.hpp>
#include <hailo/vdevice.hpp>
#include <hailo/infer_model.hpp>
#include <hailo/quantization.hpp>
#include "OutTensor.h"
#include "Allocator.h"
#include "ArrayPool.h"

using namespace std;
using namespace cv;
using namespace hailort;
using namespace std::literals::chrono_literals;
class HailoException : public std::exception
{
public:
	HailoException(const hailo_status st);
	HailoException(const string& str);
	hailo_status GetStatus();
	virtual const char* what() const noexcept override;
private:
	hailo_status _status;
	const char* _msg;
};
class HailoProcessor {
public:
	static HailoProcessor* Load(const string & fileName);
	HailoProcessor(unique_ptr<VDevice>& dev, shared_ptr<InferModel>& infer_model, shared_ptr<ConfiguredInferModel>& configured_infer_model, ConfiguredInferModel::Bindings& bindings, size_t input_frame_size);
	AnnotationResult* ProcessFrame(const YuvFrame& frame, const Rect& roi, const Size& dstSize);

	float ConfidenceThreshold() const;
	void ConfidenceThreshold(float value);

	cv::Size GetInputSize() const;
	
private:
	unique_ptr<VDevice> _dev;
	shared_ptr<InferModel> _model;
	shared_ptr<ConfiguredInferModel> _configured_infer_model;
	ConfiguredInferModel::Bindings _bindings;
	size_t _input_frame_size;
	PageAlignedAllocator _allocator;
	float _confidenceThreshold = 0.5f;
	ArrayMemoryPool<float> _floatPool;
	ArrayMemoryPool<uint8> _bytePool;
};
#endif