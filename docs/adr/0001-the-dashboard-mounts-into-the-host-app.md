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

**`UseTraxDashboard()` mutates the host's middleware pipeline.** It calls `UseStaticFiles()`
and `UseAntiforgery()`, and maps Razor components with the interactive server render mode.
Those are side effects on somebody else's application, and a host that already calls them
gets them twice.

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

- `DashboardServiceExtensionsTests` pins the registration contract: the options that land,
  the defaults (`/trax`), and that the dashboard registers **no** train discovery of its own
  and therefore depends on the host's `AddTrax()` for it.

Not covered, and both gaps are in the part that touches the host:

- Nothing tests the `TraxMarker` precondition actually throwing. The check is one `if` and
  no test would notice if it were deleted.
- Nothing asserts what `UseTraxDashboard()` does to the pipeline. A change to the middleware
  it adds, or the order it adds them in, is invisible to the suite and visible to every
  consumer.

## Changelog

- **2026-09-11**: Recorded.
