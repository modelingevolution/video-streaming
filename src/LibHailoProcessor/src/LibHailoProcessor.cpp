// LibHailoProcessor.cpp : Defines the entry point for the application.
//

#include "LibHailoProcessor.h"

#include "Frame.h"
#include "HailoProcessor.h"

using namespace std;

int main()
{
	cout << "Hallo :)" << endl;

	/*
	auto frame = YuvFrame::LoadFile("c:\\ml\\nba.jpg");

	auto dstFrameData = YuvFrame::AllocateFrameRgb(310, 310);

	Rect roi(10,10,620,620);
	Size dstSize(310, 310);
	frame.CopyToBgr(roi, dstSize, dstFrameData);


	cv::Mat mat(310, 310, CV_8UC3, dstFrameData);
	cv::imwrite("c:\\ml\\nba2.jpg", mat);
	*/
	return 0;
}
