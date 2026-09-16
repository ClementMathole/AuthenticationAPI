using System.Text;
using System.Threading.RateLimiting;
using API.Configuration;
using API.Health;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Data;
using Infrastructure.Email;
using Infrastructure.Jwt;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be provided through environment configuration.");

var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddProblemDetails();
builder.Services.AddDbContextFactory<AuthDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient<IEmailSender, MailpitEmailSender>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Email:MailpitBaseUrl"] ?? throw new InvalidOperationException("Email:MailpitBaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<IJwt, Jwt>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(
        options => options.Key.Length >= JwtOptions.MinimumKeyLength,
        $"Jwt:Key must be at least {JwtOptions.MinimumKeyLength} characters long.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Issuer),
        "Jwt:Issuer is required.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Audience),
        "Jwt:Audience is required.")
    .Validate(
        options => options.AccessTokenMinutes is > 0 and <= 60,
        "Jwt:AccessTokenMinutes must be between 1 and 60.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
        {
            var jwt = jwtOptions.Value;

            options.RequireHttpsMetadata = !isDevelopment;
            options.SaveToken = false;
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "email",
                RoleClaimType = "role",
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessionId))
                    {
                        context.Fail("Invalid session.");
                        return;
                    }

                    var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                    if (!await users.IsSessionActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
                        context.Fail("Session is no longer active.");
                }
            };
        });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext =>
            {
                var ipAddress =
                    httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    ipAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "database",
        tags: new[] { "ready" });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Configuration.GetValue<bool>("HttpsRedirection:Enabled"))
    app.UseHttpsRedirection();

if (app.Environment.IsDevelopment() && app.Configuration.GetValue("OpenApi:Enabled", defaultValue: true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

app.MapControllers();
app.Run();

public partial class Program
{
}
