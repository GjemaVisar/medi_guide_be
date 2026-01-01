using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Data;
using medi_guide_be.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// MongoDB Configuration
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] 
    ?? "disease-dataset";

// Register MongoDB Context
builder.Services.AddSingleton<MongoDbContext>(sp => 
    new MongoDbContext(mongoConnectionString, mongoDatabaseName));

// Register Repositories
builder.Services.AddScoped<IDiseaseRepository, DiseaseRepository>();

// Add services to the container
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
