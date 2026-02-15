using medi_guide_be.Domain.Repositories;
using medi_guide_be.Domain.Services;
using medi_guide_be.Infrastructure.Data;
using medi_guide_be.Infrastructure.Repositories;
using medi_guide_be.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? "disease-dataset";

builder.Services.AddSingleton<MongoDbContext>(sp =>
    new MongoDbContext(mongoConnectionString, mongoDatabaseName));
builder.Services.AddSingleton<KosovoHospitalsDbContext>(sp =>
    new KosovoHospitalsDbContext(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddScoped<IDiseaseVectorRepository, DiseaseVectorRepository>();
builder.Services.AddScoped<IDiseaseRepository, DiseaseRepository>();
builder.Services.AddScoped<IKosovoHospitalRepository, KosovoHospitalRepository>();
builder.Services.AddScoped<IDiseaseSimilarityService, CosineSimilarityService>();
builder.Services.AddHostedService<CacheWarmupService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngularApp");

app.UseAuthorization();
app.MapControllers();

app.Run();