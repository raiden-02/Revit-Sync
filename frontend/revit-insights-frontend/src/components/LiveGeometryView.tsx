import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { Environment, OrbitControls, PointerLockControls } from "@react-three/drei";
import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import * as THREE from "three";
import { ViewCube, type ViewPreset } from "./ViewCube";

export type GeometryPrimitive = {
    category: string
    centerX: number
    centerY: number
    centerZ: number
    sizeX: number
    sizeY: number
    sizeZ: number
}

export type GeometrySnapshot = {
    projectName: string
    timestampUtc: string
    primitives: GeometryPrimitive[]
}

function colorForCategory(category: string): string {
    const key = category.toLowerCase()
    if (key.includes('wall')) return '#4ade80'
    if (key.includes('column')) return '#60a5fa'
    if (key.includes('floor')) return '#f97316'
    return '#e5e7eb'
}

function PrimitiveBox({ primitive }: { primitive: GeometryPrimitive }) {
    const color = colorForCategory(primitive.category);
    const position: [number, number, number] = [
        primitive.centerX,
        primitive.centerZ, // Revit Z (up) -> Three Y (up)
        -primitive.centerY, // Revit Y -> Three -Z (flip for expected orientation)
    ];
    
    const scale: [number, number, number] = [
        primitive.sizeX,
        primitive.sizeZ, // Revit Z -> Three Y
        primitive.sizeY, // Revit Y -> Three Z
    ];

    return (
        <mesh position={position} scale={scale} castShadow receiveShadow>
            <boxGeometry args={[1, 1, 1]} />
            <meshStandardMaterial color={color} metalness={0.1} roughness={0.7} />
        </mesh>
    )
}

export function LiveGeometryView({ snapshot }: { snapshot: GeometrySnapshot }) {
    const count = snapshot.primitives?.length ?? 0;
    const [navMode, setNavMode] = useState<"orbit" | "fps">("orbit");
    const [viewPreset, setViewPreset] = useState<ViewPreset>("iso");

    const orbitRef = useRef<any>(null);
    const canvasWrapperRef = useRef<HTMLDivElement | null>(null);
    const keysRef = useRef<Record<string, boolean>>({});

    const bounds = useMemo(() => {
        if (!snapshot.primitives?.length) return null;
        let minX = Infinity, maxX = -Infinity;
        let minY = Infinity, maxY = -Infinity;
        let minZ = Infinity, maxZ = -Infinity;

        for (const primitive of snapshot.primitives) {
            // Convert Revit coords (X,Y,Z up) -> Three coords (X,Y up,Z)
            const cx = primitive.centerX;
            const cy = primitive.centerZ;
            const cz = -primitive.centerY; // flip handedness/orientation
            const sx = primitive.sizeX;
            const sy = primitive.sizeZ;
            const sz = primitive.sizeY;

            const x0 = cx - sx / 2;
            const x1 = cx + sx / 2;
            const y0 = cy - sy / 2;
            const y1 = cy + sy / 2;
            const z0 = cz - sz / 2;
            const z1 = cz + sz / 2;

            if (x0 < minX) minX = x0;
            if (x1 > maxX) maxX = x1;
            if (y0 < minY) minY = y0;
            if (y1 > maxY) maxY = y1;
            if (z0 < minZ) minZ = z0;
            if (z1 > maxZ) maxZ = z1;
        }

        const sizeX = maxX - minX;
        const sizeY = maxY - minY;
        const sizeZ = maxZ - minZ;

        const center: [number, number, number] = [
            (minX + maxX) / 2,
            (minY + maxY) / 2,
            (minZ + maxZ) / 2
        ];

        return { center, size: Math.max(sizeX, sizeY, sizeZ) };
    }, [snapshot.primitives]);

    const cameraDistance = useMemo(() => {
        if (!bounds) return 150;
        return Math.max(30, bounds.size * 1.6);
    }, [bounds]);

    const getPresetDir = useCallback((preset: ViewPreset): THREE.Vector3 => {
        switch (preset) {
            case "top": return new THREE.Vector3(0, 1, 0);
            case "bottom": return new THREE.Vector3(0, -1, 0);
            case "front": return new THREE.Vector3(0, 0, 1);
            case "back": return new THREE.Vector3(0, 0, -1);
            case "left": return new THREE.Vector3(-1, 0, 0);
            case "right": return new THREE.Vector3(1, 0, 0);
            case "iso": default: return new THREE.Vector3(1, 1, 1).normalize();
        }
    }, []);

    useEffect(() => {
        const onDown = (e: KeyboardEvent) => { keysRef.current[e.code] = true; };
        const onUp = (e: KeyboardEvent) => { keysRef.current[e.code] = false; };
        window.addEventListener("keydown", onDown);
        window.addEventListener("keyup", onUp);
        return () => {
            window.removeEventListener("keydown", onDown);
            window.removeEventListener("keyup", onUp);
        };
    }, []);

    const SceneControls = () => {
        const { camera } = useThree();
        useFrame((_state, dt: number) => {
            if (navMode !== "fps") return;
            // PointerLockControls adds isLocked on the controls instance, but we can't type it cleanly here.
            // Instead, move only when pointer is locked on the canvas element.
            const locked = document.pointerLockElement != null;
            if (!locked) return;

            const speedBase = Math.max(15, cameraDistance * 0.4); // feet/sec-ish
            const speed = (keysRef.current["ShiftLeft"] || keysRef.current["ShiftRight"]) ? speedBase * 2 : speedBase;

            const forward = new THREE.Vector3();
            camera.getWorldDirection(forward);
            forward.y = 0;
            forward.normalize();
            if (!Number.isFinite(forward.x) || !Number.isFinite(forward.z) || forward.lengthSq() < 1e-8) return;
            // In Three.js, forward is camera direction; right is forward x up (no extra negation).
            const up = new THREE.Vector3(0, 1, 0);
            const right = new THREE.Vector3().crossVectors(forward, up).normalize();

            const move = new THREE.Vector3();
            if (keysRef.current["KeyW"]) move.add(forward);
            if (keysRef.current["KeyS"]) move.sub(forward);
            if (keysRef.current["KeyA"]) move.sub(right); // left
            if (keysRef.current["KeyD"]) move.add(right); // right
            if (keysRef.current["Space"]) move.y += 1;
            if (keysRef.current["ControlLeft"] || keysRef.current["ControlRight"]) move.y -= 1;

            if (move.lengthSq() > 0) {
                move.normalize().multiplyScalar(speed * dt);
                camera.position.add(move);
            }
        });

        // Snap camera for view presets in orbit mode
        useEffect(() => {
            if (!bounds) return;
            if (navMode !== "orbit") return;
            const center = new THREE.Vector3(bounds.center[0], bounds.center[1], bounds.center[2]);
            const dir = getPresetDir(viewPreset);
            camera.position.copy(center.clone().add(dir.multiplyScalar(cameraDistance)));
            camera.lookAt(center);
            if (orbitRef.current?.target) {
                orbitRef.current.target.set(center.x, center.y, center.z);
                orbitRef.current.update?.();
            }
        }, [bounds, navMode, viewPreset, camera, cameraDistance, getPresetDir]);

        return null;
    };

    return (
        <div ref={canvasWrapperRef} className="fixed inset-0 bg-slate-950 text-slate-100">
            {/* HUD */}
            <div className="absolute left-4 top-4 z-10 rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 px-3 py-2 text-sm shadow-lg">
                <div className="font-semibold">Revit Geometry Stream</div>
                <div className="text-xs text-slate-300">
                    Project: <b>{snapshot.projectName}</b> • Primitives: <b>{count}</b> • {new Date(snapshot.timestampUtc).toLocaleTimeString()}
                </div>
                <div className="mt-2 flex gap-2 text-xs">
                    <button
                        className={`px-2 py-1 rounded ${navMode === "orbit" ? "bg-slate-700" : "bg-slate-800 hover:bg-slate-700"}`}
                        onClick={() => setNavMode("orbit")}
                    >
                        Orbit
                    </button>
                    <button
                        className={`px-2 py-1 rounded ${navMode === "fps" ? "bg-slate-700" : "bg-slate-800 hover:bg-slate-700"}`}
                        onClick={() => setNavMode("fps")}
                    >
                        First-person (WASD)
                    </button>
                </div>
                {navMode === "fps" && (
                    <div className="mt-2 text-[11px] text-slate-300">
                        Click the scene to lock mouse. WASD move, Space/CTRL up/down, Shift faster, Esc unlock.
                    </div>
                )}
            </div>

            <div className="absolute right-4 top-4 z-10">
                <ViewCube onSelect={(p) => { setNavMode("orbit"); setViewPreset(p); }} />
            </div>

            <Canvas
                shadows
                style={{ width: "100%", height: "100%" }}
                camera={{ position: [0, 80, cameraDistance], fov: 55, near: 0.1, far: 100000 }}
                onPointerDown={() => {
                    if (navMode !== "fps") return;
                    // PointerLockControls reacts to click, but this ensures focus is on the canvas wrapper.
                    canvasWrapperRef.current?.focus?.();
                }}
            >
                <color attach="background" args={["#020617"]} />
                <hemisphereLight intensity={0.6} groundColor="#020617" />
                <directionalLight position={[50, 80, 40]} intensity={1.2} castShadow />

                <SceneControls />

                <Suspense fallback={null}>
                    {snapshot.primitives?.map((primitive, index) => (
                        <PrimitiveBox key={index} primitive={primitive} />
                    ))}
                    <Environment preset="city" />
                </Suspense>

                {navMode === "orbit" && (
                    <OrbitControls ref={orbitRef} enablePan enableZoom enableRotate />
                )}
                {navMode === "fps" && (
                    <PointerLockControls />
                )}
            </Canvas>
        </div>
    );
}
