import { useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState, ErrorState, Pagination, PageTitle, Spinner, StatusPill } from '../components/Ui';
import { api, ApiError } from '../lib/api';
import { money, shortDate, shortId } from '../lib/format';
import { useAsync } from '../lib/hooks';
import { useT } from '../lib/i18n';
import type { Order, Paged, Payment } from '../lib/types';
import { toast } from '../store/toast';
import { useState } from 'react';

export function OrdersPage() {
  const { t, lang } = useT();
  const [page, setPage] = useState(1);
  const list = useAsync((signal) => api<Paged<Order>>(`/api/orders?page=${page}&pageSize=10`, { signal }), [page]);
  if (list.loading) return <Spinner />;
  if (list.error) return <div className="container-page py-10"><ErrorState message={list.error} onRetry={list.reload} /></div>;
  return (
    <div className="container-page py-8">
      <PageTitle>{t('orders.title')}</PageTitle>
      {list.data!.items.length === 0 ? <EmptyState icon="📦" title={t('orders.empty')} action={<Link to="/products" className="btn-primary">{t('cart.continue')}</Link>} /> : (
        <>
          <ul className="space-y-3">
            {list.data!.items.map((o) => (
              <li key={o.id}>
                <Link to={`/orders/${o.id}`} className="card flex flex-wrap items-center justify-between gap-3 p-4 hover:border-brand-500">
                  <div><p className="font-semibold">#{shortId(o.id)}</p><p className="text-sm text-slate-500">{shortDate(o.createdAtUtc, lang)} · {o.items.reduce((n, i) => n + i.quantity, 0)} items</p></div>
                  <StatusPill status={o.status} />
                  <span className="font-semibold">{money(o.total, o.currency, lang)}</span>
                </Link>
              </li>
            ))}
          </ul>
          <Pagination page={list.data!.page} totalPages={list.data!.totalPages} onChange={setPage} />
        </>
      )}
    </div>
  );
}

const STEPS = ['Pending', 'AwaitingPayment', 'Confirmed', 'Shipped', 'Delivered'] as const;

export function OrderDetailPage() {
  const { id = '' } = useParams();
  const { t, lang } = useT();
  const order = useAsync((signal) => api<Order>(`/api/orders/${id}`, { signal }), [id]);
  const payment = useAsync((signal) => api<Payment>(`/api/payments/order/${id}`, { signal }).catch(() => undefined), [id, order.data?.status]);

  // Orders are confirmed asynchronously (saga): poll while the order is still in flight.
  const inFlight = order.data && (order.data.status === 'Pending' || order.data.status === 'AwaitingPayment');
  useEffect(() => {
    if (!inFlight) return;
    const timer = setInterval(order.reload, 2000);
    return () => clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inFlight]);

  const cancel = async () => {
    try { await api(`/api/orders/${id}/cancel`, { method: 'POST' }); toast.success('Order cancelled.'); order.reload(); }
    catch (e) { toast.error(e instanceof ApiError ? e.message : 'Could not cancel the order.'); }
  };

  if (order.loading && !order.data) return <Spinner />;
  if (order.error || !order.data) return <div className="container-page py-10"><ErrorState message={order.error ?? 'Order not found'} onRetry={order.reload} /></div>;
  const o = order.data;
  const stepIndex = STEPS.indexOf(o.status as (typeof STEPS)[number]);

  return (
    <div className="container-page py-8">
      <PageTitle sub={shortDate(o.createdAtUtc, lang)}>Order #{shortId(o.id)} <StatusPill status={o.status} /></PageTitle>
      {inFlight && <p role="status" className="mb-4 rounded-lg bg-indigo-50 px-4 py-3 text-sm text-indigo-900">We are reserving your items and processing the (mock) payment. This page updates automatically.</p>}
      {o.status === 'Cancelled' && <p role="alert" className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">This order was cancelled. {o.failureReason}</p>}
      {o.status !== 'Cancelled' && (
        <ol className="mb-6 grid grid-cols-5 gap-2 text-center text-xs" aria-label="Order progress">
          {STEPS.map((s, i) => <li key={s} className={`rounded-lg px-1 py-2 ${i <= stepIndex ? 'bg-brand-600 font-semibold text-white' : 'bg-slate-200 text-slate-600'}`}>{s === 'AwaitingPayment' ? 'Payment' : s}</li>)}
        </ol>
      )}
      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <ul className="card divide-y divide-slate-100">
          {o.items.map((i) => (
            <li key={i.productId} className="flex items-center gap-4 p-3">
              <img src={i.imageUrl} alt="" className="h-16 w-16 rounded-lg object-cover" />
              <Link to={`/products/${i.productId}`} className="flex-1 font-medium hover:text-brand-700">{i.name}<span className="block text-xs text-slate-500">{i.quantity} × {money(i.unitPrice, o.currency, lang)}</span></Link>
              <span className="font-semibold">{money(i.lineTotal, o.currency, lang)}</span>
            </li>
          ))}
        </ul>
        <aside className="space-y-4">
          <div className="card space-y-1 p-4 text-sm">
            <div className="flex justify-between"><span>{t('cart.subtotal')}</span><span>{money(o.subtotal, o.currency, lang)}</span></div>
            <div className="flex justify-between"><span>{t('checkout.shippingFee')}</span><span>{o.shippingFee === 0 ? t('checkout.free') : money(o.shippingFee, o.currency, lang)}</span></div>
            <div className="flex justify-between border-t pt-2 text-base font-bold"><span>{t('checkout.total')}</span><span>{money(o.total, o.currency, lang)}</span></div>
          </div>
          <div className="card p-4 text-sm"><h2 className="mb-1 font-semibold">{t('checkout.shipping')}</h2>{o.shipName}<br />{o.shipLine1}<br />{o.shipCity} {o.shipPostalCode}<br />{o.shipCountry}</div>
          {payment.data && <div className="card p-4 text-sm"><h2 className="mb-1 font-semibold">Payment</h2><p>Status: <strong>{payment.data.status}</strong></p>{payment.data.isMock && <p className="mt-1 rounded bg-amber-50 p-2 text-xs text-amber-900">{payment.data.notice}</p>}</div>}
          {['Pending', 'AwaitingPayment', 'Confirmed'].includes(o.status) && <button className="btn-danger w-full" onClick={cancel}>{t('orders.cancel')}</button>}
        </aside>
      </div>
    </div>
  );
}
