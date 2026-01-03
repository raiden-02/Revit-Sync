using Autodesk.Revit.UI;

namespace RevitSync.Addin
{
    // IExternalEventHandler that performs geometry export on Revit's UI thread.
    // Triggered by DocumentChanged events via ExternalEvent.Raise().
    public class AutoExportHandler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            var uidoc = app?.ActiveUIDocument;
            var doc = uidoc?.Document;
            if (doc == null) return;

            var view = uidoc.ActiveView;

            // export (silent)
            var result = GeometryExporter.Export(doc, view, showNoElementsAsSuccess: true);

            // System.Diagnostics.Debug.WriteLine($"[RevitSync] Auto-export: {result.PrimitiveCount} primitives, success={result.Success}");
        }

        public string GetName() => "RevitSync Auto Export Handler";
    }
}
