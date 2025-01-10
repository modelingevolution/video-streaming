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

 void ArrayOperations::ConvertToFloat(const uint8_t* inputBuffer, float* outputBuffer, size_t count) {
    const float scale = 1.0f / 255.0f;  // Scaling factor for normalization
    const size_t vectorSize = 16;       // Each uint8x16_t processes 16 bytes at a time

    size_t i = 0;

    // Vectorized loop: process 16 elements at a time
    for (; i + vectorSize <= count; i += vectorSize) {
        // Load 16 uint8_t values into a NEON register
        uint8x16_t uint8Values = vld1q_u8(inputBuffer + i);

        // Convert uint8_t to float32_t (expands each byte to a 32-bit float)
        uint16x8_t uint16Low = vmovl_u8(vget_low_u8(uint8Values));  // Lower 8 values
        uint16x8_t uint16High = vmovl_u8(vget_high_u8(uint8Values)); // Higher 8 values

        float32x4_t floatValsLowLow = vcvtq_f32_u32(vmovl_u16(vget_low_u16(uint16Low)));
        float32x4_t floatValsLowHigh = vcvtq_f32_u32(vmovl_u16(vget_high_u16(uint16Low)));
        float32x4_t floatValsHighLow = vcvtq_f32_u32(vmovl_u16(vget_low_u16(uint16High)));
        float32x4_t floatValsHighHigh = vcvtq_f32_u32(vmovl_u16(vget_high_u16(uint16High)));

        // Multiply each float32x4_t by the scaling factor (1/255)
        float32x4_t scaleVec = vdupq_n_f32(scale);
        floatValsLowLow = vmulq_f32(floatValsLowLow, scaleVec);
        floatValsLowHigh = vmulq_f32(floatValsLowHigh, scaleVec);
        floatValsHighLow = vmulq_f32(floatValsHighLow, scaleVec);
        floatValsHighHigh = vmulq_f32(floatValsHighHigh, scaleVec);

        // Store the results back into the output buffer
        vst1q_f32(outputBuffer + i, floatValsLowLow);
        vst1q_f32(outputBuffer + i + 4, floatValsLowHigh);
        vst1q_f32(outputBuffer + i + 8, floatValsHighLow);
        vst1q_f32(outputBuffer + i + 12, floatValsHighHigh);
    }

    // Process remaining elements (less than 16)
    for (; i < count; ++i) {
        outputBuffer[i] = static_cast<float>(inputBuffer[i]) * scale;
    }
}

bool ArrayOperations::ContainsGreaterThan(const float* buffer, size_t size, float threshold)
{
    size_t i = 0;
    //float32x4_t threshVec = vdupq_n_f32(threshold);  // Set threshold vector

    //// Process 4 elements at a time
    //for (; i + 4 <= size; i += 4) {
    //    float32x4_t data = vld1q_f32(&buffer[i]);    // Load 4 floats from buffer
    //    uint32x4_t cmp = vcgtq_f32(data, threshVec); // Compare each element to threshold

    //    // Combine the results of the comparison
    //    uint32x2_t cmpHigh = vget_high_u32(cmp);
    //    uint32x2_t cmpLow = vget_low_u32(cmp);

    //    // Perform bitwise OR on each pair of 2 elements
    //    uint32x2_t combined = vorr_u32(cmpLow, cmpHigh);
    //    if (vget_lane_u32(combined, 0) || vget_lane_u32(combined, 1)) {
    //        return true;  // If any element exceeds the threshold, return true
    //    }
    //}

    // Process remaining elements
    for (; i < size; ++i) {
        if (buffer[i] > threshold) {
            return true;
        }
    }

    return false;
}

bool ArrayOperations::ContainsGreaterThan(const uint8* buffer, size_t size, uint8 threshold)
{
    const size_t vectorSize = 16;  // 128 bits / 8 bits per uint8_t = 16 elements
    size_t i = 0;

    // Set threshold vector for comparison
    uint8x16_t thresholdVec = vdupq_n_u8(threshold);

    // Vectorized loop
    for (; i + vectorSize <= size; i += vectorSize) {
        // Load 16 uint8_t values into a NEON register
        uint8x16_t dataVec = vld1q_u8(buffer + i);

        // Compare each element with the threshold
        uint8x16_t cmpResult = vcgtq_u8(dataVec, thresholdVec);

        // Check if any element is greater than the threshold
        if (vmaxvq_u8(cmpResult) != 0) {
            return true;
        }
    }

    // Process remaining elements (less than 16)
    for (; i < size; ++i) {
        if (buffer[i] > threshold) {
            return true;
        }
    }

    return false;
}

void ArrayOperations::NegUint8(uint8* buffer, size_t size)
{

    const size_t vectorSize = 16;  // 128 bits / 8 bits per uint8_t = 16 elements
    size_t i = 0;

    // Create a vector with all elements set to 255
    uint8x16_t maxVec = vdupq_n_u8(255);

    // Vectorized loop: process 16 bytes at a time
    for (; i + vectorSize <= size; i += vectorSize) {
        // Load 16 uint8_t values into a NEON register
        uint8x16_t dataVec = vld1q_u8(buffer + i);

        // Perform subtraction: result = 255 - dataVec
        uint8x16_t resultVec = vsubq_u8(maxVec, dataVec);

        // Store the result back to the buffer
        vst1q_u8(buffer + i, resultVec);
    }

    // Process remaining elements (less than 16)
    for (; i < size; ++i) {
        buffer[i] = 255 - buffer[i];
    }

}


