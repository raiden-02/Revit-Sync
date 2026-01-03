using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitSync.Addin
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportGeometryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            View view = uidoc.ActiveView;

            // Use shared GeometryExporter for the actual export
            var result = GeometryExporter.Export(doc, view, showNoElementsAsSuccess: false);

            if (!result.Success)
            {
                if (result.PrimitiveCount == 0)
                {
                    TaskDialog.Show("RevitSync", "No elements found for the selected categories in this view.");
                    return Result.Succeeded;
                }
                message = $"Failed to export geometry: {result.ErrorMessage}";
                return Result.Failed;
            }

            TaskDialog.Show(
                "RevitSync",
                $"Exported {result.PrimitiveCount} geometry primitives for project '{doc.Title}'."
            );

            return Result.Succeeded;
        }
    }
}
