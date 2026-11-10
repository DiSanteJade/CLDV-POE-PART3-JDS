namespace CoffeeNChill.Gateway.DTOs;

// <summary>
// Describes the current status of the API Gateway.
// </summary>
public record GatewayStatusDto(
    string Service,
    string Status
);
