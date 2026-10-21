namespace RollRadar.Api.Transcripts;

public class TranscriptOptions
{
    public const string Section = "Transcripts";

    /// <summary>Output folder of montar_tecnicas.py, relative to the API content root.</summary>
    public string OutputPath { get; set; } = "../../samples/saida";
}
