using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.DTOs;

public class RegisterRequest
{
    [Required]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = "";
}
