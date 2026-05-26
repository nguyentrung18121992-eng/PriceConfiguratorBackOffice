using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Nobia.Backend.ApplicationInsights.AspNetCore;
using Nobia.Backend.Authentication;
using Nobia.Backend.DataProtection;
using Nobia.Backend.LoginSupport;
using Nobia.CmsToolkit;
using Nobia.CmsToolkit.Entity;
using Nobia.CmsToolkit.EditingPage.FrontendDependency;
using Nobia.CmsToolkit.Models;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Infrastructure;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.Services;
using System.Text.Json.Serialization;
using Nobia.Backend.Configuration;

namespace PriceConfiguratorBackoffice;

public static class Startup
{
    public static void ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .ConfigureDataProtection(configuration)
            .ConfigureApplicationInsights(
                configuration,
                "price-configurator-back-office",
                errorCodesToBeTreatedAsSuccess: [StatusCodes.Status404NotFound]);

        services
            .AddHttpClient()
            .AddHttpContextAccessor()
            .AddHealthChecks();

        services.AddRazorPages();

        services
            .AddMvc()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = null;
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .AddApplicationPart(typeof(CmsToolkitHandle).Assembly);

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddAuthScriptsApiLogin(o =>
            {
                o.SetAllowedProviders(AuthScriptsApiAuthenticationDefaults.Providers.Azure);
            });

        services.AddNobiaAuthorization(o => o.AddCmsToolkitPolicy());

        services.AddCors(options =>
            options.AddDefaultPolicy(builder =>
                builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = _ => false;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.AddOpenApiDocument(c =>
        {
            c.Title = "Price Configurator Back Office";
            c.SchemaSettings.UseXmlDocumentation = true;
        });

        services.AddSingleton<IConfigSnapshotBuilder, ConfigSnapshotBuilder>();
        services.AddSingleton<ConfiguratorMessagesTemplateProvider>();
        services.AddSingleton<RangePriceSetPreSaveListener>();
        services.AddSingleton<ConfiguratorCardPreSaveListener>();
        services.AddSingleton<ConfiguratorMessagesPreSaveListener>();
        services.AddSingleton<Nobia.CmsToolkit.Context.IPreSaveContextChangeListener, CompositePreSaveListener>();
        services.AddScoped<IPublishedConfigurationQuery, PublishedConfigurationQuery>();
        services.AddScoped<IPublishService, PublishService>();
        services.AddScoped<IPreviewTokenService, PreviewTokenService>();
        services.AddScoped<ISeedImportService, SeedImportService>();
        var cosmosConnection = CosmosConnectionHelper.Resolve(configuration);
        if (!CosmosConnectionHelper.IsValid(cosmosConnection))
        {
            throw new InvalidOperationException(CosmosConnectionHelper.GetValidationMessage());
        }

        Console.WriteLine(
            $"[Cosmos] Using endpoint {CosmosConnectionHelper.DescribeEndpoint(cosmosConnection)}");

        if (!CloudinaryConfigurationHelper.IsConfigured(configuration))
        {
            Console.WriteLine($"[Cloudinary] {CloudinaryConfigurationHelper.GetMissingKeysMessage(configuration)}");
        }
        else if (CloudinaryConfigurationHelper.UsesKnownInvalidCloudName(configuration))
        {
            Console.WriteLine($"[Cloudinary] ERROR: {CloudinaryConfigurationHelper.GetInvalidCloudNameMessage()}");
        }
        else
        {
            Console.WriteLine($"[Cloudinary] cloud={configuration["CloudinaryName"]}, login={configuration["CloudinaryLogin"]}");
        }

        services.AddHostedService<ScheduledPublishHostedService>();

        services.AddCmsToolkit(
            o => o.UseCosmos(
                cosmosConnection,
                "PriceConfigurator",
                cosmos => cosmos.ConnectionMode(ConnectionMode.Gateway)),
            c =>
            {
                c.DbContextBuilder = CosmosEntityConfiguration.ConfigureModel;
                c.UseMemoryCache = true;
                c.AdminTools.MigrationEnabled = true;
                c.MetaData.TopLinks.Add(new SiteLink("Import from seed", "ImportSeed"));
                c.MetaData.TopLinks.Add(new SiteLink("Publish", "Publish"));

                string[] admins =
                [
                    NobiaRole.GroupAdmin,
                    "NOBG-PriceConfiguratorBackoffice-Group-Admins",
                ];

                c.AddBrand(Constants.Brands.Magnet,
                    [.. admins, "MagnetAdmin", "NOBG-PriceConfiguratorBackoffice-Magnet-Admins", "NOBIANET/Magnet-CMS-Admins"],
                    ContentLanguage.en);

                c.AddBrand(Constants.Brands.Marbodal,
                    [.. admins, "Marbadmin", "NOBG-PriceConfiguratorBackoffice-Marbodal-Admins", "NOBIANET/Marb-cms-admins"],
                    ContentLanguage.sv);

                c.AddBrand(Constants.Brands.Invita,
                    [.. admins, "InvitaAdmin", "NOBG-PriceConfiguratorBackoffice-Invita-Admins", "NOBIANET/Invita-CMS-Admins"],
                    ContentLanguage.da);

                c.AddBrand(Constants.Brands.Sigdal,
                    [.. admins, "SigdalAdmin", "NOBG-PriceConfiguratorBackoffice-Sigdal-Admins", "NOBIANET/Sigdal-CMS-Admins"],
                    ContentLanguage.no);

                c.AddBrand(Constants.Brands.Norema,
                    [.. admins, "NoremaAdmin", "NOBG-PriceConfiguratorBackoffice-Norema-Admins", "NOBIANET/Norema-CMS-Admins"],
                    ContentLanguage.no);

                c.AddBrand(Constants.Brands.Novart,
                    [.. admins, "Novartadmin", "NOBG-PriceConfiguratorBackoffice-Novart-Admins", "NOBIANET/Novart-cms-admins"],
                    ContentLanguage.fi);

                c.AddCmsEntity<ConfiguratorSettings>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<ConfiguratorMessages>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<ConfiguratorSection>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<ConfiguratorCard>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<RangePriceSet>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<PublishedConfiguration>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<ScheduledPublish>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);
                c.AddCmsEntity<PreviewToken>(CosmosEntityConfiguration.ConfigureBrandPartitionKey);

                c.AddFrontendDependency(FrontendDependencyType.StyleSheet, "/css/card-payload-editors.css");
                c.AddFrontendDependency(FrontendDependencyType.JavaScript, "/js/card-payload-editors.js");
                c.AddFrontendDependency(FrontendDependencyType.StyleSheet, "/css/configurator-messages-editor.css");
                c.AddFrontendDependency(FrontendDependencyType.JavaScript, "/js/configurator-messages-editor.js");
                c.AddFrontendDependency(FrontendDependencyType.StyleSheet, "/css/tooltip-description-list.css");
                c.AddFrontendDependency(FrontendDependencyType.JavaScript, "/js/tooltip-description-list.js");
            });

        services.AddSingleton<IValueConverter, InvariantCultureValueConverter>();
    }

    public static void ConfigureApplication(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (EnvironmentHelper.IsLocal() || env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseForwardedHeaders();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
        });

        if (!env.IsProduction())
        {
            app.UseOpenApi();
            app.UseSwaggerUi();
        }

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapAuthScriptsApiLogin();
            endpoints.MapHealthChecks("/api/healthcheck");
            endpoints.MapRazorPages();
            endpoints.MapControllers();
            endpoints.MapGet("/account/accessdenied", async c =>
            {
                if (c.User.Identity?.IsAuthenticated ?? false)
                {
                    await c.Response.WriteAsync("You are not allowed to view this page");
                }
                else
                {
                    c.Response.Redirect("/");
                }
            });
        });
    }
}
