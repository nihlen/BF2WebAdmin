using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using BF2WebAdmin.Common.External;

namespace BF2WebAdmin.Tests;

/// <summary>
/// Records the measurements of the application's static meter (<see cref="Telemetry.MeterName"/>) that pass a filter. The
/// meter is shared by every test, so the filter picks this test's own (by a server id of its own).
/// </summary>
public sealed class MetricsRecorder : IDisposable
{
    public sealed record Recorded(string Name, double Value, Dictionary<string, object?> Tags);

    private readonly MeterListener _listener = new();
    private readonly Func<Recorded, bool> _filter;
    private readonly List<Recorded> _recorded = new();

    public MetricsRecorder(Func<Recorded, bool> filter)
    {
        _filter = filter;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, Telemetry.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, tags, _) => Add(i, v, tags));
        _listener.SetMeasurementEventCallback<int>((i, v, tags, _) => Add(i, v, tags));
        _listener.SetMeasurementEventCallback<double>((i, v, tags, _) => Add(i, v, tags));
        _listener.Start();
    }

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var recorded = new Recorded(instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value));
        if (!_filter(recorded))
            return;

        lock (_recorded)
            _recorded.Add(recorded);
    }

    /// <summary>The measurements so far of <paramref name="name"/> with these tags.</summary>
    public IReadOnlyList<Recorded> All(string name, params (string Key, object Value)[] tags)
    {
        lock (_recorded)
            return _recorded.Where(r => r.Name == name && tags.All(t => Equals(r.Tags.GetValueOrDefault(t.Key), t.Value))).ToList();
    }

    public double Sum(string name, params (string Key, object Value)[] tags) => All(name, tags).Sum(r => r.Value);

    /// <summary>Reads the observable instruments now and returns only what they gave.</summary>
    public IReadOnlyList<Recorded> Observe()
    {
        int before;
        lock (_recorded)
            before = _recorded.Count;
        _listener.RecordObservableInstruments();
        lock (_recorded)
            return _recorded.Skip(before).ToList();
    }

    public void Dispose() => _listener.Dispose();
}
