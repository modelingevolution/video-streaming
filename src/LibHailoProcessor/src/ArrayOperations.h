#include <arm_neon.h>
#include <cstddef>

#include "Frame.h"
#pragma once

class ArrayOperations {
public:
    static void ConvertToUint8(const float* inputBuffer, uint8_t* outputBuffer, size_t featureSize);
	static bool ContainsGreaterThan(const float* buffer, size_t size, float threshold);
};

