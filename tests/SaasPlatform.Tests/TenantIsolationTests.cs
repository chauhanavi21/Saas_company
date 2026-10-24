using System.Net;

namespace SaasPlatform.Tests;

/// <summary>The rule that matters most in a multi-tenant app: one company must never see or touch another's data.</summary>
public class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Team_lists_only_the_callers_own_company()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        await factory.AddMember(a);

        Assert.Equal(2, (await (await a.Get("/api/team")).List()).Count);
        var bTeam = await (await b.Get("/api/team")).List();
        Assert.Single(bTeam);
        Assert.Equal(b.Session.Email, bTeam[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task Projects_are_invisible_and_untouchable_across_companies()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        var project = await (await a.Post("/api/projects", new { name = "Secret plan" })).Element();
        var id = project.Id();

        Assert.Equal("[]", await (await b.Get("/api/projects")).Text());
        Assert.Equal(HttpStatusCode.NotFound, (await b.Get($"/api/projects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Put($"/api/projects/{id}", new { name = "Hacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Post($"/api/projects/{id}/archive")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Delete($"/api/projects/{id}")).StatusCode);

        // The owner's project is untouched after all those attempts.
        var still = await (await a.Get($"/api/projects/{id}")).Element();
        Assert.Equal("Secret plan", still.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Tasks_and_comments_are_unreachable_across_companies()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        var pid = (await (await a.Post("/api/projects", new { name = "P" })).Element()).Id();
        var tid = (await (await a.Post($"/api/projects/{pid}/tasks", new { title = "T" })).Element()).Id();
        var cid = (await (await a.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "private" })).Element()).Id();

        Assert.Equal(HttpStatusCode.NotFound, (await b.Get($"/api/projects/{pid}/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Post($"/api/projects/{pid}/tasks", new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.Put($"/api/projects/{pid}/tasks/{tid}", new { title = "x", status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Delete($"/api/projects/{pid}/tasks/{tid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Get($"/api/projects/{pid}/tasks/{tid}/comments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Delete($"/api/projects/{pid}/tasks/{tid}/comments/{cid}")).StatusCode);
    }

    [Fact]
    public async Task Cannot_manage_another_companys_people()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        var bUser = b.Session.UserId;

        Assert.Equal(HttpStatusCode.NotFound, (await a.Put($"/api/team/{bUser}/role", new { role = "Member" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Delete($"/api/team/{bUser}")).StatusCode);
        Assert.Single(await (await b.Get("/api/team")).List());
    }

    [Fact]
    public async Task Invitations_are_private_to_the_company_that_created_them()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        var inv = await (await a.Post("/api/invitations", new { email = Api.Unique("x") + "@test.com", role = "Member" })).Element();
        var invId = inv.GetProperty("invitation").Id();

        Assert.Equal("[]", await (await b.Get("/api/invitations")).Text());
        Assert.Equal(HttpStatusCode.NotFound, (await b.Delete($"/api/invitations/{invId}")).StatusCode);
        Assert.Single(await (await a.Get("/api/invitations")).List());
    }

    [Fact]
    public async Task Tasks_cannot_be_assigned_to_a_person_from_another_company()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        var pid = (await (await a.Post("/api/projects", new { name = "P" })).Element()).Id();

        var res = await a.Post($"/api/projects/{pid}/tasks", new { title = "T", assigneeUserId = b.Session.UserId });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("member of your company", await res.Text());
    }

    [Fact]
    public async Task Activity_feed_and_dashboard_only_show_own_company_data()
    {
        var a = await factory.RegisterCompany();
        var b = await factory.RegisterCompany();
        await a.Post("/api/projects", new { name = "Only A knows" });

        var bFeed = await (await b.Get("/api/activity")).Text();
        Assert.DoesNotContain("Only A knows", bFeed);

        var bDash = await (await b.Get("/api/dashboard")).Element();
        Assert.Equal(0, bDash.GetProperty("activeProjects").GetInt32());
        Assert.Equal(1, bDash.GetProperty("teamMembers").GetInt32());
    }

    [Fact]
    public async Task Companies_with_the_same_name_get_distinct_workspaces()
    {
        var a = await factory.RegisterCompany("Acme");
        var b = await factory.RegisterCompany("Acme");

        Assert.NotEqual(a.Session.TenantId, b.Session.TenantId);
        await a.Post("/api/projects", new { name = "Mine" });
        Assert.Equal("[]", await (await b.Get("/api/projects")).Text());
    }
}
