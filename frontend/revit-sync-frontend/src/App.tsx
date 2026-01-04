import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { LiveGeometryView } from './components/LiveGeometryView'
import { useLatestGeometry } from './hooks/useLatestGeometry'
const qc = new QueryClient()

function AppInner() {
  const geometryQuery = useLatestGeometry()
  if (geometryQuery.isLoading) {
    return <div className="fixed inset-0 bg-slate-950 text-slate-200 grid place-items-center">Waiting for geometry export from Revit…</div>
  }

  if (geometryQuery.isError || !geometryQuery.data) {
    return (
      <div className="fixed inset-0 bg-slate-950 text-slate-200 grid place-items-center p-6 text-center">
        <div>
          <div className="text-2xl font-semibold mb-2">Revit Sync (Geometry Stream)</div>
          <div className="text-sm text-slate-400">
            In Revit, click <b>Generate Column Grid</b> (optional) then <b>Export Geometry</b>.
          </div>
        </div>
      </div>
    )
  }

  return <LiveGeometryView snapshot={geometryQuery.data} />
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <AppInner />
    </QueryClientProvider>
  )
}
