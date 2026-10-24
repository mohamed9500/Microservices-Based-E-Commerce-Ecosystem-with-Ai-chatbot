import { useState } from 'react';
import { ErrorState, PageTitle, Pagination, Rating, Spinner } from '../components/Ui';
import { api, ApiError, qs } from '../lib/api';
import { money, shortDate } from '../lib/format';
import { useAsync, useDebounced } from '../lib/hooks';
import type { OrderStats, Paged, Review, ServiceHealth, User } from '../lib/types';
import { useAuth } from '../store/auth';
import { toast } from '../store/toast';
import { BarChart, Stat, Table } from './shared';

export function CustomersAdmin() {
  const me = useAuth((s) => s.user);
  const [q, setQ] = useState(''); const dq = useDebounced(q); const [page, setPage] = useState(1);
  const list = useAsync((signal) => api<Paged<User>>(`/api/admin/users${qs({ q: dq, page, pageSize: 15 })}`, { signal }), [dq, page]);
  const toggle = async (u: User) => {
    try { await api(`/api/admin/users/${u.id}/active`, { method: 'PUT', body: { isActive: !u.isActive } }); toast.success(u.isActive ? 'Account disabled.' : 'Account enabled.'); list.reload(); }
    catch (e) { toast.error(e instanceof ApiError ? e.message : 'Failed.'); }
  };
  return (
    <>
      <PageTitle>Customers</PageTitle>
      <input className="input mb-4 max-w-xs" placeholder="Search name or email" aria-label="Search customers" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} />
      {list.loading ? <Spinner /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : (
        <>
          <Table head={['Name', 'Email', 'Role', 'Joined', 'Status', '']} empty={list.data!.items.length === 0}>
            {list.data!.items.map((u) => (
              <tr key={u.id}><td className="td font-medium">{u.fullName}</td><td className="td">{u.email}</td><td className="td">{u.role}</td><td className="td">{shortDate(u.createdAtUtc)}</td>
                <td className="td">{u.isActive ? 'Active' : 'Disabled'}</td>
                <td className="td">{u.id !== me?.id && <button className="text-brand-700 hover:underline" onClick={() => toggle(u)}>{u.isActive ? 'Disable' : 'Enable'}</button>}</td></tr>
            ))}
          </Table>
          <Pagination page={list.data!.page} totalPages={list.data!.totalPages} onChange={setPage} />
        </>
      )}
    </>
  );
}

export function ReviewsAdmin() {
  const [page, setPage] = useState(1);
  const list = useAsync((signal) => api<Paged<Review>>(`/api/admin/reviews?page=${page}&pageSize=15`, { signal }), [page]);
  const setVisible = async (r: Review, isVisible: boolean) => {
    try { await api(`/api/admin/reviews/${r.id}/visibility`, { method: 'PUT', body: { isVisible } }); toast.success(isVisible ? 'Review published.' : 'Review hidden.'); list.reload(); }
    catch (e) { toast.error(e instanceof ApiError ? e.message : 'Failed.'); }
  };
  const remove = async (r: Review) => {
    if (!confirm('Delete this review permanently?')) return;
    try { await api(`/api/reviews/${r.id}`, { method: 'DELETE' }); toast.success('Review deleted.'); list.reload(); }
    catch (e) { toast.error(e instanceof ApiError ? e.message : 'Failed.'); }
  };
  return (
    <>
      <PageTitle>Review moderation</PageTitle>
      {list.loading ? <Spinner /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : (
        <>
          <Table head={['Review', 'Author', 'Date', 'Visibility', '']} empty={list.data!.items.length === 0}>
            {list.data!.items.map((r) => (
              <tr key={r.id}><td className="td max-w-md"><Rating value={r.rating} /><p className="font-medium">{r.title}</p><p className="text-slate-600">{r.body}</p></td>
                <td className="td">{r.userName}</td><td className="td">{shortDate(r.createdAtUtc)}</td><td className="td">{r.isVisible ? 'Visible' : 'Hidden'}</td>
                <td className="td whitespace-nowrap"><button className="me-3 text-brand-700 hover:underline" onClick={() => setVisible(r, !r.isVisible)}>{r.isVisible ? 'Hide' : 'Show'}</button><button className="text-red-600 hover:underline" onClick={() => remove(r)}>Delete</button></td></tr>
            ))}
          </Table>
          <Pagination page={list.data!.page} totalPages={list.data!.totalPages} onChange={setPage} />
        </>
      )}
    </>
  );
}

export function ReportsAdmin() {
  const [days, setDays] = useState(30);
  const s = useAsync((signal) => api<OrderStats>(`/api/admin/orders/stats?days=${days}`, { signal }), [days]);
  return (
    <>
      <PageTitle>Sales report</PageTitle>
      <select className="input mb-4 max-w-[12rem]" aria-label="Period" value={days} onChange={(e) => setDays(Number(e.target.value))}>{[7, 30, 90, 180].map((d) => <option key={d} value={d}>Last {d} days</option>)}</select>
      {s.loading ? <Spinner /> : s.error || !s.data ? <ErrorState message={s.error ?? 'Failed'} onRetry={s.reload} /> : (
        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Stat label="Revenue (all time)" value={money(s.data.revenue)} /><Stat label="Revenue orders" value={s.data.revenueOrders} /><Stat label="Average order" value={money(s.data.averageOrderValue)} /><Stat label="All orders" value={s.data.totalOrders} />
          </div>
          <div className="card p-4"><h2 className="mb-2 font-semibold">Daily revenue (last {days} days)</h2><BarChart data={s.data.daily.map((d) => ({ label: d.date, value: d.revenue }))} format={(n) => money(n)} /></div>
          <div className="grid gap-6 lg:grid-cols-2">
            <div><h2 className="mb-2 font-semibold">Top products</h2><Table head={['Product', 'Units', 'Revenue']} empty={s.data.topProducts.length === 0}>{s.data.topProducts.map((p) => <tr key={p.productId}><td className="td">{p.name}</td><td className="td">{p.quantity}</td><td className="td">{money(p.revenue)}</td></tr>)}</Table></div>
            <div><h2 className="mb-2 font-semibold">Orders by status</h2><Table head={['Status', 'Orders']}>{Object.entries(s.data.byStatus).map(([k, v]) => <tr key={k}><td className="td">{k}</td><td className="td">{v}</td></tr>)}</Table></div>
          </div>
        </div>
      )}
    </>
  );
}

export function MonitoringAdmin() {
  const m = useAsync((signal) => api<{ checkedAtUtc: string; services: ServiceHealth[] }>('/api/admin/monitoring/services', { signal }), []);
  return (
    <>
      <PageTitle sub="Health endpoints of every service, checked from the API gateway. RabbitMQ: http://localhost:15672 (management UI).">Operational monitoring</PageTitle>
      <button className="btn-secondary mb-4" onClick={m.reload}>Refresh</button>
      {m.loading ? <Spinner /> : m.error || !m.data ? <ErrorState message={m.error ?? 'Failed'} onRetry={m.reload} /> : (
        <>
          <Table head={['Service', 'Status', 'Latency', 'Details']}>
            {m.data.services.map((s) => <tr key={s.name}><td className="td font-medium">{s.name}</td><td className="td">{s.healthy ? <span className="text-emerald-600">● Healthy</span> : <span className="text-red-600">● Unhealthy</span>}</td><td className="td">{s.latencyMs} ms</td><td className="td text-xs text-slate-500">{s.error ?? `HTTP ${s.status}`}</td></tr>)}
          </Table>
          <p className="mt-2 text-xs text-slate-500">Checked {shortDate(m.data.checkedAtUtc)}</p>
        </>
      )}
    </>
  );
}
