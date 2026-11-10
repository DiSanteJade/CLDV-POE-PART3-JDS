
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

public class UserTableService
{
    private readonly TableClient _table;

    public UserTableService(IConfiguration configuration)
    {
        var connectionString =
            configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Storage connection string is missing."
            );

        _table = new TableClient(connectionString, "Users");
    }

    // Create the Users table if necessary
    public async Task InitializeAsync()
    {
        await _table.CreateIfNotExistsAsync();
    }

    // Find an existing user by email
    public async Task<UserEntity?> GetByEmailAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();

        string[] roles = { "Customer", "Staff", "Admin" };

        foreach (var role in roles)
        {
            var result =
                await _table.GetEntityIfExistsAsync<UserEntity>(
                    role,
                    email
                );

            if (result.HasValue)
            {
                return result.Value;
            }
        }

        return null;
    }

    // Register a new Customer account
    public async Task<bool> AddCustomerAsync(UserEntity user)
    {
        user.PartitionKey = "Customer";
        user.RowKey = user.RowKey.Trim().ToLowerInvariant();

        // Prevent duplicate accounts
        if (await GetByEmailAsync(user.RowKey) != null)
        {
            return false;
        }

        try
        {
            await _table.AddEntityAsync(user);
            return true;
        }
        catch (RequestFailedException ex)
            when (ex.Status == 409)
        {
            return false;
        }
    }

    // Create a Staff account through trusted setup code
    public async Task<bool> AddStaffAsync(UserEntity user)
    {
        user.PartitionKey = "Staff";
        user.RowKey = user.RowKey.Trim().ToLowerInvariant();

        // Prevent duplicate accounts across roles
        if (await GetByEmailAsync(user.RowKey) != null)
        {
            return false;
        }

        try
        {
            await _table.AddEntityAsync(user);
            return true;
        }
        catch (RequestFailedException ex)
            when (ex.Status == 409)
        {
            return false;
        }
    }
}
