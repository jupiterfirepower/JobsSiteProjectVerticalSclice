using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Jobs.AccountApi.Contracts;
using Jobs.AccountApi.Services;
using Microsoft.AspNetCore.HttpOverrides;
using OpenTelemetry.Resources;
using Serilog;
using AutoMapper;
using DotNetEnv;
using Jobs.AccountApi.Extentions;
using Jobs.AccountApi.Features.Keycloak;
using Jobs.Common.Constants;
using Jobs.Common.Extentions;
using Jobs.Common.Options;
using Jobs.Common.Responses;
using Jobs.Common.Settings;
using Jobs.Core.Contracts;
using Jobs.Core.Contracts.Providers;
using Jobs.Core.DataModel;
using Jobs.Core.Extentions;
using Jobs.Core.Handlers;
using Jobs.Core.Helpers;
using Jobs.Core.Managers;
using Jobs.Core.Middleware;
using Jobs.Core.Observability.Options;
using Jobs.Core.Providers;
using Jobs.Core.Providers.Vault;
using Jobs.Core.Services;
using Jobs.Entities.DataModel;
using Jobs.Entities.Responses;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog.Context;
using StackExchange.Redis;
using Steeltoe.Discovery.Client;
using Steeltoe.Discovery.Consul;
using SecretApiService = Jobs.Core.Services.SecretApiService;
using IApiKeyService = Jobs.Core.Contracts.IApiKeyService;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

KestrelSettings kestrelSettings = new();

builder.Configuration
    .GetRequiredSection(nameof(KestrelSettings))
    .Bind(kestrelSettings);

Console.WriteLine($"MaxConcurrentConnections - {kestrelSettings.MaxConcurrentConnections}");
Console.WriteLine($"MaxConcurrentUpgradedConnections - {kestrelSettings.MaxConcurrentUpgradedConnections}");
Console.WriteLine($"MaxRequestBodySize - {kestrelSettings.MaxRequestBodySize}");
Console.WriteLine($"Port - {kestrelSettings.Port}");

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    // Maximum client connections
    serverOptions.Limits.MaxConcurrentConnections = kestrelSettings.MaxConcurrentConnections;
    // Maximum number of open, upgraded connections:
    serverOptions.Limits.MaxConcurrentUpgradedConnections = kestrelSettings.MaxConcurrentUpgradedConnections;
    // Configures MaxRequestBodySize for all requests:
    serverOptions.Limits.MaxRequestBodySize = kestrelSettings.MaxRequestBodySize;
    
    serverOptions.ListenAnyIP(kestrelSettings.Port, listenOptions =>
    {
        //listenOptions.Protocols = HttpProtocols.Http3;
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
    
    /*options.Listen(IPAddress.Any, 443, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http3;
        });
    });*/
});

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.WriteTo.Console();
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
});

Log.Information("Starting WebApi Keycloak Account Service.");

builder.Services.AddApiVersionService();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

// user-secrets
/*var accountServiceSecretKey = builder.Configuration["AccountApiService:SecretKey"];
Console.WriteLine($"accountServiceSecretKey: {accountServiceSecretKey}");
var accountServiceDefApiKey = builder.Configuration["AccountApiService:DefaultApiKey"];
Console.WriteLine($"accountServiceDefApiKey: {accountServiceDefApiKey}");*/

Env.TraversePath().Load();
    
var vaultUri = Environment.GetEnvironmentVariable("VAULT_ADDR");
var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
    
// Hashicorp Vault Secrets.
var vaultSecretsProvider = new VaultSecretProvider(vaultUri, vaultToken);

var vaultSecretKey = await vaultSecretsProvider.GetSecretValueAsync("secrets/services/account", "SecretKey", "secrets");
Console.WriteLine($"vaultSecretKey: {vaultSecretKey}");
var vaultDefaultApiKey = await vaultSecretsProvider.GetSecretValueAsync("secrets/services/account", "DefaultApiKey", "secrets");
Console.WriteLine($"vaultDefaultApiKey: {vaultDefaultApiKey}");

builder.Services.ConfigureDependencyInjection(builder.Configuration);

// Add Redis configuration
/*
var redisConfiguration = builder.Configuration.GetSection("Redis")["ConnectionString"];
var redis = ConnectionMultiplexer.Connect(redisConfiguration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRedisRepository, RedisDbRepository>();
*/

//SQLiteGenericRepository.InitDb();

//repo.AddApiKey(new ApiKey{ Key = accountApiKey, Expiration = null });

builder.Services.AddStackExchangeRedisCache(options =>
{
    var redisConfiguration = builder.Configuration.GetSection("Redis")["ConnectionString"];
    options.Configuration = redisConfiguration;
    //options.Configuration = "localhost";
    options.ConfigurationOptions = new ConfigurationOptions()
    {
        AbortOnConnectFail = true,
        EndPoints = { "localhost:6379" }
        //EndPoints = { "<server>:6379" },
        //User = "test",
        //Password = "newpwd"
    };
});

/*CryptOptions cryptOptions = new();

builder.Configuration
    .GetRequiredSection(nameof(CryptOptions))
    .Bind(cryptOptions);

builder.Services.AddScoped<IApiKeyStorageServiceProvider, MemoryApiKeyStorageServiceProvider>();
//builder.Services.AddScoped<IApiKeyManagerServiceProvider, ApiKeyManagerServiceProvider>();
builder.Services.AddScoped<IApiKeyManagerServiceProvider, ApiKeyManagerServiceProvider>(p =>
{
    var currentService = p.ResolveWith<ApiKeyManagerServiceProvider>();
    currentService.AddApiKey(new ApiKey { Key = accountServiceDefApiKey, Expiration = null });
    return currentService;
});

//builder.Services.AddScoped<IKeycloakAccountService, KeycloakAccountService>();
builder.Services.AddScoped<Login.IKeycloakLoginService, Login.KeycloakLoginService>();
builder.Services.AddScoped<Logout.IKeycloakLogoutService, Logout.KeycloakLogoutService>();
builder.Services.AddScoped<RefreshToken.IKeycloakRefreshTokenService, RefreshToken.KeycloakRefreshTokenService>();
builder.Services.AddScoped<Register.IKeycloakRegisterService, Register.KeycloakRegisterService>();

builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IEncryptionService, NaiveEncryptionService>(p => 
    p.ResolveWith<NaiveEncryptionService>(Convert.FromBase64String(cryptOptions.PKey), Convert.FromBase64String(cryptOptions.IV)));
builder.Services.AddScoped<ISignedNonceService, SignedNonceService>();
builder.Services.AddScoped<ISecretApiService, SecretApiService>(p => 
    p.ResolveWith<SecretApiService>(accountServiceSecretKey));

builder.Services.AddSingleton<PeriodicHostedService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PeriodicHostedService>());

builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly()); // AutoMapper registration
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));*/

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.IncludeFields = true;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
});

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
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
    options.HttpsPort = 443;
});

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

// Configuring Health Check
builder.Services.ConfigureHealthChecks(builder.Configuration);

// Service Discovery Consul
builder.Services.AddServiceDiscovery(o => o.UseConsul());

var app = builder.Build();

var version1 = new ApiVersion(1);

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(version1)
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

app.UseHttpsRedirection();

app.UseResponseCompression();
app.UseForwardedHeaders();
app.UseMiddleware<AdminSafeListMiddleware>(builder.Configuration["HostsSafeList"]);

// Global Exception Handler.
app.UseExceptionHandler();

// Get the Automapper, we can share this too
var mapper = app.Services.GetService<IMapper>();
if (mapper == null)
{
    throw new InvalidOperationException("Mapper not found");
}

// HealthCheck Middleware
app.AddHealthChecks();

// CorrelationId Middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers[HttpHeaderKeys.XCorrelationIdHeaderKey].FirstOrDefault() ?? Guid.NewGuid().ToString();
    context.Response.Headers[HttpHeaderKeys.XCorrelationIdHeaderKey] = correlationId;

    using (LogContext.PushProperty(HttpHeaderKeys.SerilogCorrelationIdProperty, correlationId))
    {
        await next();
    }
});

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
        Log.Information("Api Key or Nonce not found in Header.");
    }
        
    await next();
});

app.UseCors();

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

app.Run();

