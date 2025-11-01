import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query'

const qc = new QueryClient()

function useLatest() {
  return useQuery({
    queryKey: ['latest'],
    queryFn: async () => {
      const r = await fetch('http://localhost:5245/api/modeldata/latest')
      if (!r.ok) throw new Error('No data yet')
      return r.json()
    },
    retry: false
  })
}

function AppInner() {
  const { data, isLoading, error } = useLatest()
  return (
    <div className="min-h-screen p-6 grid gap-4 lg:grid-cols-2">
      <div className="card">
        <h1 className="text-2xl font-semibold mb-2">Revit Element Insights</h1>
        {isLoading && <div>Loading…</div>}
        {error && <div className="text-red-600">No data yet — export from Revit.</div>}
        {data && (
          <div className="space-y-1">
            <div><b>Project:</b> {data.projectName}</div>
            <div><b>Revit:</b> {data.revitVersion}</div>
            <div><b>Timestamp:</b> {new Date(data.timestampUtc).toLocaleString()}</div>
          </div>
        )}
      </div>
      <div className="card">3D/Charts placeholder</div>
    </div>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <AppInner />
    </QueryClientProvider>
  )
}
