import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { EmptyState, Field, PageTitle, Spinner } from '../components/Ui';
import { api, ApiError } from '../lib/api';
import { money } from '../lib/format';
import { useT } from '../lib/i18n';
import type { Order } from '../lib/types';
import { useAuth } from '../store/auth';
import { useCart } from '../store/cart';
import { toast } from '../store/toast';

const FEE = 5, FREE_OVER = 100; // display only: the Order service computes the authoritative shipping fee

export default function Checkout() {
  const { t, lang } = useT();
  const navigate = useNavigate();
  const user = useAuth((s) => s.user);
  const { cart, refresh } = useCart();
  const [form, setForm] = useState({ fullName: user?.fullName ?? '', line1: user?.addressLine ?? '', city: user?.city ?? '', postalCode: user?.postalCode ?? '', country: user?.country ?? '', phone: user?.phone ?? '' });
  const [method, setMethod] = useState<'mock_success' | 'mock_decline'>('mock_success');
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [idempotencyKey] = useState(() => crypto.randomUUID());

  useEffect(() => { refresh().catch(() => undefined); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);

  if (!cart) return <Spinner />;
  if (cart.items.length === 0) return <div className="container-page py-10"><EmptyState title={t('cart.empty')} action={<Link to="/products" className="btn-primary">{t('cart.continue')}</Link>} /></div>;

  const shipping = cart.subtotal >= FREE_OVER ? 0 : FEE;
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value });

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true); setErrors({});
    try {
      const order = await api<Order>('/api/orders', {
        method: 'POST', headers: { 'Idempotency-Key': idempotencyKey },
        body: { shipping: { fullName: form.fullName, line1: form.line1, city: form.city, postalCode: form.postalCode, country: form.country, phone: form.phone }, paymentMethod: method },
      });
      useCart.getState().reset();
      toast.success('Order placed. Confirming...');
      navigate(`/orders/${order.id}`, { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        const fe: Record<string, string> = {};
        for (const [k, v] of Object.entries(err.errors ?? {})) fe[k.replace(/^shipping\./i, '').replace(/^./, (c) => c.toLowerCase())] = v[0];
        setErrors(fe);
        toast.error(err.message);
      } else toast.error('Could not place the order.');
    } finally { setBusy(false); }
  };

  const f = (id: keyof typeof form, label: string, type = 'text') => (
    <Field label={label} htmlFor={`co-${id}`} error={errors[id]}>
      <input id={`co-${id}`} className="input" type={type} required value={form[id]} onChange={set(id)} aria-invalid={!!errors[id]} aria-describedby={errors[id] ? `co-${id}-error` : undefined} />
    </Field>
  );

  return (
    <form onSubmit={submit} className="container-page py-8">
      <PageTitle>{t('checkout.title')}</PageTitle>
      <div className="grid gap-8 lg:grid-cols-[1fr_24rem]">
        <div className="space-y-6">
          <section className="card space-y-4 p-5">
            <h2 className="text-lg font-semibold">{t('checkout.shipping')}</h2>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="sm:col-span-2">{f('fullName', t('field.fullName'))}</div>
              <div className="sm:col-span-2">{f('line1', t('field.address'))}</div>
              {f('city', t('field.city'))}{f('postalCode', t('field.postal'))}{f('country', t('field.country'))}{f('phone', t('field.phone'), 'tel')}
            </div>
          </section>
          <fieldset className="card space-y-3 p-5">
            <legend className="px-1 text-lg font-semibold">{t('checkout.payment')}</legend>
            <p role="note" className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">{t('checkout.mockNotice')}</p>
            {(['mock_success', 'mock_decline'] as const).map((m) => (
              <label key={m} className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 p-3 has-[:checked]:border-brand-600 has-[:checked]:bg-brand-50">
                <input type="radio" name="pm" checked={method === m} onChange={() => setMethod(m)} />
                <span>{m === 'mock_success' ? t('checkout.mockSuccess') : t('checkout.mockDecline')}</span>
              </label>
            ))}
          </fieldset>
        </div>
        <aside className="card h-fit space-y-3 p-5">
          <ul className="divide-y divide-slate-100 text-sm">
            {cart.items.map((i) => <li key={i.productId} className="flex justify-between gap-3 py-2"><span>{i.quantity} × {i.name}</span><span>{money(i.lineTotal, cart.currency, lang)}</span></li>)}
          </ul>
          <div className="flex justify-between text-sm"><span>{t('cart.subtotal')}</span><span>{money(cart.subtotal, cart.currency, lang)}</span></div>
          <div className="flex justify-between text-sm"><span>{t('checkout.shippingFee')}</span><span>{shipping === 0 ? t('checkout.free') : money(shipping, cart.currency, lang)}</span></div>
          <div className="flex justify-between border-t border-slate-200 pt-3 text-lg font-bold"><span>{t('checkout.total')}</span><span>{money(cart.subtotal + shipping, cart.currency, lang)}</span></div>
          <button className="btn-primary w-full" disabled={busy}>{busy ? t('common.loading') : t('checkout.place')}</button>
        </aside>
      </div>
    </form>
  );
}
