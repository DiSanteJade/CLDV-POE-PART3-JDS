using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeNChill.Gateway.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // REGISTER CUSTOMER
        auth.MapPost("/register", async (
            RegisterRequest request,
            UserTableService users,
            IPasswordHasher<UserEntity> hasher) =>
        {
            if (string.IsNullOrWhiteSpace(request.FullName)
                || request.FullName.Trim().Length > 120
                || string.IsNullOrWhiteSpace(request.Email)
                || request.Email.Length > 320
                || !new EmailAddressAttribute().IsValid(request.Email)
                || string.IsNullOrEmpty(request.Password)
                || request.Password.Length < 8)
            {
                return Results.BadRequest(new
                {
                    message = "Please provide valid registration details."
                });
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var user = new UserEntity
            {
                PartitionKey = "Customer",
                RowKey = email,
                FullName = request.FullName.Trim(),
                CreatedDate = DateTimeOffset.UtcNow
            };

            user.PasswordHash =
                hasher.HashPassword(user, request.Password);

            var created = await users.AddCustomerAsync(user);

            if (!created)
            {
                return Results.Conflict(new
                {
                    message = "An account with this email already exists."
                });
            }

            return Results.Json(
                new { message = "Account registered successfully." },
                statusCode: StatusCodes.Status201Created
            );
        })
        .AllowAnonymous()
        .WithName("RegisterUser")
        .Produces(201)
        .Produces(400)
        .Produces(409);

        // LOGIN
        auth.MapPost("/login", async (
            LoginRequest request,
            UserTableService users,
            IPasswordHasher<UserEntity> hasher,
            IConfiguration config) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email)
                || string.IsNullOrEmpty(request.Password))
            {
                return Results.BadRequest(new
                {
                    message = "Email and password are required."
                });
            }

            var user = await users.GetByEmailAsync(request.Email);

            if (user == null)
            {
                return Results.Unauthorized();
            }

            var verification = hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password
            );

            if (verification == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var key = Convert.FromBase64String(
                config["Jwt:Key"]!);

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(60);

            var claims = new[]
            {
                new Claim("sub", user.RowKey),
                new Claim("email", user.RowKey),
                new Claim("name", user.FullName),
                new Claim("role", user.PartitionKey),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = config["Jwt:Issuer"],
                Audience = config["Jwt:Audience"],
                IssuedAt = now,
                NotBefore = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256
                )
            };

            var token = new JsonWebTokenHandler()
                .CreateToken(tokenDescriptor);

            return Results.Ok(new
            {
                message = "Login successful.",
                accessToken = token,
                tokenType = "Bearer",
                expiresAt = expires,
                role = user.PartitionKey
            });
        })
        .AllowAnonymous()
        .WithName("LoginUser")
        .Produces(200)
        .Produces(400)
        .Produces(401);

        // PROTECTED USER PROFILE
        auth.MapGet("/me", (HttpContext context) =>
        {
            var user = context.User;

            return Results.Ok(new
            {
                email = user.FindFirst("email")?.Value,
                fullName = user.FindFirst("name")?.Value,
                role = user.FindFirst("role")?.Value
            });
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .Produces(200)
        .Produces(401);
    }
}

