#include "Export.h"

uint8* annotation_result_get_mask(AnnotationResult* ptr, int index)
{
	return ptr->GetMask(index);
}

int annotation_result_get_classid(AnnotationResult* ptr, int index)
{
	return ptr->GetClassId(index);
}

int annotation_result_count(AnnotationResult* ptr)
{
	return ptr->Count();
}

void annotation_result_dispose(AnnotationResult* ptr)
{
	delete ptr;
}

AnnotationResult* hailo_processor_process_frame(HailoProcessor* ptr, uint8* frame, int frameW,
                                                int frameH, int roiX, int roiY,
                                                int roiW, int roiH, int dstW, int dstH)
{
	YuvFrame f(frameW, frameH, frame);
	Rect roi(roiX, roiY, roiW, roiH);
	Size dstSz(dstW, dstH);
	auto result = ptr->ProcessFrame(f, roi, dstSz);
	return result;
}

void hailo_processor_dispose(HailoProcessor* ptr)
{
	delete ptr;
}

float hailo_processor_get_confidence(HailoProcessor* ptr)
{
	return ptr->ConfidenceThreshold();
}

void hailo_processor_set_confidence(HailoProcessor* ptr, float value)
{
	ptr->ConfidenceThreshold(value);
}

