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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, IsBoardOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IsBoardMemberHandler>();// board member authorization

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsBoardOwner", policy =>
        policy.Requirements.Add(new IsBoardOwnerRequirement()));

    options.AddPolicy("IsBoardMember", policy =>
        policy.Requirements.Add(new IsBoardMemberRequirement()));
});

builder.Services.AddScoped<IBoardService, BoardService>(); // board management
builder.Services.AddScoped<IUserProfileService, UserProfileService>(); 
builder.Services.AddScoped<IColumnService, ColumnService>();
builder.Services.AddScoped<IBoardMembersService, BoardMembersService>();
builder.Services.AddScoped<ICardService, CardService>();

// user profile management
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
app.MapBoardEndpoints();
app.MapBoardIdEndpoints();

// Board members endpoints
app.MapBoardMembersEndpoints();
app.MapColumnsEndpoints();

// Card endpoints
app.MapCardsEndpoints();

// Example test endpoint
app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
