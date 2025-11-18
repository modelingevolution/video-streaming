#include "common.h"
#include <cstdint>  // For uint64_t

template class FeatureData<unsigned char>;

std::map<int, cv::Vec3b> COLORS = {
	{0, cv::Vec3b(244, 67, 54)},
	{1, cv::Vec3b(233, 30, 99)},
	{2, cv::Vec3b(156, 39, 176)},
	{3, cv::Vec3b(103, 58, 183)},
	{4, cv::Vec3b(63, 81, 181)},
	{5, cv::Vec3b(33, 150, 243)},
	{6, cv::Vec3b(3, 169, 244)},
	{7, cv::Vec3b(0, 188, 212)},
	{8, cv::Vec3b(0, 150, 136)},
	{9, cv::Vec3b(76, 175, 80)},
	{10, cv::Vec3b(139, 195, 74)},
	{11, cv::Vec3b(205, 220, 57)},
	{12, cv::Vec3b(255, 235, 59)},
	{13, cv::Vec3b(255, 193, 7)},
	{14, cv::Vec3b(255, 152, 0)},
	{15, cv::Vec3b(255, 87, 34)},
	{16, cv::Vec3b(121, 85, 72)},
	{17, cv::Vec3b(158, 158, 158)},
	{18, cv::Vec3b(96, 125, 139)}
};
template<typename T>
FeatureData<T>::FeatureData(uint32_t buffers_size, float32_t qp_zp, float32_t qp_scale, uint32_t width,
	hailo_vstream_info_t vstream_info):
	m_buffers(buffers_size), m_qp_zp(qp_zp), m_qp_scale(qp_scale), m_width(width), m_vstream_info(vstream_info) {}

template<typename T>
bool FeatureData<T>::sort_tensors_by_size(std::shared_ptr<FeatureData> i, std::shared_ptr<FeatureData> j) { return i->m_width < j->m_width; }

void PrintMatStat(const cv::Mat &mat) {
	assert(mat.channels() == 3);

	uint64_t r_sum = 0, g_sum = 0, b_sum = 0;

	for (int y = 0; y < mat.rows; ++y) {
		for (int x = 0; x < mat.cols; ++x) {
			// Access the BGR pixel at (x, y)
			auto pixel = mat.at<cv::Vec3b>(y, x);
			b_sum += pixel[0];  // Blue channel
			g_sum += pixel[1];  // Green channel
			r_sum += pixel[2];  // Red channel
		}
	}
	std::cout << "Pixel at (0,0): " << mat.at<cv::Vec3b>(0, 0) << std::endl;
	std::cout << "Pixel at (100,100): " << mat.at<cv::Vec3b>(100, 100) << std::endl;
	std::cout << "Pixel at (200,200): " << mat.at<cv::Vec3b>(200, 200) << std::endl;
	std::cout << "Pixel at (300,300): " << mat.at<cv::Vec3b>(300, 300) << std::endl;

	std::cout << "SUM RGB:            [" << r_sum << ", " << g_sum << ", " << b_sum << "]" << std::endl;
}

std::string get_coco_name_from_int(int cls)
{
    std::string result = "N/A";
    switch(cls) {
		case 0: result = "__background__";break;
		case 1: result = "person";break;
		case 2: result = "bicycle";break;
		case 3: result = "car";break;
		case 4: result = "motorcycle";break;
		case 5: result = "airplane";break;
		case 6: result = "bus";break;
		case 7: result = "train";break;
		case 8: result = "truck";break;
		case 9: result = "boat";break;
		case 10: result = "traffic light";break;
		case 11: result = "fire hydrant";break;
		case 12: result = "stop sign";break;
		case 13: result = "parking meter";break;
		case 14: result = "bench";break;
		case 15: result = "bird";break;
		case 16: result = "cat";break;
		case 17: result = "dog";break;
		case 18: result = "horse";break;
		case 19: result = "sheep";break;
		case 20: result = "cow";break;
		case 21: result = "elephant";break;
		case 22: result = "bear";break;
		case 23: result = "zebra";break;
		case 24: result = "giraffe";break;
		case 25: result = "backpack";break;
		case 26: result = "umbrella";break;
		case 27: result = "handbag";break;
		case 28: result = "tie";break;
		case 29: result = "suitcase";break;
		case 30: result = "frisbee";break;
		case 31: result = "skis";break;
		case 32: result = "snowboard";break;
		case 33: result = "sports ball";break;
		case 34: result = "kite";break;
		case 35: result = "baseball bat";break;
		case 36: result = "baseball glove";break;;
		case 37: result = "skateboard";break;
		case 38: result = "surfboard";break;
		case 39: result = "tennis racket";break;
		case 40: result = "bottle";break;
		case 41: result = "wine glass";break;
		case 42: result = "cup";break;
		case 43: result = "fork";break;
		case 44: result = "knife";break;
		case 45: result = "spoon";break;
		case 46: result = "bowl";break;
		case 47: result = "banana";break;
		case 48: result = "apple";break;
		case 49: result = "sandwich";break;
		case 50: result = "orange";break;
		case 51: result = "broccoli";break;
		case 52: result = "carrot";break;
		case 53: result = "hot dog";break;
		case 54: result = "pizza";break;
		case 55: result = "donut";break;
		case 56: result = "cake";break;
		case 57: result = "chair";break;
		case 58: result = "couch";break;
		case 59: result = "potted plant";break;
		case 60: result = "bed";break;
		case 61: result = "dining table";break;
		case 62: result = "toilet";break;
		case 63: result = "tv";break;
		case 64: result = "laptop";break;
		case 65: result = "mouse";break;
		case 66: result = "remote";break;
		case 67: result = "keyboard";break;
		case 68: result = "cell phone";break;
		case 69: result = "microwave";break;
		case 70: result = "oven";break;
		case 71: result = "toaster";break;
		case 72: result = "sink";break;
		case 73: result = "refrigerator";break;
		case 74: result = "book";break;
		case 75: result = "clock";break;
		case 76: result = "vase";break;
		case 77: result = "scissors";break;
		case 78: result = "teddy bear";break;
		case 79: result = "hair drier";break;
		case 80: result = "toothbrush";break;
    }
	return result;
}
