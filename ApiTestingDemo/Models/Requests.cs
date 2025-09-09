
namespace ApiTestingDemo.Models;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken);

public class RefreshInfo
{
    public string Token { get; set; } = default!;
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

// Users
public class CreateUserRequest
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Role { get; set; }
}

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
}

// Products
public class CreateProductRequest
{
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal Price { get; set; }
}

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
}

// Orders
public class CreateOrderRequest
{
    public int UserId { get; set; }
    public List<OrderItemRequest>? Items { get; set; }
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
