using System.Text.Json.Serialization;
using System.Text.Json;
using Carter;
using Dapr.Client;
using Dapr.Extensions.Configuration;
using FluentValidation;
using gabMileage.IntegrationTypes;
using gabMileage.Mileage.Api;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Logging;
using trakUtility.api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
var assembly = typeof(Program).Assembly;

builder.Services.AddDaprClient();

builder.Configuration.AddDaprSecretStore(
    "gab-secretstore",
    new DaprClientBuilder().Build());

var authenticationOptions = builder.Configuration.GetSection("SwaggerAuthentication").Get<SwaggerAuthenticationOptions>();
ArgumentNullException.ThrowIfNull(authenticationOptions);

var connectionString = builder.Configuration["ConnectionStrings:MileageDB"];
ArgumentNullException.ThrowIfNull(connectionString);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        cb => cb.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.AddCustomSwagger(authenticationOptions);
builder.AddCustomAuthentication();
builder.AddCustomAuthorization();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.Lifetime = ServiceLifetime.Scoped;
});

builder.Services.AddControllers();

// builder.Services.AddDbContext<TrakPomodoroContext>(
//     options => options.UseSqlServer(connectionString));
// builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
// builder.Services.AddScoped<IUserSession, UserSession>();
// builder.Services.AddScoped<IUnitOfWork, PomodoroUnitOfWork>();
// builder.Services.AddScoped<IPomodoroRepository, PomodoroRepository>();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddValidatorsFromAssembly(assembly);

builder.Services.AddCarter();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    IdentityModelEventSource.ShowPII = true;
}

app.UseCloudEvents();
app.UseCors();

app.UseCustomSwagger(authenticationOptions);
app.UseStaticFiles();

app.MapCarter();

app.UseAuthorization();

app.MapGet("/mileagerecords", () =>
{
    var mileageRecords = Enumerable.Range(1, 5).Select(index => new MileageRecord
    {
        FilledAt = DateTime.Now.AddDays(index),

    }).ToArray();

    return mileageRecords;
})
.WithName("GetMileageRecords")
.WithOpenApi();

await app.RunAsync();





