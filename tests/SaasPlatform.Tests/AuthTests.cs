using System.Net;
using System.Net.Http.Json;

namespace SaasPlatform.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> Refresh(HttpClient http, string token) =>
        http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });

    [Fact]
    public async Task Register_then_login_works_and_email_is_case_insensitive()
    {
        var http = factory.CreateClient();
        var email = Api.Unique("Mixed") + "@Test.com";
        var reg = await http.PostAsJsonAsync("/api/auth/register", new { companyName = "C", fullName = "N", email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        Assert.Equal("Admin", (await reg.Read<Session>()).Role);

        Assert.Equal(HttpStatusCode.OK,
            (await http.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = "password123" })).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_look_identical()
    {
        var admin = await factory.RegisterCompany();
        var http = factory.CreateClient();

        var wrong = await http.PostAsJsonAsync("/api/auth/login", new { email = admin.Session.Email, password = "not-the-password" });
        var unknown = await http.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.com", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await wrong.Text(), await unknown.Text()); // no account enumeration
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_across_companies()
    {
        var admin = await factory.RegisterCompany();
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { companyName = "Other", fullName = "N", email = admin.Session.Email, password = "password123" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("already registered", await res.Text());
    }

    [Fact]
    public async Task Refresh_issues_a_new_pair_and_retires_the_old_token()
    {
        var admin = await factory.RegisterCompany();
        var http = factory.CreateClient();

        var first = await Refresh(http, admin.Session.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var renewed = await first.Read<Session>();
        Assert.NotEqual(admin.Session.RefreshToken, renewed.RefreshToken);
        Assert.NotEqual(admin.Session.Token, renewed.Token);

        // The new access token works.
        var asNew = new Client(http, renewed);
        Assert.Equal(HttpStatusCode.OK, (await asNew.Get("/api/team")).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_session_family()
    {
        var admin = await factory.RegisterCompany();
        var http = factory.CreateClient();

        var rotated = await (await Refresh(http, admin.Session.RefreshToken)).Read<Session>();

        // An attacker replays the token that was already used...
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(http, admin.Session.RefreshToken)).StatusCode);

        // ...which also kills the legitimate newer token, forcing a fresh sign-in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(http, rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Garbage_and_empty_refresh_tokens_are_rejected()
    {
        var http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(http, "definitely-not-real")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Refresh(http, "")).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var admin = await factory.RegisterCompany();
        var http = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent,
            (await http.PostAsJsonAsync("/api/auth/logout", new { refreshToken = admin.Session.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(http, admin.Session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Changing_password_requires_the_current_one_and_signs_out_other_sessions()
    {
        var admin = await factory.RegisterCompany(password: "oldpassword1");
        var http = factory.CreateClient();

        var wrong = await admin.Post("/api/auth/change-password", new { currentPassword = "nope-nope", newPassword = "brandnew123" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("current password is incorrect", await wrong.Text());

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.Post("/api/auth/change-password", new { currentPassword = "oldpassword1", newPassword = "brandnew123" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(http, admin.Session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await http.PostAsJsonAsync("/api/auth/login", new { email = admin.Session.Email, password = "oldpassword1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await http.PostAsJsonAsync("/api/auth/login", new { email = admin.Session.Email, password = "brandnew123" })).StatusCode);
    }

    [Fact]
    public async Task New_password_must_differ_and_meet_the_length_rule()
    {
        var admin = await factory.RegisterCompany(password: "samepassword1");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.Post("/api/auth/change-password", new { currentPassword = "samepassword1", newPassword = "samepassword1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.Post("/api/auth/change-password", new { currentPassword = "samepassword1", newPassword = "short" })).StatusCode);
    }

    [Fact]
    public async Task Login_is_rate_limited_per_client()
    {
        using var limited = factory.WithAuthLimit(3);
        var http = limited.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            codes.Add((await http.PostAsJsonAsync("/api/auth/login", new { email = "x@test.com", password = "wrong-password" })).StatusCode);

        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.Unauthorized));
        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.TooManyRequests));

        var blocked = await http.PostAsJsonAsync("/api/auth/login", new { email = "x@test.com", password = "wrong-password" });
        Assert.True(blocked.Headers.Contains("Retry-After"));
        Assert.Contains("Too many attempts", await blocked.Text());
    }

    [Fact]
    public async Task Register_is_rate_limited_but_normal_api_use_is_not()
    {
        using var limited = factory.WithAuthLimit(2);
        var http = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await http.PostAsJsonAsync("/api/auth/register",
                new { companyName = "C" + i, fullName = "N", email = Api.Unique("rl") + "@test.com", password = "password123" })).StatusCode);
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));

        // Authenticated, non-credential endpoints are not subject to the strict auth limit.
        var admin = await factory.RegisterCompany();
        for (var i = 0; i < 15; i++) Assert.Equal(HttpStatusCode.OK, (await admin.Get("/api/team")).StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var res = await factory.CreateClient().GetAsync("/health");
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
    }
}
