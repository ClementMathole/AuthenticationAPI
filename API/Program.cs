using Application.Interfaces;
using Application.Services;
using Infrastructure.Data;
using Infrastructure.Email;
using Infrastructure.Jwt;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuracion = builder.Configuration;

// Add services to the container.
builder.Services.AddDbContext<AuthDbContext>(option =>
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
builder.Services.AddScoped<IJwt, Jwt>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Memory Cache
builder.Services.AddMemoryCache();

// Jwt
var jwt = configuracion.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.GetValue<string>("Issuer"),
            ValidAudience = jwt.GetValue<string>("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// rate limiting
app.Use(async (context, next) =>
{
    var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    var keyIp = $"rl_{ip}";
    var attempts = cache.Get<int>(keyIp);

    attempts++;
    cache.Set(keyIp, attempts, TimeSpan.FromMinutes(1));

    if (attempts > 200)
    {
        context.Response.StatusCode = 429;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Too many requests"
        });
        return;
    }

    await next();
});

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
