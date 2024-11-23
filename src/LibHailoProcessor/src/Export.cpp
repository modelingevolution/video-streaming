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

const char* get_last_hailo_error()
{
	if (LAST_ERROR == nullptr) return nullptr;
	if (!LAST_ERROR->IsOk())
		return LAST_ERROR->LastException().what();
	return nullptr;
}

HailoProcessor* hailo_processor_load_hef(const char* filename)
{
	try 
	{
		return HailoProcessor::Load(filename);
	}
	catch (const HailoException& ex)
	{
		if (LAST_ERROR == nullptr) LAST_ERROR = new HailoError();
		LAST_ERROR->SetLastError(ex);
		return nullptr;
	}
}

AnnotationResult* hailo_processor_process_frame(HailoProcessor* ptr, uint8* frame, int frameW,
                                                int frameH, int roiX, int roiY,
                                                int roiW, int roiH, int dstW, int dstH)
{
	try
	{
		YuvFrame f(frameW, frameH, frame);
		Rect roi(roiX, roiY, roiW, roiH);
		Size dstSz(dstW, dstH);
		auto result = ptr->ProcessFrame(f, roi, dstSz);
		return result;
	}
	catch (const HailoException& ex)
	{
		if (LAST_ERROR == nullptr) LAST_ERROR = new HailoError();
		LAST_ERROR->SetLastError(ex);
		return nullptr;
	}
}

void hailo_processor_dispose(HailoProcessor* ptr)
{
	if(ptr != nullptr)
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

