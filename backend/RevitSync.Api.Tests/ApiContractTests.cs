using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RevitSync.Api.Tests;

public class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Geometry_requires_project_name()
    {
        var response = await _client.PostAsJsonAsync("/api/geometry", new
        {
            primitives = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Geometry_latest_returns_snapshot_and_etag_304()
    {
        var project = $"geom-{Guid.NewGuid():N}";
        var ingest = await _client.PostAsJsonAsync("/api/geometry", new
        {
            projectName = project,
            primitives = new[]
            {
                new
                {
                    category = "Walls",
                    elementId = "42",
                    isWebCreated = false,
                    color = "#4ade80",
                    centerX = 1.0,
                    centerY = 2.0,
                    centerZ = 3.0,
                    sizeX = 4.0,
                    sizeY = 0.5,
                    sizeZ = 8.0
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, ingest.StatusCode);

        var latest = await _client.GetAsync($"/api/geometry/latest?projectName={project}");
        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(latest.Headers.ETag?.Tag));

        using var body = JsonDocument.Parse(await latest.Content.ReadAsStringAsync());
        Assert.Equal(project, body.RootElement.GetProperty("projectName").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("primitives").GetArrayLength());

        var cached = new HttpRequestMessage(HttpMethod.Get, $"/api/geometry/latest?projectName={project}");
        cached.Headers.TryAddWithoutValidation("If-None-Match", latest.Headers.ETag!.Tag);
        var notModified = await _client.SendAsync(cached);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
    }

    [Fact]
    public async Task Commands_reject_unknown_type_and_missing_payload()
    {
        var project = $"cmd-{Guid.NewGuid():N}";

        var unknown = await _client.PostAsJsonAsync("/api/commands", new
        {
            projectName = project,
            type = "EXPLODE_MODEL"
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var emptyBoxes = await _client.PostAsJsonAsync("/api/commands", new
        {
            projectName = project,
            type = "ADD_BOXES",
            boxes = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, emptyBoxes.StatusCode);
    }

    [Fact]
    public async Task Commands_enqueue_then_dequeue_once()
    {
        var project = $"cmd-{Guid.NewGuid():N}";

        var enqueue = await _client.PostAsJsonAsync("/api/commands", new
        {
            projectName = project,
            type = "ADD_BOXES",
            boxes = new[]
            {
                new { centerX = 0.0, centerY = 0.0, centerZ = 5.0, sizeX = 10.0, sizeY = 10.0, sizeZ = 10.0 }
            }
        });
        Assert.Equal(HttpStatusCode.OK, enqueue.StatusCode);

        var queued = await enqueue.Content.ReadFromJsonAsync<CommandIdResponse>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(queued?.CommandId));

        var next = await _client.GetAsync($"/api/commands/next?projectName={project}");
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        using var cmd = JsonDocument.Parse(await next.Content.ReadAsStringAsync());
        Assert.Equal("ADD_BOXES", cmd.RootElement.GetProperty("type").GetString());
        Assert.Equal(queued!.CommandId, cmd.RootElement.GetProperty("commandId").GetString());

        var empty = await _client.GetAsync($"/api/commands/next?projectName={project}");
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
    }

    [Fact]
    public async Task Select_elements_allows_empty_ids()
    {
        var project = $"sel-{Guid.NewGuid():N}";
        var enqueue = await _client.PostAsJsonAsync("/api/commands", new
        {
            projectName = project,
            type = "SELECT_ELEMENTS",
            elementIds = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.OK, enqueue.StatusCode);
    }

    private sealed class CommandIdResponse
    {
        public string CommandId { get; set; } = "";
    }
}
