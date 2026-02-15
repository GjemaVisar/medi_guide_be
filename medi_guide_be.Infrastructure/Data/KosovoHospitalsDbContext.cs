using Microsoft.Extensions.Configuration;

namespace medi_guide_be.Infrastructure.Data;

/// <summary>
/// MongoDB context for the kosovo-hospitals database.
/// </summary>
public class KosovoHospitalsDbContext : MongoDbContext
{
    public KosovoHospitalsDbContext(IConfiguration configuration)
        : base(
            configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
            configuration["MongoDB:KosovoHospitals:DatabaseName"] ?? "kosovo-hospitals")
    {
    }
}
