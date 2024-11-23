#include <gtest/gtest.h>
#include "ArrayOperations.h"
#include <vector>
#include <cmath>

TEST(ArrayOperationsTest, ConvertToUint8_EmptyArray) {
    std::vector<float> inputBuffer;
    std::vector<uint8_t> outputBuffer(inputBuffer.size());
    ArrayOperations::ConvertToUint8(inputBuffer.data(), outputBuffer.data(), inputBuffer.size());
    EXPECT_TRUE(outputBuffer.empty());
}

TEST(ArrayOperationsTest, ConvertToUint8_SingleElement) {
    std::vector<float> inputBuffer = { 0.5f };
    std::vector<uint8_t> outputBuffer(inputBuffer.size());
    ArrayOperations::ConvertToUint8(inputBuffer.data(), outputBuffer.data(), inputBuffer.size());
    EXPECT_EQ(outputBuffer[0], static_cast<uint8_t>(std::round(0.5f * 255.0f)));
}

TEST(ArrayOperationsTest, ConvertToUint8_MultipleElements) {
    std::vector<float> inputBuffer = { 0.0f, 0.5f, 1.0f };
    std::vector<uint8_t> outputBuffer(inputBuffer.size());
    ArrayOperations::ConvertToUint8(inputBuffer.data(), outputBuffer.data(), inputBuffer.size());
    EXPECT_EQ(outputBuffer[0], static_cast<uint8_t>(std::round(0.0f * 255.0f)));
    EXPECT_EQ(outputBuffer[1], static_cast<uint8_t>(std::round(0.5f * 255.0f)));
    EXPECT_EQ(outputBuffer[2], static_cast<uint8_t>(std::round(1.0f * 255.0f)));
}
// Test for ContainsGreaterThan
TEST(ArrayOperationsTest, ContainsGreaterThan_EmptyArray) {
    std::vector<float> buffer;
    EXPECT_FALSE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
TEST(ArrayOperationsTest, ContainsGreaterThan_NoElementGreaterThanThreshold) {
    std::vector<float> buffer = { 0.1f, 0.2f, 0.3f };
    EXPECT_FALSE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
TEST(ArrayOperationsTest, ContainsGreaterThan_SomeElementsGreaterThanThreshold) {
    std::vector<float> buffer = { 0.1f, 0.6f, 0.3f };
    EXPECT_TRUE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
TEST(ArrayOperationsTest, ContainsGreaterThan_AllElementsGreaterThanThreshold) {
    std::vector<float> buffer = { 0.6f, 0.7f, 0.8f };
    EXPECT_TRUE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
