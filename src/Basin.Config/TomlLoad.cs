using Tomlyn.Model;

namespace Basin.Config;

public readonly record struct TomlLoad(TomlTable? Table, string? File, string? Fatal);
