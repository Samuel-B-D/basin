using Basin.Capabilities;
using Basin.Config;

namespace System7Wm;

internal sealed partial class System7Shell
{
    private void ApplyInput()
    {
        var settings = new List<(InputSetting, InputSettingValue)>
        {
            (InputSetting.RepeatRate, InputSettingValue.Of((uint)_config.RepeatRate)),
            (InputSetting.RepeatDelay, InputSettingValue.Of((uint)_config.RepeatDelay)),
        };
        if (_config.Tracking is { } tracking)
        {
            settings.Add((InputSetting.AccelSpeed, InputSettingValue.Number((tracking * 2) - 1)));
        }

        _inputConfigurator.Apply([new InputRule { Match = default, Settings = settings }]);
    }

    internal void SaveSetting(string table, string key, TomlValue value)
    {
        if (_config.Path is not { } path)
        {
            return;
        }

        try
        {
            var document = File.Exists(path) ? TomlDocument.Load(path) : TomlDocument.Parse("");
            document.Set(table, key, value);
            document.Save(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _log.Warn($"cannot write {table}.{key} to {path}: {error.Message}");
        }
    }
}
