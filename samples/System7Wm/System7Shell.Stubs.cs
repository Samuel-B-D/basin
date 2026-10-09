using Basin;
using Basin.Ipc;
using Basin.Scene;

namespace System7Wm;

internal sealed partial class System7Shell
{
    private void DisposeShell()
    {
        _aboutTimer?.Remove();
        _aboutTimer = null;
        foreach (var folder in _finderViews.Keys.ToList())
        {
            SaveFinderState(folder);
        }

        DisposeTerminals();
        _menuBar?.Dispose();
        foreach (var window in _stack.ToList())
        {
            window.Frame?.Dispose();
            window.ShellSurface?.Dispose();
        }

        _zoom?.Dispose();
        _outline?.Dispose();
        _desktopSurface?.Dispose();
        _watch?.Dispose();
        _x11?.Dispose();
    }

    private void PrintShellState()
    {
        _report.Line($"FRONT {_front?.Key ?? "none"} menus={_front?.MenuKindFor(_front is null ? null : ActiveOf(_front)) ?? "none"}");
        foreach (var window in _stack)
        {
            _report.Line($"WINDOW {window.Id} app={window.App.Key} kind={window.Kind} {window.Logical.X},{window.Logical.Y},{window.Logical.Width}x{window.Logical.Height} active={window.Active} shaded={window.Shaded} zoomed={window.Zoomed} hidden={window.App.Hidden} \"{window.Title}\"");
        }

        foreach (var app in _apps.Values)
        {
            _report.Line($"APP {app.Key} \"{app.Name}\" windows={app.Windows.Count} hidden={app.Hidden} menus={app.MenuKindFor(ActiveOf(app))}");
        }

        foreach (var window in _stack.Where(w => w.IsShell))
        {
            _report.Line($"SHELL {window.Id} {window.ShellKey} device={window.ShellDevice.X},{window.ShellDevice.Y},{window.ShellDevice.Width},{window.ShellDevice.Height} active={window.Active} grown={window.ShellGrown}");
        }

        var bar = _menuBar?.Surface;
        _report.Line($"MENUBAR {(_menuBar?.IsGrown == true ? "grown" : "rest")} open={_menuBar?.IsOpen == true} device={bar?.Device.Width ?? 0}x{bar?.Device.Height ?? 0} titles={string.Join(",", _menuBar?.Titles.Select(t => t.Label) ?? [])}");
        _report.Line($"FRAMECOUNT {_outputs.PrimaryRendered}");
    }

    private void RegisterShellCommands(IpcMethodRegistry methods)
    {
        _report.Register(methods, "system7wm/clock", "clock {text...}", (ref IpcParams p, IpcReply _) =>
        {
            var text = p.GetString("text");
            if (!p.Failed)
            {
                if (_clock is not null)
                {
                    _clock.Frozen = text is "off" or "" ? null : text;
                }

                _report.Line($"CLOCK {_clock?.Frozen ?? "live"}");
            }
        });

        _report.Register(methods, "system7wm/menu", "menu {path...}", (ref IpcParams p, IpcReply _) =>
        {
            var path = p.GetString("path");
            if (!p.Failed)
            {
                _report.Line(_menuBar?.Choose(path, (uint)Environment.TickCount) == true ? $"MENU {path}" : $"MENU missing {path}");
            }
        });

        _report.Register(methods, "system7wm/menus", "menus", () =>
        {
            foreach (var (label, model, source) in _menuBar?.Titles ?? [])
            {
                PrintMenu(label, model, 0);
            }
        });

        _report.Register(methods, "system7wm/appmenu", "appmenu", () =>
        {
            if (_menuBar?.Application.Menu.Items.FirstOrDefault() is { } application)
            {
                PrintMenu("Application", application, 0);
            }
        });

        _report.Register(methods, "system7wm/hide", "hide {app}", (ref IpcParams p, IpcReply _) =>
        {
            var key = p.GetString("app");
            if (!p.Failed && _apps.TryGetValue(key, out var app))
            {
                HideApp(app);
            }
        });

        _report.Register(methods, "system7wm/show-all", "show-all", ShowAll);

        _report.Register(methods, "system7wm/shade", "shade {id:int}", (ref IpcParams p, IpcReply _) =>
        {
            var id = (int)p.GetInt("id");
            if (!p.Failed && _stack.FirstOrDefault(w => w.Id == id) is { } window)
            {
                Shade(window);
            }
        });

        _report.Register(methods, "system7wm/zoom", "zoom {id:int}", (ref IpcParams p, IpcReply _) =>
        {
            var id = (int)p.GetInt("id");
            if (!p.Failed && _stack.FirstOrDefault(w => w.Id == id) is { } window)
            {
                Zoom(window);
            }
        });

        _report.Register(methods, "system7wm/panel", "panel {name...}", (ref IpcParams p, IpcReply _) =>
        {
            var text = p.GetString("name");
            if (p.Failed)
            {
                return;
            }

            var close = text.EndsWith(" close", StringComparison.OrdinalIgnoreCase);
            var name = close ? text[..^6].Trim() : text.Trim();
            var match = ControlPanels.Names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                _report.Line($"PANEL unknown {name}");
                return;
            }

            if (close)
            {
                if (ShellWindowByKey($"panel:{match}") is { } window)
                {
                    CloseShellWindow(window);
                }
            }
            else
            {
                OpenPanel(match);
            }

            _report.Line($"PANEL {match} {(close ? "closed" : "open")}");
        });

        _report.Register(methods, "system7wm/volume", "volume [{level:int}]", (ref IpcParams p, IpcReply _) =>
        {
            if (p.Has("level"))
            {
                var level = (int)p.GetInt("level");
                if (!p.Failed)
                {
                    SetVolumeLevel(level);
                }

                return;
            }

            ReportVolume();
        });

        _report.Register(methods, "system7wm/about", "about [{close}]", (ref IpcParams p, IpcReply _) =>
        {
            var close = p.Has("close");
            if (close)
            {
                if (ShellWindowByKey("about") is { } window)
                {
                    CloseShellWindow(window);
                }
            }
            else
            {
                OpenAbout();
            }

            _report.Line($"ABOUT {(close ? "closed" : "open")}");
        });

        _report.Register(methods, "system7wm/alert-choose", "alert-choose {button...}", (ref IpcParams p, IpcReply _) =>
        {
            var button = p.GetString("button");
            if (!p.Failed)
            {
                _report.Line(ChooseAlert(button) ? $"ALERT chose {button}" : $"ALERT none {button}");
            }
        });

        _report.Register(methods, "system7wm/finder", "finder {path...}", (ref IpcParams p, IpcReply _) =>
        {
            var text = p.GetString("path");
            if (p.Failed)
            {
                return;
            }

            var close = text.EndsWith(" close", StringComparison.Ordinal);
            var path = Path.GetFullPath(close ? text[..^6].Trim() : text.Trim());
            if (close)
            {
                if (ShellWindowByKey($"finder:{path}") is { } window)
                {
                    CloseShellWindow(window);
                }

                _report.Line($"FINDER {path} closed");
                return;
            }

            if (!Directory.Exists(path))
            {
                _report.Line($"FINDER missing {path}");
                return;
            }

            OpenFinder(path);
            _report.Line($"FINDER {path} open");
        });

        _report.Register(methods, "system7wm/desktop", "desktop", () =>
        {
            foreach (var icon in _desktop?.Icons ?? [])
            {
                _report.Line($"ICON {icon.Kind} \"{icon.Label}\" {icon.Position.X:0},{icon.Position.Y:0}{(icon.Selected ? " selected" : "")}");
            }
        });

        _report.Register(methods, "system7wm/trash", "trash", () =>
        {
            var summary = Trash.Summary();
            _report.Line($"TRASH items={summary.Items} bytes={summary.Bytes}");
            foreach (var item in Trash.Items())
            {
                _report.Line($"TRASHITEM {item.Name} {item.OriginalPath}");
            }
        });

        _report.Register(methods, "system7wm/desktop-drop", "desktop-drop {name} {target...}", (ref IpcParams p, IpcReply _) =>
        {
            var name = p.GetString("name");
            var target = p.GetString("target");
            if (p.Failed || _desktop is not { } desktop)
            {
                return;
            }

            var icon = desktop.Icons.FirstOrDefault(i => i.Label == name);
            var folder = target == "trash" ? ":trash" : target;
            if (icon is null)
            {
                _report.Line($"DROP missing {name}");
                return;
            }

            Transfer([icon.Path], folder, copy: false);
            _report.Line($"DROP {name} {target}");
        });

        _report.Register(methods, "system7wm/ui-click", "ui-click {key} {name}", (ref IpcParams p, IpcReply _) =>
        {
            var key = p.GetString("key");
            var name = p.GetString("name");
            if (!p.Failed)
            {
                _report.Line(ClickControl(key, name) ? $"CLICK {key} {name}" : $"CLICK missing {key} {name}");
            }
        });

        RegisterTerminalCommands(methods);

        _report.Register(methods, "system7wm/activate", "activate {id:int}", (ref IpcParams p, IpcReply _) =>
        {
            var id = (int)p.GetInt("id");
            if (!p.Failed && _stack.FirstOrDefault(w => w.Id == id) is { } window)
            {
                Activate(window);
            }
        });
    }

    private bool ClickControl(string key, string name)
    {
        _uiDriver.Pump();
        Avalonia.Controls.Control? root = key == "desktop" ? _desktop : ShellWindowByKey(key)?.ShellSurface?.Root;
        var surface = key == "desktop" ? _desktopSurface : ShellWindowByKey(key)?.ShellSurface;
        if (root is null || surface is null)
        {
            return false;
        }

        var target = root.Name == name ? root : Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<Avalonia.Controls.Control>().FirstOrDefault(c => c.Name == name);
        if (target is null || Avalonia.VisualExtensions.TranslatePoint(target, new Avalonia.Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root) is not { } center)
        {
            return false;
        }

        var view = MainView ?? _outputs.Views[0];
        var box = _layout.BoxOf(view.Output);
        var scale = view.Output.Scale;
        var n = surface.ChromeScale;
        var deviceX = surface.Device.X + (center.X * n);
        var deviceY = surface.Device.Y + (center.Y * n);
        _seat.Injector.Warp(box.X + (deviceX / scale), box.Y + (deviceY / scale));
        _seat.Injector.Button(LeftButton, true);
        _seat.Injector.Button(LeftButton, false);
        return true;
    }

    private void PrintMenu(string label, Basin.AppMenu.AppMenuItem item, int depth)
    {
        var indent = new string(' ', depth * 2);
        var shortcut = item.Shortcut is { } s ? $" {s.Modifiers}+{s.Keysym}" : "";
        var state = item.Kind switch
        {
            Basin.AppMenu.AppMenuItemKind.Separator => " ---",
            Basin.AppMenu.AppMenuItemKind.Check or Basin.AppMenu.AppMenuItemKind.Radio => item.ToggleState ? " [x]" : " [ ]",
            _ => "",
        };
        _report.Line($"{indent}ITEM \"{label}\"{state}{shortcut}{(item.Enabled ? "" : " disabled")}{(item.IsSubmenu ? " >" : "")}");
        foreach (var child in item.Children)
        {
            PrintMenu(child.Label, child, depth + 1);
        }
    }
}
