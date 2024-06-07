cmake_minimum_required(VERSION 3.10)

project(NativeCollections)

set(OpenCV_DIR /usr/lib/x86_64-linux-gnu/cmake/opencv4)

# Find OpenCV
find_package(OpenCV REQUIRED)
include_directories(${OpenCV_INCLUDE_DIRS})

# Include headers
include_directories(include)

# Add the native plugin source
add_library(NativeCollections SHARED src/videocap_plugin.cpp src/execute_cmd.cpp)

# Link OpenCV libraries
target_link_libraries(NativeCollections ${OpenCV_LIBS})
