#include "HailoProcessor.h"
#include "ArrayOperations.h"

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
}

bool HailoError::IsOk()
{
	return !this->_isSet;
}

HailoException& HailoError::LastException() const
{
	return *this->_hailoException;
}

HailoProcessor* HailoProcessor::Load(const string &hefFile)
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


	return new HailoProcessor(vdevice, infer_model, configured_infer_model, bindings, input_frame_size);
}

AnnotationResult* HailoProcessor::ProcessFrame(const YuvFrame& frame, const Rect& roi, const Size& dSize)
{
	Size dstSize = dSize;
	const auto& input_name = this->_model->get_input_names()[0];
	size_t input_frame_size = _model->input(input_name)->get_frame_size();
	if (dstSize.width == 0 && dstSize.height == 0)
		dstSize.width = dstSize.height = sqrt(input_frame_size / 3);

	if (dstSize.width * dstSize.height * 3 != input_frame_size)
		throw HailoException("Wrong destination size.");


	std::vector<OutTensor> outputNodes;
	for (const auto& output_name : _model->get_output_names()) {
		size_t output_size = _model->output(output_name)->get_frame_size();
		auto output_buffer = static_cast<uint8_t*>(_allocator.Alloc(output_size));

		if (!output_buffer)
			throw HailoException("Cannot allocate buffer.");

		auto status = _bindings.output(output_name)->set_buffer(MemoryView(output_buffer, output_size));
		if (status != HAILO_SUCCESS)
			throw HailoException(status); //TODO: memory leak, because of allocated buffer.

		const auto& quant = _model->output(output_name)->get_quant_infos()[0];
		const auto& shape = _model->output(output_name)->shape();
		const auto& format = _model->output(output_name)->format();
		outputNodes.emplace_back(output_buffer, output_name, quant, shape, format, output_size);
	}
	auto status = this->_configured_infer_model->wait_for_async_ready(1s);
	if (status != HAILO_SUCCESS)
		throw HailoException(status);

	auto job_exp = _configured_infer_model->run_async(_bindings);
	if (!job_exp) throw HailoException("Cannot run async interference.");

	auto job = job_exp.release();
	job.detach();

	status = job.wait(3s);
	if (status != HAILO_SUCCESS) throw HailoException(status);

	AnnotationResult* r = new AnnotationResult();
	
	for (auto& tensor : outputNodes) {
		std::cout << "Tensor name:" << tensor.name;

		uint32_t featureSize = tensor.shape.width * tensor.shape.height;
		auto rental = _floatPool.Rent(featureSize);
		auto floatBuffer = rental.Data();
		// Process segmentation masks
		for(int i = 0; i < tensor.shape.features; i++)
		{
			auto ptr = tensor.GetFeature(i);
			Quantization::dequantize_output_buffer(ptr, floatBuffer, featureSize, tensor.quant_info);
			if(ArrayOperations::ContainsGreaterThan(floatBuffer, featureSize, this->_confidenceThreshold))
			{
				auto byteRental = _bytePool.Rent(featureSize);
				ArrayOperations::ConvertToUint8(floatBuffer, byteRental.Data(), featureSize);
				r->Add(byteRental, i);
			}
		}
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



HailoProcessor::HailoProcessor(unique_ptr<VDevice>& dev, shared_ptr<InferModel>& infer_model, shared_ptr<ConfiguredInferModel>& configured_infer_model, ConfiguredInferModel::Bindings& bindings, size_t input_frame_size) :
	_dev(std::move(dev)), _model(std::move(infer_model)), _configured_infer_model(std::move(configured_infer_model)), _bindings(bindings), _input_frame_size(input_frame_size)
{

};