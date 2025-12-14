export type ViewPreset = 'iso' | 'top' | 'bottom' | 'front' | 'back' | 'left' | 'right';

export function ViewCube({ onSelect }: { onSelect: (preset: ViewPreset) => void }) {
    return (
        <div className="select-none rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 p-2 text-xs text-slate-200 shadow-lg">
            <div className="font-semibold text-slate-100 mb-2">View</div>
            <div className="grid grid-cols-3 gap-1">
                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('top')}>Top</button>
                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('iso')}>Iso</button>
                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('bottom')}>Bot</button>

                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('left')}>Left</button>
                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('front')}>Front</button>
                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('right')}>Right</button>

                <button className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700" onClick={() => onSelect('back')}>Back</button>
                <div />
                <div />
            </div>
        </div>
    );
}


