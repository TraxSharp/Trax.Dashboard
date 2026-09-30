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

The host's csproj also needs `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`; without it the dashboard renders
but ignores every click.

## License

MIT. There is no commercial edition, and there will not be one.

Trax is an independent open-source project and is not affiliated with the Utah Transit Authority, Trax Retail, or any
other organization using the Trax name.
