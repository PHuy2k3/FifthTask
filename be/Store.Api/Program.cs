// Program.cs
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Store.Api.Services;
using Store.Biz.Interfaces;
using Store.Biz.Services;
using Store.Data;
using Store.Data.Interfaces;
using Store.Data.Repositories;
using Store.Biz.Hubs;
using Store.Biz.Background;

var builder = WebApplication.CreateBuilder(args);

// ---- READ CONFIG ----
var cfg = builder.Configuration;
var conn = cfg.GetConnectionString("DefaultConnection");
Console.WriteLine("[DEBUG] Connection = " + conn);

// ---- CORS ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", p =>
        p.WithOrigins("http://localhost:3000")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
    );
});

// ---- Controllers + Swagger ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme{Reference=new OpenApiReference{Id="Bearer",Type=ReferenceType.SecurityScheme}}, new string[]{} }
    });
});

// ---- DbContext ----
builder.Services.AddDbContext<StoreDbContext>(opt => opt.UseSqlServer(conn));

// ---- DI: existing ----
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// ---- SignalR + Biz integrations ----
builder.Services.AddSignalR();

// Background queue + worker (in-memory)
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<NotificationWorker>();

// Biz services
builder.Services.AddScoped<IOrderService, OrderService>();

// Realtime notifier implementation (API layer implements notifier using IHubContext<OrderHub>)
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();

// ---- JWT (FIX 401) ----
var jwtKey = cfg["Jwt:Key"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(o =>
{
    o.RequireHttpsMetadata = false;
    o.SaveToken = true;

    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        NameClaimType = System.Security.Claims.ClaimTypes.Name
    };

    // Allow SignalR to read token from query string (for WebSocket clients)
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var accessToken = ctx.Request.Query["access_token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(accessToken) && ctx.Request.Path.StartsWithSegments("/hubs/orders"))
            {
                ctx.Token = accessToken;
            }

            Console.WriteLine("[JWT] OnMessageReceived: " + ctx.Request.Headers["Authorization"]);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            Console.WriteLine("[JWT] OnTokenValidated OK. Name=" + ctx.Principal.Identity.Name);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine("[JWT] FAILED: " + ctx.Exception);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            Console.WriteLine("[JWT] CHALLENGE: " + ctx.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ---- Build app ----
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---- Ensure DB ----
using (var s = app.Services.CreateScope())
{
    var db = s.ServiceProvider.GetRequiredService<StoreDbContext>();
    db.Database.EnsureCreated();

    if (!db.Users.Any(u => u.Username == "admin"))
    {
        db.Users.Add(new Store.Data.Model.User
        {
            Username = "admin",
            Email = "admin@local",
            PasswordHash = PasswordHasher.Hash("Admin123!"),
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

// ---- Middlewares ----
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();   // MUST BE BEFORE Authorization
app.UseAuthorization();

// ---- SignalR endpoints ----
// Map the hub from the Biz project (OrderHub)
app.MapHub<OrderHub>("/hubs/orders");

app.MapControllers();

app.Run();
