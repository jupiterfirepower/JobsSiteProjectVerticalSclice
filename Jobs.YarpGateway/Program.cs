using System.Diagnostics;
using System.Text;
using Jobs.Common.Constants;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Steeltoe.Discovery.Client;
using Steeltoe.Discovery.Consul;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Service Discovery Consul
builder.Services.AddServiceDiscovery(o => o.UseConsul());

// Add YARP reverse proxy services and load configuration from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .ConfigureHttpClient((_, handler) =>
    {
        handler.AllowAutoRedirect = true;
        handler.MaxConnectionsPerServer = 100;
        handler.EnableMultipleHttp2Connections = true;
    })
    .AddTransforms(transforms =>
    {
        transforms.AddRequestTransform(async context =>
        {
            // Copying the headers from the incoming request to the target request
            /*foreach (var header in context.HttpContext.Request.Headers)
            {
                context.ProxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }*/

            await Task.CompletedTask;
        });
    }).AddTransforms(builderContext =>  // Monitor YARP’s performance:
    {
        builderContext.AddRequestTransform(async transformContext =>
        {
            var activity = new Activity("ProxyRequest");
            activity.SetTag("Destination", transformContext.DestinationPrefix);
            activity.Start();
            
            transformContext.HttpContext.Items["ProxyActivity"] = activity;
            await Task.CompletedTask;
        });
        
        builderContext.AddResponseTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Items["ProxyActivity"] is Activity activity)
            {
                activity.SetTag("StatusCode", transformContext.ProxyResponse?.StatusCode);
                activity.Stop();
            }
            await Task.CompletedTask;
        });
    });;

// Optional: Add logging for diagnostics (if needed)
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddFilter("Yarp", LogLevel.Debug);
});

builder.Services.AddHttpLogging(logging =>
{
    // Customize HTTP logging here.
    logging.LoggingFields = HttpLoggingFields.All;
    //logging.RequestHeaders.Add("sec-ch-ua");
    //logging.ResponseHeaders.Add("my-response-header");
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});

/*
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
        policy.RequireAuthenticatedUser());
});*/

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.AddFixedWindowLimiter("fixed", options =>
    {
        options.Window = TimeSpan.FromSeconds(60);
        options.PermitLimit = 50;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpLogging();
app.UseHttpsRedirection();

/*
app.UseAuthentication();
app.UseAuthorization();*/
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    var corId = context.Request.Headers[HttpHeaderKeys.XCorrelationIdHeaderKey].FirstOrDefault();
    var correlationId = corId ?? Guid.NewGuid().ToString();
    
    if (string.IsNullOrWhiteSpace(corId))
    {
        context.Request.Headers[HttpHeaderKeys.XCorrelationIdHeaderKey] = correlationId;
    }
    Console.WriteLine($"Correlation Id: {correlationId}");

    //using (LogContext.PushProperty(HttpHeaderKeys.SerilogCorrelationIdProperty, correlationId))
    //{
        await next();
    //}
});   

// Map the reverse proxy routes  Register YARP middleware
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        // Logging logic before passing to the next middleware
        //await LogRequest(context);
        await next();
        // Logging logic after the response is received
        //await LogResponse(context);
    });
});

app.Run();
