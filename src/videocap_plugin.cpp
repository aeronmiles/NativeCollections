#include <iostream>
#include <cstring>
#include <fcntl.h>
#include <errno.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <linux/videodev2.h>
#include <opencv2/opencv.hpp>
#include <opencv2/imgproc/imgproc.hpp>

#define CLEAR(x) memset(&(x), 0, sizeof(x))

struct Color32 {
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;
};

struct Buffer {
    void* start;
    size_t length;
};

int fd = -1;
Buffer* buffers = nullptr;
unsigned int n_buffers = 0;

extern "C" {
    void StartCapture(int width, int height, int fps);
    void GetNextFrame(Color32* pixelBuffer, int width, int height);
    void StopCapture();
}

int xioctl(int fh, unsigned long int request, void* arg) {
    int r;
    do {
        r = ioctl(fh, request, arg);
    } while (-1 == r && EINTR == errno);
    return r;
}

void StartCapture(int width, int height, int fps) {
    struct v4l2_format fmt;
    struct v4l2_requestbuffers req;
    struct v4l2_buffer buf;

    fd = open("/dev/video0", O_RDWR | O_NONBLOCK, 0);
    if (fd == -1) {
        perror("Opening video device");
        return;
    }

    CLEAR(fmt);
    fmt.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    fmt.fmt.pix.width = width;
    fmt.fmt.pix.height = height;
    fmt.fmt.pix.pixelformat = V4L2_PIX_FMT_YUYV;
    fmt.fmt.pix.field = V4L2_FIELD_INTERLACED;
    if (xioctl(fd, VIDIOC_S_FMT, &fmt) == -1) {
        perror("Setting Pixel Format");
        close(fd);
        fd = -1;
        return;
    }

    CLEAR(req);
    req.count = 4;
    req.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    req.memory = V4L2_MEMORY_MMAP;

    if (xioctl(fd, VIDIOC_REQBUFS, &req) == -1) {
        perror("Requesting Buffer");
        close(fd);
        fd = -1;
        return;
    }

    buffers = (Buffer*)calloc(req.count, sizeof(*buffers));
    if (!buffers) {
        perror("Out of memory");
        close(fd);
        fd = -1;
        return;
    }

    for (n_buffers = 0; n_buffers < req.count; ++n_buffers) {
        CLEAR(buf);
        buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        buf.memory = V4L2_MEMORY_MMAP;
        buf.index = n_buffers;

        if (xioctl(fd, VIDIOC_QUERYBUF, &buf) == -1) {
            perror("Querying Buffer");
            StopCapture();
            return;
        }

        buffers[n_buffers].length = buf.length;
        buffers[n_buffers].start = mmap(NULL, buf.length, PROT_READ | PROT_WRITE, MAP_SHARED, fd, buf.m.offset);
        if (buffers[n_buffers].start == MAP_FAILED) {
            perror("mmap");
            StopCapture();
            return;
        }
    }

    for (unsigned int i = 0; i < n_buffers; ++i) {
        CLEAR(buf);
        buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        buf.memory = V4L2_MEMORY_MMAP;
        buf.index = i;
        if (xioctl(fd, VIDIOC_QBUF, &buf) == -1) {
            perror("Queue Buffer");
            StopCapture();
            return;
        }
    }

    enum v4l2_buf_type type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    if (xioctl(fd, VIDIOC_STREAMON, &type) == -1) {
        perror("Start Capture");
        StopCapture();
        return;
    }
}

void YUYVToColor32(uint8_t* yuyv, Color32* rgba, int width, int height) {
    for (int i = 0; i < height; ++i) {
        for (int j = 0; j < width; j += 2) {
            int index = i * width + j;
            int y0 = yuyv[index * 2];
            int u = yuyv[index * 2 + 1];
            int y1 = yuyv[index * 2 + 2];
            int v = yuyv[index * 2 + 3];

            int c0 = y0 - 16;
            int c1 = y1 - 16;
            int d = u - 128;
            int e = v - 128;

            int r0 = (298 * c0 + 409 * e + 128) >> 8;
            int g0 = (298 * c0 - 100 * d - 208 * e + 128) >> 8;
            int b0 = (298 * c0 + 516 * d + 128) >> 8;

            int r1 = (298 * c1 + 409 * e + 128) >> 8;
            int g1 = (298 * c1 - 100 * d - 208 * e + 128) >> 8;
            int b1 = (298 * c1 + 516 * d + 128) >> 8;

            rgba[index].r = std::min(std::max(r0, 0), 255);
            rgba[index].g = std::min(std::max(g0, 0), 255);
            rgba[index].b = std::min(std::max(b0, 0), 255);
            rgba[index].a = 255;

            rgba[index + 1].r = std::min(std::max(r1, 0), 255);
            rgba[index + 1].g = std::min(std::max(g1, 0), 255);
            rgba[index + 1].b = std::min(std::max(b1, 0), 255);
            rgba[index + 1].a = 255;
        }
    }
}

void GetNextFrame(Color32* pixelBuffer, int width, int height) {
    if (fd == -1) {
        std::cerr << "Device not opened." << std::endl;
        return;
    }

    struct v4l2_buffer buf;
    CLEAR(buf);
    buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    buf.memory = V4L2_MEMORY_MMAP;

    fd_set fds;
    struct timeval tv;
    int r;

    FD_ZERO(&fds);
    FD_SET(fd, &fds);

    tv.tv_sec = 2;
    tv.tv_usec = 0;

    r = select(fd + 1, &fds, NULL, NULL, &tv);

    if (r == -1) {
        perror("Waiting for Frame");
        return;
    }

    if (xioctl(fd, VIDIOC_DQBUF, &buf) == -1) {
        perror("Retrieving Frame");
        return;
    }

    YUYVToColor32((uint8_t*)buffers[buf.index].start, pixelBuffer, width, height);

    if (xioctl(fd, VIDIOC_QBUF, &buf) == -1) {
        perror("Queue Buffer");
        return;
    }
}

void StopCapture() {
    if (fd == -1) {
        return;
    }

    enum v4l2_buf_type type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    if (xioctl(fd, VIDIOC_STREAMOFF, &type) == -1) {
        perror("Stop Capture");
    }

    for (unsigned int i = 0; i < n_buffers; ++i) {
        if (buffers[i].start && munmap(buffers[i].start, buffers[i].length) == -1) {
            perror("munmap");
        }
    }

    free(buffers);
    buffers = nullptr;
    n_buffers = 0;

    if (close(fd) == -1) {
        perror("close");
    }
    fd = -1;
}
