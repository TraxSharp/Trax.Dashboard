namespace Trax.Dashboard.Services.DashboardSettings;

public interface IDashboardSettingsService
{
    TimeSpan PollingInterval { get; }
    DateTime LastPollTime { get; }
    bool HideAdminTrains { get; }
    IReadOnlyList<string> AdminTrainNames { get; }
    Task InitializeAsync();
    Task SetPollingIntervalAsync(int seconds);
    Task SetHideAdminTrainsAsync(bool hide);
    void NotifyPolled();

    /// <summary>
    /// The message of the most recent refresh that failed, or <c>null</c> once a refresh
    /// succeeds. While it is set, the data on screen is from the last refresh that worked.
    /// </summary>
    string? LastPollError => null;

    /// <summary>
    /// Records that a page's refresh failed. <see cref="NotifyPolled"/> clears it.
    /// </summary>
    void NotifyPollFailed(string message) { }

    // Dashboard component visibility
    bool ShowSummaryCards { get; }
    bool ShowExecutionsChart { get; }
    bool ShowFailures { get; }
    bool ShowAvgDuration { get; }
    bool ShowServerHealth { get; }
    Task SetComponentVisibilityAsync(string key, bool visible);
}
