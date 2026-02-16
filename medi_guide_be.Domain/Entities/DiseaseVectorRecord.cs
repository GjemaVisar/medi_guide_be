namespace medi_guide_be.Domain.Entities;

public record DiseaseVectorRecord(string Id, string Name, int[] ActiveIndices, double Magnitude);
