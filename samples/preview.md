# Observability and operations

Every theme's listing screenshot is taken from this note, so the gallery compares like with like: the sidebar open on the outline, the editor and the preview side by side.

## Telemetry pipeline

The **collector** receives spans, metrics and logs over OTLP and forwards them to the *store*. A `Marknote.Sync` meter counts documents pushed and pulled; a rising `conflict_copy` count is the single most important number.

> Alert on symptoms, not causes: a slow sync is a page, a full disk is a ticket.

```csharp
var meter = new Meter("Marknote.Sync");
var pushed = meter.CreateCounter<long>("documents.pushed");
pushed.Add(1, new KeyValuePair<string, object?>("vault", vaultId));
```

- [x] Traces sampled at 10 %
- [ ] Logs retained for 30 days
- [ ] Runbook for [health checks](#health-checks)

| Signal | Source | Retention |
| --- | --- | --- |
| Traces | collector | 7 days |
| Metrics | Prometheus | 90 days |
| Logs | Loki | 30 days |

## Health checks

Liveness answers *is the process up*; readiness answers *can it serve*. The two must never share a probe, and the equation $E = mc^2$ has nothing to do with either.
