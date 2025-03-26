using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using AutoMapper;
using Jobs.Common.Constants;
using Jobs.Common.Contracts;
using Jobs.Common.Extentions;
using Jobs.Common.Helpers;
using Jobs.Common.Options;
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
using Jobs.ReferenceApi.Contracts;
using Jobs.ReferenceApi.Data;
using Jobs.ReferenceApi.Extensions;
using Jobs.ReferenceApi.Features.Categories;
using Jobs.ReferenceApi.Features.EmploymentTypes;
//using Jobs.ReferenceApi.Features.Queries;
using Jobs.ReferenceApi.Features.WorkTypes;
using Jobs.ReferenceApi.Repositories;
using Jobs.ReferenceApi.Services;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using Serilog;

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
    
    Log.Information("Starting WebApi Reference Service.");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddApiVersionService();

    builder.Services.AddDbContext<JobsDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    //var vacancySecretKey = builder.Configuration["VacancyApiService:SecretKey"];
    var referenceSecretKey = "12345678910111213141151617";
    
    CryptOptions cryptOptions = new();

    builder.Configuration
        .GetRequiredSection(nameof(CryptOptions))
        .Bind(cryptOptions);
    
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, LocalCacheService>();
    
    builder.Services.AddScoped<IGenericRepository<WorkType>, WorkTypeRepository>();
    builder.Services.AddScoped<IGenericRepository<EmploymentType>, EmploymentTypeRepository>();
    builder.Services.AddScoped<IGenericRepository<Category>, CategoryRepository>();
    builder.Services.AddScoped<IApiKeyStorageServiceProvider, MemoryApiKeyStorageServiceProvider>();
    builder.Services.AddScoped<IApiKeyManagerServiceProvider, ApiKeyManagerServiceProvider>();
    builder.Services.AddScoped<ISecretApiKeyRepository, SecretApiKeyRepository>();
    
    builder.Services.AddScoped<Categories.ICategoryService, Categories.CategoryService>();
    builder.Services.AddScoped<WorkTypes.IWorkTypeService, WorkTypes.WorkTypeService>();
    builder.Services.AddScoped<GetWorkTypeById.IWorkTypeServiceExtended, GetWorkTypeById.WorkTypeServiceExtended>();
    builder.Services.AddScoped<EmploymentTypes.IEmploymentTypeService, EmploymentTypes.EmploymentTypeService>();
    builder.Services.AddScoped<GetEmploymentTypeById.IEmploymentTypeServiceExtended, GetEmploymentTypeById.EmploymentTypeServiceExtended>();
    
    builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
    builder.Services.AddScoped<IEncryptionService, NaiveEncryptionService>(p => 
        p.ResolveWith<NaiveEncryptionService>(Convert.FromBase64String(cryptOptions.PKey), Convert.FromBase64String(cryptOptions.IV)));
    builder.Services.AddScoped<ISignedNonceService, SignedNonceService>();
    builder.Services.AddScoped<ISecretApiService, SecretApiService>(p => p.ResolveWith<SecretApiService>(referenceSecretKey));
    
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
    /*
    builder.Services.AddOpenTelemetry().WithMetrics(opts => opts
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
    );
    
    builder.Services.AddOpenTelemetry().WithMetrics(opts => opts
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("JobStore.WebApi"))
        .AddMeter(builder.Configuration.GetValue<string>("JobsStoreMeterName"))
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter(opts =>
        {
            opts.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"]);
        })); */
    
    // forward headers configuration for reverse proxy
    builder.Services.Configure<ForwardedHeadersOptions>(options => {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddResponseCompressionService();
    
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    
    /*builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", settings =>
            settings.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
    });*/
    
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(build => {
            build.WithOrigins("https://localhost:7006");
            build.AllowAnyMethod();
            build.AllowAnyHeader();
        });
    });
    
    builder.Services.AddEndpoints(typeof(Program).Assembly);

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


   // app.UseExceptionHandlers();
   
   
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
    
    app.UseHttpsRedirection();
    
    app.UseStaticFiles(); // 🔴 here it is
    app.UseRouting(); // 🔴 here it is

    //app.UseCors("AllowAll");
    app.UseCors();
    
    /*app.UseCors(options =>
        options
            .WithOrigins("https://localhost:7111","http://localhost:5206")
            .AllowAnyMethod()
            .AllowAnyHeader());*/
    
//app.UseMiddleware<ErrorHandlerMiddleware>(); 
    // Enable compression
    

    app.UseRateLimiter();

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


//api/v{version:apiVersion}
    
    
    app.UseSerilogRequestLogging();

    app.Run();
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
