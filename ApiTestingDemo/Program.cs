
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ApiTestingDemo.Models;
using ApiTestingDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// JWT configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]!;
var jwtIssuer = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var accessMinutes = int.Parse(jwtSection["AccessTokenMinutes"]!);
var refreshDays = int.Parse(jwtSection["RefreshTokenDays"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ApiTestingDemo", Version = "v1" });
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Введите JWT токен как: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// In-memory stores
var users = new List<User>
{
    new User { Id = 1, Name = "Admin", Email = "admin@example.com", CreatedAt = DateTime.UtcNow, Role="admin" },
    new User { Id = 2, Name = "John Doe", Email = "john@example.com", CreatedAt = DateTime.UtcNow, Role="user" }
};
var products = new List<Product>
{
    new Product { Id = 1, Name="Keyboard", Category="Peripherals", Price=49.99M, CreatedAt = DateTime.UtcNow },
    new Product { Id = 2, Name="Mouse", Category="Peripherals", Price=29.99M, CreatedAt = DateTime.UtcNow }
};
var orders = new List<Order>();
var refreshStore = new Dictionary<string, RefreshInfo>(); // refreshToken -> info

var app = builder.Build();

// Security headers to satisfy Postman tests
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    await next();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiTestingDemo v1");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => new { ok=true, name="ApiTestingDemo", time=DateTime.UtcNow });

// AUTH
app.MapPost("/auth/login", (LoginRequest req) =>
{
    var role = (req.Email == "admin@example.com" && req.Password == "admin123") ? "admin" : "user";
    var user = users.FirstOrDefault(u => u.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase));
    if (user is null)
    {
        user = new User { Id = users.Max(u=>u.Id)+1, Name = req.Email.Split('@')[0], Email = req.Email, CreatedAt = DateTime.UtcNow, Role=role };
        users.Add(user);
    }

    var (access, refresh) = TokenService.IssueTokens(user, user.Role ?? "user", jwtIssuer, jwtAudience, jwtKey, accessMinutes, refreshDays);
    refreshStore[refresh] = new RefreshInfo { Token=refresh, UserId=user.Id, ExpiresAt=DateTime.UtcNow.AddDays(refreshDays) };

    return Results.Ok(new { access_token = access, refresh_token = refresh, token_type="Bearer" });
});

app.MapPost("/auth/refresh", (RefreshRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.RefreshToken) || !refreshStore.TryGetValue(req.RefreshToken, out var info))
        return Results.Unauthorized();

    if (info.ExpiresAt < DateTime.UtcNow) { refreshStore.Remove(req.RefreshToken); return Results.Unauthorized(); }

    var user = users.FirstOrDefault(u => u.Id == info.UserId);
    if (user is null) return Results.Unauthorized();

    refreshStore.Remove(req.RefreshToken);
    var (access, refresh) = TokenService.IssueTokens(user, user.Role ?? "user", jwtIssuer, jwtAudience, jwtKey, accessMinutes, refreshDays);
    refreshStore[refresh] = new RefreshInfo { Token=refresh, UserId=user.Id, ExpiresAt=DateTime.UtcNow.AddDays(refreshDays) };

    return Results.Ok(new { access_token = access, refresh_token = refresh, token_type="Bearer" });
});

app.MapPost("/auth/logout", [Authorize] (LogoutRequest req) =>
{
    if (!string.IsNullOrEmpty(req.RefreshToken))
        refreshStore.Remove(req.RefreshToken);
    return Results.Ok(new { message="logged out" });
});

// USERS
app.MapGet("/users", [Authorize] (int page = 1, int pageSize = 10) =>
{
    var query = users.OrderBy(u=>u.Id).Skip((page-1)*pageSize).Take(pageSize);
    return Results.Ok(query);
});

app.MapGet("/users/{id:int}", [Authorize] (int id) =>
{
    var u = users.FirstOrDefault(x=>x.Id==id);
    return u is null ? Results.NotFound() : Results.Ok(u);
});

app.MapPost("/users", [Authorize] (CreateUserRequest req) =>
{
    var id = users.Count==0 ? 1 : users.Max(u=>u.Id)+1;
    var u = new User { Id=id, Name=req.Name, Email=req.Email, CreatedAt=DateTime.UtcNow, Role=req.Role ?? "user"};
    users.Add(u);
    return Results.Created($"/users/{u.Id}", u);
});

app.MapPut("/users/{id:int}", [Authorize] (int id, UpdateUserRequest req) =>
{
    var u = users.FirstOrDefault(x=>x.Id==id);
    if (u is null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(req.Name)) u.Name = req.Name!;
    if (!string.IsNullOrWhiteSpace(req.Email)) u.Email = req.Email!;
    if (!string.IsNullOrWhiteSpace(req.Role)) u.Role = req.Role!;
    return Results.Ok(u);
});

app.MapDelete("/users/{id:int}", [Authorize] (int id) =>
{
    var u = users.FirstOrDefault(x=>x.Id==id);
    if (u is null) return Results.NotFound();
    users.Remove(u);
    foreach (var key in refreshStore.Where(kv=>kv.Value.UserId==id).Select(kv=>kv.Key).ToList())
        refreshStore.Remove(key);
    return Results.NoContent();
});

// PRODUCTS
app.MapGet("/products", [Authorize] (string? category, decimal? minPrice, decimal? maxPrice, string? search) =>
{
    IEnumerable<Product> q = products;
    if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p=>p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    if (minPrice.HasValue) q = q.Where(p=>p.Price>=minPrice.Value);
    if (maxPrice.HasValue) q = q.Where(p=>p.Price<=maxPrice.Value);
    if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p=>p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(q);
});

app.MapPost("/products", [Authorize] (CreateProductRequest req) =>
{
    var id = products.Count==0 ? 1 : products.Max(p=>p.Id)+1;
    var p = new Product { Id=id, Name=req.Name, Category=req.Category, Price=req.Price, CreatedAt=DateTime.UtcNow };
    products.Add(p);
    return Results.Created($"/products/{p.Id}", p);
});

app.MapPut("/products/{id:int}", [Authorize] (int id, UpdateProductRequest req) =>
{
    var p = products.FirstOrDefault(x=>x.Id==id);
    if (p is null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(req.Name)) p.Name = req.Name!;
    if (!string.IsNullOrWhiteSpace(req.Category)) p.Category = req.Category!;
    if (req.Price.HasValue) p.Price = req.Price.Value;
    return Results.Ok(p);
});

// ORDERS
app.MapPost("/orders", [Authorize] (CreateOrderRequest req) =>
{
    var user = users.FirstOrDefault(u=>u.Id==req.UserId);
    if (user is null) return Results.BadRequest(new { error="user not found"});
    var id = orders.Count==0 ? 1 : orders.Max(o=>o.Id)+1;
    var ord = new Order
    {
        Id=id,
        UserId=req.UserId,
        Items=req.Items?.Select(i=> new OrderItem { ProductId=i.ProductId, Quantity=i.Quantity }).ToList() ?? new(),
        CreatedAt=DateTime.UtcNow
    };
    orders.Add(ord);
    return Results.Created($"/orders/{id}", ord);
});

app.MapGet("/orders", [Authorize] (int? userId) =>
{
    IEnumerable<Order> q = orders;
    if (userId.HasValue) q = q.Where(o=>o.UserId==userId.Value);
    return Results.Ok(q);
});

app.MapGet("/users/{id:int}/orders", [Authorize] (int id) =>
{
    var q = orders.Where(o=>o.UserId==id);
    return Results.Ok(q);
});

app.Run();
