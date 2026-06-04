using System.Net.Http.Json;
using System.Text.Json;
using AIService.Options;

namespace AIService.Services;

public sealed class GeminiApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiApiClient(GeminiOptions options)
    {
        _options = options;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<JsonDocument> PostAsync(
        string endpoint,
        object request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(request, options: SerializerOptions);

        // Gửi request sang Gemini API.
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini API error {(int)response.StatusCode}: {responseBody}");
        }

        return JsonDocument.Parse(responseBody);
    }
}
