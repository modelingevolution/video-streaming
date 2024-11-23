#include "ArrayOperations.h"
#include <cmath>
#include <algorithm>
#include <arm_neon.h>

void ArrayOperations::ConvertToUint8(const float* inputBuffer, uint8_t* outputBuffer, size_t featureSize)
{
    size_t i = 0;
    float32x4_t factor = vdupq_n_f32(255.0f);   // Load 255.0 into all 4 elements
    float32x4_t zero = vdupq_n_f32(0.0f);
    float32x4_t maxVal = vdupq_n_f32(255.0f);

    for (; i + 4 <= featureSize; i += 4) {
        float32x4_t input = vld1q_f32(&inputBuffer[i]);     // Load 4 floats
        float32x4_t scaled = vmulq_f32(input, factor);      // Multiply by 255
        scaled = vminq_f32(vmaxq_f32(scaled, zero), maxVal); // Clamp between 0 and 255

        uint32x4_t intVals = vcvtq_u32_f32(scaled);         // Convert to uint32_t
        uint16x4_t packedVals16 = vmovn_u32(intVals);        // Narrow to uint16_t
        uint8x8_t packedVals8 = vmovn_u16(vcombine_u16(packedVals16, vdup_n_u16(0))); // Narrow to uint8_t

        vst1_lane_u32((uint32_t*)&outputBuffer[i], vreinterpret_u32_u8(packedVals8), 0);  // Store 4 uint8_t
    }

    // Process remaining elements
    for (; i < featureSize; ++i) {
        outputBuffer[i] = static_cast<uint8_t>(std::round(std::clamp(inputBuffer[i] * 255.0f, 0.0f, 255.0f)));
    }
}

bool ArrayOperations::ContainsGreaterThan(const float* buffer, size_t size, float threshold)
{
    size_t i = 0;
    float32x4_t threshVec = vdupq_n_f32(threshold);  // Set threshold vector

    // Process 4 elements at a time
    for (; i + 4 <= size; i += 4) {
        float32x4_t data = vld1q_f32(&buffer[i]);    // Load 4 floats from buffer
        uint32x4_t cmp = vcgtq_f32(data, threshVec); // Compare each element to threshold

        // Combine the results of the comparison
        uint32x2_t cmpHigh = vget_high_u32(cmp);
        uint32x2_t cmpLow = vget_low_u32(cmp);

        // Perform bitwise OR on each pair of 2 elements
        uint32x2_t combined = vorr_u32(cmpLow, cmpHigh);
        if (vget_lane_u32(combined, 0) || vget_lane_u32(combined, 1)) {
            return true;  // If any element exceeds the threshold, return true
        }
    }

    // Process remaining elements
    for (; i < size; ++i) {
        if (buffer[i] > threshold) {
            return true;
        }
    }

    return false;
}


