namespace RollRadar.Api.Curation;

/// <summary>The curator's verdict on a moment. Same values as technique_moments.status in the database plan.</summary>
public static class CurationStatus
{
    public const string Pending = "pendente";
    public const string Good = "bom";
    public const string Bad = "ruim";

    public static bool IsValid(string? status) => status is Pending or Good or Bad;
}
