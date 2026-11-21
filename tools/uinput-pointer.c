/*
 * Root-only test helper for the KDE window-shell gate.
 *
 * KWin ignores XTest pointer motion from XWayland, so xdotool cannot place the
 * real cursor for the focus hand-off checks. This helper creates one virtual
 * absolute pointer through /dev/uinput and accepts a deliberately small
 * command set over a private FIFO:
 *
 *   move X Y
 *   move-click X Y
 *   click
 *   sleep MS
 *   quit
 *
 * Build:
 *   gcc -O2 -Wall -Wextra -o /tmp/opencode/aster-uinput-pointer tools/uinput-pointer.c
 *
 * It must run as root because /dev/uinput is root-only on the test host. The
 * caller owns the FIFO and restricts it to mode 0600; this process never
 * executes shell commands or reads application data.
 */
#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <linux/uinput.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <time.h>
#include <unistd.h>

static int device_fd = -1;

static void emit(int fd, int type, int code, int value)
{
    struct input_event ev;
    memset(&ev, 0, sizeof(ev));
    ev.type = type;
    ev.code = code;
    ev.value = value;
    if (write(fd, &ev, sizeof(ev)) != (ssize_t)sizeof(ev)) {
        perror("write input_event");
        exit(1);
    }
}

static void sync_events(int fd)
{
    emit(fd, EV_SYN, SYN_REPORT, 0);
}

static void sleep_ms(int ms)
{
    struct timespec ts;
    ts.tv_sec = ms / 1000;
    ts.tv_nsec = (long)(ms % 1000) * 1000000L;
    while (nanosleep(&ts, &ts) < 0 && errno == EINTR) {
    }
}

static void set_abs(int fd, int code, int minimum, int maximum)
{
    struct uinput_abs_setup setup;
    memset(&setup, 0, sizeof(setup));
    setup.code = code;
    setup.absinfo.minimum = minimum;
    setup.absinfo.maximum = maximum;
    if (ioctl(fd, UI_ABS_SETUP, &setup) < 0) {
        perror("UI_ABS_SETUP");
        exit(1);
    }
}

static void destroy_device(void)
{
    if (device_fd >= 0) {
        ioctl(device_fd, UI_DEV_DESTROY);
        close(device_fd);
        device_fd = -1;
    }
}

static void on_alarm(int signal_number)
{
    (void)signal_number;
    destroy_device();
    _exit(0);
}

static int parse_int(const char *text, int fallback)
{
    char *end = NULL;
    long value = strtol(text, &end, 10);
    if (end == text || *end != '\0' || value < 0 || value > 1000000) {
        return fallback;
    }
    return (int)value;
}

static void click_left(void)
{
    emit(device_fd, EV_KEY, BTN_LEFT, 1);
    sync_events(device_fd);
    sleep_ms(80);
    emit(device_fd, EV_KEY, BTN_LEFT, 0);
    sync_events(device_fd);
}

int main(int argc, char **argv)
{
    const char *fifo = NULL;
    const char *ready = NULL;
    int max_x = 7999;
    int max_y = 2879;
    int timeout_seconds = 900;

    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--fifo") == 0 && i + 1 < argc) {
            fifo = argv[++i];
        } else if (strcmp(argv[i], "--ready") == 0 && i + 1 < argc) {
            ready = argv[++i];
        } else if (strcmp(argv[i], "--max-x") == 0 && i + 1 < argc) {
            max_x = parse_int(argv[++i], max_x);
        } else if (strcmp(argv[i], "--max-y") == 0 && i + 1 < argc) {
            max_y = parse_int(argv[++i], max_y);
        } else if (strcmp(argv[i], "--timeout") == 0 && i + 1 < argc) {
            timeout_seconds = parse_int(argv[++i], timeout_seconds);
        } else {
            fprintf(stderr, "unknown argument: %s\n", argv[i]);
            return 2;
        }
    }
    if (fifo == NULL) {
        fprintf(stderr, "usage: %s --fifo PATH [--ready PATH] "
                        "[--max-x N] [--max-y N] [--timeout SECONDS]\n",
                argv[0]);
        return 2;
    }

    signal(SIGALRM, on_alarm);
    alarm((unsigned)timeout_seconds);

    device_fd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
    if (device_fd < 0) {
        perror("open /dev/uinput");
        return 1;
    }

    if (ioctl(device_fd, UI_SET_EVBIT, EV_KEY) < 0 ||
        ioctl(device_fd, UI_SET_KEYBIT, BTN_LEFT) < 0 ||
        ioctl(device_fd, UI_SET_EVBIT, EV_ABS) < 0 ||
        ioctl(device_fd, UI_SET_ABSBIT, ABS_X) < 0 ||
        ioctl(device_fd, UI_SET_ABSBIT, ABS_Y) < 0) {
        perror("UI_SET_*");
        destroy_device();
        return 1;
    }

    set_abs(device_fd, ABS_X, 0, max_x);
    set_abs(device_fd, ABS_Y, 0, max_y);

    struct uinput_setup setup;
    memset(&setup, 0, sizeof(setup));
    setup.id.bustype = BUS_USB;
    setup.id.vendor = 0x1209;
    setup.id.product = 0xA57E;
    setup.id.version = 1;
    snprintf(setup.name, sizeof(setup.name), "Aster Window Pointer");
    if (ioctl(device_fd, UI_DEV_SETUP, &setup) < 0) {
        perror("UI_DEV_SETUP");
        destroy_device();
        return 1;
    }
    if (ioctl(device_fd, UI_DEV_CREATE) < 0) {
        perror("UI_DEV_CREATE");
        destroy_device();
        return 1;
    }

    /* Let KWin/libinput attach the virtual device before the first event. */
    sleep_ms(2000);
    emit(device_fd, EV_ABS, ABS_X, 0);
    emit(device_fd, EV_ABS, ABS_Y, 0);
    sync_events(device_fd);
    sleep_ms(700);

    if (ready != NULL) {
        FILE *marker = fopen(ready, "w");
        if (marker != NULL) {
            fputs("ready\n", marker);
            fclose(marker);
        }
    }

    FILE *input = fopen(fifo, "r");
    if (input == NULL) {
        perror("fopen fifo");
        destroy_device();
        return 1;
    }

    char line[256];
    while (fgets(line, sizeof(line), input) != NULL) {
        int x;
        int y;
        int ms;
        if (sscanf(line, "move-click %d %d", &x, &y) == 2) {
            if (x < 0) x = 0;
            if (x > max_x) x = max_x;
            if (y < 0) y = 0;
            if (y > max_y) y = max_y;
            emit(device_fd, EV_ABS, ABS_X, x);
            emit(device_fd, EV_ABS, ABS_Y, y);
            sync_events(device_fd);
            sleep_ms(120);
            click_left();
        } else if (sscanf(line, "move %d %d", &x, &y) == 2) {
            if (x < 0) x = 0;
            if (x > max_x) x = max_x;
            if (y < 0) y = 0;
            if (y > max_y) y = max_y;
            emit(device_fd, EV_ABS, ABS_X, x);
            emit(device_fd, EV_ABS, ABS_Y, y);
            sync_events(device_fd);
        } else if (strncmp(line, "click", 5) == 0) {
            click_left();
        } else if (sscanf(line, "sleep %d", &ms) == 1) {
            sleep_ms(ms);
        } else if (strncmp(line, "quit", 4) == 0) {
            break;
        }
    }

    fclose(input);
    destroy_device();
    return 0;
}
