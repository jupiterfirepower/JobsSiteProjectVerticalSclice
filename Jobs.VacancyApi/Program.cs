using System.Reflection;
using Asp.Versioning;
using AutoMapper;
using DotNetEnv;
using HealthChecks.UI.Client;
using HealthChecks.UI.Configuration;
using Jobs.Common.Constants;
using Jobs.Common.Contracts;
using Jobs.Common.Extentions;
using Jobs.Common.Options;
using Jobs.Common.Settings;
using Jobs.Core.Contracts;
using Jobs.Core.Contracts.Providers;
using Jobs.Core.DataModel;
using Jobs.Core.Extentions;
using Jobs.Core.Handlers;
using Jobs.Core.Managers;
using Jobs.Core.Middleware;
using Jobs.Core.Observability.Options;
using Jobs.Core.Providers;
using Jobs.Core.Providers.Vault;
using Jobs.Core.Services;
using Jobs.Entities.Models;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Data;
using Jobs.VacancyApi.Extentions;
using Jobs.VacancyApi.Features.Vacancies;
using Jobs.VacancyApi.Middleware;
using Jobs.VacancyApi.Repository;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using Serilog;
using Steeltoe.Discovery.Client;
using Steeltoe.Discovery.Consul;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog((context, loggerConfiguration) =>
    {
        loggerConfiguration.WriteTo.Console();
        loggerConfiguration.ReadFrom.Configuration(context.Configuration);
    });
    
    Log.Information("Starting WebApi Vacancy Service.");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    builder.Services.AddApiVersionService();

    builder.Services.AddDbContext<JobsDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    // user-secret
    var vacancySecretKey = builder.Configuration["VacancyApiService:SecretKey"];
    Console.WriteLine($"vacancySecretKey: {vacancySecretKey}");
    var vacancyServiceDefApiKey = builder.Configuration["VacancyApiService:DefaultApiKey"];
    
    // Load environment variables from the .env file
    Env.Load();
    Env.TraversePath().Load();
    
    var vaultUri = Environment.GetEnvironmentVariable("VAULT_ADDR");
    var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");

    var vaultSecretsProvider = new VaultSecretProvider(vaultUri, vaultToken );

    /*var vaultSecretKey = await vaultSecretsProvider.GetSecretValueAsync("secrets/services/vacancy", "SecretKey", "secrets");
    Console.WriteLine($"vaultSecretKey: {vaultSecretKey}");
    var vaultDefaultApiKey = await vaultSecretsProvider.GetSecretValueAsync("secrets/services/vacancy", "DefaultApiKey", "secrets");
    Console.WriteLine($"vaultDefaultApiKey: {vaultDefaultApiKey}");*/
    
    CryptOptions cryptOptions = new();

    builder.Configuration
        .GetRequiredSection(nameof(CryptOptions))
        .Bind(cryptOptions);

    builder.Services.AddScoped<IGenericRepository<Vacancy>, VacancyRepository>();
    
    builder.Services.AddScoped<GetVacancies.IVacanciesService, GetVacancies.VacanciesService>();
    builder.Services.AddScoped<GetVacancy.IVacancyService, GetVacancy.GetVacancyService>();
    builder.Services.AddScoped<CreateVacancy.ICreateVacancyService, CreateVacancy.CreateVacancyService>();
    builder.Services.AddScoped<UpdateVacancy.IUpdateVacancyService, UpdateVacancy.UpdateVacancyService>();
    builder.Services.AddScoped<DeleteVacancy.IDeleteVacancyService, DeleteVacancy.DeleteVacancyService>();
    
    builder.Services.AddScoped<IApiKeyStorageServiceProvider, MemoryApiKeyStorageServiceProvider>();
    builder.Services.AddScoped<IApiKeyManagerServiceProvider, ApiKeyManagerServiceProvider>(p =>
    {
        var currentService = p.ResolveWith<ApiKeyManagerServiceProvider>();
        currentService.AddApiKey(new ApiKey { Key = vacancyServiceDefApiKey, Expiration = null });
        return currentService;
    });
    builder.Services.AddScoped<ISecretApiKeyRepository, SecretApiKeyRepository>();
    builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
    builder.Services.AddScoped<IEncryptionService, NaiveEncryptionService>(p => 
        p.ResolveWith<NaiveEncryptionService>(Convert.FromBase64String(cryptOptions.PKey), Convert.FromBase64String(cryptOptions.IV)));
    builder.Services.AddScoped<ISignedNonceService, SignedNonceService>();
    builder.Services.AddScoped<ISecretApiService, SecretApiService>(p => 
        p.ResolveWith<SecretApiService>(vacancySecretKey));
    
    builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly()); // AutoMapper registration
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.WriteIndented = true;
        options.SerializerOptions.IncludeFields = true;
    });

    //builder.Services.AddRateLimiterService();
    builder.Services.AddWindowRateLimiterService();
    
    ObservabilityOptions observabilityOptions = new();

    builder.Configuration
        .GetRequiredSection(nameof(ObservabilityOptions))
        .Bind(observabilityOptions);
    
    builder.AddSerilog(observabilityOptions);
    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(observabilityOptions.ServiceName))
        .AddMetrics(observabilityOptions)
        .AddTracing(observabilityOptions);
    
    // forward headers configuration for reverse proxy
    builder.Services.Configure<ForwardedHeadersOptions>(options => {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddResponseCompressionService();
    
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    
    CorsSettings corsSettings = new();

    builder.Configuration
        .GetRequiredSection(nameof(CorsSettings))
        .Bind(corsSettings);
    
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(build => {
            build.WithOrigins(corsSettings.CorsAllowedOrigins);
            build.AllowAnyMethod();
            build.AllowAnyHeader();
        });
    });
    
    builder.Services.AddEndpoints(typeof(Program).Assembly);
    
    // Add HealthChecks
    /*builder.Services.AddHealthChecks();
    builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("NpgConnection")!, 
        name: "Postgres", failureStatus: HealthStatus.Unhealthy, tags: ["Vacancy", "Database"]);
    
    builder.Services.AddHealthChecks().AddDbContextCheck<JobsDbContext>(
        "Categories check",
        customTestQuery: (db, token) => db.Categories.AnyAsync(token),
        tags: ["ef-db"]
    );*/
    
    // Configuring Health Check
    builder.Services.ConfigureHealthChecks(builder.Configuration);
    
    // Service Discovery Consul
    builder.Services.AddServiceDiscovery(o => o.UseConsul());
    
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            //options.HttpsPort = 443;
            options.HttpsPort = 7111;
        });
    }

    var app = builder.Build();
    
    app.UseResponseCompression();
    app.UseForwardedHeaders();
    app.UseMiddleware<AdminSafeListMiddleware>(builder.Configuration["HostsSafeList"]);
    
    // Global Exception Handler.
    app.UseExceptionHandler();
    
    var version1 = new ApiVersion(1);

    var apiVersionSet = app.NewApiVersionSet()
        .HasApiVersion(version1)
        //.HasApiVersion(new ApiVersion(2))
        .ReportApiVersions()
        .Build();

    RouteGroupBuilder versionedGroup = app
        .MapGroup("api/v{version:apiVersion}")
        .WithApiVersionSet(apiVersionSet);

    app.MapEndpoints(versionedGroup);
    
   
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Get the Automapper, we can share this too
    var mapper = app.Services.GetService<IMapper>();
    if (mapper == null)
    {
        throw new InvalidOperationException("Mapper not found");
    }

    //app.UseLogHeaders(); // add here right after you create app
    app.UseExceptionHandlers();
    //app.UseSecurityHeaders();

    //app.UseErrorHandler(); // add here right after you create app
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }
    
    app.UseHttpsRedirection();
    
    app.UseStaticFiles(); // 🔴 here it is
    app.UseRouting(); // 🔴 here it is

    app.UseCors();

    app.UseRateLimiter();
    
    // HealthCheck Middleware
    app.AddHealthChecks();

    // Ensure database is created during application startup
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    // Request Logging Middleware
    app.Use(async (context, next) =>
    {
        try
        {
            var key = context.Request.Headers[HttpHeaderKeys.XApiHeaderKey];
            var nonce = context.Request.Headers[HttpHeaderKeys.SNonceHeaderKey];
            var secret = context.Request.Headers[HttpHeaderKeys.XApiSecretHeaderKey];
            Log.Information($"Incoming Request: {context.Request.Protocol} {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
            Log.Information($"Key - {key}, Nonce - {nonce}, Secret - {secret}");
        }
        catch (Exception)
        {
            Log.Information($"Api Key or Nonce not found in Header.");
        }
        
        await next();
    });

    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(async () =>
        {
            using var scope = app.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
            var cryptService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            if (context.Response.StatusCode == StatusCodes.Status200OK || 
                context.Response.StatusCode == StatusCodes.Status201Created ||
                context.Response.StatusCode == StatusCodes.Status204NoContent
               )
            {
                var apiKey = await service.GenerateApiKeyAsync();
                var cryptApiKey = cryptService.Encrypt(apiKey.Key);
                context.Response.Headers.Append(HttpHeaderKeys.XApiHeaderKey, cryptApiKey);
            }
        });
        await next.Invoke();
    });

    app.UseSerilogRequestLogging();
    
    app.Urls.Add("https://localhost:7111"); // 👈 Add the URL

    app.Run(); // "https://localhost:7111"
}
catch (Exception ex)
{
    Log.Fatal(ex, "server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
