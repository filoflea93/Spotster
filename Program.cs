using Spotster.Data;

using Spotster.Entities;

using Spotster.Hubs;

using Spotster.Infrastructure.Email;

using Spotster.Infrastructure.Auth;

using Spotster.Infrastructure.Json;

using Spotster.Infrastructure.Redis;

using Spotster.Infrastructure.Storage;

using Spotster.Middleware;

using Spotster.Repositories;

using Spotster.Services;

using Spotster.Services.AntiFraud;

using Spotster.Services.Cache;

using Spotster.Services.Realtime;

using Spotster.Services.Geo;

using Spotster.Services.Localization;

using Spotster.Services.Users;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Localization;

using Microsoft.EntityFrameworkCore;

using Microsoft.IdentityModel.Tokens;

using System.Globalization;

using System.Text;

using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);



builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = new[] { "it", "en" };
    options.SetDefaultCulture("it")
        .AddSupportedCultures(supported)
        .AddSupportedUICultures(supported);
    options.RequestCultureProviders.Insert(0, new HeaderCultureProvider());
});



builder.Services.AddControllers()

    .AddJsonOptions(options =>

    {

        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());

        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());

    });






var redisSettings = builder.Configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>() ?? new RedisSettings();
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection(RedisSettings.SectionName));

if (redisSettings.IsConfigured)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisSettings.ConnectionString;
        options.InstanceName = "spotster:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.UseNetTopologySuite()));



var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT configuration missing in appsettings.");

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.SectionName));
builder.Services.Configure<BlobStorageSettings>(builder.Configuration.GetSection(BlobStorageSettings.SectionName));

var blobSettings = builder.Configuration.GetSection(BlobStorageSettings.SectionName).Get<BlobStorageSettings>() ?? new BlobStorageSettings();
if (blobSettings.UseAzure)
{
    builder.Services.AddSingleton<IBlobStorage, AzureBlobStorage>();
}
else
{
    builder.Services.AddSingleton<IBlobStorage, LocalBlobStorage>();
}

builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<User>, LegacyMigratingPasswordHasher>();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();



var signalRBuilder = builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

if (redisSettings.IsConfigured)
{
    signalRBuilder.AddStackExchangeRedis(redisSettings.ConnectionString!);
}

builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SignalRUserIdProvider>();

builder.Services.AddCors(options =>

{

    options.AddDefaultPolicy(policy =>

        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

});



builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("geocode", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "user"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});



builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IParkingRepository, ParkingRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IParkingService, ParkingService>();

builder.Services.AddScoped<IReputationService, ReputationService>();

builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();

builder.Services.AddScoped<IAntiFraudService, AntiFraudService>();

builder.Services.AddScoped<IGeoService, GeoService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserReviewRepository, UserReviewRepository>();
builder.Services.AddScoped<IUserReviewService, UserReviewService>();

builder.Services.AddScoped<IStatsService, StatsService>();

builder.Services.AddScoped<IParkingCacheService, ParkingCacheService>();

builder.Services.AddScoped<IParkingRequestCacheService, ParkingRequestCacheService>();

builder.Services.AddScoped<IParkingRealtimeNotifier, ParkingRealtimeNotifier>();

builder.Services.AddScoped<IParkingRequestRepository, ParkingRequestRepository>();

builder.Services.AddScoped<IParkingRequestService, ParkingRequestService>();

builder.Services.AddScoped<IRequestMessagingService, RequestMessagingService>();

builder.Services.AddScoped<IParkingRequestMessageRepository, ParkingRequestMessageRepository>();
builder.Services.AddScoped<IParkingRequestBlockRepository, ParkingRequestBlockRepository>();

builder.Services.AddScoped<PaymentMethodLabels>();

builder.Services.AddGeocoding();

builder.Services.AddHostedService<ParkingExpirationService>();

builder.Services.AddHostedService<ParkingCacheRefreshService>();

builder.Services.AddHostedService<ParkingRequestCacheRefreshService>();



var app = builder.Build();



app.UseMiddleware<GlobalExceptionMiddleware>();

var locOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.UseDefaultFiles();

app.UseStaticFiles();



app.MapControllers();

app.MapHub<ParkingHub>("/hubs/parking");

app.MapFallbackToFile("index.html");



using (var scope = app.Services.CreateScope())

{

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();



    logger.LogInformation("Applying database migrations...");

    await db.Database.MigrateAsync();

    logger.LogInformation("Seeding demo data...");

    await DbInitializer.SeedAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<User>>());

}



app.Run();


