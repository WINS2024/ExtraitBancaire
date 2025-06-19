using System.Text.Json.Serialization;

public class SplitPdfResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("splitFiles")]
    public List<string> SplitFiles { get; set; }

    [JsonPropertyName("splitFileNames")]
    public List<string> SplitFileNames { get; set; }
}