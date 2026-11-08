namespace SchoolAdmission.Api.Models;

/// <summary>
/// Status values for seat reservation holds.
/// </summary>
public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}
