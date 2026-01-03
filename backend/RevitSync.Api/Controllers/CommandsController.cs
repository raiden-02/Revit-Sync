using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace RevitSync.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommandsController : ControllerBase
    {
        // Command coming from the web UI
        public class GeometryCommandDto
        {
            public string ProjectName { get; set; } = "";
            public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

            // "ADD_BOXES", "DELETE_ELEMENTS", "MOVE_ELEMENT", "SELECT_ELEMENTS"
            public string Type { get; set; } = "ADD_BOXES";

            // For ADD_BOXES
            public List<BoxDto> Boxes { get; set; } = new();

            // For DELETE_ELEMENTS - list of element IDs to delete
            public List<string> ElementIds { get; set; } = new();

            // For MOVE_ELEMENT - single element move
            public string? TargetElementId { get; set; }
            public double? NewCenterX { get; set; }
            public double? NewCenterY { get; set; }
            public double? NewCenterZ { get; set; }
        }

        // Box in REVIT WORLD COORDINATES.
        public class BoxDto
        {
            public string Category { get; set; } = "WebBox";
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double CenterZ { get; set; }
            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }
        }

        // Per-project queue
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<GeometryCommandDto>> _queues =
            new(StringComparer.OrdinalIgnoreCase);

        [HttpPost]
        public IActionResult Enqueue([FromBody] GeometryCommandDto cmd)
        {
            if (cmd == null) return BadRequest("Invalid payload.");
            if (string.IsNullOrWhiteSpace(cmd.ProjectName)) return BadRequest("ProjectName is required.");
            if (string.IsNullOrWhiteSpace(cmd.Type)) return BadRequest("Type is required.");

            // Validate based on command type
            switch (cmd.Type)
            {
                case "ADD_BOXES":
                    if (cmd.Boxes == null || cmd.Boxes.Count == 0)
                        return BadRequest("Boxes is required for ADD_BOXES.");
                    break;
                case "DELETE_ELEMENTS":
                    if (cmd.ElementIds == null || cmd.ElementIds.Count == 0)
                        return BadRequest("ElementIds is required for DELETE_ELEMENTS.");
                    break;
                case "MOVE_ELEMENT":
                    if (string.IsNullOrWhiteSpace(cmd.TargetElementId))
                        return BadRequest("TargetElementId is required for MOVE_ELEMENT.");
                    if (cmd.NewCenterX == null || cmd.NewCenterY == null || cmd.NewCenterZ == null)
                        return BadRequest("NewCenterX/Y/Z are required for MOVE_ELEMENT.");
                    break;
                case "SELECT_ELEMENTS":
                    // ElementIds can be empty (to clear selection)
                    break;
                default:
                    return BadRequest($"Unknown command type: {cmd.Type}");
            }

            cmd.CreatedUtc = DateTime.UtcNow;

            var q = _queues.GetOrAdd(cmd.ProjectName, _ => new ConcurrentQueue<GeometryCommandDto>());
            q.Enqueue(cmd);

            return Ok(new { cmd.CommandId });
        }

        // Revit polls this. If projectName is omitted, it will dequeue from any queue.
        [HttpGet("next")]
        public IActionResult Dequeue([FromQuery] string? projectName = null)
        {
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                if (!_queues.TryGetValue(projectName, out var q)) return NoContent();
                if (!q.TryDequeue(out var cmd)) return NoContent();
                return Ok(cmd);
            }

            // No project specified: try any queue 
            foreach (var kv in _queues)
            {
                var q = kv.Value;
                if (q.TryDequeue(out var cmd))
                    return Ok(cmd);
            }

            return NoContent();
        }
    }
}
