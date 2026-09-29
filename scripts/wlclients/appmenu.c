/* appmenu kde|gtk [SECONDS]: map one 256x256 xdg toplevel titled appmenu and export
 * a small fixed menu on the session bus, then tell the compositor where it is.
 *
 * kde: a hand-written com.canonical.dbusmenu at /MenuBar/1, announced through
 *      org_kde_kwin_appmenu_manager.set_address, as Qt does under the KDE theme.
 * gtk: a GMenuModel and two action groups exported with GLib's own exporters,
 *      announced through gtk_surface1.set_dbus_properties, as GtkApplication does.
 *
 * The menu is File (Open ^O, a separator, Quit ^Q) and Edit (Copy ^C, and a
 * Wrap check item). Prints READY once exported and announced, CAPABILITIES N when
 * gtk_shell1 sends them, and ACTIVATED NAME for every item the compositor runs.
 * Needs a session bus. */
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/mman.h>
#include <gio/gio.h>
#include <glib-unix.h>
#include <wayland-client.h>
#include "xdg-shell-client-protocol.h"
#include "appmenu-client-protocol.h"
#include "gtk-shell-client-protocol.h"

static struct wl_display *display;
static struct wl_compositor *compositor;
static struct wl_shm *shm;
static struct xdg_wm_base *wm_base;
static struct org_kde_kwin_appmenu_manager *kde_manager;
static struct gtk_shell1 *gtk_shell;
static struct wl_surface *surface;
static struct xdg_toplevel *toplevel;
static GMainLoop *loop;
static GDBusConnection *bus;
static int gtk_mode;
static gboolean wrap_on;

static void activated(const char *name)
{
    printf("ACTIVATED %s\n", name);
    fflush(stdout);
}

/* ---- the hand-written dbusmenu ------------------------------------------------ */

static const char dbusmenu_xml[] =
    "<node><interface name='com.canonical.dbusmenu'>"
    "<method name='GetLayout'><arg type='i' direction='in'/><arg type='i' direction='in'/>"
    "<arg type='as' direction='in'/><arg type='u' direction='out'/><arg type='(ia{sv}av)' direction='out'/></method>"
    "<method name='AboutToShow'><arg type='i' direction='in'/><arg type='b' direction='out'/></method>"
    "<method name='Event'><arg type='i' direction='in'/><arg type='s' direction='in'/>"
    "<arg type='v' direction='in'/><arg type='u' direction='in'/></method>"
    "<signal name='LayoutUpdated'><arg type='u'/><arg type='i'/></signal>"
    "<property name='Version' type='u' access='read'/>"
    "</interface></node>";

struct node { int id; const char *label; const char *key; int separator; int toggle; int parent; };

static const struct node nodes[] = {
    { 1, "_File", NULL, 0, 0, 0 },
    { 2, "_Open…", "o", 0, 0, 1 },
    { 3, "", NULL, 1, 0, 1 },
    { 4, "_Quit", "q", 0, 0, 1 },
    { 5, "_Edit", NULL, 0, 0, 0 },
    { 6, "_Copy", "c", 0, 0, 5 },
    { 7, "_Wrap", NULL, 0, 1, 5 },
};

static const char *node_name(int id)
{
    switch (id) {
    case 2: return "open";
    case 4: return "quit";
    case 6: return "copy";
    case 7: return "wrap";
    default: return "unknown";
    }
}

static GVariant *layout_of(int id)
{
    GVariantBuilder props, children;
    g_variant_builder_init(&props, G_VARIANT_TYPE("a{sv}"));
    g_variant_builder_init(&children, G_VARIANT_TYPE("av"));
    int has_children = 0;
    for (size_t i = 0; i < G_N_ELEMENTS(nodes); i++) {
        const struct node *n = &nodes[i];
        if (n->id == id) {
            if (n->separator) g_variant_builder_add(&props, "{sv}", "type", g_variant_new_string("separator"));
            else g_variant_builder_add(&props, "{sv}", "label", g_variant_new_string(n->label));
            if (n->key) {
                const char *keys[] = { "Control", n->key, NULL };
                GVariantBuilder shortcut;
                g_variant_builder_init(&shortcut, G_VARIANT_TYPE("aas"));
                g_variant_builder_add_value(&shortcut, g_variant_new_strv(keys, -1));
                g_variant_builder_add(&props, "{sv}", "shortcut", g_variant_builder_end(&shortcut));
            }
            if (n->toggle) {
                g_variant_builder_add(&props, "{sv}", "toggle-type", g_variant_new_string("checkmark"));
                g_variant_builder_add(&props, "{sv}", "toggle-state", g_variant_new_int32(wrap_on ? 1 : 0));
            }
        }
        if (n->parent == id && n->id != id) {
            g_variant_builder_add(&children, "v", layout_of(n->id));
            has_children = 1;
        }
    }
    if (has_children && id != 0)
        g_variant_builder_add(&props, "{sv}", "children-display", g_variant_new_string("submenu"));
    return g_variant_new("(i@a{sv}@av)", id, g_variant_builder_end(&props), g_variant_builder_end(&children));
}

static guint layout_revision = 1;

static void dbusmenu_call(GDBusConnection *c, const gchar *sender, const gchar *path, const gchar *iface,
                          const gchar *method, GVariant *params, GDBusMethodInvocation *inv, gpointer data)
{
    (void)c; (void)sender; (void)path; (void)iface; (void)data;
    if (strcmp(method, "GetLayout") == 0) {
        g_dbus_method_invocation_return_value(inv, g_variant_new("(u@(ia{sv}av))", layout_revision, layout_of(0)));
    } else if (strcmp(method, "AboutToShow") == 0) {
        g_dbus_method_invocation_return_value(inv, g_variant_new("(b)", FALSE));
    } else if (strcmp(method, "Event") == 0) {
        gint32 id;
        const gchar *event;
        g_variant_get(params, "(i&svu)", &id, &event, NULL, NULL);
        if (strcmp(event, "clicked") == 0) {
            if (id == 7) {
                wrap_on = !wrap_on;
                g_dbus_connection_emit_signal(bus, NULL, "/MenuBar/1", "com.canonical.dbusmenu", "LayoutUpdated",
                                              g_variant_new("(ui)", ++layout_revision, 0), NULL);
            }
            activated(node_name(id));
        }
        g_dbus_method_invocation_return_value(inv, NULL);
    } else {
        g_dbus_method_invocation_return_dbus_error(inv, "org.freedesktop.DBus.Error.UnknownMethod", method);
    }
}

static GVariant *dbusmenu_property(GDBusConnection *c, const gchar *sender, const gchar *path, const gchar *iface,
                                   const gchar *name, GError **error, gpointer data)
{
    (void)c; (void)sender; (void)path; (void)iface; (void)name; (void)error; (void)data;
    return g_variant_new_uint32(3);
}

static const GDBusInterfaceVTable dbusmenu_vtable = { dbusmenu_call, dbusmenu_property, NULL, { 0 } };

/* ---- the GLib export ---------------------------------------------------------- */

static void on_action(GSimpleAction *action, GVariant *parameter, gpointer data)
{
    (void)parameter; (void)data;
    activated(g_action_get_name(G_ACTION(action)));
}

static void on_wrap(GSimpleAction *action, GVariant *parameter, gpointer data)
{
    (void)parameter; (void)data;
    GVariant *state = g_action_get_state(G_ACTION(action));
    g_simple_action_set_state(action, g_variant_new_boolean(!g_variant_get_boolean(state)));
    g_variant_unref(state);
    activated("wrap");
}

static void export_gtk(void)
{
    GSimpleActionGroup *app = g_simple_action_group_new();
    GSimpleActionGroup *win = g_simple_action_group_new();
    static const GActionEntry app_entries[] = { { "quit", on_action, NULL, NULL, NULL, { 0 } } };
    static const GActionEntry win_entries[] = {
        { "open", on_action, NULL, NULL, NULL, { 0 } },
        { "copy", on_action, NULL, NULL, NULL, { 0 } },
        { "wrap", on_wrap, NULL, "false", NULL, { 0 } },
    };
    g_action_map_add_action_entries(G_ACTION_MAP(app), app_entries, G_N_ELEMENTS(app_entries), NULL);
    g_action_map_add_action_entries(G_ACTION_MAP(win), win_entries, G_N_ELEMENTS(win_entries), NULL);

    GMenu *menubar = g_menu_new();
    GMenu *file = g_menu_new();
    GMenu *open_section = g_menu_new();
    GMenu *quit_section = g_menu_new();
    GMenuItem *item = g_menu_item_new("_Open…", "win.open");
    g_menu_item_set_attribute(item, "accel", "s", "<Primary>o");
    g_menu_append_item(open_section, item);
    g_object_unref(item);
    item = g_menu_item_new("_Quit", "app.quit");
    g_menu_item_set_attribute(item, "accel", "s", "<Primary>q");
    g_menu_append_item(quit_section, item);
    g_object_unref(item);
    g_menu_append_section(file, NULL, G_MENU_MODEL(open_section));
    g_menu_append_section(file, NULL, G_MENU_MODEL(quit_section));
    GMenu *edit = g_menu_new();
    item = g_menu_item_new("_Copy", "win.copy");
    g_menu_item_set_attribute(item, "accel", "s", "<Primary>c");
    g_menu_append_item(edit, item);
    g_object_unref(item);
    g_menu_append(edit, "_Wrap", "win.wrap");
    g_menu_append_submenu(menubar, "_File", G_MENU_MODEL(file));
    g_menu_append_submenu(menubar, "_Edit", G_MENU_MODEL(edit));

    GError *error = NULL;
    if (!g_dbus_connection_export_action_group(bus, "/org/basin/appmenu", G_ACTION_GROUP(app), &error) ||
        !g_dbus_connection_export_action_group(bus, "/org/basin/appmenu/window/1", G_ACTION_GROUP(win), &error) ||
        !g_dbus_connection_export_menu_model(bus, "/org/basin/appmenu/menus/menubar", G_MENU_MODEL(menubar), &error)) {
        fprintf(stderr, "appmenu: export failed: %s\n", error->message);
        exit(1);
    }

    struct gtk_surface1 *gtk_surface = gtk_shell1_get_gtk_surface(gtk_shell, surface);
    gtk_surface1_set_dbus_properties(gtk_surface, "org.basin.appmenu", NULL, "/org/basin/appmenu/menus/menubar",
                                     "/org/basin/appmenu/window/1", "/org/basin/appmenu",
                                     g_dbus_connection_get_unique_name(bus));
}

/* ---- Wayland ------------------------------------------------------------------ */

static void capabilities(void *data, struct gtk_shell1 *s, uint32_t caps)
{
    (void)data; (void)s;
    printf("CAPABILITIES %u\n", caps);
    fflush(stdout);
}

static const struct gtk_shell1_listener gtk_shell_listener = { capabilities };

static void global(void *data, struct wl_registry *r, uint32_t name, const char *iface, uint32_t version)
{
    (void)data;
    if (strcmp(iface, wl_compositor_interface.name) == 0)
        compositor = wl_registry_bind(r, name, &wl_compositor_interface, 4);
    else if (strcmp(iface, wl_shm_interface.name) == 0)
        shm = wl_registry_bind(r, name, &wl_shm_interface, 1);
    else if (strcmp(iface, xdg_wm_base_interface.name) == 0)
        wm_base = wl_registry_bind(r, name, &xdg_wm_base_interface, 1);
    else if (strcmp(iface, org_kde_kwin_appmenu_manager_interface.name) == 0)
        kde_manager = wl_registry_bind(r, name, &org_kde_kwin_appmenu_manager_interface, version < 2 ? version : 2);
    else if (strcmp(iface, gtk_shell1_interface.name) == 0) {
        gtk_shell = wl_registry_bind(r, name, &gtk_shell1_interface, version < 5 ? version : 5);
        gtk_shell1_add_listener(gtk_shell, &gtk_shell_listener, NULL);
    }
}

static void global_remove(void *data, struct wl_registry *r, uint32_t name) { (void)data; (void)r; (void)name; }

static const struct wl_registry_listener registry_listener = { global, global_remove };

static void ping(void *data, struct xdg_wm_base *b, uint32_t serial) { (void)data; xdg_wm_base_pong(b, serial); }

static const struct xdg_wm_base_listener wm_base_listener = { ping };

static struct wl_buffer *make_buffer(int w, int h)
{
    int stride = w * 4, size = stride * h;
    int fd = memfd_create("appmenu", 0);
    if (fd < 0 || ftruncate(fd, size) < 0) return NULL;
    uint32_t *pixels = mmap(NULL, size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    for (int i = 0; i < w * h; i++) pixels[i] = 0xFFE0E0E0;
    struct wl_shm_pool *pool = wl_shm_create_pool(shm, fd, size);
    struct wl_buffer *buffer = wl_shm_pool_create_buffer(pool, 0, w, h, stride, WL_SHM_FORMAT_XRGB8888);
    wl_shm_pool_destroy(pool);
    munmap(pixels, size);
    close(fd);
    return buffer;
}

static void xdg_configure(void *data, struct xdg_surface *s, uint32_t serial)
{
    (void)data;
    xdg_surface_ack_configure(s, serial);
    wl_surface_attach(surface, make_buffer(256, 256), 0, 0);
    wl_surface_damage_buffer(surface, 0, 0, 256, 256);
    wl_surface_commit(surface);
}

static const struct xdg_surface_listener xdg_surface_listener = { xdg_configure };

static void top_configure(void *d, struct xdg_toplevel *t, int32_t w, int32_t h, struct wl_array *s) { (void)d; (void)t; (void)w; (void)h; (void)s; }
static void top_close(void *d, struct xdg_toplevel *t) { (void)d; (void)t; g_main_loop_quit(loop); }
static void top_bounds(void *d, struct xdg_toplevel *t, int32_t w, int32_t h) { (void)d; (void)t; (void)w; (void)h; }
static void top_caps(void *d, struct xdg_toplevel *t, struct wl_array *c) { (void)d; (void)t; (void)c; }

static const struct xdg_toplevel_listener toplevel_listener = { top_configure, top_close, top_bounds, top_caps };

static gboolean on_wayland(gint fd, GIOCondition condition, gpointer data)
{
    (void)fd; (void)data;
    if (condition & (G_IO_HUP | G_IO_ERR) || wl_display_dispatch(display) < 0) {
        g_main_loop_quit(loop);
        return G_SOURCE_REMOVE;
    }
    wl_display_flush(display);
    return G_SOURCE_CONTINUE;
}

static gboolean flush_wayland(gpointer data)
{
    (void)data;
    wl_display_dispatch_pending(display);
    wl_display_flush(display);
    return G_SOURCE_CONTINUE;
}

static gboolean timeout(gpointer data) { (void)data; g_main_loop_quit(loop); return G_SOURCE_REMOVE; }

int main(int argc, char **argv)
{
    if (argc < 2 || (strcmp(argv[1], "kde") != 0 && strcmp(argv[1], "gtk") != 0)) {
        fprintf(stderr, "usage: appmenu kde|gtk [SECONDS]\n");
        return 2;
    }
    gtk_mode = strcmp(argv[1], "gtk") == 0;
    GError *error = NULL;
    bus = g_bus_get_sync(G_BUS_TYPE_SESSION, NULL, &error);
    if (!bus) {
        fprintf(stderr, "appmenu: no session bus: %s\n", error->message);
        return 1;
    }

    display = wl_display_connect(NULL);
    if (!display) {
        fprintf(stderr, "appmenu: cannot connect to a Wayland display\n");
        return 1;
    }
    struct wl_registry *registry = wl_display_get_registry(display);
    wl_registry_add_listener(registry, &registry_listener, NULL);
    wl_display_roundtrip(display);
    if (!compositor || !shm || !wm_base) {
        fprintf(stderr, "appmenu: the compositor lacks wl_compositor, wl_shm or xdg_wm_base\n");
        return 1;
    }
    if (gtk_mode ? !gtk_shell : !kde_manager) {
        fprintf(stderr, "appmenu: the compositor does not offer %s\n", gtk_mode ? "gtk_shell1" : "org_kde_kwin_appmenu_manager");
        return 1;
    }
    xdg_wm_base_add_listener(wm_base, &wm_base_listener, NULL);
    surface = wl_compositor_create_surface(compositor);
    struct xdg_surface *xdg = xdg_wm_base_get_xdg_surface(wm_base, surface);
    xdg_surface_add_listener(xdg, &xdg_surface_listener, NULL);
    toplevel = xdg_surface_get_toplevel(xdg);
    xdg_toplevel_add_listener(toplevel, &toplevel_listener, NULL);
    xdg_toplevel_set_title(toplevel, "appmenu");
    xdg_toplevel_set_app_id(toplevel, gtk_mode ? "org.basin.appmenu" : "appmenu-kde");

    if (gtk_mode) {
        export_gtk();
    } else {
        GDBusNodeInfo *info = g_dbus_node_info_new_for_xml(dbusmenu_xml, &error);
        if (!info || !g_dbus_connection_register_object(bus, "/MenuBar/1", info->interfaces[0], &dbusmenu_vtable, NULL, NULL, &error)) {
            fprintf(stderr, "appmenu: export failed: %s\n", error->message);
            return 1;
        }
        struct org_kde_kwin_appmenu *menu = org_kde_kwin_appmenu_manager_create(kde_manager, surface);
        org_kde_kwin_appmenu_set_address(menu, g_dbus_connection_get_unique_name(bus), "/MenuBar/1");
    }
    wl_surface_commit(surface);
    wl_display_roundtrip(display);
    printf("READY %s %s\n", argv[1], g_dbus_connection_get_unique_name(bus));
    fflush(stdout);

    loop = g_main_loop_new(NULL, FALSE);
    g_unix_fd_add(wl_display_get_fd(display), G_IO_IN | G_IO_HUP | G_IO_ERR, on_wayland, NULL);
    g_timeout_add(50, flush_wayland, NULL);
    if (argc > 2) g_timeout_add_seconds(atoi(argv[2]), timeout, NULL);
    g_main_loop_run(loop);
    wl_display_disconnect(display);
    return 0;
}
