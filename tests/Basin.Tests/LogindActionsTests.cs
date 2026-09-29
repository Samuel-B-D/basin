using Basin.Session;
using Tmds.DBus.Protocol;
using Xunit;

namespace Basin.Tests;

public sealed class LogindActionsTests
{
    private sealed class FakeLogind : IPathMethodHandler
    {
        public string Path => "/org/freedesktop/login1";

        public bool HandlesChildPaths => false;

        public List<(string Method, bool Interactive)> Calls { get; } = [];

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            var member = context.Request.MemberAsString ?? "";
            if (member.StartsWith("Can", StringComparison.Ordinal))
            {
                using var reply = context.CreateReplyWriter("s");
                reply.WriteString(member == "CanSuspend" ? "na" : "yes");
                context.Reply(reply.CreateMessage());
                return ValueTask.CompletedTask;
            }

            var interactive = context.Request.GetBodyReader().ReadBool();
            lock (Calls)
            {
                Calls.Add((member, interactive));
            }

            using var writer = context.CreateReplyWriter(null);
            context.Reply(writer.CreateMessage());
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Each_action_calls_the_manager_without_interaction()
    {
        using var daemon = PrivateBus.Start();
        using var service = new DBusConnection(daemon.Address);
        await service.ConnectAsync();
        var logind = new FakeLogind();
        service.AddMethodHandler(logind);
        await service.RequestNameAsync("org.freedesktop.login1", RequestNameOptions.None);

        Assert.True(await LogindActions.RunAsync(LogindAction.Reboot, daemon.Address));
        Assert.True(await LogindActions.RunAsync(LogindAction.PowerOff, daemon.Address));
        Assert.True(await LogindActions.RunAsync(LogindAction.Suspend, daemon.Address));
        Assert.Equal([("Reboot", false), ("PowerOff", false), ("Suspend", false)], logind.Calls);

        Assert.True(await LogindActions.CanAsync(LogindAction.Reboot, daemon.Address));
        Assert.False(await LogindActions.CanAsync(LogindAction.Suspend, daemon.Address));
    }

    [Fact]
    public async Task A_bus_without_logind_answers_false()
    {
        using var daemon = PrivateBus.Start();
        Assert.False(await LogindActions.RunAsync(LogindAction.Reboot, daemon.Address));
        Assert.False(await LogindActions.CanAsync(LogindAction.Reboot, daemon.Address));
    }
}
