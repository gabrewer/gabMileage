using Dapr.Client;
using Dapr.Extensions.Configuration;
using gabMileage.IntegrationTypes;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddDaprSecretStore(
    "gab-secretstore",
    new DaprClientBuilder().Build());

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.Run();

