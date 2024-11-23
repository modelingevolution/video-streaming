#include <gtest/gtest.h>
#include "ArrayOperations.h"
#include <vector>
#include <cmath>

// Test suite for ArrayOperations class
class ArrayOperationsTest : public ::testing::Test {
protected:
    // No need for instance initialization as we are testing static methods
    void SetUp() override {
        // Optionally initialize some things before each test if necessary
    }

    void TearDown() override {
        // Optionally clean up after tests
    }
};

TEST_F(ArrayOperationsTest, ConvertToUint8_EmptyArray) {
    std::vector<float> inputBuffer;
    std::vector<uint8_t> outputBuffer(inputBuffer.size());
    ArrayOperations::ConvertToUint8(inputBuffer.data(), outputBuffer.data(), inputBuffer.size());
    EXPECT_TRUE(outputBuffer.empty());
}

TEST_F(ArrayOperationsTest, ConvertToUint8_SingleElement) {
    std::vector<float> inputBuffer = { 0.5f };
    std::vector<uint8_t> outputBuffer(inputBuffer.size());
    ArrayOperations::ConvertToUint8(inputBuffer.data(), outputBuffer.data(), inputBuffer.size());
    EXPECT_EQ(outputBuffer[0], static_cast<uint8_t>(std::round(0.5f * 255.0f)));
}

TEST_F(ArrayOperationsTest, ConvertToUint8_MultipleElements) {
    std::vector<float> inputBuffer = { 0.0f, 0.5f, 1.0f, 0.5f, 0.75f };
    std::vector<uint8_t> outputBuffer(inputBuffer.size());
    ArrayOperations::ConvertToUint8(inputBuffer.data(), outputBuffer.data(), inputBuffer.size());
    EXPECT_EQ(outputBuffer[0], 0);
    EXPECT_EQ(outputBuffer[1], 127);
    EXPECT_EQ(outputBuffer[2], 255);
    EXPECT_EQ(outputBuffer[3], 127);
    EXPECT_EQ(outputBuffer[4], 191);
}

// Test for ContainsGreaterThan
TEST_F(ArrayOperationsTest, ContainsGreaterThan_EmptyArray) {
    std::vector<float> buffer;
    EXPECT_FALSE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
TEST_F(ArrayOperationsTest, ContainsGreaterThan_NoElementGreaterThanThreshold) {
    std::vector<float> buffer = { 0.1f, 0.2f, 0.3f };
    EXPECT_FALSE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
TEST_F(ArrayOperationsTest, ContainsGreaterThan_SomeElementsGreaterThanThreshold) {
    std::vector<float> buffer = { 0.1f, 0.6f, 0.3f, 0.2f, 0.3f };
    EXPECT_TRUE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
TEST_F(ArrayOperationsTest, ContainsGreaterThan_AllElementsGreaterThanThreshold) {
    std::vector<float> buffer = { 0.6f, 0.7f, 0.8f };
    EXPECT_TRUE(ArrayOperations::ContainsGreaterThan(buffer.data(), buffer.size(), 0.5f));
}
