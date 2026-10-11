using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpsAI.Core.Enums;
using OpsAI.Core.Models;
using OpsAI.Infrastructure.Channels;

namespace OpsAI.Tests;

public sealed class IncidentChannelTests
{
    [Fact]
    public async Task EnqueueAndRead_ShouldSafelyBufferAndDrainItems()
    {
        var channel = new IncidentChannel(NullLogger<IncidentChannel>.Instance, capacity: 50);

        var incident1 = new Incident { ServiceName = "svc-1", Title = "Error 1", Type = TelemetryType.UnhandledException };
        var incident2 = new Incident { ServiceName = "svc-2", Title = "Error 2", Type = TelemetryType.SlowQuery };

        var write1 = await channel.EnqueueAsync(incident1);
        var write2 = await channel.EnqueueAsync(incident2);

        write1.Should().BeTrue();
        write2.Should().BeTrue();

        channel.Complete();

        var readItems = new List<Incident>();
        await foreach (var item in channel.ReadAllAsync())
        {
            readItems.Add(item);
        }

        readItems.Should().HaveCount(2);
        readItems[0].Id.Should().Be(incident1.Id);
        readItems[1].Id.Should().Be(incident2.Id);
    }
}
