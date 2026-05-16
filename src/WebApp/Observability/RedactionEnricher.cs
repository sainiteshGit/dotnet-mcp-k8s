using Serilog.Core;
using Serilog.Events;

namespace WebApp.Observability;

/// <summary>
/// Serilog enricher applying the shared <see cref="Redactor"/> policy to every
/// scalar property on the event. Property keys named as secrets get their value
/// replaced with the marker; remaining string values are regex-redacted.
/// </summary>
public sealed class RedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // Snapshot keys because we mutate the dictionary during iteration.
        foreach (var key in logEvent.Properties.Keys.ToArray())
        {
            var value = logEvent.Properties[key];

            if (Redactor.ShouldRedactKey(key))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, Redactor.Marker));
                continue;
            }

            if (value is ScalarValue { Value: string s })
            {
                var redacted = Redactor.RedactValue(s);
                if (!ReferenceEquals(redacted, s))
                {
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, redacted));
                }
            }
        }
    }
}
