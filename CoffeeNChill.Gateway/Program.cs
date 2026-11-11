
using CoffeeNChill.Gateway.Services;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Endpoints;
using CoffeeNChill.Gateway.Models;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is missing.");

var jwtSecret = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is missing.");

var jwtKey = Convert.FromBase64String(jwtSecret);

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtKey),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = "name",
                RoleClaimType = "role"
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffOrAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Staff", "Admin");
    });
});

builder.Services.AddSingleton<UserTableService>();

builder.Services.AddScoped<
    IPasswordHasher<UserEntity>,
    PasswordHasher<UserEntity>>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoffeeNChill API",
        Version = "v1",
        Description = "CoffeeNChill's unified API Gateway."
    });

    var xmlFile =
        $"{builder.Environment.ApplicationName}.xml";

    var xmlPath = Path.Combine(
        AppContext.BaseDirectory,
        xmlFile
    );

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy")
    );

builder.Services.AddHttpClient("CoffeeBackend", client =>
{
    var backend = builder.Configuration[
        "ReverseProxy:Clusters:coffeeCluster:" +
        "Destinations:coffeeBackend:Address"
    ] ?? "http://localhost:7225/";

    client.BaseAddress = new Uri(backend);
});

var app = builder.Build();

var userTable = app.Services
    .GetRequiredService<UserTableService>();

await userTable.InitializeAsync();

if (app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () =>
    "CoffeeNChill Gateway is running!"
);

app.MapGet("/api/gateway/health", () =>
    new GatewayStatusDto(
        "CoffeeNChill Gateway",
        "Running"
    )
)
.WithName("GatewayHealth")
.WithTags("Gateway")
.Produces<GatewayStatusDto>(200);

app.MapGet("/api/menu", ForwardMenuGet)
    .WithName("GetAllMenuItems")
    .WithTags("Menu")
    .Produces<JsonElement>(200, "application/json")
    .ProducesProblem(502);

app.MapGet(
    "/api/menu/category/{category}",
    ForwardMenuGet
)
.WithName("GetMenuItemsByCategory")
.WithTags("Menu")
.Produces<JsonElement>(200, "application/json")
.ProducesProblem(502);

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.MapReverseProxy();

if (await StaffBootstrapper.RunIfRequestedAsync(app))
{
    return;
}

app.Run();

static async Task<IResult> ForwardMenuGet(
    HttpContext context,
    IHttpClientFactory factory)
{
    var client = factory.CreateClient("CoffeeBackend");

    try
    {
        var path = context.Request.Path.Value!
            .TrimStart('/') + context.Request.QueryString;

        using var response = await client.GetAsync(path);

        var body = await response.Content.ReadAsStringAsync();

        return Results.Content(
            body,
            response.Content.Headers.ContentType?.ToString()
                ?? "application/json",
            statusCode: (int)response.StatusCode
        );
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            "Azure Functions is unavailable.",
            statusCode: 502
        );
    }
}

