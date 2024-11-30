#include "Export.h"



EXPORT_API float segment_get_confidence(Segment* segment) {
	return (segment) ? segment->Confidence : 0.0f;
}
EXPORT_API const char* segment_get_label(Segment* segment) {
	return (segment) ? segment->Label.c_str() : nullptr;
}
EXPORT_API int segment_get_classid(Segment* segment) {
	return (segment) ? segment->ClassId : -1;
}
EXPORT_API float* segment_get_data(Segment* segment) {
	return (segment) ? segment->Data() : nullptr;
}

EXPORT_API cv::Rect2f segment_get_bbox(Segment *segment) {
	if(segment) {
		return segment->Bbox;
	}
	return {0,0,0,0};
}

EXPORT_API cv::Size segment_get_resolution(Segment *segment) {
	if(segment)
		return segment->Resolution;
	return {0,0};
}

EXPORT_API int segment_compute_polygon(Segment* segment,float threshod, int* buffer, int maxSize) {
	if (!segment || !buffer || maxSize <= 0) {
		return 0;
	}
	return segment->ComputePolygon(threshod, buffer, maxSize);
}
EXPORT_API Segment* segmentation_result_get(SegmentationResult* ptr, int index) {
	if (!ptr || index < 0 || index >= ptr->Count()) {
		return nullptr;
	}
	return &ptr->Get(index);
}

EXPORT_API int segmentation_result_count(SegmentationResult* ptr)
{
	return (ptr) ? ptr->Count() : 0;
}

EXPORT_API void segmentation_result_dispose(SegmentationResult* ptr)
{
	if (ptr) {
		delete ptr;
	}
}

EXPORT_API FrameIdentifier segmentation_result_id(SegmentationResult *ptr) {
	return ptr->Id();
}

EXPORT_API const char* get_last_hailo_error()
{
	if (LAST_ERROR == nullptr) return nullptr;
	if (!LAST_ERROR->IsOk())
		return LAST_ERROR->LastException().what();
	return nullptr;
}

EXPORT_API HailoAsyncProcessor* hailo_processor_load_hef(const char* filename)
{
	try 
	{
		return HailoAsyncProcessor::Load(filename).release();
	}
	catch (const HailoException& ex)
	{
		if (LAST_ERROR == nullptr) LAST_ERROR = new HailoError();
		LAST_ERROR->SetLastError(ex);
		return nullptr;
	}
}


EXPORT_API void hailo_processor_start_async(HailoAsyncProcessor *ptr, CallbackWithContext callback, void *context) {
	ptr->StartAsync(callback, context);
}

EXPORT_API void hailo_processor_write_frame(HailoAsyncProcessor *ptr, uint8 *frame,FrameIdentifier frameId, int frameW, int frameH, int roiX, int roiY,
                                 int roiW, int roiH) {
	try
	{
		YuvFrame f(frameW, frameH, frame);
		Rect roi(roiX, roiY, roiW, roiH);
		ptr->Write(f, roi, frameId);
	}
	catch (const HailoException& ex)
	{
		if (LAST_ERROR == nullptr) LAST_ERROR = new HailoError();
		LAST_ERROR->SetLastError(ex);

	}
}

EXPORT_API void hailo_processor_stop(HailoAsyncProcessor* ptr)
{
	if(ptr != nullptr) {
		ptr->Stop();
		delete ptr;
	}
}

EXPORT_API float hailo_processor_get_confidence(HailoAsyncProcessor* ptr)
{
	return ptr->ConfidenceThreshold();
}

EXPORT_API void hailo_processor_set_confidence(HailoAsyncProcessor* ptr, float value)
{
	ptr->ConfidenceThreshold(value);
}

