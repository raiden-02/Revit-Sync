# RevitSync frontend

React + Three.js viewer. It polls `GET /api/geometry/latest` and POSTs commands to the local ASP.NET API. The Revit add-in is the other side of that loop.

Setup, ports, supported operations, and limits: **[root README](../../README.md)**.

```bash
npm install
npm run dev
```

Dev server: `http://127.0.0.1:5173` (expects the API at `http://localhost:5245`).
