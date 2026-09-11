---
authors: [Theauxm]
areas: [platform, ui]
status: accepted
---

# The dashboard mounts into the host's application

`Trax.Dashboard` is a Blazor Server component library, not a deployable. A host calls
`AddTraxDashboard()` and `UseTraxDashboard()` and the pages appear inside its own
application, under `/trax` by default. There is no separate process, no separate port, and
no separate deployment.

## Status

**Accepted.**

## Considered options

**A standalone dashboard application**, which is what most job schedulers ship. It is the
obvious shape and it was rejected because of what it drags in: its own host, its own
deployment, its own TLS and ingress, and above all **its own authentication**. A separate
app looking at the same database has to be told who is allowed to see it, and that is a
second auth system to configure and get wrong.

Mounting into the host means the dashboard is behind whatever the host already does. If the
application requires a login, so does `/trax`, with no configuration at all.

## Consequences

**`UseTraxDashboard()` mutates the host's application.** It calls `UseStaticFiles()`,
`UseAntiforgery()` and `MapStaticAssets()`, maps Razor components with the interactive server
render mode, and writes the route prefix, title and environment name into the shared
`DashboardOptions` singleton. Those are side effects on somebody else's application, and a
host that already calls that middleware gets it twice.

**The host must be a Blazor-capable ASP.NET Core app.** Interactive server components need a
circuit, which means SignalR and sticky sessions if the host scales out. A pure Web API
host cannot take the dashboard without becoming something else.

**Radzen is a transitive dependency of every consumer** that mounts it, and its component
styles land in the host's static asset pipeline.

**`AddTrax()` must come first**, and saying so is the one ordering rule this package
enforces. `AddTraxDashboard()` checks for the `TraxMarker` and throws naming the call to add,
which is the precondition shape `api/0002` describes as safe: it cannot silently change
behaviour, it can only refuse.

## Exemplars

- `DashboardServiceExtensionsTests` pins the registration half: the options that land, the
  defaults (`/trax`), the scoped services, that the dashboard registers **no** train
  discovery of its own and therefore depends on the host's `AddTrax()` for it, and that the
  `TraxMarker` precondition throws with a message naming the call to add.

Not covered: **nothing asserts what `UseTraxDashboard()` does to the host's pipeline.** The
suite covers `AddTraxDashboard` and stops there. A change to the middleware it adds, or to
the order it adds it in, is invisible here and visible to every consumer, which is the half
of this decision that reaches furthest into somebody else's application.

## Changelog

- **2026-09-11**: Listed the two side effects the Consequences section had omitted, in a section whose point is enumerating them.
- **2026-09-11**: Corrected a false gap: the TraxMarker precondition is tested, including
  that its message names the call to add. Only the UseTraxDashboard half is uncovered.
- **2026-09-11**: Recorded.
