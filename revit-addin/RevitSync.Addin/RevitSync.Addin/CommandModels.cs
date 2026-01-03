using System.Collections.Generic;

namespace RevitSync.Addin
{
    public class GeometryCommandDto
    {
        public string ProjectName { get; set; } = "";
        public string CommandId { get; set; } = "";
        
        // "ADD_BOXES", "DELETE_ELEMENTS", "MOVE_ELEMENT", "SELECT_ELEMENTS"
        public string Type { get; set; } = "ADD_BOXES";
        
        // For ADD_BOXES
        public List<BoxDto> Boxes { get; set; } = new List<BoxDto>();
        
        // For DELETE_ELEMENTS
        public List<string> ElementIds { get; set; } = new List<string>();
        
        // For MOVE_ELEMENT
        public string TargetElementId { get; set; } = "";
        public double? NewCenterX { get; set; }
        public double? NewCenterY { get; set; }
        public double? NewCenterZ { get; set; }
    }

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
}
