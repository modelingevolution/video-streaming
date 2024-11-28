#include <arm_neon.h>
#include <cstddef>

#include "Frame.h"
#pragma once

class ArrayOperations {
public:
    static void ConvertToUint8(const float* inputBuffer, uint8_t* outputBuffer, size_t count);
	static void ConvertToFloat(const uint8* inputBuffer, float* outputBuffer, size_t count);
	static bool ContainsGreaterThan(const float* buffer, size_t size, float threshold);
	static bool ContainsGreaterThan(const uint8* buffer, size_t size, uint8 threshold);
	static void NegUint8(uint8* buffer, size_t size);
};

