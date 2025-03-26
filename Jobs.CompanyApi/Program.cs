using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Asp.Versioning;
using AutoMapper;
using Jobs.Common.Constants;
using Jobs.Common.Contracts;
using Jobs.Common.Extentions;
using Jobs.Common.Options;
using Jobs.Common.Settings;
using Jobs.CompanyApi.DBContext;
using Jobs.CompanyApi.Features.Commands;
using Jobs.CompanyApi.Features.Notifications;
using Jobs.CompanyApi.Features.Queries;
using Jobs.CompanyApi.Helpers;
using Jobs.CompanyApi.Repositories;
using Jobs.CompanyApi.Services;
using Jobs.CompanyApi.Services.Contracts;
using Jobs.Core.Contracts;
using Jobs.Core.Contracts.Providers;
using Jobs.Core.Extentions;
using Jobs.Core.Filters;
using Jobs.Core.Handlers;
using Jobs.Core.Managers;
using Jobs.Core.Middleware;
using Jobs.Core.Observability.Options;
using Jobs.Core.Providers;
using Jobs.Core.Services;
using Jobs.DTO;
using Jobs.DTO.In;
using Jobs.Entities.Models;
using Keycloak.AuthServices.Authentication;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.WriteTo.Console();
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
});

Log.Information("Starting WebApi Company Service.");

var companySecretKey = builder.Configuration["JobsCompanyApi:SecretKey"];
Console.WriteLine($"CompanySecretKey - {companySecretKey}");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo{ Title = "My API", Version = "v1" });
    options.AddSecurityDefinition("Keycloak", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri("http://localhost:9001/realms/mjobs/protocol/openid-connect/auth"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "openid" },
                    { "profile", "profile" }
                }
            }
        }
    });
    
    OpenApiSecurityScheme keycloakSecurityScheme = new()
    {
        Reference = new OpenApiReference
        {
            Id = "Keycloak",
            Type = ReferenceType.SecurityScheme,
        },
        In = ParameterLocation.Header,
        Name = "Bearer",
        Scheme = "Bearer",
    };

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { keycloakSecurityScheme, Array.Empty<string>() },
    });
    
    //var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    //var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    //options.IncludeXmlComments(xmlPath);
});

builder.Services.AddApiVersionService();

builder.Services.AddDbContext<CompanyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

CryptOptions cryptOptions = new();

builder.Configuration
    .GetRequiredSection(nameof(CryptOptions))
    .Bind(cryptOptions);

builder.Services.AddScoped<IGenericRepository<Company>, CompanyRepository>();
builder.Services.AddScoped<IApiKeyStorageServiceProvider, MemoryApiKeyStorageServiceProvider>();
builder.Services.AddScoped<IApiKeyManagerServiceProvider, ApiKeyManagerServiceProvider>();
builder.Services.AddScoped<ISecretApiKeyRepository, SecretApiKeyRepository>();
builder.Services.AddScoped<IProcessingService, ProcessingService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<ISignedNonceService, SignedNonceService>();
builder.Services.AddScoped<IEncryptionService, NaiveEncryptionService>(p => 
    p.ResolveWith<NaiveEncryptionService>(Convert.FromBase64String(cryptOptions.PKey), Convert.FromBase64String(cryptOptions.IV)));
builder.Services.AddScoped<ISecretApiService, SecretApiService>(p => 
    p.ResolveWith<SecretApiService>(companySecretKey));


builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly()); // AutoMapper registration
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.IncludeFields = true;
});

//builder.Services.AddRateLimiterService();
builder.Services.AddWindowRateLimiterService();

builder.Services.AddHttpClient();

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

// Keycloak Auth.
builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

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

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    //app.UseSwaggerUI();
    
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
        options.OAuthClientId("confmjobs");
    });
}

app.UseResponseCompression();
app.UseForwardedHeaders();
app.UseMiddleware<AdminSafeListMiddleware>(builder.Configuration["HostsSafeList"]);

// Global Exception Handler.
app.UseExceptionHandler();

//app.UseHttpsRedirection();

// Get the Automapper, we can share this too
var mapper = app.Services.GetService<IMapper>();
if (mapper == null)
{
    throw new InvalidOperationException("Mapper not found");
}

//app.UseLogHeaders(); // add here right after you create app
//app.UseExceptionHandlers();

//app.UseSecurityHeaders();

//app.UseErrorHandler(); // add here right after you create app

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 443;
    });
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

app.UseCors();
app.UseRateLimiter();

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

void Guards(ISender mediatr, IApiKeyService service,
    IEncryptionService cryptService, ISignedNonceService signedNonceService,
    IHttpContextAccessor httpContextAccessor)
{
    ArgumentNullException.ThrowIfNull(mediatr, nameof(mediatr));
    ArgumentNullException.ThrowIfNull(service, nameof(service));
    ArgumentNullException.ThrowIfNull(cryptService, nameof(cryptService));
    ArgumentNullException.ThrowIfNull(signedNonceService, nameof(signedNonceService));
    ArgumentNullException.ThrowIfNull(httpContextAccessor, nameof(httpContextAccessor));
}

app.MapGet("api/v{version:apiVersion}/companies", async Task<Results<Ok<List<CompanyDto>>, BadRequest>> (HttpContext context, 
        ClaimsPrincipal user,
        [FromServices] ISender mediatr, 
        [FromServices] IApiKeyService service,
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IEncryptionService cryptService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        app.Logger.LogInformation($"UserName: {user.Identity?.Name}");
        Console.WriteLine($"UserAgent - {httpContextAccessor.HttpContext?.Request.Headers.UserAgent}");
        
        Guards(mediatr, service, cryptService, signedNonceService, httpContextAccessor);
        
        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }

        var ipAddress = context.Request.GetIpAddress();
        Log.Information($"ClientIPAddress - {ipAddress}.");
        
        var companies = await mediatr.Send(new ListCompaniesQuery());
        return TypedResults.Ok(companies);
    }).WithName("GetCompanies")
    .MapApiVersion(apiVersionSet, version1)
    .RequireRateLimiting("FixedWindow")
    .RequireAuthorization()
    .WithOpenApi();

app.MapGet("api/v{version:apiVersion}/companies/{id:int}", async Task<Results<Ok<CompanyDto>, BadRequest, NotFound>> (int id,
        ISender mediatr,
        [FromServices] IApiKeyService service, 
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IEncryptionService cryptService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        Guards(mediatr, service, cryptService, signedNonceService, httpContextAccessor);
        
        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }

        var company = await mediatr.Send(new GetCompanyQuery(id));
        return company == null ? TypedResults.NotFound() : TypedResults.Ok(company);
    })
    .AddEndpointFilter(async (context, next) =>
    {
        var id = context.GetArgument<int>(0);
    
        if (id <= 0)
        {
            return TypedResults.BadRequest();
        }

        return await next(context);
    })
    .WithName("GetCompany")
    .MapApiVersion(apiVersionSet, version1)
    .RequireRateLimiting("FixedWindow")
    .WithOpenApi();

app.MapPost("api/v{version:apiVersion}/companies", async Task<Results<Created<CompanyDto>, BadRequest>> ([FromBody] CompanyInDto company,
        [FromServices] IApiKeyService service, 
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IEncryptionService cryptService,
        [FromServices] ISender mediatr, 
        [FromServices] IPublisher publisher, 
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
        {
            Guards(mediatr, service, cryptService, signedNonceService, httpContextAccessor);
            
            if (IsBadRequest(httpContextAccessor, 
                    cryptService, signedNonceService, service, 
                    apiKey, signedNonce, apiSecret))
            {
                return TypedResults.BadRequest();
            }
            
            var sanitized = SanitizerDtoHelper.SanitizeCompanyInDto(company);

            var result = await mediatr.Send(new CreateCompanyCommand(sanitized));
            if (0 == result.CompanyId) return TypedResults.BadRequest();
                
            await publisher.Publish(new CompanyCreatedNotification(result.CompanyId));
            
            return TypedResults.Created($"/companies/{result.CompanyId}", result);
    })
    .AddEndpointFilter(async (context, next) =>
    {
        var company = context.GetArgument<CompanyInDto>(0);
    
        if (company.CompanyId != 0)
        {
            return TypedResults.BadRequest();
        }

        return await next(context);
    })
    .AddEndpointFilter<DtoModeValidationFilter<CompanyInDto>>()
    .WithName("AddCompany")
    .MapApiVersion(apiVersionSet, version1)
    .RequireRateLimiting("FixedWindow")
    .WithOpenApi();

app.MapPut("api/v{version:apiVersion}/companies/{id:int}", async Task<Results<BadRequest, NoContent, NotFound>> (int id, 
        CompanyInDto company, 
        [FromServices] ISender mediatr,
        [FromServices] IApiKeyService service, 
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IEncryptionService cryptService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        Guards(mediatr, service, cryptService, signedNonceService, httpContextAccessor);
        
        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }

        var sanitized = SanitizerDtoHelper.SanitizeCompanyInDto(company);
        var result = await mediatr.Send(new UpdateCompanyCommand(sanitized));

        return result > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
    })
    .AddEndpointFilter(async (context, next) =>
    {
        var id = context.GetArgument<int>(0);
        var company = context.GetArgument<CompanyInDto>(1);
    
        if (id <= 0 || id != company.CompanyId)
        {
            return TypedResults.BadRequest();
        }

        return await next(context);
    })
    .AddEndpointFilter<DtoModeValidationFilter<CompanyInDto>>()
    .WithName("ChangeCompany")
    .MapApiVersion(apiVersionSet, version1)
    .RequireRateLimiting("FixedWindow")
    .WithOpenApi();

app.MapDelete("api/v{version:apiVersion}/companies/{id:int}", async Task<Results<BadRequest, NoContent, NotFound>> (int id, 
        ISender mediatr,
        [FromServices] IApiKeyService service, 
        [FromServices] ISignedNonceService signedNonceService,
        [FromServices] IEncryptionService cryptService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
        [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
         StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
        [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
         StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
    {
        Guards(mediatr, service, cryptService, signedNonceService, httpContextAccessor);
        
        if (IsBadRequest(httpContextAccessor, 
                cryptService, signedNonceService, service, 
                apiKey, signedNonce, apiSecret))
        {
            return TypedResults.BadRequest();
        }

        var result = await mediatr.Send(new DeleteCompanyCommand(id));
        return result == -1 ? TypedResults.NotFound() : TypedResults.NoContent();
    })
    .AddEndpointFilter(async (context, next) =>
    {
        var id = context.GetArgument<int>(0);
   
        if (id <= 0)
        {
            return TypedResults.BadRequest();
        }

        return await next(context);
    })
    .WithName("RemoveCompany")
    .MapApiVersion(apiVersionSet, version1)
    .RequireRateLimiting("FixedWindow")
    .WithOpenApi();
    
app.UseSerilogRequestLogging();

app.Run();

