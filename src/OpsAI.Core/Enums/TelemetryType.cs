namespace OpsAI.Core.Enums;

public enum TelemetryType
{
    UnhandledException = 0,
    SlowQuery = 1,
    Deadlock = 2,
    DownstreamTimeout = 3,
    ConnectionPoolExhaustion = 4,
    Custom = 5
}
