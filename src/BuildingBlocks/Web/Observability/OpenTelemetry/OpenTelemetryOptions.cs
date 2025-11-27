using System.ComponentModel.DataAnnotations;

namespace FSH.Framework.Web.Observability.OpenTelemetry;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetryOptions";

    /// <summary>
    /// Global switch to turn OpenTelemetry on/off.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public TracingOptions Tracing { get; set; } = new();

    public MetricsOptions Metrics { get; set; } = new();

    public ExporterOptions Exporter { get; set; } = new();

    public sealed class TracingOptions
    {
        public bool Enabled { get; set; } = true;
    }

    public sealed class MetricsOptions
    {
        public bool Enabled { get; set; } = true;
        public string[]? MeterNames { get; set; }
    }

    public sealed class ExporterOptions
    {
        public OtlpOptions Otlp { get; set; } = new();
    }

    public sealed class OtlpOptions
    {
        public bool Enabled { get; set; } = true;

        [Url]
        public string? Endpoint { get; set; }

        /// <summary>
        /// Transport protocol, e.g. "grpc" or "http/protobuf".
        /// </summary>
        public string? Protocol { get; set; }
    }

}
