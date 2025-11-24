#ifndef FRAME_IDENTIFIER_H
#define FRAME_IDENTIFIER_H

#include <cstdint>
#include <string>
#include <utility> // for std::pair
#pragma pack(push, 1)
struct alignas(1) FrameIdentifier {
    const uint64_t FrameId;
    const uint32_t CameraId;

    // Default constructor
    FrameIdentifier();

    // Parameterized constructor
    FrameIdentifier(uint32_t cameraId, uint64_t frameId);

    // Copy constructor
    FrameIdentifier(const FrameIdentifier& other) = default;

    // Move constructor
    FrameIdentifier(FrameIdentifier&& other) noexcept = default;

    // Copy assignment operator
    FrameIdentifier& operator=(const FrameIdentifier& other) = default;

    // Move assignment operator
    FrameIdentifier& operator=(FrameIdentifier&& other) noexcept = default;

    // Equality operator
    bool operator==(const FrameIdentifier& other) const;

    // Inequality operator
    bool operator!=(const FrameIdentifier& other) const;

    // Utility function for creating a pair
    std::pair<uint32_t, uint64_t> toPair() const;

    // Utility function for string representation
    std::string toString() const;
};
#pragma pack(pop)

// Hash function for use with unordered containers
namespace std {
    template<> struct hash<FrameIdentifier> {
        std::size_t operator()(const FrameIdentifier& k) const;
    };
}

#endif // FRAME_IDENTIFIER_H