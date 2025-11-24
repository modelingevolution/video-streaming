#include "FrameIdentifier.h"
#include <string>
#include <sstream>
#include <functional>

FrameIdentifier::FrameIdentifier() : CameraId(0), FrameId(0) {}

FrameIdentifier::FrameIdentifier(uint32_t cameraId, uint64_t frameId)
    : CameraId(cameraId), FrameId(frameId) {}

bool FrameIdentifier::operator==(const FrameIdentifier& other) const {
    return CameraId == other.CameraId && FrameId == other.FrameId;
}

bool FrameIdentifier::operator!=(const FrameIdentifier& other) const {
    return !( *this== other);
}

std::pair<uint32_t, uint64_t> FrameIdentifier::toPair() const {
    return std::make_pair(CameraId, FrameId);
}

std::string FrameIdentifier::toString() const {
    std::ostringstream ss;
    ss << "Camera: " << static_cast<int>(CameraId) << ", Frame: " << FrameId;
    return ss.str();
}



namespace std {
    std::size_t hash<FrameIdentifier>::operator()(const FrameIdentifier& k) const {
        // A simple hash combining the two fields. Note: This might not be the best hash function for all scenarios.
        std::size_t hash = std::hash<uint8_t>()(k.CameraId);
        hash ^= std::hash<uint64_t>()(k.FrameId) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
        return hash;
    }
}