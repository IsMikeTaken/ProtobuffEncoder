record WeatherJsonRequest
{
    public string? City { get; init; }
    public int Days { get; init; }
    public bool IncludeHourly { get; init; }
}