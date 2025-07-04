using System.Text.Json.Serialization;

public struct Chat
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("users")]
    public string[] Users { get; set; }
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; }
}

public struct ChatMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; }
    [JsonPropertyName("version")]
    public int Version { get; set; }
    [JsonPropertyName("userId")]
    public string UserId { get; set; }
    [JsonPropertyName("content")]
    public string Content { get; set; }
}