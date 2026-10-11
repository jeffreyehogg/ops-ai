using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OpsAI.Core.Interfaces;
using OpsAI.Core.Models;

namespace OpsAI.Infrastructure.Channels;

public sealed class IncidentChannel : IIncidentChannel
{
    private readonly Channel<Incident> _channel;
    private readonly ILogger<IncidentChannel> _logger;

    public IncidentChannel(ILogger<IncidentChannel> logger, int capacity = 10_000)
    {
        _logger = logger;
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Incident>(options);
    }

    public async ValueTask<bool> EnqueueAsync(Incident incident, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        try
        {
            await _channel.Writer.WriteAsync(incident, ct);
            _logger.LogDebug("Incident {IncidentId} enqueued to triage channel.", incident.Id);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Enqueue operation for incident {IncidentId} was canceled.", incident.Id);
            return false;
        }
    }

    public IAsyncEnumerable<Incident> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
