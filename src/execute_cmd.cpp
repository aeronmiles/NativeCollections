#include <stdio.h>
#include <execute_cmd.h>
#include <stdlib.h>
#include <string.h>

extern "C" {
    const char* ExecuteCommand(const char* command) {
        static char buffer[4096];
        memset(buffer, 0, sizeof(buffer));

        FILE* pipe = popen(command, "r");
        if (!pipe) {
            return "popen failed!";
        }

        char* ptr = buffer;
        size_t len = sizeof(buffer);
        while (fgets(ptr, len, pipe) != NULL) {
            ptr += strlen(ptr);
            len -= strlen(ptr);
            if (len <= 0) break;
        }

        pclose(pipe);
        return buffer;
    }
}
