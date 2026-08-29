import { useQuery } from "@tanstack/react-query";
import type { GeometrySnapshot } from "../components/LiveGeometryView";

const lastByKey = new Map<string, { etag?: string; snapshot: GeometrySnapshot }>();

async function fetchLatest(url: string, cacheKey: string): Promise<Response> {
    const cached = lastByKey.get(cacheKey);
    const headers: HeadersInit = {};
    if (cached?.etag) headers["If-None-Match"] = cached.etag;
    return fetch(url, { headers });
}

export function useLatestGeometry(projectName?: string) {
    return useQuery<GeometrySnapshot, Error>({
        queryKey: ['geometry-latest', projectName],
        
        queryFn: async () => {
            const baseUrl = "http://localhost:5245/api/geometry/latest";
            const url = projectName
                ? `${baseUrl}?projectName=${encodeURIComponent(projectName)}`
                : baseUrl;
            const cacheKey = projectName ?? "";

            let response = await fetchLatest(url, cacheKey);

            // Fallback to latest across all projects if specific project not found
            if (response.status === 404 && projectName) {
                response = await fetchLatest(baseUrl, "");
            }

            if (response.status === 304) {
                const cached = lastByKey.get(cacheKey) ?? lastByKey.get("");
                if (cached) return cached.snapshot;
                response = await fetch(url);
            }

            if (!response.ok) {
                throw new Error(await response.text());
            }

            const snapshot = await response.json() as GeometrySnapshot;
            const etag = response.headers.get("ETag") ?? undefined;
            lastByKey.set(cacheKey, { etag, snapshot });
            return snapshot;
        },

        // Data fresh until next poll - prevents extra refetches from focus/re-renders
        staleTime: 2000,
        
        // Poll every 2s for real-time updates
        refetchInterval: 2000,
        
        // Don't retry on error - polling is frequent enough
        retry: false,
    });
}