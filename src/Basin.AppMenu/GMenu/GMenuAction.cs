using Tmds.DBus.Protocol;

namespace Basin.AppMenu;

internal sealed record GMenuAction(bool Enabled, VariantValue? State);
