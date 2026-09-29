#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wayland-client.h>
#include "river-input-management-v1-client-protocol.h"

#define MAX_DEVICES 64

struct device {
    struct river_input_device_v1 *proxy;
    uint32_t type;
    int typed;
    char name[256];
};

static struct river_input_manager_v1 *manager;
static struct device devices[MAX_DEVICES];
static int count;

static void device_removed(void *data, struct river_input_device_v1 *proxy)
{
    (void)proxy;
    ((struct device *)data)->typed = -1;
}

static void device_type(void *data, struct river_input_device_v1 *proxy, uint32_t type)
{
    (void)proxy;
    struct device *device = data;
    device->type = type;
    device->typed = 1;
}

static void device_name(void *data, struct river_input_device_v1 *proxy, const char *name)
{
    (void)proxy;
    snprintf(((struct device *)data)->name, sizeof ((struct device *)data)->name, "%s", name);
}

static void device_done(void *data, struct river_input_device_v1 *proxy)
{
    (void)data;
    (void)proxy;
}

static const struct river_input_device_v1_listener device_listener = {
    .removed = device_removed,
    .type = device_type,
    .name = device_name,
    .done = device_done,
};

static void manager_finished(void *data, struct river_input_manager_v1 *proxy)
{
    (void)data;
    (void)proxy;
}

static void manager_input_device(void *data, struct river_input_manager_v1 *proxy, struct river_input_device_v1 *id)
{
    (void)data;
    (void)proxy;
    if (count == MAX_DEVICES) {
        return;
    }

    struct device *device = &devices[count++];
    device->proxy = id;
    river_input_device_v1_add_listener(id, &device_listener, device);
}

static const struct river_input_manager_v1_listener manager_listener = {
    .finished = manager_finished,
    .input_device = manager_input_device,
};

static void global(void *data, struct wl_registry *registry, uint32_t name, const char *interface, uint32_t version)
{
    (void)data;
    if (strcmp(interface, river_input_manager_v1_interface.name) == 0) {
        manager = wl_registry_bind(registry, name, &river_input_manager_v1_interface, version < 2 ? version : 2);
    }
}

static void global_remove(void *data, struct wl_registry *registry, uint32_t name)
{
    (void)data;
    (void)registry;
    (void)name;
}

static const struct wl_registry_listener registry_listener = { global, global_remove };

int main(int argc, char **argv)
{
    if (argc < 3) {
        fprintf(stderr, "usage: riverrepeat RATE DELAY [NAME]\n");
        fprintf(stderr, "  sends river_input_device_v1.set_repeat_info to every keyboard, or to those whose name contains NAME\n");
        return 2;
    }

    int rate = atoi(argv[1]);
    int delay = atoi(argv[2]);
    const char *match = argc > 3 ? argv[3] : NULL;
    struct wl_display *display = wl_display_connect(NULL);
    if (display == NULL) {
        fprintf(stderr, "riverrepeat: cannot connect to the compositor\n");
        return 1;
    }

    struct wl_registry *registry = wl_display_get_registry(display);
    wl_registry_add_listener(registry, &registry_listener, NULL);
    wl_display_roundtrip(display);
    if (manager == NULL) {
        fprintf(stderr, "riverrepeat: no river_input_manager_v1\n");
        return 1;
    }

    river_input_manager_v1_add_listener(manager, &manager_listener, NULL);
    wl_display_roundtrip(display);
    wl_display_roundtrip(display);

    int sent = 0;
    for (int i = 0; i < count; i++) {
        struct device *device = &devices[i];
        if (device->typed != 1 || device->type != RIVER_INPUT_DEVICE_V1_TYPE_KEYBOARD) {
            continue;
        }

        if (match != NULL && strstr(device->name, match) == NULL) {
            continue;
        }

        river_input_device_v1_set_repeat_info(device->proxy, rate, delay);
        printf("REPEAT %d %d %s\n", rate, delay, device->name);
        sent++;
    }

    wl_display_roundtrip(display);
    wl_display_disconnect(display);
    return sent > 0 ? 0 : 1;
}
