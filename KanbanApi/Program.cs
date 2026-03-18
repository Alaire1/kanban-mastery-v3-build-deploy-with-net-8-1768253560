using KanbanApi.Data;
using KanbanApi.Endpoints;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity with API endpoints
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Register application services
builder.Services.AddScoped<IAuthorizationHandler, IsBoardOwnerHandler>(); // board owner authorization

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsBoardOwner", policy =>
        policy.Requirements.Add(new IsBoardOwnerRequirement()));
});

builder.Services.AddScoped<IBoardService, BoardService>(); // board management
builder.Services.AddScoped<IUserProfileService, UserProfileService>(); // user profile management
// Minimal API helpers
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Identity API endpoints (e.g., /register, /login, /logout, etc.)
app.MapIdentityApi<ApplicationUser>();

// User-related endpoints
app.MapUserEndpoints();

// Board-related endpoints
app.MapBoardEnpoints();

// Board members endpoints
app.MapBoardMembersEndpoints();

// Example test endpoint
app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
