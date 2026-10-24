using System.Net;
using System.Text.Json;

namespace SaasPlatform.Tests;

public class ProjectTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<Guid> NewProject(Client c, string name = "Project") =>
        (await (await c.Post("/api/projects", new { name })).Element()).Id();

    private static async Task<JsonElement> NewTask(Client c, Guid pid, object body) =>
        await (await c.Post($"/api/projects/{pid}/tasks", body)).Element();

    [Fact]
    public async Task Create_update_archive_and_delete_a_project()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin, "Alpha");

        var updated = await admin.Put($"/api/projects/{pid}", new { name = "Alpha v2", description = "  notes  " });
        var u = await updated.Element();
        Assert.Equal("Alpha v2", u.GetProperty("name").GetString());
        Assert.Equal("notes", u.GetProperty("description").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Post($"/api/projects/{pid}/archive")).StatusCode);
        Assert.Equal("[]", await (await admin.Get("/api/projects")).Text());
        Assert.Single(await (await admin.Get("/api/projects?includeArchived=true")).List());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Post($"/api/projects/{pid}/unarchive")).StatusCode);
        Assert.Single(await (await admin.Get("/api/projects")).List());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Delete($"/api/projects/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Get($"/api/projects/{pid}")).StatusCode);
    }

    [Fact]
    public async Task Archived_projects_reject_new_tasks()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin);
        await admin.Post($"/api/projects/{pid}/archive");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post($"/api/projects/{pid}/tasks", new { title = "late" })).StatusCode);
    }

    [Fact]
    public async Task Project_search_matches_names_and_treats_wildcards_literally()
    {
        var admin = await factory.RegisterCompany();
        await NewProject(admin, "Website Redesign");
        await NewProject(admin, "Mobile App");
        await NewProject(admin, "100% Coverage");

        Assert.Single(await (await admin.Get("/api/projects?search=redesign")).List());
        Assert.Equal("Mobile App", (await (await admin.Get("/api/projects?search=mobile")).List())[0].GetProperty("name").GetString());
        // "%" must not act as a wildcard that matches everything.
        var pct = await (await admin.Get("/api/projects?search=%25")).List();
        Assert.Single(pct);
        Assert.Equal("100% Coverage", pct[0].GetProperty("name").GetString());
        Assert.Empty(await (await admin.Get("/api/projects?search=zzz")).List());
    }

    [Fact]
    public async Task Tasks_default_to_todo_and_medium_and_keep_assignee_priority_and_due_date()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);
        var pid = await NewProject(admin);

        var plain = await NewTask(admin, pid, new { title = "Plain" });
        Assert.Equal("Todo", plain.GetProperty("status").GetString());
        Assert.Equal("Medium", plain.GetProperty("priority").GetString());

        var rich = await NewTask(admin, pid, new { title = "Rich", priority = "high", assigneeUserId = member.Session.UserId, dueDate = "2030-01-15" });
        Assert.Equal("High", rich.GetProperty("priority").GetString());
        Assert.Equal(member.Session.UserId, rich.GetProperty("assigneeUserId").GetGuid());
        Assert.Equal("2030-01-15", rich.GetProperty("dueDate").GetString());
        Assert.False(rich.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task Moving_a_task_updates_status_and_progress_counts()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin);
        var t1 = (await NewTask(admin, pid, new { title = "A" })).Id();
        await NewTask(admin, pid, new { title = "B" });

        var moved = await admin.Put($"/api/projects/{pid}/tasks/{t1}", new { title = "A", status = "done" });
        Assert.Equal("Done", (await moved.Element()).GetProperty("status").GetString());

        var project = await (await admin.Get($"/api/projects/{pid}")).Element();
        Assert.Equal(2, project.GetProperty("taskCount").GetInt32());
        Assert.Equal(1, project.GetProperty("doneCount").GetInt32());
    }

    [Fact]
    public async Task Task_filters_search_status_assignee_and_mine_work_together()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);
        var pid = await NewProject(admin);
        await NewTask(admin, pid, new { title = "Write docs", assigneeUserId = member.Session.UserId });
        var done = (await NewTask(admin, pid, new { title = "Ship release", assigneeUserId = admin.Session.UserId })).Id();
        await NewTask(admin, pid, new { title = "Fix bug" });
        await admin.Put($"/api/projects/{pid}/tasks/{done}", new { title = "Ship release", status = "Done", assigneeUserId = admin.Session.UserId });

        Assert.Equal(3, (await (await admin.Get($"/api/projects/{pid}/tasks")).List()).Count);
        Assert.Single(await (await admin.Get($"/api/projects/{pid}/tasks?search=docs")).List());
        Assert.Single(await (await admin.Get($"/api/projects/{pid}/tasks?status=Done")).List());
        Assert.Single(await (await admin.Get($"/api/projects/{pid}/tasks?assignee={member.Session.UserId}")).List());
        Assert.Single(await (await member.Get($"/api/projects/{pid}/tasks?mine=true")).List());
        Assert.Single(await (await admin.Get($"/api/projects/{pid}/tasks?mine=true&status=Done")).List());
        Assert.Empty(await (await admin.Get($"/api/projects/{pid}/tasks?search=docs&status=Done")).List());
    }

    [Fact]
    public async Task Past_due_open_tasks_are_flagged_overdue_but_done_ones_are_not()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin);
        var late = (await NewTask(admin, pid, new { title = "Late", dueDate = "2020-01-01" }));
        Assert.True(late.GetProperty("isOverdue").GetBoolean());

        var done = await admin.Put($"/api/projects/{pid}/tasks/{late.Id()}", new { title = "Late", status = "Done", dueDate = "2020-01-01" });
        Assert.False((await done.Element()).GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task Task_in_the_wrong_project_is_not_found()
    {
        var admin = await factory.RegisterCompany();
        var p1 = await NewProject(admin, "One");
        var p2 = await NewProject(admin, "Two");
        var tid = (await NewTask(admin, p1, new { title = "T" })).Id();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Put($"/api/projects/{p2}/tasks/{tid}", new { title = "T", status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Delete($"/api/projects/{p2}/tasks/{tid}")).StatusCode);
    }

    [Fact]
    public async Task Comments_are_listed_in_order_with_author_and_counted_on_the_task()
    {
        var admin = await factory.RegisterCompany();
        var member = await factory.AddMember(admin);
        var pid = await NewProject(admin);
        var tid = (await NewTask(admin, pid, new { title = "T" })).Id();

        await admin.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "  first  " });
        await member.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "second" });

        var comments = await (await admin.Get($"/api/projects/{pid}/tasks/{tid}/comments")).List();
        Assert.Equal(new[] { "first", "second" }, comments.Select(c => c.GetProperty("body").GetString()).ToArray());
        Assert.Equal(admin.Session.FullName, comments[0].GetProperty("authorName").GetString());

        var task = (await (await admin.Get($"/api/projects/{pid}/tasks")).List())[0];
        Assert.Equal(2, task.GetProperty("commentCount").GetInt32());
    }

    [Fact]
    public async Task Deleting_a_project_removes_its_tasks_and_comments()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin);
        var tid = (await NewTask(admin, pid, new { title = "T" })).Id();
        await admin.Post($"/api/projects/{pid}/tasks/{tid}/comments", new { body = "bye" });

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Delete($"/api/projects/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Get($"/api/projects/{pid}/tasks/{tid}/comments")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_counts_open_overdue_and_completed_work_and_lists_my_tasks()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin);
        var done = (await NewTask(admin, pid, new { title = "Done one", assigneeUserId = admin.Session.UserId })).Id();
        await NewTask(admin, pid, new { title = "Mine soon", assigneeUserId = admin.Session.UserId, dueDate = "2031-01-01" });
        await NewTask(admin, pid, new { title = "Overdue", dueDate = "2020-01-01" });
        await admin.Put($"/api/projects/{pid}/tasks/{done}", new { title = "Done one", status = "Done", assigneeUserId = admin.Session.UserId });

        var d = await (await admin.Get("/api/dashboard")).Element();
        Assert.Equal(1, d.GetProperty("activeProjects").GetInt32());
        Assert.Equal(2, d.GetProperty("openTasks").GetInt32());
        Assert.Equal(1, d.GetProperty("overdueTasks").GetInt32());
        Assert.Equal(1, d.GetProperty("completedThisWeek").GetInt32());

        var mine = d.GetProperty("myTasks").EnumerateArray().ToList();
        Assert.Single(mine); // the done one is excluded
        Assert.Equal("Mine soon", mine[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Dashboard_ignores_archived_projects()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin);
        await NewTask(admin, pid, new { title = "T" });
        await admin.Post($"/api/projects/{pid}/archive");

        var d = await (await admin.Get("/api/dashboard")).Element();
        Assert.Equal(0, d.GetProperty("activeProjects").GetInt32());
        Assert.Equal(0, d.GetProperty("openTasks").GetInt32());
    }

    [Fact]
    public async Task Activity_feed_records_who_did_what_newest_first()
    {
        var admin = await factory.RegisterCompany();
        var pid = await NewProject(admin, "Tracked");
        var tid = (await NewTask(admin, pid, new { title = "Step" })).Id();
        await admin.Put($"/api/projects/{pid}/tasks/{tid}", new { title = "Step", status = "InProgress" });

        var feed = await (await admin.Get("/api/activity")).List();
        var actions = feed.Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Contains("company.created", actions);
        Assert.Contains("project.created", actions);
        Assert.Contains("task.created", actions);
        Assert.Contains("task.moved", actions);
        Assert.Equal("task.moved", actions[0]); // newest first
        Assert.All(feed, a => Assert.Equal(admin.Session.FullName, a.GetProperty("actorName").GetString()));

        Assert.Single(await (await admin.Get("/api/activity?take=1")).List());
    }
}
