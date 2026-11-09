using System.Text.Json;
using CoffeeNChill.ApiGateway.Middleware;
using CoffeeNChill.ApiGateway.Options;
using CoffeeNChill.ApiGateway.Services;
using CoffeeNChill.ApiGateway.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Validated before anything else so an unusable secret fails at startup instead of on the first login attempt.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? new JwtOptions();
jwtOptions.EnsureValid(builder.Environment.EnvironmentName);

var storageOptions = StorageOptions.Resolve(builder.Configuration);

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(storageOptions);

builder.Services.AddControllers();

// Microsoft Learn - Use Swagger/OpenAPI with ASP.NET Core
// https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger?view=aspnetcore-10.0
// Swashbuckle.AspNetCore - ASP.NET Core Swagger tooling
// https://github.com/domaindrivendev/Swashbuckle.AspNetCore
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoffeeNChill API Gateway",
        Version = "v3.0",
        Description =
            "Unified ASP.NET Core gateway for the CoffeeNChill canteen system. " +
            "Anonymous students can browse the menu and queue orders; Admin and Staff JWT " +
            "holders can mutate the menu, manage staff documents and advance order status. " +
            "Obtain a token from POST /api/auth/login and paste it into the Authorize dialog.",
        Contact = new OpenApiContact
        {
            Name = "Keyur Keshav (ST10472464) — Team Member 1",
            Url = new Uri("https://github.com/ST10472464")
        },
        License = new OpenApiLicense
        {
            Name = "CLDV6212 Portfolio of Evidence",
            Url = new Uri("https://www.iie.ac.za/")
        }
    });

    // The generated XML file carries the <summary>, <param> and <response> tags that become the operation summaries, parameter descriptions and response codes shown in Swagger UI.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description =
            "Paste the JWT returned by POST /api/auth/login. " +
            "Swagger adds the 'Bearer ' prefix automatically."
    });

    // Built against the host document so the reference serialises as a real $ref rather than an empty object. [AllowAnonymous] operations opt back out of it in AuthorizeCheckOperationFilter.
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });

    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

// Microsoft Learn - JwtBearer Class
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer?view=aspnetcore-10.0
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // The token already carries ClaimTypes.Name and ClaimTypes.Role, so these two settings only confirm the defaults rather than remap anything.
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        // 401 and 403 go through the same problem+json contract as every other error, so clients never have to special-case authentication failures.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteProblemAsync(context.Response, StatusCodes.Status401Unauthorized,
                    "Authentication required",
                    "Supply a valid JWT as 'Authorization: Bearer <token>'. " +
                    "Obtain one from POST /api/auth/login.",
                    context.Request.Path);
            },
            OnForbidden = async context =>
            {
                await WriteProblemAsync(context.Response, StatusCodes.Status403Forbidden,
                    "Insufficient role",
                    "The supplied token is valid but does not grant access to this resource. " +
                    "An Admin or Staff role is required.",
                    context.Request.Path);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IMenuService, MenuService>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();

builder.Services.AddHttpClient<IFunctionsProxyService, FunctionsProxyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

// Origins come from configuration; with none configured the policy stays open so the local Swagger UI can still call the API.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CoffeeNChill", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        else
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
    });
});

var app = builder.Build();

// Outermost, so nothing below it can leak a stack trace to the caller.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Served in every environment: the brief requires a reachable /swagger on the deployed instance.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoffeeNChill API Gateway v3.0");
    options.DocumentTitle = "CoffeeNChill API Gateway";
    options.RoutePrefix = "swagger";
    options.EnablePersistAuthorization();
    options.EnableTryItOutByDefault();
});

app.UseCors("CoffeeNChill");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Writes an RFC 7807 body using the shared camelCase contract.
static async Task WriteProblemAsync(
    HttpResponse response, int status, string title, string detail, PathString instance)
{
    response.StatusCode = status;
    response.ContentType = "application/problem+json";

    var body = JsonSerializer.Serialize(new
    {
        title,
        detail,
        status,
        instance = instance.Value
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    await response.WriteAsync(body);
}
