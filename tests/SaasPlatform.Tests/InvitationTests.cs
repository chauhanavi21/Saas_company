using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaasPlatform.Infrastructure.Persistence;

namespace SaasPlatform.Tests;

public class InvitationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<(string Token, Guid Id, string Email)> Invite(Client admin, string role = "Member")
    {
        var email = Api.Unique("invitee") + "@test.com";
        var res = await (await admin.Post("/api/invitations", new { email, role })).Element();
        return (res.GetProperty("token").GetString()!, res.GetProperty("invitation").Id(), email);
    }

    [Fact]
    public async Task Full_lifecycle_create_preview_accept_and_sign_in()
    {
        var admin = await factory.RegisterCompany("Lifecycle Ltd");
        var (token, _, email) = await Invite(admin, "Member");
        var anon = factory.CreateClient();

        var preview = await (await anon.GetAsync($"/api/invitations/preview?token={token}")).Element();
        Assert.Equal("Lifecycle Ltd", preview.GetProperty("companyName").GetString());
        Assert.Equal(email, preview.GetProperty("email").GetString());
        Assert.Equal("Member", preview.GetProperty("role").GetString());

        var accept = await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "New Person", password = "password123" });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var session = await accept.Read<Session>();
        Assert.Equal(admin.Session.TenantId, session.TenantId);
        Assert.Equal("Member", session.Role);
        Assert.Equal("Lifecycle Ltd", session.CompanyName);

        // The new member can sign in normally afterwards and sees the company's team.
        var login = await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(2, (await (await admin.Get("/api/team")).List()).Count);
    }

    [Fact]
    public async Task An_invitation_works_only_once()
    {
        var admin = await factory.RegisterCompany();
        var (token, _, _) = await Invite(admin);
        var anon = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK,
            (await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "First", password = "password123" })).StatusCode);

        var second = await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "Second", password = "password123" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("invalid or has expired", await second.Text());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/invitations/preview?token={token}")).StatusCode);
    }

    [Fact]
    public async Task Accepted_invitations_leave_the_pending_list()
    {
        var admin = await factory.RegisterCompany();
        var (token, _, _) = await Invite(admin);
        Assert.Single(await (await admin.Get("/api/invitations")).List());

        await factory.CreateClient().PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "X", password = "password123" });
        Assert.Equal("[]", await (await admin.Get("/api/invitations")).Text());
    }

    [Fact]
    public async Task A_revoked_invitation_no_longer_works()
    {
        var admin = await factory.RegisterCompany();
        var (token, id, _) = await Invite(admin);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Delete($"/api/invitations/{id}")).StatusCode);

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/invitations/preview?token={token}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "X", password = "password123" })).StatusCode);
    }

    [Fact]
    public async Task An_expired_invitation_is_rejected()
    {
        var admin = await factory.RegisterCompany();
        var (token, id, _) = await Invite(admin);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Invitations SET ExpiresAtUtc = {DateTime.UtcNow.AddMinutes(-1):yyyy-MM-dd HH:mm:ss} WHERE Id = {id}");
        }

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/invitations/preview?token={token}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "Late", password = "password123" })).StatusCode);
        Assert.Equal("[]", await (await admin.Get("/api/invitations")).Text());
    }

    [Fact]
    public async Task Garbage_tokens_are_rejected_without_leaking_anything()
    {
        var anon = factory.CreateClient();
        foreach (var bad in new[] { "nope", "  ", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "' OR 1=1 --" })
        {
            var preview = await anon.GetAsync($"/api/invitations/preview?token={Uri.EscapeDataString(bad)}");
            Assert.True(preview.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest, $"token '{bad}' gave {preview.StatusCode}");
        }
    }

    [Fact]
    public async Task Inviting_an_email_that_already_has_an_account_fails()
    {
        var admin = await factory.RegisterCompany();
        var other = await factory.RegisterCompany();

        var sameCompany = await admin.Post("/api/invitations", new { email = admin.Session.Email, role = "Member" });
        var otherCompany = await admin.Post("/api/invitations", new { email = other.Session.Email, role = "Member" });
        Assert.Equal(HttpStatusCode.BadRequest, sameCompany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherCompany.StatusCode);
        Assert.Contains("already has an account", await otherCompany.Text());
    }

    [Fact]
    public async Task Inviting_the_same_email_again_replaces_the_pending_invite()
    {
        var admin = await factory.RegisterCompany();
        var email = Api.Unique("twice") + "@test.com";
        var first = (await (await admin.Post("/api/invitations", new { email, role = "Member" })).Element()).GetProperty("token").GetString()!;
        var second = (await (await admin.Post("/api/invitations", new { email, role = "Admin" })).Element()).GetProperty("token").GetString()!;

        Assert.Single(await (await admin.Get("/api/invitations")).List());

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/invitations/preview?token={first}")).StatusCode);
        var accepted = await anon.PostAsJsonAsync("/api/invitations/accept", new { token = second, fullName = "Re", password = "password123" });
        Assert.Equal("Admin", (await accepted.Read<Session>()).Role);
    }

    [Fact]
    public async Task Accepting_validates_name_and_password()
    {
        var admin = await factory.RegisterCompany();
        var (token, _, _) = await Invite(admin);
        var anon = factory.CreateClient();

        var shortPw = await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "N", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, shortPw.StatusCode);
        Assert.Contains("at least 8", await shortPw.Text());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = " ", password = "password123" })).StatusCode);

        // A rejected attempt must not burn the invitation.
        Assert.Equal(HttpStatusCode.OK,
            (await anon.PostAsJsonAsync("/api/invitations/accept", new { token, fullName = "Real", password = "password123" })).StatusCode);
    }

    [Fact]
    public async Task Only_a_hash_of_the_token_is_stored()
    {
        var admin = await factory.RegisterCompany();
        var (token, id, _) = await Invite(admin);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Invitations.IgnoreQueryFilters().Where(i => i.Id == id).Select(i => i.TokenHash).SingleAsync();

        Assert.NotEqual(token, stored);
        Assert.DoesNotContain(token, stored);
        Assert.Equal(64, stored.Length); // hex SHA-256
    }
}
