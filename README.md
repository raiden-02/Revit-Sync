# RevitSync

**A personal learning project:** bidirectional sync between Autodesk Revit and a web-based 3D viewer.

> **Unofficial — not Autodesk work.** RevitSync is an independent portfolio project by [Mayur Reddy](https://github.com/raiden-02). It is **not** an Autodesk product and is **not** affiliated with, endorsed by, or sponsored by Autodesk, Inc. Autodesk and Revit are trademarks of Autodesk, Inc.

Built to learn the Revit API while wrapping it in a small full-stack app (C# add-in → ASP.NET Core → React / Three.js). Local demo only — not production software.

### Demo

[![Watch the demo](https://img.youtube.com/vi/9N6vfX0DKNM/hqdefault.jpg)](https://www.youtube.com/watch?v=9N6vfX0DKNM)

[youtube.com/watch?v=9N6vfX0DKNM](https://www.youtube.com/watch?v=9N6vfX0DKNM) — Revit on the left, web viewer on the right: export, selection sync, live DocumentChanged updates, and click-to-place boxes.

---

## Features

### Revit → Web
- **Geometry export**: bounding boxes from 13 Revit categories (not full meshes)
- **Auto-sync**: `DocumentChanged` triggers a debounced re-export (500ms)
- **Selection sync**: Revit selection highlighted in the viewer (cyan)
- **Properties panel**: Family, Type, Level, Area, Volume, and related parameters
- **Category colors**: one color per category for massing visualization

### Web → Revit
- **Click-to-place**: add boxes in the browser; they appear as `DirectShape` elements in Revit
- **Drag-to-move**: reposition **web-created** elements with transform handles
- **Delete**: remove **web-created** elements on both sides
- **Selection sync**: click in the viewer, then “Select in Revit” to highlight and zoom

### Categories exported
Walls, Roofs, Floors, Structural Columns, Structural Framing, Structural Foundation, Windows, Doors, Curtain Wall Panels, Curtain Wall Mullions, Stairs, Ramps, Generic Model

### Intentional limits
- In-memory API storage (lost on restart); no auth, no multi-user
- Bounding boxes only — good for massing, not fabrication geometry
- All three processes run on one machine (`localhost`)
- Move/delete from the web only applies to elements tagged as created by this add-in

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| **Revit add-in** | C# / .NET Framework 4.8 / Revit API (project files target Revit 2026) |
| **Backend API** | ASP.NET Core 9, in-memory store, REST + Swagger |
| **Frontend** | React 19, TypeScript, Three.js (React Three Fiber), TanStack Query, Tailwind CSS |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              REVIT ADD-IN                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │    App.cs    │  │  Geometry    │  │   Command    │  │  AutoExport  │    │
│  │  (Startup)   │  │  Exporter    │  │   Poller     │  │   Handler    │    │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘    │
│         │                 │                 │                 │             │
│         └─────── DocumentChanged / Idling ──┴──── ExternalEvent ────────────┘
└─────────────────────────────────────────────────────────────────────────────┘
                     │ POST /geometry              │ GET /commands/next
                     ▼                             ▲
┌─────────────────────────────────────────────────────────────────────────────┐
│                           ASP.NET CORE BACKEND                               │
│  ┌────────────────────────────────┐    ┌────────────────────────────────┐  │
│  │     GeometryController         │    │     CommandsController         │  │
│  │  POST /api/geometry            │    │  POST /api/commands            │  │
│  │  GET  /api/geometry/latest     │    │  GET  /api/commands/next       │  │
│  └────────────────────────────────┘    └────────────────────────────────┘  │
│           ConcurrentDictionary                    ConcurrentQueue           │
└─────────────────────────────────────────────────────────────────────────────┘
                     │ GET /geometry/latest        │ POST /commands
                     ▼                             ▲
┌─────────────────────────────────────────────────────────────────────────────┐
│                          REACT FRONTEND                                      │
│  ┌────────────────────────────────┐    ┌────────────────────────────────┐  │
│  │    useLatestGeometry           │    │    useEnqueueCommand           │  │
│  │    (polls every 2s)            │    │    (mutation hook)             │  │
│  └────────────────────────────────┘    └────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                    LiveGeometryView (Three.js)                        │  │
│  │   3D Canvas  │  Properties Panel  │  Control Panel  │  Category Legend│  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Data flow
- **Revit → Web**: add-in POSTs a snapshot; frontend polls `GET /api/geometry/latest` every 2s (ETag / 304 when unchanged)
- **Web → Revit**: frontend POSTs a command; add-in polls `GET /api/commands/next` every 1.5s and applies it on the Revit UI thread via `ExternalEvent`
- **Typical latency**: ~1.5–2s (polling, not WebSockets)

---

## Prerequisites

- **Autodesk Revit 2026** at the default path `C:\Program Files\Autodesk\Revit 2026\` (the add-in project references those `RevitAPI.dll` / `RevitAPIUI.dll` assemblies). Other years: change `RevitYear` in the `.csproj` and copy into that year’s Addins folder.
- **.NET 9 SDK** (or newer, with the net9.0 targeting pack) for the backend
- **Node.js 18+** for the frontend
- **Visual Studio 2022 or later** with .NET desktop development (for the .NET Framework 4.8 add-in)

---

## Quick Start

Run the API and the viewer first, then load the add-in in Revit. Everything talks to `http://localhost:5245`.

### 1. Backend

```bash
cd backend/RevitSync.Api
dotnet run
```

API: `http://localhost:5245` · Swagger: `http://localhost:5245/swagger`

### 2. Frontend

```bash
cd frontend/revit-sync-frontend
npm install
npm run dev
```

Viewer: `http://127.0.0.1:5173` (Vite is pinned to that host and port). `http://localhost:5173` also works.

### 3. Revit add-in

1. Open `revit-addin/RevitSync.Addin/RevitSync.Addin.sln` in Visual Studio.
2. Restore NuGet packages if prompted (`Newtonsoft.Json`).
3. Build the solution (**Build → Build Solution**). A post-build step copies `RevitSync.Addin.dll`, `RevitSync.addin`, and the ribbon icon into:

   `%APPDATA%\Autodesk\Revit\Addins\2026\`

4. Restart Revit. The **RevitSync** panel is on the **Add-Ins** tab.

If you need to install the manifest by hand, copy [`revit-addin/RevitSync.Addin/RevitSync.Addin/RevitSync.addin`](revit-addin/RevitSync.Addin/RevitSync.Addin/RevitSync.addin) next to the DLL in that Addins folder. The Assembly path is the DLL file name (same directory as the `.addin`).

**Revit 2025+ note:** Autodesk’s official add-in target for Revit 2025/2026 is .NET 8. This repo’s add-in is still a .NET Framework 4.8 class library (as originally written). If the add-in does not load, that mismatch is the first thing to check — this project does not retarget the add-in.

---

## Usage

1. Start the backend and frontend (steps 1–2 above).
2. Open Revit with a project that has walls, floors, roofs, etc.
3. Click **Export Geometry** on the RevitSync ribbon.
4. Open the viewer at `http://127.0.0.1:5173`.
5. Orbit / pan / zoom the 3D view. Click an element for properties.
6. **Select in Revit** highlights and zooms that element in Revit.
7. Select in Revit — the same element highlights cyan in the viewer.
8. **Click to Place** → click the ground plane to create a box.
9. Select a web-created box → drag the transform arrows to move it.
10. Select a web-created box → **Delete**.

After the first export, model edits sync automatically via `DocumentChanged` (no need to click Export again). **Generate Column Grid** is an optional test helper if you do not have a model handy.

---

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/geometry` | POST | Ingest geometry snapshot from Revit |
| `/api/geometry/latest` | GET | Latest snapshot (ETag / `If-None-Match` → 304) |
| `/api/commands` | POST | Queue a command for Revit |
| `/api/commands/next` | GET | Dequeue next command (polled by the add-in) |

### Command types

| Type | Description |
|------|-------------|
| `ADD_BOXES` | Create DirectShape boxes in Revit |
| `DELETE_ELEMENTS` | Delete web-created elements by id |
| `MOVE_ELEMENT` | Move a web-created element |
| `SELECT_ELEMENTS` | Select and zoom to elements |

---

## License

[MIT](LICENSE) © Mayur Reddy

Autodesk® and Revit® are registered trademarks of Autodesk, Inc. This project is a personal learning exercise and is not an official Autodesk sample or plugin.
