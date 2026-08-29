# RevitSync

Revit posts a bounding-box snapshot to a local ASP.NET Core API. A React / Three.js page polls that API and draws the same elements. Clicks in the browser enqueue commands. The add-in polls the queue and applies them on Revit's UI thread.

```
Revit add-in                    ASP.NET Core API                 Browser (React + Three.js)
────────────                    ────────────────                 ──────────────────────────
Export / DocumentChanged
  POST /api/geometry     ──►    latest snapshot in memory  ──►   GET /api/geometry/latest (2s)
                                                                     draw boxes, highlight

Click / drag / delete
  GET /api/commands/next ◄──    per-project command queue  ◄──   POST /api/commands
  ExternalEvent + Transaction
```

Local demo only. Three processes on one machine (`localhost:5245`). This is a personal learning project, not production software.

> **Unofficial, not Autodesk work.** Independent portfolio project by [Mayur Reddy](https://github.com/raiden-02). Not an Autodesk product. Not affiliated with, endorsed by, or sponsored by Autodesk, Inc. Autodesk and Revit are trademarks of Autodesk, Inc.

### Demo

[![Watch the demo](https://img.youtube.com/vi/9N6vfX0DKNM/hqdefault.jpg)](https://www.youtube.com/watch?v=9N6vfX0DKNM)

[youtube.com/watch?v=9N6vfX0DKNM](https://www.youtube.com/watch?v=9N6vfX0DKNM) : Revit on the left, web viewer on the right. Export, selection sync, live `DocumentChanged` updates, click-to-place boxes.

---

## What it does

### Revit to web

- **Export**: world-axis bounding boxes from 13 categories in the **active view** (whole document only if there is no view)
- **Auto-sync**: `DocumentChanged` starts a 500 ms debounce, then an `ExternalEvent` re-exports
- **Selection**: `Idling` checks selection every 300 ms. Selected ids ride along on the next snapshot. Viewer outlines them cyan
- **Properties**: Name, Family, Type, Level, Mark, Comments, Length, Area, Volume, wall/floor offsets, phases, and Workset when the model is workshared
- **Colors**: one hex color per category

### Web to Revit

| Command | What happens in Revit |
|---------|------------------------|
| `ADD_BOXES` | Creates `DirectShape` boxes in `OST_GenericModel`, tagged `ApplicationId = "RevitSync"` |
| `MOVE_ELEMENT` | Translates that tagged DirectShape to a new center |
| `DELETE_ELEMENTS` | Deletes those tagged DirectShapes only |
| `SELECT_ELEMENTS` | `Selection.SetElementIds` plus `ShowElements` (zoom). No transaction |

Move and delete from the web do **not** apply to ordinary Revit walls, floors, or families.

### Categories exported

Walls, Roofs, Floors, Structural Columns, Structural Framing, Structural Foundation, Windows, Doors, Curtain Wall Panels, Curtain Wall Mullions, Stairs, Ramps, Generic Model.

### Limits (exact)

- Geometry is **axis-aligned bounding boxes**, not meshes, faces, or rooms
- Export with zero primitives does **not** POST, so the last snapshot stays in the API
- Snapshot and command queue live in process memory. Restart the API and they are gone
- No auth, no TLS on the demo URL, no multi-user, no conflict resolution
- Add-in and viewer hard-code `http://localhost:5245`
- Viewer poll: 2 s. Add-in command poll: 1.5 s. Not WebSockets
- The API sends `ETag`. The viewer sends `If-None-Match` and reuses the last snapshot on 304
- DTOs are copied in the add-in, the API, and TypeScript. There is no shared contract package
- Command handler keeps one `Pending` slot. A second dequeue before `Execute` can drop the first command
- Element ids on move/delete/select are parsed with `int.TryParse`
- `Generate Column Grid` needs an active crop box and a structural column family
- Add-in project is **.NET Framework 4.8** targeting Revit **2026** assemblies. Autodesk's official add-in target for Revit 2025/2026 is .NET 8. If the add-in does not load, check that mismatch first. This repo does not retarget it

---

## Tech

| Process | Stack |
|---------|--------|
| Revit add-in | C#, .NET Framework 4.8, Revit API (`RevitYear` defaults to 2026) |
| Backend | ASP.NET Core 9, in-memory `ConcurrentDictionary` / `ConcurrentQueue`, Swagger |
| Frontend | React 19, TypeScript, Three.js (React Three Fiber), TanStack Query, Tailwind CSS |

---

## Run it

API and viewer first, then Revit. Everything talks to `http://localhost:5245`.

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

Needs **Autodesk Revit 2026** at `C:\Program Files\Autodesk\Revit 2026\` (the `.csproj` references those `RevitAPI.dll` / `RevitAPIUI.dll` files). Other years: set `RevitYear` in the `.csproj` and copy into that year's Addins folder.

1. Open `revit-addin/RevitSync.Addin/RevitSync.Addin.sln` in Visual Studio.
2. Restore NuGet if prompted (`Newtonsoft.Json`).
3. Build the solution. A post-build step copies `RevitSync.Addin.dll`, `RevitSync.addin`, and the ribbon icon into `%APPDATA%\Autodesk\Revit\Addins\2026\`
4. Restart Revit. The **RevitSync** panel is on the **Add-Ins** tab.

Manual install: copy [`revit-addin/RevitSync.Addin/RevitSync.Addin/RevitSync.addin`](revit-addin/RevitSync.Addin/RevitSync.Addin/RevitSync.addin) next to the DLL in that Addins folder. The Assembly path is the DLL file name.

Also needed: **.NET 9 SDK** for the API, **Node.js 18+** for the viewer, **Visual Studio 2022+** with .NET desktop development for the 4.8 add-in.

### Demo clicks

1. Start API and viewer.
2. Open a Revit model that has walls, floors, or roofs in the **active view**.
3. Click **Export Geometry** on the RevitSync ribbon.
4. Open `http://127.0.0.1:5173`. Orbit / pan / zoom. Click a box for properties.
5. **Select in Revit** highlights and zooms that element.
6. Select in Revit. The same box goes cyan in the viewer.
7. **Click to Place** → click the ground plane. A box appears as a DirectShape after the next command poll (~1.5 s) plus the next viewer poll (~2 s).
8. Select a **web-created** box → drag the transform arrows to move, or **Delete**.
9. After the first export, model edits re-export through `DocumentChanged`. **Generate Column Grid** is an optional helper if you have no model yet (needs crop box + a column family).

---

## API

| Endpoint | Method | What it does |
|----------|--------|----------------|
| `/api/geometry` | POST | Store latest snapshot for `projectName` |
| `/api/geometry/latest` | GET | Latest snapshot. Optional `?projectName=`. `If-None-Match` → 304 |
| `/api/commands` | POST | Enqueue `ADD_BOXES`, `DELETE_ELEMENTS`, `MOVE_ELEMENT`, or `SELECT_ELEMENTS` |
| `/api/commands/next` | GET | Dequeue one command. Optional `?projectName=`. 204 if empty |

---

## Checks you can run without Revit

```bash
dotnet test backend/RevitSync.Api.Tests/RevitSync.Api.Tests.csproj
cd frontend/revit-sync-frontend && npm run build && npm run lint
```

The add-in needs Revit assemblies on disk. Visual Studio **Build Solution** is the check for that project.

---

## License

[MIT](LICENSE) © Mayur Reddy

Autodesk and Revit are registered trademarks of Autodesk, Inc. This project is a personal learning exercise and is not an official Autodesk sample or plugin.
