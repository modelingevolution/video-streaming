#include "HailoProcessor2.h"

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