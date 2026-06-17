using BankTransactions.Shared;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BankTransactions.Client;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ApiClient(Uri baseAddress)
    {
        _httpClient = new HttpClient { BaseAddress = baseAddress };
    }

    public Uri BaseAddress => _httpClient.BaseAddress ?? new Uri("http://localhost:5055");

    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), JsonOptions);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Invalid username or password.");
        }

        var auth = await ReadAsync<AuthResponse>(response);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return auth;
    }

    public async Task<List<T>> SearchAsync<T>(string path, string mode, string? search)
    {
        var url = $"{path}?mode={Uri.EscapeDataString(mode)}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        return await ReadAsync<List<T>>(await _httpClient.GetAsync(url));
    }

    public async Task<T> PostAsync<TRequest, T>(string path, string mode, TRequest request)
    {
        return await ReadAsync<T>(await _httpClient.PostAsJsonAsync($"{path}?mode={mode}", request, JsonOptions));
    }

    public async Task<T> PutAsync<TRequest, T>(string path, int id, string mode, TRequest request)
    {
        return await ReadAsync<T>(await _httpClient.PutAsJsonAsync($"{path}/{id}?mode={mode}", request, JsonOptions));
    }

    public async Task DeleteAsync(string path, int id, string mode)
    {
        var response = await _httpClient.DeleteAsync($"{path}/{id}?mode={mode}");
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return result ?? throw new InvalidOperationException("Empty response from server.");
    }

    private static async Task ThrowApiExceptionAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        try
        {
            var error = JsonSerializer.Deserialize<ApiError>(text, JsonOptions);
            throw new InvalidOperationException(error?.Message ?? response.ReasonPhrase ?? "API request failed.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(response.ReasonPhrase ?? "API request failed.");
        }
    }
}
