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


HailoException& HailoError::LastException() const
{
	return *this->_hailoException;
}

void HailoAsyncProcessor::Stop() {
	this->_isRunning = false;
	// Triggers
	_input_vstream->abort();
	//_readOpNotifier.EnqueueWork();
	 _callbackChannel.TryWrite(nullptr);
	for(auto & _thread : _threads)
	 	_thread.join();
	std::cout << "All threads stopped." << std::endl;
}

HailoProcessorStats & HailoAsyncProcessor::Stats() {
	return  this->_stats;
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
void HailoAsyncProcessor::OnWrite(const cv::Mat &org_frame, FrameContext *frameId) {
	if(_stats.readInterferenceProcessing.Behind() >= 2) {
		OnFrameDrop_OnWrite(frameId);
		return;
	}

	frameId->Total.Start();
	frameId->WriteWatch.Start();
	auto input_shape = _input_vstream->get_info().shape;
	int height = input_shape.height;
	int width = input_shape.width;

	auto dstSize = cv::Size(width, height);
	size_t frame_size = _input_vstream->get_frame_size();

	if(org_frame.size() != dstSize) {
		// we need to resize.
		Mat dst(dstSize, 3, CV_8UC1);
		cv::resize(org_frame, dst, dstSize);
		OnWrite(dst,frame_size,frameId);
	} else {
		OnWrite(org_frame, frame_size, frameId); // Writing height * width, 3 channels of uint8
	}
}
void HailoAsyncProcessor::OnWrite(const cv::Mat &org_frame,size_t frame_size, FrameContext *frameId) {
	std::lock_guard<std::mutex> lock(this->_writeMx);
	frameId->Iteration = this->_iteration++;
	if(!this->_writeChannel.TryWrite(frameId)) {
		this->OnFrameDrop(frameId);
		return;
	}
	_input_vstream->write(MemoryView(org_frame.data, frame_size));

	this->_stats.writeProcessing.FrameProcessed(frameId->WriteWatch.Stop(),frameId->Iteration);
	frameId->InterferenceAndReadWatch.Restart();
}

void HailoAsyncProcessor::Write(const YuvFrame &frame,const FrameIdentifier &frameId) {
	const Rect r(0,0,frame.Width(), frame.Height());
	Write(frame,r, frameId);
}

void HailoAsyncProcessor::Write(const YuvFrame &frame, const cv::Rect &roi, const FrameIdentifier &frameId) {
	auto mat = frame.ToMatBgr(roi);
	//PrintMatStat(mat);
	FrameContext* info = new FrameContext(frameId, roi);
	OnWrite(mat, info);
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
				//std::cout << "Read: " << frame << std::endl;
				FrameContext* v;

				if(_writeChannel.TryRead(v, 1s)) {
					auto rt = v->InterferenceAndReadWatch.Stop();
					_stats.readInterferenceProcessing.FrameProcessed(rt,v->Iteration);

					if(!_postProcessingChannel.TryWrite(v)) {
						_stats.postProcessing.FrameDropped(v->Iteration);
					}
				}
				else throw std::runtime_error("Cannot read write channel.");
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

	FrameContext *context;
	while(this->_postProcessingChannel.TryRead(context, 10s))
	{
		context->PostProcessingWatch.Start();
		auto& iteration = context->Iteration;
		auto result = make_unique<SegmentationResult>();

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


		for (size_t i = 0; i < filtered_masks.size(); ++i)
		{
			cv::Mat& mask = filtered_masks[i];
			auto &detection = detections[i];
			HailoBBox bbox = detection->get_bbox();
			Rect2f roiBox(bbox.xmin(), bbox.ymin(), bbox.width(), bbox.height());
			result->Add(mask, detection->get_class_id(), mask.size(),roiBox, detection->get_confidence(), detection->get_label());

		}
		auto t = context->PostProcessingWatch.Stop();
		this->_stats.postProcessing.FrameProcessed(t, context->Iteration);
		context->Result = result.release();
		if(!this->_callbackChannel.TryWrite(context))
			this->_stats.callbackProcessing.FrameDropped(context->Iteration);
	};
}
void HailoAsyncProcessor::OnFrameDrop(FrameContext * ptr) {
	if(ptr != nullptr) {
		if(ptr->Result != nullptr)
			delete ptr->Result;
		delete ptr;
	}
}
void HailoAsyncProcessor::OnFrameDrop_OnWrite(FrameContext * ptr) {
	_stats.writeProcessing.FrameDropped(ptr->Iteration);
	OnFrameDrop(ptr);
}
void HailoAsyncProcessor::OnFrameDrop_OnRead(FrameContext * ptr) {
	_stats.readInterferenceProcessing.FrameDropped(ptr->Iteration);
	OnFrameDrop(ptr);
}
void HailoAsyncProcessor::OnFrameDrop_OnPostProcess(FrameContext * ptr) {
	_stats.postProcessing.FrameDropped(ptr->Iteration);
	OnFrameDrop(ptr);
}
void HailoAsyncProcessor::OnFrameDrop_OnCallback(FrameContext * ptr) {
	_stats.callbackProcessing.FrameDropped(ptr->Iteration);
	OnFrameDrop(ptr);
}

using namespace boost::placeholders;
HailoAsyncProcessor::HailoAsyncProcessor(std::unique_ptr<VDevice> &dev, std::shared_ptr<ConfiguredNetworkGroup> networkGroup) :
_dev(std::move(dev)),
_callbackChannel(2, DiscardPolicy::Oldest),
_callback(nullptr),
_context(nullptr),
_postProcessingChannel(4, DiscardPolicy::Oldest),
_readChannel(2, DiscardPolicy::Oldest),
_writeChannel(2, DiscardPolicy::Oldest),
_isRunning(false),
_stats(1,1,1,1,4)
{
	_readChannel.connectDropped(boost::bind(&HailoAsyncProcessor::OnFrameDrop_OnRead, this, _1));
	_writeChannel.connectDropped(boost::bind(&HailoAsyncProcessor::OnFrameDrop_OnWrite, this, _1));
	_postProcessingChannel.connectDropped(boost::bind(&HailoAsyncProcessor::OnFrameDrop_OnPostProcess, this, _1));
	_callbackChannel.connectDropped(boost::bind(&HailoAsyncProcessor::OnFrameDrop_OnCallback, this, _1));


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
void HailoAsyncProcessor::StartAsync(unsigned int postProcessThreadCount)
{
	std::vector<OutputVStream>& output_vstreams = _vstreams.second;
	auto output_vstreams_size = output_vstreams.size();
	this->_isRunning = true;
	for (size_t i = 0; i < output_vstreams_size; i++) {
		//std::async(std::launch::async, &HailoAsyncProcessor::OnRead, this, i);
		//std::thread(&HailoAsyncProcessor::OnRead, this, i).detach();
		_threads.emplace_back(std::thread(&HailoAsyncProcessor::OnRead, this, i));
	}
	_stats.readInterferenceProcessing.SetThreadCount(output_vstreams_size);
	// Create the postprocessing thread
	//std::async(std::launch::async, &HailoAsyncProcessor::PostProcess, this);
	for(int i = 0; i < postProcessThreadCount; i++)
		_threads.emplace_back(std::thread( &HailoAsyncProcessor::PostProcess, this));
	_stats.postProcessing.SetThreadCount(postProcessThreadCount);

	for(int i = 0; i < 2; i++)
		_threads.emplace_back(std::thread(&HailoAsyncProcessor::OnCallback, this));
	_stats.callbackProcessing.SetThreadCount(2);

	//std::thread( &HailoAsyncProcessor::PostProcess, this).detach();
	//std::thread(&HailoAsyncProcessor::OnCallback, this).detach();
}
void HailoAsyncProcessor::OnCallback() {

	while (_isRunning) {
		FrameContext* value;
		//unique_ptr<SegmentationResult> value;
		if (_callbackChannel.TryRead(value, 5s)) {
			StopWatch sw = StopWatch::StartNew();
			if (_callback && value != nullptr) // nullptr is important because of Dispose.
			{
				_callback(value->Result, _context);
				_stats.callbackProcessing.FrameProcessed(sw.Stop(), value->Iteration);
				_stats.totalProcessing.FrameProcessed(value->Total.Stop(),value->Iteration);
			}
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
	unsigned int numCores = std::thread::hardware_concurrency();
	StartAsync(numCores);
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


