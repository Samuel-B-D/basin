using Basin;
using Basin.Capabilities;
using Wayland.Server;

namespace LunaWm;

internal sealed class LunaActivationTokens : IActivationTokens
{
    private readonly Dictionary<string, string?> _client = [];
    private readonly HashSet<string> _shell = [];
    private int _counter;

    public event Action<string, Surface, bool>? Redeemed;

    public string Mint(WlClient client, Surface? requestingSurface, uint serial, string? appId)
    {
        ArgumentNullException.ThrowIfNull(client);
        var token = $"luna-{++_counter}";
        _client[token] = appId;
        return token;
    }

    public string MintShell()
    {
        var token = $"luna-launch-{++_counter}";
        _shell.Add(token);
        return token;
    }

    public bool Redeem(string token, Surface target)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(target);
        if (_shell.Remove(token))
        {
            Redeemed?.Invoke(token, target, true);
            return true;
        }

        if (_client.Remove(token))
        {
            Redeemed?.Invoke(token, target, false);
            return true;
        }

        return false;
    }

    public void ExpireAll()
    {
        _client.Clear();
        _shell.Clear();
    }
}
