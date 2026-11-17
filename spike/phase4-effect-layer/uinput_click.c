#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <linux/uinput.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <time.h>
#include <unistd.h>

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

int main(int argc, char **argv)
{
    if (argc < 3 || (argc - 1) % 2 != 0) {
        fprintf(stderr, "usage: %s x1 y1 [x2 y2 ...]\n", argv[0]);
        return 2;
    }

    int fd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
    if (fd < 0) {
        perror("open /dev/uinput");
        return 1;
    }

    if (ioctl(fd, UI_SET_EVBIT, EV_KEY) < 0 ||
        ioctl(fd, UI_SET_KEYBIT, BTN_LEFT) < 0 ||
        ioctl(fd, UI_SET_EVBIT, EV_ABS) < 0 ||
        ioctl(fd, UI_SET_ABSBIT, ABS_X) < 0 ||
        ioctl(fd, UI_SET_ABSBIT, ABS_Y) < 0) {
        perror("UI_SET_*");
        close(fd);
        return 1;
    }

    set_abs(fd, ABS_X, 0, 7999);
    set_abs(fd, ABS_Y, 0, 2879);

    struct uinput_setup setup;
    memset(&setup, 0, sizeof(setup));
    setup.id.bustype = BUS_USB;
    setup.id.vendor = 0x1209;
    setup.id.product = 0xA57E;
    setup.id.version = 1;
    snprintf(setup.name, sizeof(setup.name), "Aster Gate Pointer");
    if (ioctl(fd, UI_DEV_SETUP, &setup) < 0) {
        perror("UI_DEV_SETUP");
        close(fd);
        return 1;
    }
    if (ioctl(fd, UI_DEV_CREATE) < 0) {
        perror("UI_DEV_CREATE");
        close(fd);
        return 1;
    }

    sleep_ms(2000);

    /* Warm-up move: let KWin/libinput attach the device and apply one ABS
       event before the first click. Without this, the first click can land at
       the device's stale/default position. */
    emit(fd, EV_ABS, ABS_X, 0);
    emit(fd, EV_ABS, ABS_Y, 0);
    sync_events(fd);
    sleep_ms(700);

    for (int i = 1; i + 1 < argc; i += 2) {
        int x = atoi(argv[i]);
        int y = atoi(argv[i + 1]);
        if (x < 0) x = 0;
        if (x > 7999) x = 7999;
        if (y < 0) y = 0;
        if (y > 2879) y = 2879;

        emit(fd, EV_ABS, ABS_X, x);
        emit(fd, EV_ABS, ABS_Y, y);
        sync_events(fd);
        sleep_ms(800);

        emit(fd, EV_KEY, BTN_LEFT, 1);
        sync_events(fd);
        sleep_ms(120);
        emit(fd, EV_KEY, BTN_LEFT, 0);
        sync_events(fd);
        sleep_ms(800);
    }

    sleep_ms(900);
    ioctl(fd, UI_DEV_DESTROY);
    close(fd);
    return 0;
}
