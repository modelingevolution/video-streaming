# CMakeList.txt : CMake project for HailoProcessor, include source and define
# project specific logic here.
#

# Add source to this project's executable.
add_library (HailoProcessor SHARED "LibHailoProcessor.cpp" "LibHailoProcessor.h" "HailoProcessor.h" "HailoProcessor.cpp" "Frame.h" "Frame.cpp" "Export.h" "Export.cpp" "OutTensor.h" "OutTensor.cpp" "Allocator.h" "ArrayOperations.h" "ArrayPool.h" "ArrayOperations.cpp")

if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET HailoProcessor PROPERTY CXX_STANDARD 20)
endif()
 
# Link OpenCV libraries to your target
target_link_libraries(HailoProcessor ${OpenCV_LIBS} HailoRT::libhailort)

target_include_directories(HailoProcessor PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

set_target_properties(HailoProcessor PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)