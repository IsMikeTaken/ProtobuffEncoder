public class ChatJsonMessage
{
    public string? Source { get; set; }
    public string? Text { get; set; }
    public int Level { get; set; }
    public List<string>? Tags { get; set; }
    public int? ByteSize { get; set; }
    public double? ProcessingTimeMs { get; set; }
}
