#pragma once

#include <memory>
#include <string>
#include <hailo/hailort.h>
#include <hailo/hailort_common.hpp>
#include <hailo/vdevice.hpp>
#include <hailo/infer_model.hpp>
#include <hailo/quantization.hpp>
typedef unsigned int uint;


class OutTensor {
public:
	uint8_t* data;
	std::string            name;
	hailo_quant_info_t     quant_info;
	hailo_3d_image_shape_t shape;
	hailo_format_t         format;
	const size_t		   output_size;
	OutTensor(uint8_t* data, const std::string& name, const hailo_quant_info_t& quant_info,
		const hailo_3d_image_shape_t& shape, hailo_format_t format, const size_t dataSize)
		: data(data), name(name), quant_info(quant_info), shape(shape), format(format), output_size(dataSize) {
	}
	uint8* GetFeature(int channel) {
		uint32_t offset = channel * shape.width * shape.height;
		return data + offset;
	}
	uint8_t get(uint row, uint col, uint channel)
	{
		
		uint width = shape.width;
		uint features = shape.features;
		int pos = (width * features) * row + features * col + channel;
		return data[pos];
	}
	float get_dequantized(uint row, uint col, uint channel)
	{
		return fix_scale(get(row, col, channel));
	}
	float fix_scale(uint8_t num)
	{
		return (float(num) - quant_info.qp_zp) * quant_info.qp_scale;
	}
	static bool SortFunction(const OutTensor& l, const OutTensor& r) {
		return l.shape.width < r.shape.width;
	}
};
