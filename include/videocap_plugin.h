#ifndef NATIVE_COLLECTIONS_PLUGIN_H
#define NATIVE_COLLECTIONS_PLUGIN_H

#include <stdint.h>
#include <linux/videodev2.h>

// Macro to clear a structure
#define CLEAR(x) memset(&(x), 0, sizeof(x))

// Structure to represent a color in RGBA format
struct Color32 {
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;
};

// Structure to represent a buffer
struct Buffer {
    void* start;
    size_t length;
};

// Global variables for file descriptor and buffers
extern int fd;
extern Buffer* buffers;
extern unsigned int n_buffers;

// Function prototypes for the video capture plugin
extern "C" {
    void StartCapture(int width, int height, int fps);
    void GetNextFrame(Color32* pixelBuffer, int width, int height);
    void StopCapture();
}

// Utility function for ioctl calls
int xioctl(int fh, unsigned long int request, void* arg);

// Utility function to convert YUYV to RGBA
void YUYVToColor32(uint8_t* yuyv, Color32* rgba, int width, int height);

#endif // NATIVE_COLLECTIONS_PLUGIN_H
