using Microsoft.AspNetCore.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add YARP reverse proxy services and load configuration from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .ConfigureHttpClient((_, handler) =>
    {
        handler.AllowAutoRedirect = true;
    })
    .AddTransforms(transforms =>
    {
        transforms.AddRequestTransform(async context =>
        {
            // Copying the headers from the incoming request to the target request
            foreach (var header in context.HttpContext.Request.Headers)
            {
                context.ProxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            await Task.CompletedTask;
        });
    });;

// Optional: Add logging for diagnostics (if needed)
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

/*
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.AddFixedWindowLimiter("fixed", options =>
    {
        options.Window = TimeSpan.FromSeconds(10);
        options.PermitLimit = 5;
    });
});*/

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

/*
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();*/

// Map the reverse proxy routes
app.MapReverseProxy();

app.Run();
