import { type ReactNode } from 'react';

export function Table({ head, children, empty }: { head: string[]; children: ReactNode; empty?: boolean }) {
  return (
    <div className="card overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-200">
        <thead className="bg-slate-50"><tr>{head.map((h) => <th key={h} className="th" scope="col">{h}</th>)}</tr></thead>
        <tbody className="divide-y divide-slate-100">{children}</tbody>
      </table>
      {empty && <p className="p-6 text-center text-sm text-slate-500">No records.</p>}
    </div>
  );
}

export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/50 p-4" role="dialog" aria-modal="true" aria-label={title} onKeyDown={(e) => e.key === 'Escape' && onClose()}>
      <div className="card my-8 w-full max-w-2xl p-6">
        <div className="mb-4 flex items-center justify-between"><h2 className="text-xl font-bold">{title}</h2><button aria-label="Close" className="rounded-md px-2 py-1 hover:bg-slate-100" onClick={onClose}>✕</button></div>
        {children}
      </div>
    </div>
  );
}

export function Stat({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return <div className="card p-4"><p className="text-sm text-slate-500">{label}</p><p className="mt-1 text-2xl font-bold">{value}</p>{hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}</div>;
}

export function BarChart({ data, format }: { data: { label: string; value: number }[]; format: (n: number) => string }) {
  const max = Math.max(1, ...data.map((d) => d.value));
  const w = Math.max(320, data.length * 18);
  return (
    <div className="overflow-x-auto">
      <svg role="img" aria-label="Bar chart" viewBox={`0 0 ${w} 180`} className="h-48 min-w-full" style={{ minWidth: w }}>
        {data.map((d, i) => {
          const h = (d.value / max) * 140;
          return (
            <g key={d.label}>
              <title>{`${d.label}: ${format(d.value)}`}</title>
              <rect x={i * 18 + 4} y={150 - h} width={12} height={h} rx={3} className="fill-brand-500" />
              {(i % Math.ceil(data.length / 8) === 0) && <text x={i * 18 + 10} y={170} textAnchor="middle" className="fill-slate-500" fontSize="9">{d.label.slice(5)}</text>}
            </g>
          );
        })}
        <line x1="0" y1="150" x2={w} y2="150" className="stroke-slate-300" />
      </svg>
    </div>
  );
}
