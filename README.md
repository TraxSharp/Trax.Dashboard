# Trax.Dashboard

[![Build](https://github.com/TraxSharp/Trax.Dashboard/actions/workflows/nuget_release.yml/badge.svg?branch=main)](https://github.com/TraxSharp/Trax.Dashboard/actions/workflows/nuget_release.yml?query=branch%3Amain)
[![NuGet](https://img.shields.io/nuget/v/Trax.Dashboard)](https://www.nuget.org/packages/Trax.Dashboard)
[![codecov](https://codecov.io/gh/TraxSharp/Trax.Dashboard/branch/main/graph/badge.svg)](https://codecov.io/gh/TraxSharp/Trax.Dashboard)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/TraxSharp/Trax.Dashboard/blob/main/LICENSE)
[![Docs](https://img.shields.io/badge/docs-traxsharp.net-blue)](https://traxsharp.net/docs/dashboard)

> Part of [Trax](https://github.com/TraxSharp): business logic you can call, schedule, or serve as an API, with every
> run recorded in your Postgres. [Docs](https://traxsharp.net/docs) · [Getting started](https://traxsharp.net/docs/getting-started) · [All repos](https://github.com/TraxSharp)

Trax.Dashboard is a Blazor Server dashboard for Trax runs, schedules, dead letters and the work queue. It mounts at
`/trax` inside the ASP.NET Core app that already hosts Trax, so there is no separate service to deploy. It builds on
[Trax.Api](https://github.com/TraxSharp/Trax.Api) and [Trax.Scheduler](https://github.com/TraxSharp/Trax.Scheduler).

<!-- screenshot: dashboard run view -->

## Install

```bash
dotnet add package Trax.Dashboard
dotnet add package Trax.Effect.Data.Postgres   # storage the example below reads from
```

## Example

```csharp
builder.Services.AddTrax(trax =>
    trax.AddEffects(effects => effects.UsePostgres(connectionString))
        .AddMediator(typeof(Program).Assembly));

builder.Services.AddAuthorization(o =>
    o.AddPolicy("TraxAdmin", p => p.RequireRole("Admin")));

// Must follow AddTrax; it throws otherwise.
builder.AddTraxDashboard(dashboard => dashboard.RequirePolicy("TraxAdmin"));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseTraxDashboard();   // mounts at /trax
```

The host's csproj must opt into the framework's Blazor web assets. Without it `_framework/blazor.web.js` is missing
and the dashboard renders but ignores every click:

```xml
<PropertyGroup>
  <RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>
</PropertyGroup>
```

Call `AddTraxDashboard` on the `WebApplicationBuilder`, not on `builder.Services`: only that overload serves the
dashboard's CSS and JS outside Development. The pages always live under `/trax`, so leave the route prefix of
`UseTraxDashboard()` at its default.

## Authorization

The dashboard can queue, run and cancel trains and change scheduler settings, so `UseTraxDashboard()` refuses to start
until you say who may use it. The posture covers every endpoint the dashboard maps, the Blazor circuit hub included.

| Option | Means |
|---|---|
| `RequirePolicy("name")` | The caller must satisfy a policy registered with `AddAuthorization`; the host fails at startup if it is missing |
| `RequireRoles("Admin", "Ops")` | The caller must hold one of the roles. Combined with `RequirePolicy`, both must pass |
| `AllowAnonymousDashboard()` | No gate of its own, for a host that protects `/trax` another way (a fallback policy, an ingress rule). Logs a warning on every start |

## Pages

| Route | Shows |
|---|---|
| `/trax` | Server health, run counts by state, runs over time, recent failures and throughput |
| `/trax/trains` | Every registered train with its input and output types; run or queue one with new input |
| `/trax/data/metadata` | The run records: filter by state, train or time, open a run to read the failing junction and exception, cancel a running one |
| `/trax/data/logs` | Logs captured during runs |
| `/trax/data/manifests` | Schedules from Trax.Scheduler: cron or interval, retry policy, last run; enable, disable or trigger one |
| `/trax/data/manifest-groups` | Per-group statistics and dispatch settings (max active jobs, priority) |
| `/trax/data/dead-letters` | Runs that exhausted their retries; requeue or acknowledge them |
| `/trax/data/work-queue` | Entries waiting to be dispatched, staged ones included; cancel one or many |
| `/trax/data/persisted-operations` | The GraphQL persisted operations the API accepts; upload, edit, deactivate |
| `/trax/settings/effects` | Turn effect providers on or off and change their settings without a restart |
| `/trax/settings/server` | Scheduler settings: polling interval, max active jobs, worker count, default retry policy |
| `/trax/settings/user` | Per-browser preferences: the refresh interval and which home sections show |

## What it does not do

- It is a Razor Class Library for Interactive Server rendering. It does not run as a standalone app or as WebAssembly.
- It reads the host's own Trax database. One dashboard shows one Trax deployment.

## Where this fits

Trax is split into layers, one repo each. Take the ones you need; the trains you wrote do not change. **You are here: Trax.Dashboard.**

| Repo | What it adds |
|---|---|
| [Trax.Core](https://github.com/TraxSharp/Trax.Core) | Trains, junctions and the chain, with no database and no DI container |
| [Trax.Effect](https://github.com/TraxSharp/Trax.Effect) | A recorded run for every execution (Postgres, SQLite or in memory), DI, effect providers, the state-machine engine |
| [Trax.Mediator](https://github.com/TraxSharp/Trax.Mediator) | The train bus: run a train by handing over its input, with every chain checked at startup |
| [Trax.Scheduler](https://github.com/TraxSharp/Trax.Scheduler) | Cron and interval schedules, retries, dead letters, and workers on other machines or in Lambda |
| [Trax.Api](https://github.com/TraxSharp/Trax.Api) | GraphQL generated from your trains, with authentication, audit and typed clients |
| **[Trax.Dashboard](https://github.com/TraxSharp/Trax.Dashboard)** | **A Blazor Server UI for runs, schedules and dead letters, mounted in your app** |
| [Trax.Cli](https://github.com/TraxSharp/Trax.Cli) | The `trax` tool: scaffold a hub and trains from an OpenAPI or GraphQL schema, and state-machine codegen |
| [Trax.Samples](https://github.com/TraxSharp/Trax.Samples) | Complete sample apps, and the `trax-api`, `trax-scheduler` and `trax-hub` templates |

Docs live in [Trax.Docs](https://github.com/TraxSharp/Trax.Docs) and are published at [traxsharp.net/docs](https://traxsharp.net/docs).

## Documentation

- [Dashboard](https://traxsharp.net/docs/dashboard): setup, pages and authorization
- [AddTraxDashboard](https://traxsharp.net/docs/sdk-reference/dashboard-api/add-trax-dashboard)
- [UseTraxDashboard](https://traxsharp.net/docs/sdk-reference/dashboard-api/use-trax-dashboard)
- [DashboardOptions](https://traxsharp.net/docs/sdk-reference/dashboard-api/dashboard-options)
- [What gets recorded](https://traxsharp.net/docs/effect/metadata)

## Contributing

Read [AGENTS.md](https://github.com/TraxSharp/Trax.Dashboard/blob/main/AGENTS.md) before changing code. Report
vulnerabilities privately as described in [SECURITY.md](https://github.com/TraxSharp/Trax.Dashboard/blob/main/SECURITY.md).

## License

MIT. There is no commercial edition, and there will not be one.

Trax is an independent open-source project and is not affiliated with the Utah Transit Authority, Trax Retail, or any
other organization using the Trax name.
