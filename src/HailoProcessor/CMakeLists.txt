cmake_minimum_required(VERSION 3.11)
project(HailoProcessor)

set(CMAKE_CXX_STANDARD 20)
message("C++ compiler: ${CMAKE_CXX_COMPILER}")
message("C compiler: ${CMAKE_C_COMPILER}")
#set(COMPILE_OPTIONS -Wextra -Wconversion -O3 -Wno-reorder -Wno-ignored-qualifiers -Wno-extra -Wno-unused-local-typedefs -Wno-conversion -Wno-parentheses -Wno-array-bounds)
set(COMPILE_OPTIONS

        -Wextra
        -Wconversion
        -O3 -mcpu=cortex-a76 -ftree-vectorize -funsafe-math-optimizations -ffp-contract=fast -funroll-loops -fomit-frame-pointer
        -Wno-reorder
        -Wno-ignored-qualifiers
        -Wno-extra
        -Wno-unused-local-typedefs
        -Wno-conversion
        -Wno-parentheses
        -Wno-array-bounds)

set(BASE_DIR /home/pi/src/Hailo-Application-Code-Examples/runtime/cpp/instance_segmentation/yolov8seg)

set(CMAKE_THREAD_LIBS_INIT "-lpthread")
SET(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -pthread")
set(CMAKE_HAVE_THREADS_LIBRARY 1)
set(CMAKE_USE_WIN32_THREADS_INIT 0)
set(CMAKE_USE_PTHREADS_INIT 1)
set(THREADS_PREFER_PTHREAD_FLAG ON)

find_package(Threads)
# Find the Boost library
find_package(Boost REQUIRED)

# Include Boost headers
if(Boost_FOUND)
    include_directories(${Boost_INCLUDE_DIRS})
    message(STATUS "Boost found: ${Boost_INCLUDE_DIRS}")
else()
    message(FATAL_ERROR "Boost not found!")
endif()

find_package(HailoRT REQUIRED)

find_package( OpenCV REQUIRED)
message(STATUS "Found OpenCV: " ${OpenCV_INCLUDE_DIRS})

file(GLOB SOURCES
    ./*.cpp
)

include(ExternalProject)

set(EXTERNAL_INSTALL_LOCATION ${CMAKE_BINARY_DIR}/external)

ExternalProject_Add(xtl-test
    GIT_REPOSITORY https://github.com/xtensor-stack/xtl
    CMAKE_ARGS -DCMAKE_INSTALL_PREFIX=${EXTERNAL_INSTALL_LOCATION}
)

ExternalProject_Add(xtensor-test
    GIT_REPOSITORY https://github.com/xtensor-stack/xtensor
    CMAKE_ARGS -DCMAKE_INSTALL_PREFIX=${EXTERNAL_INSTALL_LOCATION} -Dxtl_DIR=${BASE_DIR}/build/x86_64/external/share/cmake/xtl/
)

ExternalProject_Add(xtensor-blas-test
    GIT_REPOSITORY https://github.com/xtensor-stack/xtensor-blas
    CMAKE_ARGS -DCMAKE_INSTALL_PREFIX=${EXTERNAL_INSTALL_LOCATION} -Dxtl_DIR=${BASE_DIR}/build/x86_64/external/share/cmake/xtl/
)


include_directories(${EXTERNAL_INSTALL_LOCATION}/include)
message("include api")
#add_subdirectory(/home/pi/src/Hailo-Application-Code-Examples/runtime/cpp/instance_segmentation/yolov8seg/api)
link_directories(${EXTERNAL_INSTALL_LOCATION}/lib)

link_libraries(stdc++fs)

add_executable(${PROJECT_NAME} ${SOURCES})

add_dependencies(${PROJECT_NAME} xtl-test xtensor-test xtensor-blas-test)
include_directories(${OpenCV_INCLUDE_DIRS})
include_directories(${ONNXRUNTIME_INCLUDE_DIR})
target_compile_options(${PROJECT_NAME} PRIVATE ${COMPILE_OPTIONS} -fconcepts)
target_link_libraries(${PROJECT_NAME} HailoRT::libhailort ${CMAKE_THREAD_LIBS_INIT} ${OpenCV_LIBS})

add_library(x${PROJECT_NAME} SHARED ${SOURCES})
add_dependencies(x${PROJECT_NAME} xtl-test xtensor-test xtensor-blas-test)
target_compile_options(x${PROJECT_NAME} PRIVATE ${COMPILE_OPTIONS} -fconcepts)
target_link_libraries(x${PROJECT_NAME} HailoRT::libhailort ${CMAKE_THREAD_LIBS_INIT} ${OpenCV_LIBS})
