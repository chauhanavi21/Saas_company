using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SaasPlatform.Tests;

public class RoleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var http = factory.CreateClient();
        foreach (var url in new[] { "/api/team", "/api/projects", "/api/invitations", "/api/dashboard", "/api/activity", "/api/account" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var admin = await factory.RegisterCompany();
        admin.Session = admin.Session with { Token = admin.Session.Token[..^4] + "AAAA" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Get("/api/team")).StatusCode);
    }

    [Fact]
    public async Task Members_can_read_the_team_and_do_project_work()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);

        Assert.Equal(HttpStatusCode.OK, (await member.Get("/api/team")).StatusCode);
        var created = await member.Post("/api/projects", new { name = "By a member" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var pid = (await created.Element()).Id();
        Assert.Equal(HttpStatusCode.OK, (await member.Post($"/api/projects/{pid}/tasks", new { title = "Do it" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.Post($"/api/projects/{pid}/archive")).StatusCode);
    }

    [Fact]
    public async Task Members_cannot_manage_people_invites_or_delete_projects()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);
        var pid = (await (await admin.Post("/api/projects", new { name = "P" })).Element()).Id();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.Post("/api/invitations", new { email = Api.Unique("n") + "@test.com", role = "Member" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Get("/api/invitations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Put($"/api/team/{admin.Session.UserId}/role", new { role = "Member" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Delete($"/api/team/{admin.Session.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Delete($"/api/projects/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Put("/api/account/company", new { name = "Mine now" })).StatusCode);
    }

    [Fact]
    public async Task Admins_can_promote_and_remove_members()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);

        var promoted = await admin.Put($"/api/team/{member.Session.UserId}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal("Admin", (await promoted.Element()).GetProperty("role").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Delete($"/api/team/{member.Session.UserId}")).StatusCode);
        Assert.Single(await (await admin.Get("/api/team")).List());
    }

    [Fact]
    public async Task The_last_admin_cannot_demote_or_remove_themselves()
    {
        var admin = await factory.RegisterCompany();

        var demote = await admin.Put($"/api/team/{admin.Session.UserId}/role", new { role = "Member" });
        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Contains("at least one admin", await demote.Text());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Delete($"/api/team/{admin.Session.UserId}")).StatusCode);
        Assert.Single(await (await admin.Get("/api/team")).List());
    }

    [Fact]
    public async Task An_admin_can_step_down_once_another_admin_exists()
    {
        var admin = await factory.RegisterCompany();
        var second = await factory.AddMember(admin, "Admin");

        Assert.Equal(HttpStatusCode.OK, (await admin.Put($"/api/team/{admin.Session.UserId}/role", new { role = "Member" })).StatusCode);

        // The second admin is now the only one, so they are protected in turn.
        Assert.Equal(HttpStatusCode.BadRequest, (await second.Put($"/api/team/{second.Session.UserId}/role", new { role = "Member" })).StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_remove_themselves_even_with_another_admin()
    {
        var admin = await factory.RegisterCompany();
        await factory.AddMember(admin, "Admin");
        var res = await admin.Delete($"/api/team/{admin.Session.UserId}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("remove yourself", await res.Text());
    }

    [Fact]
    public async Task Invalid_role_values_are_rejected()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put($"/api/team/{member.Session.UserId}/role", new { role = "Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.Post("/api/invitations", new { email = Api.Unique("x") + "@test.com", role = "Root" })).StatusCode);
    }

    [Fact]
    public async Task Removing_a_member_unassigns_their_tasks_and_ends_their_sessions()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);
        var pid = (await (await admin.Post("/api/projects", new { name = "P" })).Element()).Id();
        await admin.Post($"/api/projects/{pid}/tasks", new { title = "Owned", assigneeUserId = member.Session.UserId });

        await admin.Delete($"/api/team/{member.Session.UserId}");

        var tasks = await (await admin.Get($"/api/projects/{pid}/tasks")).List();
        Assert.Equal(JsonValueKind.Null, tasks[0].GetProperty("assigneeUserId").ValueKind);

        var refresh = await member.Http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = member.Session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Comment_deletion_is_limited_to_the_author_or_an_admin()
    {
        var admin = await factory.RegisterCompany();
        var m1 = await factory.AddMember(admin);
        var m2 = await factory.AddMember(admin);
        var pid = (await (await admin.Post("/api/projects", new { name = "P" })).Element()).Id();
        var tid = (await (await admin.Post($"/api/projects/{pid}/tasks", new { title = "T" })).Element()).Id();

        var c1 = (await (await m1.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "mine" })).Element()).Id();
        Assert.Equal(HttpStatusCode.Forbidden, (await m2.Delete($"/api/projects/{pid}/tasks/{tid}/comments/{c1}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Delete($"/api/projects/{pid}/tasks/{tid}/comments/{c1}")).StatusCode);

        var c2 = (await (await m1.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "again" })).Element()).Id();
        Assert.Equal(HttpStatusCode.NoContent, (await m1.Delete($"/api/projects/{pid}/tasks/{tid}/comments/{c2}")).StatusCode);
    }
}
