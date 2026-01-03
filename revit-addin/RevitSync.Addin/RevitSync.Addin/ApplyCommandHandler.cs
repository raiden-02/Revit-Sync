using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitSync.Addin
{
    public class ApplyCommandHandler : IExternalEventHandler
    {
        // Set by poller thread, read on UI thread in Execute()
        public GeometryCommandDto Pending;

        public void Execute(UIApplication app)
        {
            var cmd = Pending;
            Pending = null;
            if (cmd == null) return;

            var uidoc = app.ActiveUIDocument;
            var doc = uidoc?.Document;
            if (doc == null) return;

            try
            {
                switch (cmd.Type)
                {
                    case "ADD_BOXES":
                        ExecuteAddBoxes(doc, cmd);
                        break;
                    case "DELETE_ELEMENTS":
                        ExecuteDeleteElements(doc, cmd);
                        break;
                    case "MOVE_ELEMENT":
                        ExecuteMoveElement(doc, cmd);
                        break;
                    case "SELECT_ELEMENTS":
                        ExecuteSelectElements(uidoc, cmd);
                        break;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RevitSync", $"Failed to apply command '{cmd.Type}'.\n{ex.Message}");
            }
        }

        public string GetName() => "RevitSync Apply Command Handler";

        private void ExecuteSelectElements(UIDocument uidoc, GeometryCommandDto cmd)
        {
            if (uidoc == null) return;

            var doc = uidoc.Document;
            var elementIds = new List<ElementId>();

            // Parse element IDs from command
            if (cmd.ElementIds != null)
            {
                foreach (var idStr in cmd.ElementIds)
                {
                    if (!int.TryParse(idStr, out int idVal)) continue;
                    
                    var elementId = new ElementId(idVal);
                    var element = doc.GetElement(elementId);
                    
                    // Only select existing elements
                    if (element != null)
                    {
                        elementIds.Add(elementId);
                    }
                }
            }

            // Set the selection in Revit (this highlights the elements)
            uidoc.Selection.SetElementIds(elementIds);

            // Optionally zoom to the selected elements
            if (elementIds.Count > 0)
            {
                try
                {
                    uidoc.ShowElements(elementIds);
                }
                catch
                {
                    // ShowElements can fail in some views, ignore
                }
            }
        }

        private void ExecuteAddBoxes(Document doc, GeometryCommandDto cmd)
        {
            using (var tx = new Transaction(doc, "RevitSync: ADD_BOXES"))
            {
                tx.Start();

                foreach (var b in cmd.Boxes)
                    CreateBoxDirectShape(doc, cmd, b);

                tx.Commit();
            }
        }

        private void ExecuteDeleteElements(Document doc, GeometryCommandDto cmd)
        {
            if (cmd.ElementIds == null || cmd.ElementIds.Count == 0) return;

            using (var tx = new Transaction(doc, "RevitSync: DELETE_ELEMENTS"))
            {
                tx.Start();

                int deletedCount = 0;
                foreach (var idStr in cmd.ElementIds)
                {
                    if (!int.TryParse(idStr, out int idVal)) continue;
                    
                    var elementId = new ElementId(idVal);
                    var element = doc.GetElement(elementId);
                    
                    if (element == null) continue;

                    // Only allow deleting RevitSync-created DirectShapes for safety
                    var ds = element as DirectShape;
                    if (ds != null && ds.ApplicationId == "RevitSync")
                    {
                        doc.Delete(elementId);
                        deletedCount++;
                    }
                }

                tx.Commit();
            }
        }

        private void ExecuteMoveElement(Document doc, GeometryCommandDto cmd)
        {
            if (string.IsNullOrEmpty(cmd.TargetElementId)) return;
            if (cmd.NewCenterX == null || cmd.NewCenterY == null || cmd.NewCenterZ == null) return;

            if (!int.TryParse(cmd.TargetElementId, out int idVal)) return;
            
            var elementId = new ElementId(idVal);
            var element = doc.GetElement(elementId);
            
            if (element == null) return;

            // Only allow moving RevitSync-created DirectShapes for safety
            var ds = element as DirectShape;
            if (ds == null || ds.ApplicationId != "RevitSync") return;

            // Get current bounding box to calculate offset
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) return;

            XYZ currentMin, currentMax;
            if (!TryGetWorldAabb(bbox, out currentMin, out currentMax)) return;

            var currentCenter = (currentMin + currentMax) / 2.0;
            var newCenter = new XYZ(cmd.NewCenterX.Value, cmd.NewCenterY.Value, cmd.NewCenterZ.Value);
            var translation = newCenter - currentCenter;

            using (var tx = new Transaction(doc, "RevitSync: MOVE_ELEMENT"))
            {
                tx.Start();
                ElementTransformUtils.MoveElement(doc, elementId, translation);
                tx.Commit();
            }
        }

        private void CreateBoxDirectShape(Document doc, GeometryCommandDto cmd, BoxDto b)
        {
            // Create DirectShape in Generic Models
            var ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));

            // IDs help you later add MOVE/DELETE
            ds.ApplicationId = "RevitSync";
            ds.ApplicationDataId = $"{cmd.CommandId}:{b.Category ?? "WebBox"}";

            // Box extents
            double hx = Math.Abs(b.SizeX) / 2.0;
            double hy = Math.Abs(b.SizeY) / 2.0;
            double hz = Math.Abs(b.SizeZ) / 2.0;

            // avoid degenerate solids
            if (hx <= 1e-6 || hy <= 1e-6 || hz <= 1e-6) return;

            var center = new XYZ(b.CenterX, b.CenterY, b.CenterZ);
            var min = center - new XYZ(hx, hy, hz);
            var max = center + new XYZ(hx, hy, hz);

            // Create a box solid by extruding a rectangle in XY from min.Z to max.Z
            var baseLoop = RectangleLoop(min.X, min.Y, max.X, max.Y, min.Z);
            var solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { baseLoop },
                XYZ.BasisZ,
                (max.Z - min.Z)
            );

            ds.SetShape(new List<GeometryObject> { solid });
        }

        private CurveLoop RectangleLoop(double x0, double y0, double x1, double y1, double z)
        {
            var p00 = new XYZ(x0, y0, z);
            var p10 = new XYZ(x1, y0, z);
            var p11 = new XYZ(x1, y1, z);
            var p01 = new XYZ(x0, y1, z);

            var loop = new CurveLoop();
            loop.Append(Line.CreateBound(p00, p10));
            loop.Append(Line.CreateBound(p10, p11));
            loop.Append(Line.CreateBound(p11, p01));
            loop.Append(Line.CreateBound(p01, p00));
            return loop;
        }

        private static bool TryGetWorldAabb(BoundingBoxXYZ bbox, out XYZ minWorld, out XYZ maxWorld)
        {
            minWorld = null;
            maxWorld = null;

            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return false;

            Transform t = bbox.Transform ?? Transform.Identity;

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;

            XYZ bmin = bbox.Min;
            XYZ bmax = bbox.Max;

            double[] xs = { bmin.X, bmax.X };
            double[] ys = { bmin.Y, bmax.Y };
            double[] zs = { bmin.Z, bmax.Z };

            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            for (int zi = 0; zi < 2; zi++)
            {
                XYZ local = new XYZ(xs[xi], ys[yi], zs[zi]);
                XYZ w = t.OfPoint(local);

                if (w.X < minX) minX = w.X;
                if (w.Y < minY) minY = w.Y;
                if (w.Z < minZ) minZ = w.Z;

                if (w.X > maxX) maxX = w.X;
                if (w.Y > maxY) maxY = w.Y;
                if (w.Z > maxZ) maxZ = w.Z;
            }

            if (double.IsInfinity(minX) || double.IsInfinity(maxX))
                return false;

            minWorld = new XYZ(minX, minY, minZ);
            maxWorld = new XYZ(maxX, maxY, maxZ);
            return true;
        }
    }
}
