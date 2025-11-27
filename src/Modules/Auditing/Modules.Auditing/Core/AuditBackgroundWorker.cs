using FSH.Modules.Auditing.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Auditing;

/// <summary>
/// Drains the channel and writes to the configured sink in batches.
/// </summary>
public sealed class AuditBackgroundWorker : BackgroundService
{
    private readonly ChannelAuditPublisher _publisher;
    private readonly IAuditSink _sink;
    private readonly ILogger<AuditBackgroundWorker> _logger;

    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    public AuditBackgroundWorker(
        ChannelAuditPublisher publisher,
        IAuditSink sink,
        ILogger<AuditBackgroundWorker> logger,
        int batchSize = 200,
        int flushIntervalMs = 1000)
    {
        _publisher = publisher;
        _sink = sink;
        _logger = logger;
        _batchSize = Math.Max(1, batchSize);
        _flushInterval = TimeSpan.FromMilliseconds(Math.Max(50, flushIntervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _publisher.Reader;
        var batch = new List<AuditEnvelope>(_batchSize);

        // Single delay task we reuse/reset to avoid concurrent waits.
        Task delayTask = Task.Delay(_flushInterval, stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Greedily drain whatever is available, up to batch size.
                while (batch.Count < _batchSize && reader.TryRead(out var item))
                    batch.Add(item);

                // If we've filled the batch, flush immediately.
                if (batch.Count >= _batchSize)
                {
                    await FlushAsync(batch, stoppingToken);
                    delayTask = Task.Delay(_flushInterval, stoppingToken); // reset window after flush
                    continue;
                }

                // If we have nothing yet, wait for either data to arrive or the flush window to elapse.
                var readTask = reader.WaitToReadAsync(stoppingToken).AsTask();
                var winner = await Task.WhenAny(readTask, delayTask);

                if (winner == readTask)
                {
                    // If channel is completed, exit the loop.
                    if (!await readTask.ConfigureAwait(false))
                        break;

                    // Loop back to drain newly available items (no flush yet).
                    continue;
                }

                // Timer window elapsed: flush whatever we have (if any) and open a new window.
                if (batch.Count > 0)
                    await FlushAsync(batch, stoppingToken);

                delayTask = Task.Delay(_flushInterval, stoppingToken); // start a fresh window
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit background worker crashed.");
        }

        // Best-effort final flush on shutdown.
        if (batch.Count > 0 && !stoppingToken.IsCancellationRequested)
        {
            try { await _sink.WriteAsync(batch, stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Final audit flush failed."); }
        }
    }

    private async Task FlushAsync(List<AuditEnvelope> batch, CancellationToken ct)
    {
        try
        {
            await _sink.WriteAsync(batch, ct);
        }
        catch (Exception ex)
        {
            // Don't crash the worker; log and keep going.
            _logger.LogError(ex, "Audit background flush failed.");
            await Task.Delay(250, ct);
        }
        finally
        {
            batch.Clear();
        }
    }
}

