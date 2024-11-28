#pragma once

#include <memory>
#include <string>
#include <hailo/hailort.h>
#include <hailo/hailort_common.hpp>
#include <hailo/vdevice.hpp>
#include <hailo/infer_model.hpp>
#include <hailo/quantization.hpp>

#include "Allocator.h"
typedef unsigned int uint;


class OutTensor {
public:
	shared_ptr<unsigned char> data;
	std::string            name;
	hailo_quant_info_t     quant_info;
	hailo_3d_image_shape_t shape;
	hailo_format_t         format;
	shared_ptr<PageAlignedMemoryPool>   allocator;
	const size_t		   output_size;
	OutTensor(shared_ptr<PageAlignedMemoryPool> alloc, const std::string& name, const hailo_quant_info_t& quant_info,
		const hailo_3d_image_shape_t& shape, hailo_format_t format, const size_t dataSize)
		: name(name), quant_info(quant_info), shape(shape), format(format), output_size(dataSize),
		allocator(alloc), data(alloc->Rent<uint8_t>(dataSize))
	{
		
	}
	~OutTensor()
	{
	}
	uint8* GetFeature(int channel) {
		uint32_t offset = channel * shape.width * shape.height;
		return data.get() + offset;
	}
	void CopyTo(uint8* dst, uint channel)
	{
		for(int x = 0; x < shape.width; x++)
			for(int y = 0; y < shape.height; y++)
			{
				auto offset = y * shape.width + x;
				dst[offset] = Get(y, x, channel);
			}
	}
	uint8_t Get(uint row, uint col, uint channel)
	{
		uint width = shape.width;
		uint features = shape.features;
		int pos = features * (width * row + col) + channel;
		return data.get()[pos];
	}
	float GetDequantized(uint row, uint col, uint channel)
	{
		return FixScale(Get(row, col, channel));
	}
	float FixScale(uint8_t num)
	{
		return (float(num) - quant_info.qp_zp) * quant_info.qp_scale;
	}

	Size ShapeSize() const { return Size(this->shape.width, this->shape.height); }

	static bool SortFunction(const OutTensor& l, const OutTensor& r) {
		return l.shape.width < r.shape.width;
	}
};
