using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace RevitInsights.Addin
{
    [Transaction(TransactionMode.Manual)]
    public class ExportCommand : IExternalCommand
    {
        static readonly HttpClient http = new HttpClient { BaseAddress = new System.Uri("http://localhost:5245/") };


        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument?.Document;
            if (doc is null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            var categories = doc.Settings.Categories
                .Cast<Category>()
                .Where(c => c.AllowsBoundParameters)
                .Select(c =>
                {
                    int count = new FilteredElementCollector(doc)
                        .OfCategoryId(c.Id)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                    return new { name = c.Name, count };
                })
                .Where(x => x.count > 0)
                .ToList();

            var payload = new
            {
                projectName = doc.Title,
                timestampUtc = System.DateTime.UtcNow,
                revitVersion = doc.Application.VersionName,
                categories
            };

            var json = JsonConvert.SerializeObject(payload);
            var resp = http.PostAsync("api/modeldata", new StringContent(json, Encoding.UTF8, "application/json")).Result;

            if (!resp.IsSuccessStatusCode)
            {
                message = $"Upload failed: {resp.StatusCode}";
                return Result.Failed;
            }

            TaskDialog.Show("Revit Insights", "Model summary uploaded.");
            return Result.Succeeded;
        }
    }
}
