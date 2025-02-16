#pragma once
class HailoError;
#include "HailoProcessor.h"
#include "Frame.h"
#include "FrameIdentifier.h"

#ifdef __cplusplus
#define EXPORT_API extern "C" __attribute__((visibility("default")))
#else
#define EXPORT_API __attribute__((visibility("default")))
#endif


static __thread HailoError* LAST_ERROR = nullptr;

struct RECT_INT {
    int x,y,w,h;
};
struct HailoProcessorStatsDto;
EXPORT_API Segment* segmentation_result_get(SegmentationResult* ptr, int index);
EXPORT_API int    segmentation_result_count(SegmentationResult* ptr);
EXPORT_API float  segmentation_result_threshold(SegmentationResult* ptr);
EXPORT_API int    segmentation_result_uncertainCounter(SegmentationResult* ptr);
EXPORT_API void   segmentation_result_dispose(SegmentationResult* ptr);
EXPORT_API FrameIdentifier segmentation_result_id(SegmentationResult* ptr);
EXPORT_API cv::Rect segmentation_result_roi(SegmentationResult* ptr);

EXPORT_API float segment_get_confidence(Segment *segment);
EXPORT_API int segment_get_classid(Segment *segment);
EXPORT_API const char* segment_get_label(Segment *segment);
EXPORT_API float* segment_get_data(Segment *segment);
EXPORT_API cv::Rect2f segment_get_bbox(Segment *segment);
EXPORT_API cv::Size segment_get_resolution(Segment *segment);
EXPORT_API int segment_compute_polygon(Segment *segment, float threshod, int *buffer, int maxSize);

EXPORT_API const char* get_last_hailo_error();

#ifdef HAILO
// can return nullptr, then check get_last_hailo_error
EXPORT_API HailoAsyncProcessor*   hailo_processor_load_hef(const char* filename);

EXPORT_API void hailo_processor_start_async(HailoAsyncProcessor *ptr, CallbackWithContext callback, void* context);
EXPORT_API void hailo_processor_update_stats(HailoAsyncProcessor *ptr, HailoProcessorStatsDto *dto);
EXPORT_API void hailo_processor_write_frame(HailoAsyncProcessor* ptr,
                                                                           uint8* frame,
                                                                           uint32_t cameraId, uint64_t frameId,
                                                                           int frameW, int frameH, int roiX, int roiY,
                                                                           int roiW, int roiH, float threshold);

EXPORT_API void hailo_processor_stop(HailoAsyncProcessor* ptr);
EXPORT_API float             hailo_processor_get_confidence(HailoAsyncProcessor* ptr);
EXPORT_API void              hailo_processor_set_confidence(HailoAsyncProcessor* ptr, float value);
#endif
