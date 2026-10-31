import { useState } from 'react';
import { ErrorState, PageTitle, Pagination, Spinner } from '../components/Ui';
import { api, ApiError, qs } from '../lib/api';
import { shortDate } from '../lib/format';
import { useAsync } from '../lib/hooks';
import type { AdminStock, Paged, Product } from '../lib/types';
import { toast } from '../store/toast';
import { Modal, Table } from './shared';

interface Adjustment { id: string; delta: number; quantityAfter: number; reason: string; source: string; createdAtUtc: string }

export default function InventoryAdmin() {
  const [page, setPage] = useState(1);
  const [lowOnly, setLowOnly] = useState(false);
  const [adjust, setAdjust] = useState<{ stock: AdminStock; name: string } | null>(null);
  const [history, setHistory] = useState<{ name: string; rows: Adjustment[] } | null>(null);
  const [delta, setDelta] = useState('');
  const [reason, setReason] = useState('');

  const list = useAsync(async (signal) => {
    const stock = await api<Paged<AdminStock>>(`/api/admin/inventory${qs({ page, pageSize: 20, lowOnly })}`, { signal });
    const ids = stock.items.map((s) => s.productId).join(',');
    const products = ids ? await api<Product[]>(`/api/products/by-ids${qs({ ids })}`, { signal }) : [];
    return { stock, names: Object.fromEntries(products.map((p) => [p.id, p.name])) as Record<string, string> };
  }, [page, lowOnly]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!adjust) return;
    try {
      await api(`/api/admin/inventory/${adjust.stock.productId}/adjust`, { method: 'POST', body: { delta: Number(delta), reason } });
      toast.success('Stock updated.'); setAdjust(null); setDelta(''); setReason(''); list.reload();
    } catch (ex) { toast.error(ex instanceof ApiError ? ex.message : 'Failed.'); }
  };
  const showHistory = async (s: AdminStock, name: string) => {
    try { setHistory({ name, rows: (await api<Paged<Adjustment>>(`/api/admin/inventory/${s.productId}/history?pageSize=30`)).items }); }
    catch (ex) { toast.error(ex instanceof ApiError ? ex.message : 'Failed.'); }
  };

  return (
    <>
      <PageTitle sub="Available = on hand − reserved by open orders. Products without a stock row are treated as out of stock.">Inventory</PageTitle>
      <label className="mb-4 flex items-center gap-2 text-sm"><input type="checkbox" checked={lowOnly} onChange={(e) => { setLowOnly(e.target.checked); setPage(1); }} /> Show only low / out of stock</label>
      {list.loading ? <Spinner /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : (
        <>
          <Table head={['Product', 'On hand', 'Reserved', 'Available', '']} empty={list.data!.stock.items.length === 0}>
            {list.data!.stock.items.map((s) => {
              const name = list.data!.names[s.productId] ?? s.productId.slice(0, 8);
              return (
                <tr key={s.productId}><td className="td font-medium">{name}</td><td className="td">{s.onHand}</td><td className="td">{s.reserved}</td>
                  <td className={`td font-semibold ${s.available <= 0 ? 'text-red-600' : s.available <= 5 ? 'text-amber-600' : ''}`}>{s.available}</td>
                  <td className="td whitespace-nowrap"><button className="me-3 text-brand-700 hover:underline" onClick={() => setAdjust({ stock: s, name })}>Adjust</button><button className="text-slate-600 hover:underline" onClick={() => showHistory(s, name)}>History</button></td></tr>
              );
            })}
          </Table>
          <Pagination page={list.data!.stock.page} totalPages={list.data!.stock.totalPages} onChange={setPage} />
        </>
      )}
      {adjust && (
        <Modal title={`Adjust stock: ${adjust.name}`} onClose={() => setAdjust(null)}>
          <form onSubmit={submit} className="space-y-3">
            <p className="text-sm text-slate-600">On hand: {adjust.stock.onHand} · Reserved: {adjust.stock.reserved}</p>
            <label className="block text-sm">Change (use a negative number to remove stock)<input className="input mt-1" type="number" required value={delta} onChange={(e) => setDelta(e.target.value)} /></label>
            <label className="block text-sm">Reason<input className="input mt-1" required minLength={3} maxLength={200} value={reason} onChange={(e) => setReason(e.target.value)} /></label>
            <div className="flex justify-end gap-3"><button type="button" className="btn-secondary" onClick={() => setAdjust(null)}>Cancel</button><button className="btn-primary">Apply</button></div>
          </form>
        </Modal>
      )}
      {history && (
        <Modal title={`History: ${history.name}`} onClose={() => setHistory(null)}>
          <Table head={['When', 'Change', 'After', 'Source', 'Reason']} empty={history.rows.length === 0}>
            {history.rows.map((r) => <tr key={r.id}><td className="td">{shortDate(r.createdAtUtc)}</td><td className="td">{r.delta > 0 ? `+${r.delta}` : r.delta}</td><td className="td">{r.quantityAfter}</td><td className="td">{r.source}</td><td className="td">{r.reason}</td></tr>)}
          </Table>
        </Modal>
      )}
    </>
  );
}
