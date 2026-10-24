using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaasPlatform.Infrastructure.Persistence;

namespace SaasPlatform.Tests;

/// <summary>
/// Hosts the real API in-process against a private in-memory SQLite database, so every test runs through the
/// same pipeline as production: JWT auth, validation, role checks and tenant query filters. No mocks.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // A shared-cache in-memory database stays alive as long as this connection is open.
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString = $"Data Source=test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz",
            ["ConnectionStrings:Default"] = _connectionString,
            ["RateLimiting:AuthPermitLimit"] = "1000", // rate limiting has its own dedicated test
            ["Database:MigrateOnStartup"] = "true"
        }));
    }

    /// <summary>A factory whose login/register limit is tiny, for proving the limiter returns 429.</summary>
    public WebApplicationFactory<Program> WithAuthLimit(int permits) => WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = permits.ToString() })));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}

public record Session(string Token, string RefreshToken, Guid TenantId, Guid UserId, string Email, string FullName, string Role, string CompanyName);

/// <summary>A signed-in caller. Holds the access token and can send requests as that user.</summary>
public class Client(HttpClient http, Session session)
{
    public Session Session { get; set; } = session;
    public HttpClient Http => http;

    public Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.Token);
        if (body is not null) req.Content = JsonContent.Create(body);
        return http.SendAsync(req);
    }

    public Task<HttpResponseMessage> Get(string url) => Send(HttpMethod.Get, url);
    public Task<HttpResponseMessage> Post(string url, object? body = null) => Send(HttpMethod.Post, url, body ?? new { });
    public Task<HttpResponseMessage> Put(string url, object body) => Send(HttpMethod.Put, url, body);
    public Task<HttpResponseMessage> Delete(string url) => Send(HttpMethod.Delete, url);
}

public static class Api
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> Read<T>(this HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidOperationException($"Empty body: {text}");
    }

    public static async Task<string> Text(this HttpResponseMessage r) => await r.Content.ReadAsStringAsync();

    public static async Task<JsonElement> Element(this HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    public static async Task<List<JsonElement>> List(this HttpResponseMessage r) =>
        (await r.Element()).EnumerateArray().ToList();

    private static int _counter;
    public static string Unique(string prefix) => $"{prefix}{Interlocked.Increment(ref _counter)}{Guid.NewGuid().ToString("N")[..6]}";

    /// <summary>Registers a brand-new company and returns its admin, signed in.</summary>
    public static async Task<Client> RegisterCompany(this ApiFactory f, string? company = null, string password = "password123")
    {
        var http = f.CreateClient();
        var name = company ?? Unique("Co");
        var res = await http.PostAsJsonAsync("/api/auth/register", new
        {
            companyName = name, fullName = "Admin " + name, email = Unique("admin") + "@test.com", password
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return new Client(http, await res.Read<Session>());
    }

    /// <summary>Admin invites someone and they accept, ending up as a signed-in member of the admin's company.</summary>
    public static async Task<Client> AddMember(this ApiFactory f, Client admin, string role = "Member", string password = "password123")
    {
        var email = Unique("member") + "@test.com";
        var created = await (await admin.Post("/api/invitations", new { email, role })).Element();
        var token = created.GetProperty("token").GetString()!;

        var http = f.CreateClient();
        var res = await http.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "Member " + email, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return new Client(http, await res.Read<Session>());
    }

    public static Guid Id(this JsonElement e, string prop = "id") => e.GetProperty(prop).GetGuid();
}
