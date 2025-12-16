# RevitSync

**Real-time two-way sync between Autodesk Revit and a web-based 3D viewer.**

<!-- TODO: Add demo video -->
> 🎬 **Demo Video:** Coming soon...

---

## Features

- **Revit → Web**: Export geometry from Revit, visualize as 3D bounding boxes in browser
- **Web → Revit**: Click-to-place boxes in the web viewer, they appear in Revit as DirectShapes
- **Drag-to-move**: Select and drag web-created elements to reposition them in Revit
- **Delete**: Remove web-created elements from both viewer and Revit
- **Live polling**: Frontend auto-refreshes when new exports arrive

## Tech Stack

| Component | Technology |
|-----------|------------|
| **Revit Add-in** | C# / .NET Framework 4.8 / Revit API 2026 |
| **Backend API** | ASP.NET Core 9, In-memory queues |
| **Frontend** | React 18, TypeScript, Three.js (React Three Fiber), TailwindCSS |

---

## Prerequisites

- **Autodesk Revit 2026** (or adjust API references for your version)
- **.NET 9 SDK** (for backend)
- **Node.js 18+** (for frontend)
- **Visual Studio 2022** (for Revit add-in)

---

## Quick Start

### 1. Backend

```bash
cd backend/RevitSync.Api
dotnet run
```
Runs on `http://localhost:5245`

### 2. Frontend

```bash
cd frontend/revit-sync-frontend
npm install
npm run dev
```
Runs on `http://localhost:5173`

### 3. Revit Add-in

1. Open `revit-addin/RevitSync.Addin/RevitSync.Addin.sln` in Visual Studio
2. Build the solution
3. Copy `RevitSync.Addin.dll` to `%APPDATA%\Autodesk\Revit\Addins\2026\`
4. Create a `.addin` manifest file (see below)
5. Restart Revit

#### Addin Manifest

Create `RevitSync.addin` in `%APPDATA%\Autodesk\Revit\Addins\2026\`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>RevitSync</Name>
    <Assembly>RevitSync.Addin.dll</Assembly>
    <FullClassName>RevitSync.Addin.App</FullClassName>
    <AddInId>7AF9D8DB-6CEA-4E88-98FE-B2ED1BF112C3</AddInId>
    <VendorId>RevitSync</VendorId>
  </AddIn>
</RevitAddIns>
```

---

## Usage

1. Open a Revit project with walls, columns, or floors
2. Click **Export Geometry** in the RevitSync ribbon panel
3. Open `http://localhost:5173` in your browser
4. See your Revit geometry rendered as 3D boxes
5. Click **"Click to Place"** → click in the scene to add a box
6. The box appears in Revit as a DirectShape
7. Select a web-created box → drag arrows to move, or delete it
8. Click **Export Geometry** again to sync changes back to the viewer

---

## Architecture

<!-- TODO: Add architecture diagram -->
> 📐 **Architecture Diagram:** Coming soon...

```
Revit Add-in ←→ Backend API ←→ React Frontend
     ↓              ↓              ↓
  Export      Queue/Store      3D Viewer
  Commands    Geometry        (Three.js)
```

---

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/geometry` | POST | Ingest geometry snapshot from Revit |
| `/api/geometry/latest` | GET | Get latest geometry snapshot |
| `/api/commands` | POST | Enqueue command (ADD_BOXES, DELETE_ELEMENTS, MOVE_ELEMENT) |
| `/api/commands/next` | GET | Dequeue next command (polled by Revit) |

---

## License

MIT

