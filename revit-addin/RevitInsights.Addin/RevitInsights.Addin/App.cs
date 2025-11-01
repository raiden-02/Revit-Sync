using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitInsights.Addin
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel("Revit Insights");

            string asmPath = Assembly.GetExecutingAssembly().Location;
            var btn = new PushButtonData(
                "ExportSummary",
                "Export Summary",
                asmPath,
                "RevitInsights.Addin.ExportCommand"
            );

            panel.AddItem(btn);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
