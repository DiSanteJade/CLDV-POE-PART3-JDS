
using Azure.Data.Tables;
using CoffeeNChill.Gateway.Models;
using Microsoft.AspNetCore.Identity;

namespace CoffeeNChill.Gateway.Services;

public static class StaffBootstrapper
{
    public static async Task<bool> RunIfRequestedAsync(
        WebApplication app)
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("BOOTSTRAP_STAFF"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? email = Environment
            .GetEnvironmentVariable("STAFF_SEED_EMAIL")
            ?.Trim()
            .ToLowerInvariant();

        string? password = Environment
            .GetEnvironmentVariable("STAFF_SEED_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            password.Length < 12)
        {
            throw new InvalidOperationException(
                "Staff provisioning requires an email and a password of at least 12 characters.");
        }

        string connectionString =
            app.Configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Storage:ConnectionString is not configured.");

        var table = new TableClient(
            connectionString,
            "Users");

        await table.CreateIfNotExistsAsync();

        // Prevent conflicting accounts with the same email.
        foreach (string role in new[]
                 { "Customer", "Admin", "Staff" })
        {
            var existing =
                await table.GetEntityIfExistsAsync<UserEntity>(
                    role,
                    email);

            if (existing.HasValue)
            {
                if (role == "Staff")
                {
                    Console.WriteLine(
                        "Staff account already exists. No changes made.");
                    return true;
                }

                throw new InvalidOperationException(
                    "This email already belongs to another role.");
            }
        }

        using var scope = app.Services.CreateScope();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<UserEntity>>();

        var staff = new UserEntity
        {
            PartitionKey = "Staff",
            RowKey = email,
            FullName = "CoffeeNChill Demo Staff",
            CreatedDate = DateTimeOffset.UtcNow
        };

        staff.PasswordHash =
            hasher.HashPassword(staff, password);

        await table.AddEntityAsync(staff);

        Console.WriteLine(
            "Staff account provisioned successfully.");

        // Stop the one-time provisioning process.
        return true;
    }
}
