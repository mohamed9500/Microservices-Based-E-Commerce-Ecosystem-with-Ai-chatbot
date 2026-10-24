import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState, ErrorState, PageTitle, Spinner } from '../components/Ui';
import { ApiError } from '../lib/api';
import { money } from '../lib/format';
import { useT } from '../lib/i18n';
import { useAuth } from '../store/auth';
import { useCart } from '../store/cart';
import { toast } from '../store/toast';

export default function CartPage() {
  const { t, lang } = useT();
  const token = useAuth((s) => s.token);
  const { cart, loading, refresh, setQuantity, remove } = useCart();

  useEffect(() => { if (token) refresh().catch((e) => toast.error(e instanceof ApiError ? e.message : 'Could not load the cart.')); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [token]);

  const run = async (fn: () => Promise<void>) => { try { await fn(); } catch (e) { toast.error(e instanceof ApiError ? e.message : 'Cart update failed.'); } };

  if (!token) return (
    <div className="container-page py-10"><EmptyState icon="🔐" title={t('cart.loginRequired')} action={<Link to="/login" state={{ from: '/cart' }} className="btn-primary">{t('nav.login')}</Link>} /></div>
  );
  if (loading && !cart) return <Spinner />;
  if (!cart) return <div className="container-page py-10"><ErrorState message={t('common.error')} onRetry={() => refresh()} /></div>;

  return (
    <div className="container-page py-8">
      <PageTitle>{t('cart.title')}</PageTitle>
      {cart.warnings.map((w) => <p key={w} role="status" className="mb-3 rounded-lg bg-amber-50 px-4 py-2 text-sm text-amber-800">{w}</p>)}
      {cart.items.length === 0 ? <EmptyState title={t('cart.empty')} action={<Link to="/products" className="btn-primary">{t('cart.continue')}</Link>} /> : (
        <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
          <ul className="space-y-3">
            {cart.items.map((i) => (
              <li key={i.productId} className="card flex gap-4 p-3">
                <Link to={`/products/${i.productId}`}><img src={i.imageUrl} alt={i.name} className="h-24 w-24 rounded-xl object-cover" /></Link>
                <div className="flex min-w-0 flex-1 flex-col justify-between">
                  <div className="flex justify-between gap-3">
                    <Link to={`/products/${i.productId}`} className="font-semibold hover:text-brand-700">{i.name}</Link>
                    <span className="font-semibold">{money(i.lineTotal, cart.currency, lang)}</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <button className="btn-secondary px-3 py-1" aria-label="Decrease quantity" disabled={i.quantity <= 1} onClick={() => run(() => setQuantity(i.productId, i.quantity - 1))}>−</button>
                      <span className="w-6 text-center" aria-live="polite">{i.quantity}</span>
                      <button className="btn-secondary px-3 py-1" aria-label="Increase quantity" disabled={i.quantity >= 20} onClick={() => run(() => setQuantity(i.productId, i.quantity + 1))}>+</button>
                      <span className="ms-2 text-sm text-slate-500">{money(i.unitPrice, cart.currency, lang)}</span>
                    </div>
                    <button className="text-sm font-medium text-red-600 hover:underline" onClick={() => run(() => remove(i.productId))}>{t('cart.remove')}</button>
                  </div>
                </div>
              </li>
            ))}
          </ul>
          <aside className="card h-fit space-y-4 p-5">
            <div className="flex justify-between text-lg font-semibold"><span>{t('cart.subtotal')}</span><span>{money(cart.subtotal, cart.currency, lang)}</span></div>
            <p className="text-xs text-slate-500">Prices are re-validated by the server at checkout. Shipping is calculated on the next step.</p>
            <Link to="/checkout" className="btn-primary w-full">{t('cart.checkout')}</Link>
          </aside>
        </div>
      )}
    </div>
  );
}
