using KanbanApi.Data;
using KanbanApi.Endpoints;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext with SQLite for development and SQL Server for production
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"ENV: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
Console.WriteLine($"CONN: {(connectionString == null ? "NULL" : "FOUND")}");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
// Add Identity with API endpoints
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Register application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, IsBoardOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IsBoardMemberHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsBoardOwner", policy =>
        policy.Requirements.Add(new IsBoardOwnerRequirement()));
    options.AddPolicy("IsBoardMember", policy =>
        policy.Requirements.Add(new IsBoardMemberRequirement()));
});

builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IColumnService, ColumnService>();
builder.Services.AddScoped<IBoardMembersService, BoardMembersService>();
builder.Services.AddScoped<ICardService, CardService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options => {
    options.AddPolicy("AllowReact", policy => {
        policy.WithOrigins(
            "http://localhost:3000",
            "https://your-frontend-url.com"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();



// Swagger available in all environments
app.UseSwagger();
app.UseSwaggerUI();

var webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath)
});

app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<ApplicationUser>();
app.MapUserEndpoints();
app.MapBoardEndpoints();
app.MapBoardIdEndpoints();
app.MapBoardMembersEndpoints();
app.MapColumnsEndpoints();
app.MapCardsEndpoints();
app.MapCardsAssignEndpoints();

app.MapFallbackToFile("index.html");



using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}


app.Run();
public partial class Program { }