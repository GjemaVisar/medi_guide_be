namespace medi_guide_be.Domain.Entities;

public record DiseaseVectorRecord(string Id, string Name, byte[] Vector, double Magnitude);
