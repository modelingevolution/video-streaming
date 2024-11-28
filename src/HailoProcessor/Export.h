#pragma once
class HailoError;
#include "HailoProcessor.h"
#include "Frame.h"

#ifdef __cplusplus
#define EXPORT_API extern "C" __attribute__((visibility("default")))
#else
#define EXPORT_API __attribute__((visibility("default")))
#endif


static __thread HailoError* LAST_ERROR = nullptr;

EXPORT_API Segment* segmentation_result_get(SegmentationResult* ptr, int index);
EXPORT_API int    segmentation_result_count(SegmentationResult* ptr);
EXPORT_API void   segmentation_result_dispose(SegmentationResult* ptr);

EXPORT_API float segment_get_confidence(Segment *segment);
EXPORT_API int segment_get_classid(Segment *segment);
EXPORT_API const char* segment_get_label(Segment *segment);
EXPORT_API float* segment_get_data(Segment *segment);
EXPORT_API int segment_compute_polygon(Segment *segment, float threshod, int *buffer, int maxSize);

EXPORT_API const char* get_last_hailo_error();

#ifdef HAILO
// can return nullptr, then check get_last_hailo_error
EXPORT_API HailoAsyncProcessor*   hailo_processor_load_hef(const char* filename);

// can return nullptr, then check get_last_hailo_error
EXPORT_API SegmentationResult* hailo_processor_process_frame(HailoAsyncProcessor* ptr,
                                                                           uint8* frame,
                                                                           int frameW, int frameH, int roiX, int roiY,
                                                                           int roiW, int roiH);


EXPORT_API void hailo_processor_start_async(HailoAsyncProcessor *ptr, CallbackWithContext callback, void* context);

EXPORT_API void hailo_processor_write_frame(HailoAsyncProcessor* ptr,
                                                                           uint8* frame,
                                                                           int frameW, int frameH, int roiX, int roiY,
                                                                           int roiW, int roiH);

EXPORT_API void hailo_processor_stop(HailoAsyncProcessor* ptr);
EXPORT_API float             hailo_processor_get_confidence(HailoAsyncProcessor* ptr);
EXPORT_API void              hailo_processor_set_confidence(HailoAsyncProcessor* ptr, float value);
#endif
