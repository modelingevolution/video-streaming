#include "HailoProcessor.h"

#include <barrier>
#include <mutex>
#include <future>
#include <latch>

#include <hailo/vstream.hpp>
#include "common/hailo_common.hpp"
#include "ArrayOperations.h"
#include "common.h"
#include "yolov8seg_postprocess.hpp"
#include "common/math.hpp"
#include "common/tensors.hpp"
#include "common/labels/coco_eighty.hpp"
#include "defs.h"
using namespace xt::placeholders;

#define SCORE_THRESHOLD 0.6
#define IOU_THRESHOLD 0.7
#define NUM_CLASSES 80

HailoException::HailoException(const hailo_status st) : std::exception(), _status(st)
{
	this->_msg = hailo_get_status_message(st);
}

HailoException::HailoException(const string& str) : _status(HAILO_SUCCESS)
{
	this->_msg = str.c_str();
}

HailoException::HailoException(const HailoException& c) : _status(c._status), _msg(c._msg)
{


}

hailo_status HailoException::GetStatus()
{
	return this->_status;
}

const char* HailoException::what() const noexcept
{
	return this->_msg;
}

HailoError::HailoError() 
{
	_hailoException = nullptr;
}

void HailoError::SetLastError(const HailoException& ex)
{
	if (_hailoException != nullptr) 
		delete _hailoException;
	this->_hailoException = new HailoException(ex);
	this->_isSet = true;
}

bool HailoError::IsOk()
{
	return !this->_isSet;
}
// Constructor
AiProcessorStats::AiProcessorStats() : _frames(0), _preprocessingTotal(0), _interferenceTotal(0),
                                       _postProcessingTotal(0), _lastPreprocessing(0),
                                       _lastInterference(0), _lastPostProcessing(0) {}

// Stopper methods
AiProcessorStats::Stopper::Stopper(std::chrono::nanoseconds& duration) : start(std::chrono::high_resolution_clock::now()), duration(duration) {}

StopWatch::StopWatch() : start(std::chrono::high_resolution_clock::now()) {}
std::chrono::milliseconds StopWatch::GetMs() {
	return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::high_resolution_clock::now() - start);
}
AiProcessorStats::Stopper::~Stopper() {
    duration += std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::high_resolution_clock::now() - start);
}

// Measurement methods
AiProcessorStats::Stopper AiProcessorStats::MeasurePreprocessing() {
    _lastPreprocessing = std::chrono::nanoseconds::zero();
    return Stopper(_lastPreprocessing);
}

AiProcessorStats::Stopper AiProcessorStats::MeasureInterference() {
    _lastInterference = std::chrono::nanoseconds::zero();
    return Stopper(_lastInterference);
}

AiProcessorStats::Stopper AiProcessorStats::MeasurePostProcessing() {
    _lastPostProcessing = std::chrono::nanoseconds::zero();
    return Stopper(_lastPostProcessing);
}

// Operator overloading
AiProcessorStats AiProcessorStats::operator+(const AiProcessorStats& other) const {
    AiProcessorStats result(*this);
    result._frames += other._frames;
    result._preprocessingTotal += other._preprocessingTotal;
    result._interferenceTotal += other._interferenceTotal;
    result._postProcessingTotal += other._postProcessingTotal;
    return result;
}

AiProcessorStats& AiProcessorStats::operator++() {
    ++_frames;
    _preprocessingTotal += _lastPreprocessing;
    _interferenceTotal += _lastInterference;
    _postProcessingTotal += _lastPostProcessing;
    return *this;
}

// Computed properties
std::chrono::milliseconds AiProcessorStats::PreProcessingAvg() const {
    return std::chrono::duration_cast<std::chrono::milliseconds>(_preprocessingTotal) / (_frames ? _frames : 1);
}

std::chrono::milliseconds AiProcessorStats::InterferenceAvg() const {
    return std::chrono::duration_cast<std::chrono::milliseconds>(_interferenceTotal) / (_frames ? _frames : 1);
}

std::chrono::milliseconds AiProcessorStats::PostProcessingAvg() const {
    return std::chrono::duration_cast<std::chrono::milliseconds>(_postProcessingTotal) / (_frames ? _frames : 1);
}
HailoException& HailoError::LastException() const
{
	return *this->_hailoException;
}

void HailoAsyncProcessor::Stop() {
	this->_isRunning = false;
	// Triggers
	_input_vstream->abort();
	_readOpNotifier.EnqueueWork();
	 _callbackArgs.TryWrite(nullptr);
	for(auto & _thread : _threads)
	 	_thread.join();
	std::cout << "All threads stopped." << std::endl;
}



std::shared_ptr<ConfiguredNetworkGroup> HailoAsyncProcessor::ConfigureNetworkGroup(
	VDevice &vdevice, const std::string &yolov_hef)
{
	auto hef_exp = Hef::create(yolov_hef);
	if (!hef_exp) {
		throw HailoException(hef_exp.status());
	}
	auto hef = hef_exp.release();

	auto configure_params = hef.create_configure_params(HAILO_STREAM_INTERFACE_PCIE);
	if (!configure_params) {
		throw HailoException(configure_params.status());
	}

	auto network_groups = vdevice.configure(hef, configure_params.value());
	if (!network_groups) {
		throw HailoException(network_groups.status());
	}

	if (1 != network_groups->size()) {
		std::cerr << "Invalid amount of network groups" << std::endl;
		throw HailoException(HAILO_INTERNAL_FAILURE);
	}

	return std::move(network_groups->at(0));
}
constexpr bool QUANTIZED = true;
constexpr hailo_format_type_t FORMAT_TYPE = HAILO_FORMAT_TYPE_AUTO;

unique_ptr<HailoAsyncProcessor> HailoAsyncProcessor::Load(const string &fileName) {
	auto vdevice_exp = VDevice::create();

	if (!vdevice_exp)
		throw HailoException(vdevice_exp.status());

	std::unique_ptr<VDevice> vdevice = vdevice_exp.release();

	std::shared_ptr<ConfiguredNetworkGroup> networkGroup = HailoAsyncProcessor::ConfigureNetworkGroup(
		*vdevice, fileName);

	auto ptr = new HailoAsyncProcessor(vdevice,networkGroup);
	return std::unique_ptr<HailoAsyncProcessor>(ptr);
}


std::shared_ptr<FeatureData<uint8>> HailoAsyncProcessor::CreateFeature(const hailo_vstream_info_t &vstream_info, size_t frameSize) {
	return std::make_shared<FeatureData<uint8>>(static_cast<uint32_t>(frameSize), vstream_info.quant_info.qp_zp,
		vstream_info.quant_info.qp_scale, vstream_info.shape.width, vstream_info);

}
void HailoAsyncProcessor::Write(const cv::Mat &org_frame) {
	auto input_shape = _input_vstream->get_info().shape;
	int height = input_shape.height;
	int width = input_shape.width;

	auto dstSize = cv::Size(width, height);
	size_t frame_size = _input_vstream->get_frame_size();

	if(org_frame.size() != dstSize) {
		// we need to resize.
		Mat dst(dstSize, 3, CV_8UC1);
		cv::resize(org_frame, dst, dstSize);
		_input_vstream->write(MemoryView(dst.data, frame_size)); // Writing height * width, 3 channels of uint8
	} else {
		_input_vstream->write(MemoryView(org_frame.data, frame_size)); // Writing height * width, 3 channels of uint8
	}
}

void HailoAsyncProcessor::Write(const YuvFrame &frame) {
	auto prep = this->_stats.MeasurePreprocessing();
	const Rect r(0,0,frame.Width(), frame.Height());
	Write(frame,r);
}

void HailoAsyncProcessor::Write(const YuvFrame &frame, const cv::Rect &roi) {
	auto mat = frame.ToMatBgr(roi);
	//PrintMatStat(mat);
	Write(mat);
}

void HailoAsyncProcessor::OnRead(int nr) {
	unsigned long frame = 0;
	auto max = _features.size();
	auto feature = this->_features[nr];
	auto &output_vstream = _vstreams.second[nr];
	for (;this->_isRunning; frame++)
	{
		auto &buffer = feature->m_buffers.get_write_buffer();
		//std::this_thread::sleep_for(200ms);
		hailo_status status = HAILO_SUCCESS;
		do {
			status = output_vstream.read(MemoryView(buffer.data(), buffer.size()));
		} while (status == HAILO_TIMEOUT && this->_isRunning);

		if (!this->_isRunning) {
			break;
		}

		feature->m_buffers.release_write_buffer();

		if (status != HAILO_SUCCESS) {
			std::string txt = hailo_get_status_message(status);
			std::cout << "Status failed in read loop: " << txt << std::endl;
		} else {
#ifdef DEBUG
			//std::cout << "Read completed. Output node: " << nr << " Frame: " << frame++ << std::endl;
#endif
			auto prv = this->_readOutputCounter.fetch_add(1);

			if(prv == max-1) {
				_readOutputCounter.fetch_sub(max);
				std::cout << "Read: " << frame << std::endl;
				_readOpNotifier.EnqueueWork();
			}
		}
	};
	output_vstream.abort();
	//std::cout << "Exiting read loop: " << nr << std::endl;
}
HailoTensorPtr PopProto(std::vector<HailoTensorPtr> &tensors){
	auto it = tensors.begin();
	while (it != tensors.end()) {
		auto tensor = *it;
		if (tensor->features() == 32 && tensor->height() == 160 && tensor->width() == 160){
			auto proto = tensor;
			tensors.erase(it);
			return proto;
		}
		else{
			++it;
		}
	}
	return nullptr;
}
Quadruple GetBoxesScoreMask(std::vector<HailoTensorPtr> &tensors, int num_classes, int regression_length){

    auto raw_proto = PopProto(tensors);

    std::vector<HailoTensorPtr> outputs_boxes(tensors.size() / 3);
    std::vector<HailoTensorPtr> outputs_masks(tensors.size() / 3);

    // Prepare the scores xarray at the size we will fill in in-place
    int total_scores = 0;
    for (int i = 0; i < tensors.size(); i = i + 3) {
        total_scores += tensors[i+1]->width() * tensors[i+1]->height();
    }

    std::vector<size_t> scores_shape = { (long unsigned int)total_scores, (long unsigned int)num_classes };

    xt::xarray<float> scores(scores_shape);

    std::vector<size_t> proto_shape = { {(long unsigned int)raw_proto->height(),
                                                (long unsigned int)raw_proto->width(),
                                                (long unsigned int)raw_proto->features()} };
    xt::xarray<float> proto(proto_shape);

    int view_index_scores = 0;

    for (uint i = 0; i < tensors.size(); i = i + 3)
    {
        // Bounding boxes extraction will be done later on only on the boxes that surpass the score threshold
        outputs_boxes[i / 3] = tensors[i];

        // Extract and dequantize the scores outputs
        auto dequantized_output_s = common::dequantize(common::get_xtensor(tensors[i+1]), tensors[i+1]->vstream_info().quant_info.qp_scale, tensors[i+1]->vstream_info().quant_info.qp_zp);
        int num_proposals_scores = dequantized_output_s.shape(0)*dequantized_output_s.shape(1);

        // From the layer extract the scores
        auto output_scores = xt::view(dequantized_output_s, xt::all(), xt::all(), xt::all());
        xt::view(scores, xt::range(view_index_scores, view_index_scores + num_proposals_scores), xt::all()) = xt::reshape_view(output_scores, {num_proposals_scores, num_classes});
        view_index_scores += num_proposals_scores;

        // Keypoints extraction will be done later according to the boxes that surpass the threshold
        outputs_masks[i / 3] = tensors[i+2];
    }

    proto = common::dequantize(common::get_xtensor(raw_proto), raw_proto->vstream_info().quant_info.qp_scale, raw_proto->vstream_info().quant_info.qp_zp);

    return Quadruple{outputs_boxes, scores, outputs_masks, proto};
}
std::vector<xt::xarray<double>> GetCenters(std::vector<int>& strides, std::vector<int>& network_dims,
										std::size_t boxes_num, int strided_width, int strided_height){

	std::vector<xt::xarray<double>> centers(boxes_num);

	for (uint i=0; i < boxes_num; i++) {
		strided_width = network_dims[0] / strides[i];
		strided_height = network_dims[1] / strides[i];

		// Create a meshgrid of the proper strides
		xt::xarray<int> grid_x = xt::arange(0, strided_width);
		xt::xarray<int> grid_y = xt::arange(0, strided_height);

		auto mesh = xt::meshgrid(grid_x, grid_y);
		grid_x = std::get<1>(mesh);
		grid_y = std::get<0>(mesh);

		// Use the meshgrid to build up box center prototypes
		auto ct_row = (xt::flatten(grid_y) + 0.5) * strides[i];
		auto ct_col = (xt::flatten(grid_x) + 0.5) * strides[i];

		centers[i] = xt::stack(xt::xtuple(ct_col, ct_row, ct_col, ct_row), 1);
	}

	return centers;
}
float DequantizeValue(uint8_t val, float32_t qp_scale, float32_t qp_zp){
	return (float(val) - qp_zp) * qp_scale;
}
void DequantizeMaskValues(xt::xarray<float>& dequantized_outputs, int index,
						xt::xarray<uint8_t>& quantized_outputs,
						size_t dim1, float32_t qp_scale, float32_t qp_zp){
	for (size_t i = 0; i < dim1; i++){
		dequantized_outputs(i) = DequantizeValue(quantized_outputs(index, i), qp_scale, qp_zp);
	}
}
void DequantizeBoxValues(xt::xarray<float>& dequantized_outputs, int index,
						xt::xarray<uint8_t>& quantized_outputs,
						size_t dim1, size_t dim2, float32_t qp_scale, float32_t qp_zp){
	for (size_t i = 0; i < dim1; i++){
		for (size_t j = 0; j < dim2; j++){
			dequantized_outputs(i, j) = DequantizeValue(quantized_outputs(index, i, j), qp_scale, qp_zp);
		}
	}
}
std::vector<std::pair<HailoDetection, xt::xarray<float>>> decode_boxes_and_extract_masks(std::vector<HailoTensorPtr> raw_boxes_outputs,
                                                                                std::vector<HailoTensorPtr> raw_masks_outputs,
                                                                                xt::xarray<float> scores,
                                                                                std::vector<int> network_dims,
                                                                                std::vector<int> strides,
                                                                                int regression_length) {
    int strided_width, strided_height, class_index;
    std::vector<std::pair<HailoDetection, xt::xarray<float>>> detections_and_masks;
    int instance_index = 0;
    float confidence = 0.0;
    std::string label;

    auto centers = GetCenters(std::ref(strides), std::ref(network_dims), raw_boxes_outputs.size(), strided_width, strided_height);

    // Box distribution to distance
    auto regression_distance =  xt::reshape_view(xt::arange(0, regression_length + 1), {1, 1, regression_length + 1});

    for (uint i = 0; i < raw_boxes_outputs.size(); i++)
    {
        // Boxes setup
        float32_t qp_scale = raw_boxes_outputs[i]->vstream_info().quant_info.qp_scale;
        float32_t qp_zp = raw_boxes_outputs[i]->vstream_info().quant_info.qp_zp;

        auto output_b = common::get_xtensor(raw_boxes_outputs[i]);
        int num_proposals = output_b.shape(0) * output_b.shape(1);
        auto output_boxes = xt::view(output_b, xt::all(), xt::all(), xt::all());
        xt::xarray<uint8_t> quantized_boxes = xt::reshape_view(output_boxes, {num_proposals, 4, regression_length + 1});

        auto shape = {quantized_boxes.shape(1), quantized_boxes.shape(2)};

        // Masks setup
        float32_t qp_scale_mask = raw_masks_outputs[i]->vstream_info().quant_info.qp_scale;
        float32_t qp_zp_mask = raw_masks_outputs[i]->vstream_info().quant_info.qp_zp;

        auto output_m = common::get_xtensor(raw_masks_outputs[i]);
        int num_proposals_masks = output_m.shape(0) * output_m.shape(1);
        auto output_masks = xt::view(output_m, xt::all(), xt::all(), xt::all());
        xt::xarray<uint8_t> quantized_masks = xt::reshape_view(output_masks, {num_proposals_masks, 32});

        auto mask_shape = {quantized_masks.shape(1)};

        // Bbox decoding
        for (uint j = 0; j < num_proposals; j++) {
            class_index = xt::argmax(xt::row(scores, instance_index))(0);
            confidence = scores(instance_index, class_index);
            instance_index++;
            if (confidence < SCORE_THRESHOLD)
                continue;

            xt::xarray<float> box(shape);

            DequantizeBoxValues(box, j, quantized_boxes,
                                    box.shape(0), box.shape(1),
                                    qp_scale, qp_zp);
            common::softmax_2D(box.data(), box.shape(0), box.shape(1));

            xt::xarray<float> mask(mask_shape);

            DequantizeMaskValues(mask, j, quantized_masks,
                                    mask.shape(0), qp_scale_mask,
                                    qp_zp_mask);

            auto box_distance = box * regression_distance;
            xt::xarray<float> reduced_distances = xt::sum(box_distance, {2});
            auto strided_distances = reduced_distances * strides[i];

            // Decode box
            auto distance_view1 = xt::view(strided_distances, xt::all(), xt::range(_, 2)) * -1;
            auto distance_view2 = xt::view(strided_distances, xt::all(), xt::range(2, _));
            auto distance_view = xt::concatenate(xt::xtuple(distance_view1, distance_view2), 1);
            auto decoded_box = centers[i] + distance_view;

            HailoBBox bbox(decoded_box(j, 0) / network_dims[0],
                           decoded_box(j, 1) / network_dims[1],
                           (decoded_box(j, 2) - decoded_box(j, 0)) / network_dims[0],
                           (decoded_box(j, 3) - decoded_box(j, 1)) / network_dims[1]);

            label = common::coco_eighty[class_index + 1];
            HailoDetection detected_instance(bbox, class_index, label, confidence);

            detections_and_masks.push_back(std::make_pair(detected_instance, mask));

        }
    }

    return detections_and_masks;
}
float IouCalc(const HailoBBox &box_1, const HailoBBox &box_2)
{
	// Calculate IOU between two detection boxes
	const float width_of_overlap_area = std::min(box_1.xmax(), box_2.xmax()) - std::max(box_1.xmin(), box_2.xmin());
	const float height_of_overlap_area = std::min(box_1.ymax(), box_2.ymax()) - std::max(box_1.ymin(), box_2.ymin());
	const float positive_width_of_overlap_area = std::max(width_of_overlap_area, 0.0f);
	const float positive_height_of_overlap_area = std::max(height_of_overlap_area, 0.0f);
	const float area_of_overlap = positive_width_of_overlap_area * positive_height_of_overlap_area;
	const float box_1_area = (box_1.ymax() - box_1.ymin()) * (box_1.xmax() - box_1.xmin());
	const float box_2_area = (box_2.ymax() - box_2.ymin()) * (box_2.xmax() - box_2.xmin());
	// The IOU is a ratio of how much the boxes overlap vs their size outside the overlap.
	// Boxes that are similar will have a higher overlap threshold.
	return area_of_overlap / (box_1_area + box_2_area - area_of_overlap);
}
std::vector<std::pair<HailoDetection, xt::xarray<float>>> Nms(std::vector<std::pair<HailoDetection, xt::xarray<float>>> &detections_and_masks,
															const float iou_thr, bool should_nms_cross_classes = false) {

	std::vector<std::pair<HailoDetection, xt::xarray<float>>> detections_and_masks_after_nms;

	for (uint index = 0; index < detections_and_masks.size(); index++)
	{
		if (detections_and_masks[index].first.get_confidence() != 0.0f)
		{
			for (uint jindex = index + 1; jindex < detections_and_masks.size(); jindex++)
			{
				if ((should_nms_cross_classes || (detections_and_masks[index].first.get_class_id() == detections_and_masks[jindex].first.get_class_id())) &&
					detections_and_masks[jindex].first.get_confidence() != 0.0f)
				{
					// For each detection, calculate the IOU against each following detection.
					float iou = IouCalc(detections_and_masks[index].first.get_bbox(), detections_and_masks[jindex].first.get_bbox());
					// If the IOU is above threshold, then we have two similar detections,
					// and want to delete the one.
					if (iou >= iou_thr)
					{
						// The detections are arranged in highest score order,
						// so we want to erase the latter detection.
						detections_and_masks[jindex].first.set_confidence(0.0f);
					}
				}
			}
		}
	}
	for (uint index = 0; index < detections_and_masks.size(); index++)
	{
		if (detections_and_masks[index].first.get_confidence() != 0.0f)
		{
			detections_and_masks_after_nms.push_back(std::make_pair(detections_and_masks[index].first, detections_and_masks[index].second));
		}
	}
	return detections_and_masks_after_nms;
}
xt::xarray<float> dot(xt::xarray<float> mask, xt::xarray<float> reshaped_proto,
					size_t proto_height, size_t proto_width, size_t mask_num = 32){

	auto shape = {proto_height, proto_width};
	xt::xarray<float> mask_product(shape);

	for (size_t i = 0; i < mask_product.shape(0); i++) {
		for (size_t j = 0; j < mask_product.shape(1); j++) {
			for (size_t k = 0; k < mask_num; k++) {
				mask_product(i,j) += mask(k) * reshaped_proto(k, i, j);
			}
		}
	}
	return mask_product;
}
void Sigmoid(float *data, const int size) {
	for (int i = 0; i < size; i++)
		data[i] = 1.0f / (1.0f + std::exp(-1.0 * data[i]));
}
cv::Mat Xarray2Mat(xt::xarray<float> xarr) {
	cv::Mat mat (xarr.shape()[0], xarr.shape()[1], CV_32FC1, xarr.data(), 0);
	return mat;
}

cv::Mat CropMask(cv::Mat mask, HailoBBox box) {
	auto x_min = box.xmin();
	auto y_min = box.ymin();
	auto x_max = box.xmax();
	auto y_max = box.ymax();

	int rows = mask.rows;
	int cols = mask.cols;

	// Ensure ROI coordinates are within the valid range
	int top_start = std::max(0, static_cast<int>(std::ceil(y_min * rows)));
	int bottom_end = std::min(rows, static_cast<int>(std::ceil(y_max * rows)));
	int left_start = std::max(0, static_cast<int>(std::ceil(x_min * cols)));
	int right_end = std::min(cols, static_cast<int>(std::ceil(x_max * cols)));

	// Create ROI rectangles
	cv::Rect top_roi(0, 0, cols, top_start);
	cv::Rect bottom_roi(0, bottom_end, cols, rows - bottom_end);
	cv::Rect left_roi(0, 0, left_start, rows);
	cv::Rect right_roi(right_end, 0, cols - right_end, rows);

	// Set values to zero in the specified ROIs
	mask(top_roi) = 0;
	mask(bottom_roi) = 0;
	mask(left_roi) = 0;
	mask(right_roi) = 0;

	return mask;
}
std::vector<DetectionAndMask> decode_masks(std::vector<std::pair<HailoDetection, xt::xarray<float>>> detections_and_masks_after_nms,
																		xt::xarray<float> proto, int org_image_height, int org_image_width){

	std::vector<DetectionAndMask> detections_and_cropped_masks(detections_and_masks_after_nms.size(),
																DetectionAndMask({
																	HailoDetection(HailoBBox(0.0,0.0,0.0,0.0), "", 0.0),
																	cv::Mat(org_image_height, org_image_width, CV_32FC1)}
																	));

	int mask_height = static_cast<int>(proto.shape(0));
	int mask_width = static_cast<int>(proto.shape(1));
	int mask_features = static_cast<int>(proto.shape(2));

	auto reshaped_proto = xt::reshape_view(xt::transpose(xt::reshape_view(proto, {-1, mask_features}), {1,0}), {-1, mask_height, mask_width});

	for (int i = 0; i < detections_and_masks_after_nms.size(); i++) {

		auto curr_detection = detections_and_masks_after_nms[i].first;
		auto curr_mask = detections_and_masks_after_nms[i].second;

		auto mask_product = dot(curr_mask, reshaped_proto, reshaped_proto.shape(1), reshaped_proto.shape(2), curr_mask.shape(0));

		Sigmoid(mask_product.data(), mask_product.size());

		cv::Mat mask = Xarray2Mat(mask_product).clone();
		cv::resize(mask, mask, cv::Size(org_image_width, org_image_height), 0, 0, cv::INTER_LINEAR);

		mask = CropMask(mask, curr_detection.get_bbox());

		detections_and_cropped_masks[i] = DetectionAndMask({curr_detection, mask});
	}

	return detections_and_cropped_masks;
}

std::vector<DetectionAndMask> yolov8segPostprocess(std::vector<HailoTensorPtr> &tensors,
																				std::vector<int> network_dims,
																				std::vector<int> strides,
																				int regression_length,
																				int num_classes,
																				int org_image_height,
																				int org_image_width) {
	std::vector<DetectionAndMask> detections_and_cropped_masks;
	if (tensors.size() == 0)
	{
		return detections_and_cropped_masks;
	}

	Quadruple boxes_scores_masks_mask_matrix = GetBoxesScoreMask(tensors, num_classes, regression_length);

	std::vector<HailoTensorPtr> raw_boxes = boxes_scores_masks_mask_matrix.boxes;
	xt::xarray<float> scores = boxes_scores_masks_mask_matrix.scores;
	std::vector<HailoTensorPtr> raw_masks = boxes_scores_masks_mask_matrix.masks;
	xt::xarray<float> proto = boxes_scores_masks_mask_matrix.proto_data;

	// Decode the boxes and get masks
	auto detections_and_masks = decode_boxes_and_extract_masks(raw_boxes, raw_masks, scores, network_dims, strides, regression_length);

	// Filter with NMS
	auto detections_and_masks_after_nms = Nms(detections_and_masks, IOU_THRESHOLD, true);

	// Decode the masking
	auto detections_and_decoded_masks = decode_masks(detections_and_masks_after_nms, proto, org_image_height, org_image_width);

	return detections_and_decoded_masks;
}

std::vector<cv::Mat> Yolov8(HailoROIPtr roi, int org_image_height, int org_image_width)
{
	// anchor params
	int regression_length = 15;
	std::vector<int> strides = {8, 16, 32};
	std::vector<int> network_dims = {640, 640};

	std::vector<HailoTensorPtr> tensors = roi->get_tensors();
	auto filtered_detections_and_masks = yolov8segPostprocess(tensors,
															network_dims,
															strides,
															regression_length,
															NUM_CLASSES,
															org_image_height,
															org_image_width);

	std::vector<HailoDetection> detections;
	std::vector<cv::Mat> masks;

	for (auto& det_and_msk : filtered_detections_and_masks){
		detections.push_back(det_and_msk.detection);
		masks.push_back(det_and_msk.mask);
	}

	hailo_common::add_detections(roi, detections);

	return masks;
}

std::vector<cv::Mat> Filter(HailoROIPtr roi, int org_image_height, int org_image_width)
{
	return Yolov8(roi, org_image_height, org_image_width);
}
void HailoAsyncProcessor::PostProcess() {
	unsigned long  iteration = 0;

	for(;this->_readOpNotifier.Wait() && this->_isRunning; iteration++)
	{
		StopWatch sw;
		auto result = make_unique<SegmentationResult>();

		std::cout << "Post process iteration: " << iteration << endl;

		std::sort(_features.begin(), _features.end(), &FeatureData<uint8>::sort_tensors_by_size);
		HailoROIPtr roi = std::make_shared<HailoROI>(HailoROI(HailoBBox(0.0f, 0.0f, 1.0f, 1.0f)));

		for (uint j = 0; j < _features.size(); j++) {
			roi->add_tensor(std::make_shared<HailoTensor>(
				reinterpret_cast<uint8 *>(_features[j]->m_buffers.get_read_buffer().data()), _features[j]->m_vstream_info));
		}

		// not sure why we filter here.
		auto filtered_masks = Filter(roi,640,640);

		for (auto &feature: _features) {
			feature->m_buffers.release_read_buffer();
		}

		std::vector<HailoDetectionPtr> detections = hailo_common::get_hailo_detections(roi);

		std::cout << "Detections: " << detections.size() << " filtered_masks: " << filtered_masks.size() << endl;

		for (size_t i = 0; i < filtered_masks.size(); ++i)
		{
			cv::Mat& mask = filtered_masks[i];
			auto &detection = detections[i];
			HailoBBox bbox = detection->get_bbox();
			Rect roiBox(bbox.xmin(), bbox.ymin(), bbox.width(), bbox.height());
			result->Add(mask, detection->get_class_id(), mask.size(),roiBox, detection->get_confidence(), detection->get_label());

		}
		std::cout << "Post process iteration: " << iteration << " completed in " << sw.GetMs() << endl;
		this->_callbackArgs.TryWrite(result.release());
	};
}


HailoAsyncProcessor::HailoAsyncProcessor(std::unique_ptr<VDevice> &dev, std::shared_ptr<ConfiguredNetworkGroup> networkGroup) :
_dev(std::move(dev)), _callbackArgs(2, DiscardPolicy::Oldest,[](SegmentationResult *ptr) { delete ptr; }), _callback(nullptr), _context(nullptr)
{
	Expected<std::pair<std::vector<InputVStream>, std::vector<OutputVStream> > > vstreams_exp =
			VStreamsBuilder::create_vstreams(*networkGroup, QUANTIZED, FORMAT_TYPE);
	if (!vstreams_exp) throw HailoException(vstreams_exp.status());

	this->_vstreams = vstreams_exp.release();
	this->_input_vstream = &_vstreams.first[0];

	std::vector<OutputVStream>& output_vstreams = _vstreams.second;

	cout << "Input vstream at: " << _input_vstream << endl;

	hailo_status status = HAILO_UNINITIALIZED;

	std::string model_type = "";

	auto output_vstreams_size = output_vstreams.size();

	bool nms_on_hailo = false;
	std::string output_name = (std::string)output_vstreams[0].get_info().name;
	if (output_vstreams_size == 1 && (output_name.find("nms") != std::string::npos)) {
		nms_on_hailo = true;
		model_type = output_name.substr(0, output_name.find('/'));
	}

	_features.reserve(output_vstreams_size);

	for (size_t i = 0; i < output_vstreams_size; i++)
	{
		auto feature = this->CreateFeature(output_vstreams[i].get_info(), output_vstreams[i].get_frame_size());
		_features.emplace_back(feature);
	}

}
void HailoAsyncProcessor::StartAsync() {
	std::vector<OutputVStream>& output_vstreams = _vstreams.second;
	auto output_vstreams_size = output_vstreams.size();
	this->_isRunning = true;
	for (size_t i = 0; i < output_vstreams_size; i++) {
		//std::async(std::launch::async, &HailoAsyncProcessor::OnRead, this, i);
		//std::thread(&HailoAsyncProcessor::OnRead, this, i).detach();
		_threads.emplace_back(std::thread(&HailoAsyncProcessor::OnRead, this, i));
	}
	// Create the postprocessing thread
	//std::async(std::launch::async, &HailoAsyncProcessor::PostProcess, this);
	_threads.emplace_back(std::thread( &HailoAsyncProcessor::PostProcess, this));
	_threads.emplace_back(std::thread(&HailoAsyncProcessor::OnCallback, this));
	//std::thread( &HailoAsyncProcessor::PostProcess, this).detach();
	//std::thread(&HailoAsyncProcessor::OnCallback, this).detach();
}
void HailoAsyncProcessor::OnCallback() {

	while (_isRunning) {
		SegmentationResult* value;
		//unique_ptr<SegmentationResult> value;
		if (_callbackArgs.TryRead(value, 5s)) {
			if (_callback && value != nullptr) // nullptr is important because of Dispose.
				_callback(value, _context);
			else if(value != nullptr)
				delete value;
		}
		else { // this should only happen when time-out happens.
			std::this_thread::yield();  // Yield to allow other threads to run
		}
	}

}
void HailoAsyncProcessor::StartAsync(CallbackWithContext callback, void *context) {
	 _callback = callback;
	 _context = context;
	StartAsync();
}

float HailoAsyncProcessor::ConfidenceThreshold() {
	return 0;
}

void HailoAsyncProcessor::ConfidenceThreshold(float value) {

}

void HailoAsyncProcessor::Deallocate() {
	// should we delete the ptr?
	this->_dev.release();
}

unique_ptr<HailoProcessor> HailoProcessor::Load(const string& hefFile)
{
	auto vdevice_exp = VDevice::create();

	if (!vdevice_exp) 
		throw HailoException(vdevice_exp.status());


	auto vdevice = vdevice_exp.release();
	auto infer_model_exp = vdevice->create_infer_model(hefFile);
	if (!infer_model_exp) 
		throw HailoException(infer_model_exp.status());
	

	auto infer_model = infer_model_exp.release();

	infer_model->set_hw_latency_measurement_flags(HAILO_LATENCY_MEASURE);

	auto outputStream = infer_model->output();
	//outputStream->set_nms_score_threshold(0.5f);

	int nnWidth = infer_model->inputs()[0].shape().width;
	int nnHeight = infer_model->inputs()[0].shape().height;

	auto configured_infer_model_exp = infer_model->configure();
	if (!configured_infer_model_exp)
		throw HailoException(configured_infer_model_exp.status());

	auto configured_infer_model = std::make_shared<ConfiguredInferModel>(configured_infer_model_exp.release());
	auto bindings_exp = configured_infer_model->create_bindings();
	if (!bindings_exp)
		throw HailoException(bindings_exp.status());

	auto bindings = bindings_exp.release();

	// Input preparation
	const auto& input_name = infer_model->get_input_names()[0];
	size_t input_frame_size = infer_model->input(input_name)->get_frame_size();


	auto ret = new HailoProcessor(vdevice, infer_model, configured_infer_model, bindings, input_frame_size);
	return unique_ptr<HailoProcessor>(ret);
}

unique_ptr<SegmentationResult> HailoProcessor::ProcessFrame(const YuvFrame& frame, const Rect& roi, const Size& dSize)
{
	Size dstSize = dSize;
	const auto& input_name = this->_model->get_input_names()[0];
	size_t input_frame_size = _model->input(input_name)->get_frame_size();
	if (dstSize.width == 0 && dstSize.height == 0)
		dstSize.width = dstSize.height = sqrt(input_frame_size / 3);

	if (dstSize.width * dstSize.height * 3 != input_frame_size)
		throw HailoException("Wrong destination size.");

	auto dstMat = frame.ToMatRgb(roi, dstSize);

	
	auto iq = _model->input(input_name)->get_quant_infos()[0];
	if (iq.qp_scale != 1.0f)
		throw HailoException("Unexpected input quantization.");

	// We don't need this, it seems that the quantization is basically wrong.
	//auto normalizedDst = this->_floatPool.Rent(input_frame_size);
	//auto quantizedDst = this->_bytePool.Rent(input_frame_size);
	//ArrayOperations::ConvertToFloat(dstMat.data, normalizedDst.Data(), input_frame_size);
	//Quantization::quantize_input_buffer(normalizedDst.Data(), quantizedDst.Data(), input_frame_size, iq);

	auto status = _bindings.input(input_name)->set_buffer(MemoryView(dstMat.data, input_frame_size));

	if (status != HAILO_SUCCESS) throw HailoException(status);

	std::vector<OutTensor> outputNodes;
	for (const auto& output_name : _model->get_output_names()) {
		size_t output_size = _model->output(output_name)->get_frame_size();
		
		const auto& quant = _model->output(output_name)->get_quant_infos()[0];
		const auto& shape = _model->output(output_name)->shape();
		const auto& format = _model->output(output_name)->format();
		OutTensor& t = outputNodes.emplace_back(_allocator, output_name, quant, shape, format, output_size);

		status = _bindings.output(output_name)->set_buffer(MemoryView(t.data.get(), output_size));
		if (status != HAILO_SUCCESS) throw HailoException(status);
	}
	status = this->_configured_infer_model->wait_for_async_ready(1s);
	if (status != HAILO_SUCCESS) throw HailoException(status);

	/*status = _configured_infer_model->activate();
	if (status != HAILO_SUCCESS)
		throw HailoException(status);*/
	
	status = _configured_infer_model->run(_bindings, 5s);
	if (status != HAILO_SUCCESS)
		throw HailoException(status);
	/*auto job_exp = _configured_infer_model->run_async(_bindings);
	if (!job_exp) 
		throw HailoException(job_exp.status());

	auto job = job_exp.release();
	job.detach();*/

	/*status = job.wait(3s);
	if (status != HAILO_SUCCESS) throw HailoException(status);*/

	auto r = make_unique<SegmentationResult>();
	
	for (auto& tensor : outputNodes) {
		std::cout << "Tensor name:" << tensor.name;
		
		uint32_t featureSize = tensor.shape.width * tensor.shape.height;
		auto rental = _floatPool.Rent(featureSize);
		//auto floatBuffer = rental.Data();
		// Process segmentation masks
		int threshold = static_cast<int>(this->_confidenceThreshold * 255.0f);
		cout << "Threshold is: " << threshold << endl;
		for(int i = 0; i < tensor.shape.features; i++)
		{
			//auto ptr = tensor.GetFeature(i);

			auto dst = new uint8[featureSize];
			tensor.CopyTo(dst, i);
			//Quantization::dequantize_output_buffer(ptr, floatBuffer, featureSize, tensor.quant_info);
			/*ArrayOperations::ConvertToFloat(ptr, floatBuffer, featureSize);
			if(ArrayOperations::ContainsGreaterThan(floatBuffer, featureSize, this->_confidenceThreshold))
			{
				auto byteRental = _bytePool.Rent(featureSize);
				ArrayOperations::ConvertToUint8(floatBuffer, byteRental.Data(), featureSize);
				r->Add(byteRental, i, tensor.ShapeSize());
			}*/
			//ArrayOperations::NegUint8(ptr, featureSize);
			if (ArrayOperations::ContainsGreaterThan(dst, featureSize, threshold))
			{
				std::cout << "Not implemented" << endl;
				//r->Add(dst, i, tensor.ShapeSize());
			}
		}
		//r->AppendBuffer(tensor.data);
	}
	return r;

}

float HailoProcessor::ConfidenceThreshold() const
{
	return this->_confidenceThreshold;
}

void HailoProcessor::ConfidenceThreshold(float value)
{
	this->_confidenceThreshold = value;
}


cv::Size HailoProcessor::GetInputSize() const
{
	int nnWidth = this->_model->inputs()[0].shape().width;
	int nnHeight = this->_model->inputs()[0].shape().height;
	return Size(nnWidth, nnHeight);
}

void HailoProcessor::StartAsync(CallbackWithContext callback, void *context)
{

}


HailoProcessor::HailoProcessor(unique_ptr<VDevice>& dev, 
                               shared_ptr<InferModel>& infer_model,
                               shared_ptr<ConfiguredInferModel>& configured_infer_model,
                               const ConfiguredInferModel::Bindings& bindings, size_t input_frame_size) :
	_dev(std::move(dev)), _model(std::move(infer_model)), _configured_infer_model(std::move(configured_infer_model)), _bindings(bindings), _input_frame_size(input_frame_size)
{

};
