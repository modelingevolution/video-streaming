#include "HailoProcessor.h"
#include "Frame.h"

#define EXPORT_API __attribute__((visibility("default")))

static __thread HailoError* LAST_ERROR = nullptr;

EXPORT_API uint8* annotation_result_get_mask(AnnotationResult* ptr, int index);
EXPORT_API int    annotation_result_get_classid(AnnotationResult* ptr, int index);
EXPORT_API int    annotation_result_count(AnnotationResult* ptr);
EXPORT_API void   annotation_result_dispose(AnnotationResult* ptr);

EXPORT_API const char* get_last_hailo_error();

#ifdef HAILO
// can return nullptr, then check get_last_hailo_error
EXPORT_API HailoProcessor*   hailo_processor_load_hef(const char* filename);

// can return nullptr, then check get_last_hailo_error
EXPORT_API AnnotationResult* hailo_processor_process_frame(HailoProcessor* ptr,
                                                                           uint8* frame,
                                                                           int frameW, int frameH, int roiX, int roiY,
                                                                           int roiW, int roiH, int dstW, int dstH);
EXPORT_API void              hailo_processor_dispose(HailoProcessor* ptr);
EXPORT_API float             hailo_processor_get_confidence(HailoProcessor* ptr);
EXPORT_API void              hailo_processor_set_confidence(HailoProcessor* ptr, float value);
#endif
