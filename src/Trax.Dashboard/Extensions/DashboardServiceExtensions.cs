using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radzen;
using Trax.Dashboard.Components;
using Trax.Dashboard.Configuration;
using Trax.Dashboard.Services.DashboardSettings;
using Trax.Dashboard.Services.LocalStorage;
using Trax.Dashboard.Services.ThemeState;
using Trax.Effect.Configuration.TraxBuilder;

namespace Trax.Dashboard.Extensions;

public static class DashboardServiceExtensions
{
    /// <summary>
    /// Registers Trax.Core Dashboard services including Radzen components and train discovery.
    /// Also ensures static web assets (CSS, JS) from NuGet packages are available in all environments.
    /// This is the recommended overload for dashboard consumers.
    /// </summary>
    public static WebApplicationBuilder AddTraxDashboard(
        this WebApplicationBuilder builder,
        Action<DashboardOptions>? configure = null
    )
    {
        // UseStaticWebAssets is only called automatically in Development.
        // The dashboard requires it in all environments to serve Radzen CSS/JS and
        // dashboard assets from NuGet packages via _content/ paths.
        // This is idempotent and no-ops when the manifest is absent (e.g. published apps).
        if (!builder.Environment.IsDevelopment())
            builder.WebHost.UseStaticWebAssets();

        // Add a MemoryConfigurationSource as the last (highest priority) source so that
        // runtime configuration overrides (e.g. log level changes from the dashboard)
        // survive IConfigurationRoot.Reload() — the memory provider's Load() is a no-op.
        builder.Configuration.AddInMemoryCollection();

        builder.Services.AddTraxDashboard(configure);
        return builder;
    }

    /// <summary>
    /// Registers Trax.Core Dashboard services including Radzen components and train discovery.
    /// When using this overload, ensure static web assets are configured for non-Development environments
    /// by calling <c>builder.WebHost.UseStaticWebAssets()</c> before <c>builder.Build()</c>.
    /// Prefer the <see cref="AddTraxDashboard(WebApplicationBuilder, Action{DashboardOptions}?)"/> overload instead.
    /// </summary>
    public static IServiceCollection AddTraxDashboard(
        this IServiceCollection services,
        Action<DashboardOptions>? configure = null
    )
    {
        if (!services.Any(sd => sd.ServiceType == typeof(TraxMarker)))
            throw new InvalidOperationException(
                "AddTraxDashboard() requires AddTrax() to be called first. "
                    + "Call services.AddTrax(trax => ...) before services.AddTraxDashboard()."
            );

        var options = new DashboardOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        services.AddScoped<ILocalStorageService, LocalStorageService>();
        services.AddScoped<IThemeStateService, ThemeStateService>();
        services.AddScoped<IDashboardSettingsService, DashboardSettingsService>();

        services.AddRadzenComponents();

        services.AddRazorComponents().AddInteractiveServerComponents();

        return services;
    }

    /// <summary>
    /// Maps the Trax.Core Dashboard Blazor components at the configured route prefix, and
    /// applies the authorization posture chosen in <c>AddTraxDashboard</c> to every endpoint
    /// they map: the pages and the Blazor circuit hub.
    /// </summary>
    /// <returns>
    /// The convention builder for those endpoints, so the host can add its own conventions
    /// (<c>RequireHost</c>, rate limiting, a further <c>RequireAuthorization</c>). Conventions
    /// added there are additive; they do not replace the posture.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// No posture was chosen (call <see cref="DashboardOptions.RequirePolicy"/>,
    /// <see cref="DashboardOptions.RequireRoles"/> or
    /// <see cref="DashboardOptions.AllowAnonymousDashboard"/>), or the named policy is not
    /// registered.
    /// </exception>
    public static RazorComponentsEndpointConventionBuilder UseTraxDashboard(
        this WebApplication app,
        string routePrefix = "/trax",
        string? title = null
    )
    {
        routePrefix = "/" + routePrefix.Trim('/');

        var options = app.Services.GetRequiredService<DashboardOptions>();
        VerifyAuthorizationPosture(app, options);

        options.RoutePrefix = routePrefix;

        if (title is not null)
            options.Title = title;
        options.EnvironmentName = app.Environment.EnvironmentName;

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapStaticAssets();
        var endpoints = app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        if (options.AnonymousAllowed)
        {
            app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Trax.Dashboard")
                .LogWarning(
                    "AllowAnonymousDashboard() is set: the Trax dashboard at {RoutePrefix} "
                        + "applies no authorization of its own, so anyone who can reach it can "
                        + "use every page, including queueing, running and cancelling trains and "
                        + "changing scheduler settings. Use RequirePolicy() or RequireRoles() in "
                        + "AddTraxDashboard() unless something in front of it is the gate.",
                    routePrefix
                );
            return endpoints;
        }

        endpoints.RequireAuthorization(
            new AuthorizeAttribute
            {
                Policy = options.Policy,
                Roles = options.Roles is null ? null : string.Join(",", options.Roles),
            }
        );
        return endpoints;
    }

    private static void VerifyAuthorizationPosture(WebApplication app, DashboardOptions options)
    {
        if (!options.HasAuthorizationPosture)
            throw new InvalidOperationException(
                "UseTraxDashboard() needs to know who may use the dashboard, which can queue, "
                    + "run and cancel trains and change scheduler settings. Choose one in "
                    + "AddTraxDashboard(o => ...): o.RequirePolicy(\"<policy>\"), "
                    + "o.RequireRoles(\"<role>\"), or o.AllowAnonymousDashboard() when something "
                    + "in front of it (a fallback policy, an ingress rule) is the gate."
            );

        if (options.AnonymousAllowed)
            return;

        // AddRazorComponents(), which AddTraxDashboard() calls, registers the authorization
        // services, so only the policy name can be missing here.
        if (
            options.Policy is not null
            && app
                .Services.GetRequiredService<IAuthorizationPolicyProvider>()
                .GetPolicyAsync(options.Policy)
                .GetAwaiter()
                .GetResult()
                is null
        )
            throw new InvalidOperationException(
                $"The dashboard requires the authorization policy '{options.Policy}', which is "
                    + "not registered. Add it with builder.Services.AddAuthorization(o => "
                    + $"o.AddPolicy(\"{options.Policy}\", ...))."
            );
    }
}
