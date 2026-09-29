#!/usr/bin/env python3
"""Export the appmenu tool's GMenu on the bus named by argv[1] with GLib's own exporters.

Prints READY UNIQUE-NAME once exported and ACTIVATED NAME for every activation.
Exits when stdin closes.
"""
import sys

import gi

gi.require_version('Gio', '2.0')
from gi.repository import Gio, GLib


def main():
    bus = Gio.DBusConnection.new_for_address_sync(
        sys.argv[1],
        Gio.DBusConnectionFlags.AUTHENTICATION_CLIENT | Gio.DBusConnectionFlags.MESSAGE_BUS_CONNECTION,
        None, None)
    loop = GLib.MainLoop()

    def activated(action, _parameter):
        if action.get_name() == 'wrap':
            action.set_state(GLib.Variant.new_boolean(not action.get_state().get_boolean()))
        print(f'ACTIVATED {action.get_name()}', flush=True)

    app = Gio.SimpleActionGroup()
    window = Gio.SimpleActionGroup()
    quit_action = Gio.SimpleAction.new('quit', None)
    quit_action.connect('activate', activated)
    app.add_action(quit_action)
    for name in ('open', 'copy'):
        action = Gio.SimpleAction.new(name, None)
        action.connect('activate', activated)
        window.add_action(action)
    wrap = Gio.SimpleAction.new_stateful('wrap', None, GLib.Variant.new_boolean(False))
    wrap.connect('activate', activated)
    window.add_action(wrap)
    disabled = Gio.SimpleAction.new('print', None)
    disabled.set_enabled(False)
    window.add_action(disabled)

    menubar = Gio.Menu()
    file = Gio.Menu()
    top = Gio.Menu()
    item = Gio.MenuItem.new('_Open…', 'win.open')
    item.set_attribute_value('accel', GLib.Variant.new_string('<Primary>o'))
    top.append_item(item)
    top.append('_Print', 'win.print')
    bottom = Gio.Menu()
    item = Gio.MenuItem.new('_Quit', 'app.quit')
    item.set_attribute_value('accel', GLib.Variant.new_string('<Primary>q'))
    bottom.append_item(item)
    file.append_section(None, top)
    file.append_section(None, bottom)
    edit = Gio.Menu()
    item = Gio.MenuItem.new('_Copy', 'win.copy')
    item.set_attribute_value('accel', GLib.Variant.new_string('<Primary>c'))
    edit.append_item(item)
    edit.append('_Wrap', 'win.wrap')
    menubar.append_submenu('_File', file)
    menubar.append_submenu('_Edit', edit)

    bus.export_action_group('/org/basin/appmenu', app)
    bus.export_action_group('/org/basin/appmenu/window/1', window)
    bus.export_menu_model('/org/basin/appmenu/menus/menubar', menubar)
    print(f'READY {bus.get_unique_name()}', flush=True)

    def stdin_closed(_source, _condition):
        loop.quit()
        return False

    GLib.io_add_watch(GLib.IOChannel.unix_new(sys.stdin.fileno()), GLib.IOCondition.HUP | GLib.IOCondition.IN, stdin_closed)
    GLib.timeout_add_seconds(30, loop.quit)
    loop.run()


if __name__ == '__main__':
    main()
