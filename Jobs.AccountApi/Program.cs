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
using Jobs.Core.Managers;
using Jobs.Core.Middleware;
using Jobs.Core.Observability.Options;
using Jobs.Core.Providers;
using Jobs.Core.Services;
using Jobs.Entities.DataModel;
using Jobs.Entities.Responses;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using StackExchange.Redis;
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
        listenOptions.Protocols = HttpProtocols.Http3;
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

CryptOptions cryptOptions = new();

builder.Configuration
    .GetRequiredSection(nameof(CryptOptions))
    .Bind(cryptOptions);

var accountSecretKey = builder.Configuration["AccountApiService:SecretKey"];
var accountApiKey = builder.Configuration["AccountApiService:ApiKey"];

Console.WriteLine($"AccountSecretKey - {accountSecretKey}");
Console.WriteLine($"AccountApiKey - {accountApiKey}");

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

var storage = new MemoryApiKeyStorageServiceProvider();
storage.AddApiKey(new ApiKey{ Key = accountApiKey, Expiration = null });

builder.Services.AddScoped<IApiKeyStorageServiceProvider, MemoryApiKeyStorageServiceProvider>();
builder.Services.AddScoped<IApiKeyManagerServiceProvider, ApiKeyManagerServiceProvider>();
builder.Services.AddScoped<IKeycloakAccountService, KeycloakAccountService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IEncryptionService, NaiveEncryptionService>(p => 
    p.ResolveWith<NaiveEncryptionService>(Convert.FromBase64String(cryptOptions.PKey), Convert.FromBase64String(cryptOptions.IV)));
builder.Services.AddScoped<ISignedNonceService, SignedNonceService>();
builder.Services.AddScoped<ISecretApiService, SecretApiService>(p => 
    p.ResolveWith<SecretApiService>(accountSecretKey));

builder.Services.AddSingleton<PeriodicHostedService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PeriodicHostedService>());

builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly()); // AutoMapper registration
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

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

var app = builder.Build();

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

var version1 = new ApiVersion(1);

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(version1)
    //.HasApiVersion(new ApiVersion(2))
    .ReportApiVersions()
    .Build();

bool IsBadRequest(IHttpContextAccessor httpContextAccessor, 
    IEncryptionService cryptService,
    ISignedNonceService signedNonceService,
    IApiKeyService service,
    string apiKey, 
    string signedNonce,
    string apiSecret)
{
    if (!UserAgentConstant.AppUserAgent.Equals(httpContextAccessor.HttpContext?.Request.Headers.UserAgent))
    {
        return true;
    }
        
    var (longNonce ,resultParse) = signedNonceService.IsSignedNonceValid(signedNonce);

    if (builder.Environment.IsDevelopment())
    {
        longNonce = DateTime.UtcNow.Ticks;
    }

    if (!resultParse)
    {
        return true;
    }
            
    // apiKey must be in Base64
    var realApiKey = cryptService.Decrypt(apiKey);
    var realApiSecret = cryptService.Decrypt(apiSecret);
            
    if (!service.IsValid(realApiKey, longNonce, realApiSecret))
    {
        return true;
    }

    return false;
}

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

app.MapPost("api/v{version:apiVersion}/login", async Task<Results<Ok<KeycloakTokenResponse>, BadRequest, NotFound>> ([FromBody] LoginUser user,
        HttpContext context,
        [FromServices] IKeycloakAccountService accountService,
        [FromServices] ISender mediatr,
        [FromServices] IPublisher publisher,
        [FromServices] IApiKeyService service,
        [FromServices] IEncryptionService cryptService,
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        Log.Information($"UserName: {user.UserName} , Password: {user.Password}");
        Console.WriteLine($"UserAgent - {httpContextAccessor.HttpContext?.Request.Headers.UserAgent}");
        
        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }

        var ipAddress = context.Request.GetIpAddress();
        Log.Information($"ClientIPAddress - {ipAddress}.");

        try
        {
            var result = await accountService.LoginAsync(user.UserName, user.Password).ConfigureAwait(false);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return TypedResults.NotFound();
        }
    }).WithName("Login")
    .WithOpenApi()
    .MapApiVersion(apiVersionSet, version1)
    .AllowAnonymous()
    .RequireRateLimiting("FixedWindow");

app.MapPost("api/v{version:apiVersion}/logout", async Task<Results<Ok, BadRequest>> ([FromBody] LogoutUser user,
        HttpContext context,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IApiKeyService service, 
        [FromServices] IKeycloakAccountService accountService,
        [FromServices] ISender mediatr, 
        [FromServices] IPublisher publisher,
        [FromServices] IEncryptionService cryptService,
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        Log.Information($"User Name: {user.Username}");
        Console.WriteLine($"UserAgent - {httpContextAccessor.HttpContext?.Request.Headers.UserAgent}");

        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }
        
        var ipAddress = context.Request.GetIpAddress();
        Log.Information($"ClientIPAddress - {ipAddress}.");
        
        //POST /admin/realms/{realm}/users/{user-id}/logout
        var result = await accountService.LogoutAsync(user).ConfigureAwait(false);
        
        return result ? TypedResults.Ok() : TypedResults.BadRequest();
    }).WithName("Logout")
    .WithOpenApi()
    .MapApiVersion(apiVersionSet, version1)
    .RequireRateLimiting("FixedWindow");

app.MapPost("api/v{version:apiVersion}/register", async Task<Results<Ok<RegisterUserResponse>, BadRequest>> ([FromBody] User user,
        HttpContext context,
        [FromServices] IApiKeyService service,
        [FromServices] IKeycloakAccountService accountService,
        [FromServices] ISender mediatr,
        [FromServices] IPublisher publisher,
        [FromServices] IEncryptionService cryptService,
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        Log.Information($"User Email: {user.Email} , Password: {user.Password}");
        Console.WriteLine($"UserAgent - {httpContextAccessor.HttpContext?.Request.Headers.UserAgent}");
        
        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }
        
        var ipAddress = context.Request.GetIpAddress();
        Log.Information($"ClientIPAddress - {ipAddress}.");
        
        var result = await accountService.RegisterUser(user).ConfigureAwait(false);

        return TypedResults.Ok(result);
    }).WithName("Register")
    .WithOpenApi()
    .MapApiVersion(apiVersionSet, version1)
    .AllowAnonymous()
    .RequireRateLimiting("FixedWindow");

    app.MapPost("api/v{version:apiVersion}/refresh", async Task<Results<Ok<KeycloakRespone>, BadRequest>> ([FromBody] string refreshToken,
            HttpContext context,
            [FromServices] IApiKeyService service,
            [FromServices] IKeycloakAccountService accountService,
            [FromServices] ISender mediatr,
            [FromServices] IPublisher publisher,
            [FromServices] IEncryptionService cryptService,
            [FromServices] ISignedNonceService signedNonceService,
            [FromServices] IHttpContextAccessor httpContextAccessor,
            [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
             StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
            [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
             StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
            [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
             StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
        {
            Log.Information($"Get token by refresh token!");
            Console.WriteLine($"UserAgent - {httpContextAccessor.HttpContext?.Request.Headers.UserAgent}");
            Console.WriteLine($"RefreshToken - {refreshToken}");

            if (IsBadRequest(httpContextAccessor,
                    cryptService, signedNonceService, service,
                    apiKey, signedNonce, apiSecret))
            {
                return TypedResults.BadRequest();
            }

            var ipAddress = context.Request.GetIpAddress();
            Log.Information($"ClientIPAddress - {ipAddress}.");

            var result = await accountService.RefreshTokenAsync(refreshToken).ConfigureAwait(false);

            return TypedResults.Ok(result);
        })
        .WithName("Refresh")
        .WithOpenApi()
        .MapApiVersion(apiVersionSet, version1)
        .AllowAnonymous()
        .RequireRateLimiting("FixedWindow");

    app.UseSerilogRequestLogging();

    app.Run();

