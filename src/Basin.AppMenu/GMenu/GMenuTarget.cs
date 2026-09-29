using Tmds.DBus.Protocol;

namespace Basin.AppMenu;

internal sealed record GMenuTarget(string Action, VariantValue? Target);
