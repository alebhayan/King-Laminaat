namespace FSH.Framework.Web.RateLimiting;

public sealed class RateLimitingOptions
{
    public bool Enabled { get; set; } = true;
    public FixedWindowPolicyOptions Global { get; set; } = new();
    public FixedWindowPolicyOptions Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60, QueueLimit = 0 };
}

