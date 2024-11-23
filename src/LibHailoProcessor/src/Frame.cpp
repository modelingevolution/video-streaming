#include "Frame.h"

#include "ArrayPool.h"

RgbColor RgbColor::FromArgb(uint8_t r, uint8_t g, uint8_t b)
{
	return RgbColor{ r, g, b };
}

YuvColor YuvColor::From(const RgbColor& c)
{
	auto r = c.r;
	auto g = c.g;
	auto b = c.b;
	auto y = static_cast<uint8_t>(std::clamp(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16, 0, 255));     // Y = 0.257 * R + 0.504 * G + 0.098 * B + 16
	auto u = static_cast<uint8_t>(std::clamp(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128, 0, 255));   // U = -0.148 * R - 0.291 * G + 0.439 * B + 128
	auto v = static_cast<uint8_t>(std::clamp(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128, 0, 255));    // V = 0.439 * R - 0.368 * G - 0.071 * B + 128
	return YuvColor{ y, u, v };
}

RgbColor YuvColor::ToRgb() const
{
	int c = static_cast<int>(y) - 16;
	int d = static_cast<int>(u) - 128;
	int e = static_cast<int>(v) - 128;

	int r = (298 * c + 409 * e + 128) >> 8;
	int g = (298 * c - 100 * d - 208 * e + 128) >> 8;
	int b = (298 * c + 516 * d + 128) >> 8;

	// Clamp values to 0-255
	r = std::clamp(r, 0, 255);
	g = std::clamp(g, 0, 255);
	b = std::clamp(b, 0, 255);

	return RgbColor::FromArgb(static_cast<uint8_t>(r), static_cast<uint8_t>(g), static_cast<uint8_t>(b));

}

YuvColor::operator RgbColor() const
{
	return this->ToRgb();
}

YuvFrame::YuvFrame(int w, int h) : _y_plane_size(w*h),
	_u_plane_size(w*h/4),
	_size(w*h*3/2),
	_width(w),
	_height(h),
	_external(false)
{
	this->_d = new uint8[_size];
}

YuvFrame::YuvFrame(int w, int h, uint8* data)
	: _y_plane_size(w * h),
	_u_plane_size(w * h / 4),
	_size(w * h * 3 / 2),
	_width(w),
	_height(h),
	_external(true)
{
	this->_d = data;
}

YuvFrame::~YuvFrame()
{
	if (!_external)
		delete _d;
}

void YuvFrame::Dump() const
{
	for (int x = 0; x < _width; x++)
	{
		for (int y = 0; y < _height; y++)
		{
			int offset = y * _width + x;
			auto value = _d[offset];
			auto decNormalized = value * 10 / 255;
			if (decNormalized >= 10) decNormalized = 9;
			std::cout << decNormalized;
		}
		std::cout << endl;
	}
	std::cout << endl;
}

uint8* YuvFrame::GetData() const
{
	return this->_d;
}

void YuvFrame::SwapData(uint8* data)
{
	this->_d = data;
}

YuvColor YuvFrame::GetPixel(int x, int y) const
{
	int yOffset = y * _width + x;
	int uvWidth = _width / 2;
	int uvOffset = (y / 2) * uvWidth + (x / 2);
	// Access Y, U, and V values
	YuvColor ret;
	ret.y = _d[yOffset];
	ret.u = _d[_y_plane_size + uvOffset];
	ret.v = _d[_y_plane_size + _u_plane_size + uvOffset];

	return ret;
}

Mat YuvFrame::ToMat() const
{
	auto roi = Rect(0, 0, _width, _height);
	return ToMatBgr(roi);
}
Mat YuvFrame::ToMatRgb(const Rect& roi) const
{
	Mat dst(roi.width, roi.height, CV_8UC3, cv::Scalar(0, 0, 0));

	for (int ix = 0; ix < roi.width; ix++)
		for (int iy = 0; iy < roi.height; iy++) {
			cv::Vec3b& pixel = dst.at<cv::Vec3b>(iy, ix);

			RgbColor px = this->GetPixel(roi.x + ix, roi.y + iy);

			pixel[0] = px.r;
			pixel[1] = px.g;
			pixel[2] = px.b;
		}
	return dst;
}

cv::Mat YuvFrame::ToMatRgb(const cv::Rect& roi, const cv::Size& dstSize) const
{
	auto tmp = this->ToMatRgb(roi);
	if (tmp.size() != dstSize) {
		cv::resize(tmp, tmp, dstSize);
	}
	return tmp;
}
void YuvFrame::CopyToRgb(const Rect& roi, Mat dst) const
{
	for (int ix = 0; ix < roi.width; ix++)
		for (int iy = 0; iy < roi.height; iy++) {
			cv::Vec3b& pixel = dst.at<cv::Vec3b>(iy, ix);

			RgbColor px = this->GetPixel(roi.x + ix, roi.y + iy);

			pixel[0] = px.r;
			pixel[1] = px.g;
			pixel[2] = px.b;
		}
}
void YuvFrame::CopyToBgr(const Rect& roi, Mat dst) const
{
	for (int ix = 0; ix < roi.width; ix++)
		for (int iy = 0; iy < roi.height; iy++) {
			cv::Vec3b& pixel = dst.at<cv::Vec3b>(iy, ix);

			RgbColor px = this->GetPixel(roi.x + ix, roi.y + iy);

			pixel[0] = px.b;
			pixel[1] = px.g;
			pixel[2] = px.r;
		}
}

Mat YuvFrame::ToMatBgr(const Rect& roi) const
{
	Mat dst(roi.width, roi.height, CV_8UC3, cv::Scalar(0, 0, 0));

	CopyToBgr(roi, dst);
	return dst;
}
void YuvFrame::CopyToRgb(const cv::Rect& roi, uint8* dst) const
{
	Mat tmp(roi.width, roi.height, CV_8UC3, dst);
	CopyToRgb(roi, tmp);
}

void YuvFrame::CopyToRgb(const cv::Rect& roi, const cv::Size& dstSize, uint8* dst) const
{
	if (roi.size() != dstSize) {
		auto src = this->ToMatRgb(roi);
		Mat dstMat(dstSize.width, dstSize.height, CV_8UC3, dst);
		cv::resize(src, dstMat, dstSize);
	}
	else
	{
		Mat dstMat(dstSize.width, dstSize.height, CV_8UC3, dst);
		this->CopyToRgb(roi, dstMat);
	}
}

uint8* YuvFrame::AllocateFrameYuv(int w, int h)
{
	int size = w * h;
	size += size / 2;
	auto ret = new uint8[size];
	return ret;
}

uint8* YuvFrame::AllocateFrameRgb(int w, int h)
{
	int size = w * h;
	return new uint8[size * 3];
}

void YuvFrame::CopyToBgr(const cv::Rect& roi, uint8* dst) const
{
	Mat tmp(roi.width, roi.height, CV_8UC3, dst);
	CopyToBgr(roi, tmp);
}

cv::Size YuvFrame::Size() const
{
	return cv::Size(_width, _height);
}

void YuvFrame::CopyToBgr(const cv::Rect& roi, const cv::Size& dstSize, uint8* dst) const
{
	if (roi.size() != dstSize) {
		auto src = this->ToMatBgr(roi);
		Mat dstMat(dstSize.width, dstSize.height, CV_8UC3, dst);
		cv::resize(src, dstMat, dstSize, 0, 0, INTER_LINEAR);
	}
	else
	{
		Mat dstMat(dstSize.width, dstSize.height, CV_8UC3, dst);
		this->CopyToBgr(roi, dstMat);
	}
}

cv::Mat YuvFrame::ToMatBgr(const cv::Rect& roi, const cv::Size& dstSize) const
{
	auto tmp = this->ToMatBgr(roi);
	if (tmp.size() != dstSize) {
		cv::resize(tmp, tmp, dstSize);
	}
	return tmp;
}


YuvFrame YuvFrame::LoadFile(string file) {
	cv::Mat img = cv::imread(file);
	cv::Mat yuv;
	cv::cvtColor(img, yuv, COLOR_BGR2YUV_I420);

	int ySize = img.cols * img.rows;                // Y plane size (full resolution)
	int size = ySize + ySize / 2;



	YuvFrame frame(img.cols, img.rows);
	std::memcpy(frame.GetData(), yuv.data, size);
	// now we should just return img.ptr, and make img won't delete the allocated array.
	//frame.Dump();
	return frame;
}
uint8* AnnotationResult::GetMask(int index) const {
	return this->_items[index].Mask.Data();
}
int AnnotationResult::GetClassId(int index) const {
	return this->_items[index].ClassId;
}

int AnnotationResult::Count() const
{
	return this->_items.size();
}

void AnnotationResult::Add(const ArrayOwner<uint8> &mask, int classid)
{
	this->_items.emplace_back(mask, classid);
}
