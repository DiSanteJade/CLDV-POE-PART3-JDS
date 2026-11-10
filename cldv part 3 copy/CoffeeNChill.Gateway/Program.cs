
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

// JWT configuration
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

// Register JWT authentication
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

// Role-based authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffOrAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Staff", "Admin");
    });
});

// Azure Table Storage and password hashing
builder.Services.AddSingleton<UserTableService>();

builder.Services.AddScoped<
    IPasswordHasher<UserEntity>,
    PasswordHasher<UserEntity>>();

// Swagger and OpenAPI
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

// Configure YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy")
    );

// Configure backend HTTP client
builder.Services.AddHttpClient("CoffeeBackend", client =>
{
    var backend = builder.Configuration[
        "ReverseProxy:Clusters:coffeeCluster:" +
        "Destinations:coffeeBackend:Address"
    ] ?? "http://localhost:7225/";

    client.BaseAddress = new Uri(backend);
});

var app = builder.Build();

// Prepare Azure Table Storage when Gateway starts
var userTable = app.Services
    .GetRequiredService<UserTableService>();

await userTable.InitializeAsync();

// Swagger is available in Development.
// In Production, it must be explicitly enabled.
if (app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Gateway homepage
app.MapGet("/", () =>
    "CoffeeNChill Gateway is running!"
);

// Gateway health endpoint
app.MapGet("/api/gateway/health", () =>
    new GatewayStatusDto(
        "CoffeeNChill Gateway",
        "Running"
    )
)
.WithName("GatewayHealth")
.WithTags("Gateway")
.Produces<GatewayStatusDto>(200);

// Get all menu items
app.MapGet("/api/menu", ForwardMenuGet)
    .WithName("GetAllMenuItems")
    .WithTags("Menu")
    .Produces<JsonElement>(200, "application/json")
    .ProducesProblem(502);

// Get menu items by category
app.MapGet(
    "/api/menu/category/{category}",
    ForwardMenuGet
)
.WithName("GetMenuItemsByCategory")
.WithTags("Menu")
.Produces<JsonElement>(200, "application/json")
.ProducesProblem(502);

// Authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Register authentication endpoints
app.MapAuthEndpoints();

// Forward remaining API requests through YARP
app.MapReverseProxy();

app.Run();

// Forward menu requests to Azure Functions
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

