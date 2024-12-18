#pragma once
#include <string>
#include <opencv2/opencv.hpp>

#include "ArrayPool.h"
#include "common.h"
#include "FrameIdentifier.h"
#include <cstdint>
#include "StopWatch.h"

class SegmentationResult;

using namespace std;
using namespace cv;




struct FrameContext {
	FrameContext(const FrameIdentifier &id, const Rect rect, float threshold);
	uint64_t Iteration;
	SegmentationResult *Result;
	const FrameIdentifier Id;
	const Rect Roi;
	const float Threshold;

	StopWatch InterferenceAndReadWatch;
	StopWatch WriteWatch;
	StopWatch PostProcessingWatch;
	StopWatch Total;
};

struct RgbColor {
	uint8_t r;
	uint8_t g;
	uint8_t b;

	inline static RgbColor FromArgb(uint8_t r, uint8_t g, uint8_t b);

};
struct YuvColor {
	uint8_t y;
	uint8_t u;
	uint8_t v;

	inline static YuvColor From(const RgbColor& c);
	inline RgbColor ToRgb() const;
	inline operator RgbColor() const;
};


class YuvFrame {
public:
	YuvFrame(int w, int h);
	YuvFrame(int w, int h, uint8* data);
	~YuvFrame();
	void Dump() const;
	uint8* GetData() const;
	void SwapData(uint8* data);
	cv::Size Size() const;
	YuvColor GetPixel(int x, int y) const;

	cv::Mat ToMat() const;
	cv::Mat ToMatBgr(const cv::Rect& roi) const;
	cv::Mat ToMatBgr(const cv::Rect& roi, const cv::Size& dstSize) const;

	void CopyToBgr(const cv::Rect& roi, uint8* dst) const;
	void CopyToBgr(const cv::Rect& roi, const cv::Size& dstSize, uint8* dst) const;

	cv::Mat ToMatRgb(const cv::Rect& roi) const;
	cv::Mat ToMatRgb(const cv::Rect& roi, const cv::Size& dstSize) const;

	void CopyToRgb(const cv::Rect& roi, uint8* dst) const;
	void CopyToRgb(const cv::Rect& roi, const cv::Size& dstSize, uint8* dst) const;

	static uint8* AllocateFrameYuv(int w, int h);
	static uint8* AllocateFrameRgb(int w, int h);

	static unique_ptr<YuvFrame> LoadFile(const string& file);
	int Width() const;
	int Height() const;
private:
	void CopyToBgr(const Rect& roi, Mat dst) const;
	void CopyToRgb(const Rect& roi, Mat dst) const;
	const int _y_plane_size;
	const int _u_plane_size;
	const int _size;
	const int _width;
	const int _height;
	const bool _external;
	uint8* _d;
};


struct Segment {
	Mat Mask;
	const int ClassId;
	const Size Resolution;
	const Rect2f Bbox;
	const float Confidence;
	const string Label;
	void SaveFile(const string &fileName) const;
	float At(int x, int y) const;
	float* Data() const;
	unique_ptr<vector<cv::Point>> ComputePolygon(float thredshold);
	int ComputePolygon(float thredshold, int* dstBuffer, int maxSize);
	~Segment();
private:

};

class SegmentationResult {
public:
	SegmentationResult(const FrameIdentifier &id, const Rect &roi, float threshold);
	float* GetMask(int index)const;
	Size GetResolution(int index) const;
	int GetClassId(int index)const;
	int Count() const;
	int UncertainCounter() const;
	float Threshold() const;
	Rect Roi() const;
	Segment& Get(int index) ;
	void Add(const Mat &mask, int classid, const Size &size, const Rect2f &bbox, float confidence, const string &label);
	void IncrementUncertainCounter();

	FrameIdentifier Id() const;
private:
	vector<Segment> _items;
	Rect _roi;
	FrameIdentifier _id;
	float _threshold;
	int _uncertainCounter;
};