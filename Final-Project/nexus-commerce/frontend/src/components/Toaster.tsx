import { useToast } from '../store/toast';

export function Toaster() {
  const { toasts, dismiss } = useToast();
  const color = { success: 'bg-emerald-600', error: 'bg-red-600', info: 'bg-slate-800' } as const;
  return (
    <div className="pointer-events-none fixed bottom-4 start-4 z-[60] flex w-[min(92vw,22rem)] flex-col gap-2" aria-live="polite">
      {toasts.map((t) => (
        <div key={t.id} role={t.kind === 'error' ? 'alert' : 'status'} className={`pointer-events-auto flex animate-fadeIn items-start justify-between gap-3 rounded-xl px-4 py-3 text-sm text-white shadow-lg ${color[t.kind]}`}>
          <span>{t.message}</span>
          <button aria-label="Dismiss" className="opacity-80 hover:opacity-100" onClick={() => dismiss(t.id)}>✕</button>
        </div>
      ))}
    </div>
  );
}
