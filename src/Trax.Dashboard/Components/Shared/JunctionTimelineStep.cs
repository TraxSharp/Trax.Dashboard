using Trax.Api.DTOs;
using Trax.Effect.Models.JunctionRun;

namespace Trax.Dashboard.Components.Shared;

/// <summary>
/// One row of the run page's junction timeline: the step as the API maps it, plus whether its
/// junction name is withheld and the routing step whose track it ran on. Part of the dashboard UI;
/// not intended to be used directly.
/// </summary>
/// <param name="Step">The step, as <see cref="JunctionStep.From(JunctionRun)"/> maps it.</param>
/// <param name="NameWithheld">
/// True when the junction ran after a routing step whose answer is withheld, so its name would give
/// the answer away and is never shown.
/// </param>
/// <param name="TrackPosition">
/// The position of the latest routing step the run took before this junction, or null before any.
/// </param>
public sealed record JunctionTimelineStep(JunctionStep Step, bool NameWithheld, int? TrackPosition)
{
    /// <summary>The row for a recorded <c>trax.junction_run</c> row.</summary>
    /// <param name="row">The row.</param>
    // JunctionStep does not carry the two flags yet, so they are read from the row here, the one
    // place to change when it does.
    public static JunctionTimelineStep From(JunctionRun row) =>
        new(JunctionStep.From(row), row.NameWithheld, row.TrackPosition);
}
