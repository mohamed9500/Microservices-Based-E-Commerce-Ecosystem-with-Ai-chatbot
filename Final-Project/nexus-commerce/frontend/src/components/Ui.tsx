import { type ReactNode } from 'react';
import { useT } from '../lib/i18n';
import type { StockStatus } from '../lib/types';

export function Spinner({ label }: { label?: string }) {
  const { t } = useT();
  return (
    <div role="status" className="flex items-center justify-center gap-3 py-12 text-slate-500">
      <span className="h-5 w-5 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600" aria-hidden />
      <span>{label ?? t('common.loading')}</span>
    </div>
  );
}

export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-3 xl:grid-cols-4" aria-busy="true" aria-label="Loading products">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="card animate-pulse overflow-hidden">
          <div className="aspect-square bg-slate-200" />
          <div className="space-y-2 p-4"><div className="h-3 w-1/3 rounded bg-slate-200" /><div className="h-4 w-4/5 rounded bg-slate-200" /><div className="h-4 w-1/4 rounded bg-slate-200" /></div>
        </div>
      ))}
    </div>
  );
}

export function EmptyState({ title, action, icon = '🛍️' }: { title: string; action?: ReactNode; icon?: string }) {
  return (
    <div className="card flex flex-col items-center gap-4 px-6 py-14 text-center">
      <div className="text-5xl" aria-hidden>{icon}</div>
      <p className="text-lg font-medium text-slate-700">{title}</p>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const { t } = useT();
  return (
    <div role="alert" className="card flex flex-col items-center gap-3 border-red-200 bg-red-50 px-6 py-10 text-center">
      <p className="font-medium text-red-800">{message}</p>
      {onRetry && <button className="btn-secondary" onClick={onRetry}>{t('common.retry')}</button>}
    </div>
  );
}

export function StockBadge({ status }: { status: StockStatus }) {
  const { t } = useT();
  const map: Record<StockStatus, [string, string]> = {
    in_stock: [t('product.inStock'), 'bg-emerald-50 text-emerald-700 ring-emerald-200'],
    low_stock: [t('product.lowStock'), 'bg-amber-50 text-amber-800 ring-amber-200'],
    out_of_stock: [t('product.outOfStock'), 'bg-red-50 text-red-700 ring-red-200'],
    unknown: [t('product.unknownStock'), 'bg-slate-100 text-slate-600 ring-slate-200'],
  };
  const [label, cls] = map[status] ?? map.unknown;
  return <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset ${cls}`}>{label}</span>;
}

export function Rating({ value, count }: { value: number; count?: number }) {
  const full = Math.round(value);
  return (
    <span className="inline-flex items-center gap-1 text-sm" aria-label={`Rated ${value.toFixed(1)} out of 5`}>
      <span className="text-amber-500" aria-hidden>{'★'.repeat(full)}<span className="text-slate-300">{'★'.repeat(5 - full)}</span></span>
      {count !== undefined && <span className="text-slate-500">({count})</span>}
    </span>
  );
}

export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (p: number) => void }) {
  const { t } = useT();
  if (totalPages <= 1) return null;
  return (
    <nav className="mt-8 flex items-center justify-center gap-3" aria-label="Pagination">
      <button className="btn-secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>{t('common.prev')}</button>
      <span className="text-sm text-slate-600">{t('common.page')} {page} / {totalPages}</span>
      <button className="btn-secondary" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>{t('common.next')}</button>
    </nav>
  );
}

export function Field({ label, error, children, htmlFor }: { label: string; error?: string; children: ReactNode; htmlFor: string }) {
  return (
    <div>
      <label htmlFor={htmlFor} className="mb-1 block text-sm font-medium text-slate-700">{label}</label>
      {children}
      {error && <p id={`${htmlFor}-error`} role="alert" className="mt-1 text-sm text-red-600">{error}</p>}
    </div>
  );
}

export function PageTitle({ children, sub }: { children: ReactNode; sub?: string }) {
  return (
    <div className="mb-6">
      <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">{children}</h1>
      {sub && <p className="mt-1 text-slate-600">{sub}</p>}
    </div>
  );
}

export const statusColor: Record<string, string> = {
  Pending: 'bg-slate-100 text-slate-700', AwaitingPayment: 'bg-amber-50 text-amber-800', Confirmed: 'bg-emerald-50 text-emerald-700',
  Shipped: 'bg-sky-50 text-sky-700', Delivered: 'bg-emerald-100 text-emerald-800', Cancelled: 'bg-red-50 text-red-700',
};

export function StatusPill({ status }: { status: string }) {
  return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusColor[status] ?? 'bg-slate-100'}`}>{status}</span>;
}
