using System.Net;
using System.Net.Http.Json;

namespace SaasPlatform.Tests;

public class ValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_reports_every_problem_by_field()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { companyName = "", fullName = "", email = "not-an-email", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Element();
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("companyName", out _));
        Assert.True(errors.TryGetProperty("fullName", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString())); // what the UI shows
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("missing@tld")]
    [InlineData("@no-user.com")]
    [InlineData("spaces in@test.com")]
    public async Task Invalid_emails_are_rejected(string email)
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { companyName = "C", fullName = "N", email, password = "password123" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Overlong_passwords_are_rejected_to_protect_the_hasher()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { companyName = "C", fullName = "N", email = Api.Unique("long") + "@test.com", password = new string('a', 129) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Project_and_task_fields_are_validated()
    {
        var admin = await factory.RegisterCompany();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post("/api/projects", new { name = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post("/api/projects", new { name = new string('x', 201) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.Post("/api/projects", new { name = "ok", description = new string('x', 2001) })).StatusCode);

        var pid = (await (await admin.Post("/api/projects", new { name = "P" })).Element()).Id();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post($"/api/projects/{pid}/tasks", new { title = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post($"/api/projects/{pid}/tasks", new { title = new string('x', 301) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post($"/api/projects/{pid}/tasks", new { title = "t", priority = "Urgent" })).StatusCode);
    }

    [Theory]
    [InlineData("Nope")]
    [InlineData("99")]
    [InlineData("")]
    public async Task Task_status_must_be_a_real_status_name(string status)
    {
        var admin = await factory.RegisterCompany();
        var pid = (await (await admin.Post("/api/projects", new { name = "P" })).Element()).Id();
        var tid = (await (await admin.Post($"/api/projects/{pid}/tasks", new { title = "T" })).Element()).Id();

        var res = await admin.Put($"/api/projects/{pid}/tasks/{tid}", new { title = "T", status });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Comments_cannot_be_empty_or_huge()
    {
        var admin = await factory.RegisterCompany();
        var pid = (await (await admin.Post("/api/projects", new { name = "P" })).Element()).Id();
        var tid = (await (await admin.Post($"/api/projects/{pid}/tasks", new { title = "T" })).Element()).Id();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = new string('x', 2001) })).StatusCode);
    }

    [Fact]
    public async Task Malformed_json_returns_a_clean_400_not_a_500()
    {
        var admin = await factory.RegisterCompany();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/projects") { Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Session.Token);
        var res = await admin.Http.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Company_name_can_be_changed_by_an_admin_only_and_is_validated()
    {
        var admin = await factory.RegisterCompany();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put("/api/account/company", new { name = "  " })).StatusCode);
        var ok = await admin.Put("/api/account/company", new { name = "Renamed Inc" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Renamed Inc", (await ok.Element()).GetProperty("companyName").GetString());
    }
}
