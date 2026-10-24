import { useState } from 'react';
import { ErrorState, PageTitle, Pagination, Spinner, StatusPill } from '../components/Ui';
import { api, ApiError, qs } from '../lib/api';
import { money, shortDate, shortId } from '../lib/format';
import { useAsync, useDebounced } from '../lib/hooks';
import type { Order, Paged } from '../lib/types';
import { toast } from '../store/toast';
import { Modal, Table } from './shared';

const STATUSES = ['', 'Pending', 'AwaitingPayment', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled'];

export default function OrdersAdmin() {
  const [status, setStatus] = useState('');
  const [q, setQ] = useState('');
  const dq = useDebounced(q);
  const [page, setPage] = useState(1);
  const [open, setOpen] = useState<Order | null>(null);
  const list = useAsync((signal) => api<Paged<Order>>(`/api/admin/orders${qs({ status, q: dq, page, pageSize: 15 })}`, { signal }), [status, dq, page]);

  const move = async (o: Order, to: 'Shipped' | 'Delivered') => {
    try { await api(`/api/admin/orders/${o.id}/status`, { method: 'PUT', body: { status: to } }); toast.success(`Order marked ${to}.`); setOpen(null); list.reload(); }
    catch (e) { toast.error(e instanceof ApiError ? e.message : 'Failed.'); }
  };

  return (
    <>
      <PageTitle>Orders</PageTitle>
      <div className="mb-4 flex flex-wrap gap-3">
        <input className="input max-w-xs" placeholder="Search customer or email" aria-label="Search orders" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} />
        <select className="input max-w-[12rem]" aria-label="Status filter" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>{STATUSES.map((s) => <option key={s} value={s}>{s || 'All statuses'}</option>)}</select>
      </div>
      {list.loading ? <Spinner /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : (
        <>
          <Table head={['Order', 'Customer', 'Date', 'Total', 'Status', '']} empty={list.data!.items.length === 0}>
            {list.data!.items.map((o) => (
              <tr key={o.id}><td className="td font-medium">#{shortId(o.id)}</td><td className="td">{o.customerName}<span className="block text-xs text-slate-500">{o.userEmail}</span></td>
                <td className="td">{shortDate(o.createdAtUtc)}</td><td className="td">{money(o.total, o.currency)}</td><td className="td"><StatusPill status={o.status} /></td>
                <td className="td"><button className="text-brand-700 hover:underline" onClick={() => setOpen(o)}>Details</button></td></tr>
            ))}
          </Table>
          <Pagination page={list.data!.page} totalPages={list.data!.totalPages} onChange={setPage} />
        </>
      )}
      {open && (
        <Modal title={`Order #${shortId(open.id)}`} onClose={() => setOpen(null)}>
          <div className="space-y-3 text-sm">
            <p><StatusPill status={open.status} /> {open.failureReason && <span className="text-red-600">{open.failureReason}</span>}</p>
            <p>{open.shipName}, {open.shipLine1}, {open.shipCity} {open.shipPostalCode}, {open.shipCountry} · {open.shipPhone}</p>
            <ul className="divide-y">{open.items.map((i) => <li key={i.productId} className="flex justify-between py-1.5"><span>{i.quantity} × {i.name}</span><span>{money(i.lineTotal, open.currency)}</span></li>)}</ul>
            <p className="text-end font-bold">Total {money(open.total, open.currency)} {open.paymentIsMock && <span className="text-xs font-normal text-amber-700">(mock payment)</span>}</p>
            <div className="flex justify-end gap-3">
              {open.status === 'Confirmed' && <button className="btn-primary" onClick={() => move(open, 'Shipped')}>Mark as shipped</button>}
              {open.status === 'Shipped' && <button className="btn-primary" onClick={() => move(open, 'Delivered')}>Mark as delivered</button>}
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}
