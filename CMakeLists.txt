cmake_minimum_required(VERSION 3.10)

project(VideoCapPlugin)

set(OpenCV_DIR /usr/lib/x86_64-linux-gnu/cmake/opencv4)

# Find OpenCV
find_package(OpenCV REQUIRED)
include_directories(${OpenCV_INCLUDE_DIRS})

# Add the native plugin source
add_library(VideoCapPlugin SHARED src/videocap_plugin.cpp)

# Link OpenCV libraries
target_link_libraries(VideoCapPlugin ${OpenCV_LIBS})