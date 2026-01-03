using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using System.Windows.Media.Imaging;

namespace RevitSync.Addin
{
    public class App : IExternalApplication
    {
        // Web -> Revit command handling
        private static ApplyCommandHandler _handler;
        private static ExternalEvent _externalEvent;
        private static CommandPoller _poller;

        // Revit -> Web auto-export handling
        private static AutoExportHandler _autoExportHandler;
        private static ExternalEvent _autoExportEvent;
        private static Timer _debounceTimer;
        private static volatile bool _exportPending;
        private const int DebounceMs = 500; // Wait 500ms after last change before exporting

        // Selection change tracking (for selection sync)
        private static HashSet<long> _lastSelectionIds = new HashSet<long>();
        private static DateTime _lastSelectionCheck = DateTime.MinValue;
        private const int SelectionCheckIntervalMs = 300; // Check selection every 300ms

        public Result OnStartup(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel("RevitSync");

            string asmPath = Assembly.GetExecutingAssembly().Location;
            string asmDir = Path.GetDirectoryName(asmPath);

            string iconPath = Path.Combine(asmDir, "Icons", "revitsync-icon.png");
            BitmapSource largeIcon = LoadIcon(iconPath, 32);  // 32x32 for large button
            BitmapSource smallIcon = LoadIcon(iconPath, 16);  // 16x16 for small button

            // Button for generate column grid
            var genGridBtnData = new PushButtonData(
                "GenerateColumnGrid",
                "Generate\nColumn Grid",
                asmPath,
                "RevitSync.Addin.GenerateColumnGridCommand"
            )
            {
                ToolTip = "Generate test geometry quickly by placing a grid of structural columns in the active plan view crop.",
                LargeImage = largeIcon,
                Image = smallIcon
            };

            // Export lightweight geometry snapshot
            var exportGeomBtnData = new PushButtonData(
                "ExportGeometry",
                "Export\nGeometry",
                asmPath,
                "RevitSync.Addin.ExportGeometryCommand"
            )
            {
                ToolTip = "Stream a lightweight 3D geometry snapshot (bounding boxes) to the local viewer.",
                LargeImage = largeIcon,
                Image = smallIcon
            };

            panel.AddItem(genGridBtnData);
            panel.AddItem(exportGeomBtnData);

            // Two-way sync: poll web -> apply in Revit via ExternalEvent
            _handler = new ApplyCommandHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            _poller = new CommandPoller(cmd =>
            {
                // store pending and raise
                _handler.Pending = cmd;
                _externalEvent.Raise();
            });
            _poller.Start();

            // Auto-export: subscribe to DocumentChanged for real-time sync
            _autoExportHandler = new AutoExportHandler();
            _autoExportEvent = ExternalEvent.Create(_autoExportHandler);

            // Debounce timer - waits for changes to settle before triggering export
            _debounceTimer = new Timer(DebounceMs);
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += (_, __) =>
            {
                if (_exportPending)
                {
                    _exportPending = false;
                    _autoExportEvent.Raise();
                }
            };

            // Subscribe to DocumentChanged event
            app.ControlledApplication.DocumentChanged += OnDocumentChanged;

            // Subscribe to Idling event for selection change detection
            app.Idling += OnIdling;

            return Result.Succeeded;
        }

        private static void OnIdling(object sender, IdlingEventArgs e)
        {
            // Throttle selection checks to avoid performance impact
            if ((DateTime.Now - _lastSelectionCheck).TotalMilliseconds < SelectionCheckIntervalMs)
                return;
            _lastSelectionCheck = DateTime.Now;

            try
            {
                var uiApp = sender as UIApplication;
                var uidoc = uiApp?.ActiveUIDocument;
                if (uidoc == null) return;

                // Get current selection (using Value for Revit 2024+)
                var currentIds = new HashSet<long>(
                    uidoc.Selection.GetElementIds().Select(id => id.Value)
                );

                // Check if selection changed
                if (!currentIds.SetEquals(_lastSelectionIds))
                {
                    _lastSelectionIds = currentIds;

                    // Trigger export to sync selection to web
                    _exportPending = true;
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
            }
            catch
            {
                // Ignore errors in Idling handler
            }
        }

        private static void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            // Check if meaningful elements were changed (not just view changes, etc.)
            var added = e.GetAddedElementIds();
            var deleted = e.GetDeletedElementIds();
            var modified = e.GetModifiedElementIds();

            if (added.Count == 0 && deleted.Count == 0 && modified.Count == 0)
                return;

            // Mark export as pending and restart debounce timer
            _exportPending = true;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            // Unsubscribe from events
            try { app.ControlledApplication.DocumentChanged -= OnDocumentChanged; } catch { }
            try { app.Idling -= OnIdling; } catch { }

            // Dispose debounce timer
            try { _debounceTimer?.Stop(); _debounceTimer?.Dispose(); } catch { }
            _debounceTimer = null;
            _autoExportEvent = null;
            _autoExportHandler = null;

            // Dispose command poller
            try { _poller?.Dispose(); } catch { }
            _poller = null;
            _externalEvent = null;
            _handler = null;

            return Result.Succeeded;
        }

        private static BitmapSource LoadIcon(string path, int size)
        {
            if (!File.Exists(path))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = size;   // Resize width
            bitmap.DecodePixelHeight = size;  // Resize height
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // Required for cross-thread access

            return bitmap;
        }
    }
}
