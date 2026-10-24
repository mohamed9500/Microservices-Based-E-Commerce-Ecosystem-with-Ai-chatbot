import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { EmptyState, ErrorState, Field, Rating, Spinner, StockBadge } from '../components/Ui';
import { api, ApiError, qs } from '../lib/api';
import { money, shortDate } from '../lib/format';
import { useAsync } from '../lib/hooks';
import { useT } from '../lib/i18n';
import type { Paged, Product, RatingSummary, Review, StockInfo } from '../lib/types';
import { useAddToCart } from '../lib/useAddToCart';
import { useAuth } from '../store/auth';
import { toast } from '../store/toast';
import { useWishlist } from '../store/wishlist';

export default function ProductDetail() {
  const { id = '' } = useParams();
  const { t, lang } = useT();
  const addToCart = useAddToCart();
  const token = useAuth((s) => s.token);
  const wished = useWishlist((s) => s.ids.includes(id));
  const toggleWish = useWishlist((s) => s.toggle);
  const [img, setImg] = useState(0);
  const [qty, setQty] = useState(1);
  const location = useLocation();

  const data = useAsync(async (signal) => {
    const product = await api<Product>(`/api/products/${id}`, { signal });
    const [stock, summary] = await Promise.all([
      api<StockInfo[]>(`/api/inventory/stock${qs({ ids: id })}`, { signal }).catch(() => []),
      api<RatingSummary[]>(`/api/reviews/summary${qs({ ids: id })}`, { signal }).catch(() => []),
    ]);
    return { product, stock: stock[0] as StockInfo | undefined, rating: summary[0] as RatingSummary | undefined };
  }, [id]);
  const reviews = useAsync((signal) => api<Paged<Review>>(`/api/reviews/product/${id}?pageSize=20`, { signal }), [id]);

  if (data.loading) return <Spinner />;
  if (data.error || !data.data) return (
    <div className="container-page py-10">
      <ErrorState message={data.error ?? 'Product not found'} onRetry={data.reload} />
      <div className="mt-4 text-center"><Link to="/products" className="btn-secondary">{t('cart.continue')}</Link></div>
    </div>
  );
  const { product: p, stock, rating } = data.data;
  const gallery = p.gallery.length ? p.gallery : [p.imageUrl];
  const out = stock?.status === 'out_of_stock';
  const max = stock ? Math.max(1, Math.min(20, stock.available)) : 20;

  return (
    <div className="container-page py-8">
      <nav aria-label="Breadcrumb" className="mb-4 text-sm text-slate-500">
        <Link to="/products" className="hover:underline">{t('nav.shop')}</Link> / <Link to={`/products?category=${p.categorySlug}`} className="hover:underline">{p.categoryName}</Link>
      </nav>
      <div className="grid gap-8 lg:grid-cols-2">
        <div>
          <div className="card overflow-hidden"><img src={gallery[img]} alt={p.name} className="aspect-square w-full object-cover" /></div>
          <div className="mt-3 flex gap-3" role="tablist" aria-label="Product images">
            {gallery.map((g, i) => (
              <button key={g} role="tab" aria-selected={i === img} aria-label={`Image ${i + 1}`} onClick={() => setImg(i)}
                className={`overflow-hidden rounded-xl border-2 ${i === img ? 'border-brand-600' : 'border-transparent'}`}>
                <img src={g} alt="" className="h-20 w-20 object-cover" />
              </button>
            ))}
          </div>
        </div>
        <div className="space-y-5">
          <p className="text-sm font-semibold uppercase tracking-wide text-brand-600">{p.brand}</p>
          <h1 className="text-3xl font-bold tracking-tight">{p.name}</h1>
          <div className="flex items-center gap-3">
            {rating && rating.count > 0 ? <Rating value={rating.average} count={rating.count} /> : <span className="text-sm text-slate-500">{t('product.noReviews')}</span>}
            {stock && <StockBadge status={stock.status} />}
          </div>
          <p className="text-3xl font-extrabold">{money(p.price, p.currency, lang)}</p>
          <p className="leading-relaxed text-slate-700">{p.description}</p>
          <div className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2 text-sm font-medium">{t('product.quantity')}
              <select className="input w-20" value={qty} onChange={(e) => setQty(Number(e.target.value))} disabled={out}>
                {Array.from({ length: Math.min(max, 10) }, (_, i) => i + 1).map((n) => <option key={n}>{n}</option>)}
              </select>
            </label>
            <button className="btn-primary flex-1 sm:flex-none sm:px-8" disabled={out} onClick={() => addToCart(p.id, p.name, qty)}>{out ? t('product.outOfStock') : t('product.add')}</button>
            <button className="btn-secondary" aria-pressed={wished} onClick={() => toggleWish(p.id)}>{wished ? '♥' : '♡'} {wished ? t('product.wishlistRemove') : t('product.wishlistAdd')}</button>
          </div>
          <p className="text-xs text-slate-500">{t('product.sku')}: {p.sku}</p>
          {Object.keys(p.specs).length > 0 && (
            <div>
              <h2 className="mb-2 text-lg font-semibold">{t('product.specs')}</h2>
              <dl className="card divide-y divide-slate-100">
                {Object.entries(p.specs).map(([k, v]) => <div key={k} className="grid grid-cols-2 gap-4 px-4 py-2.5 text-sm"><dt className="text-slate-500">{k}</dt><dd className="font-medium">{v}</dd></div>)}
              </dl>
            </div>
          )}
        </div>
      </div>

      <section className="mt-14" aria-labelledby="rev-h">
        <h2 id="rev-h" className="mb-4 text-2xl font-bold">{t('product.reviews')}</h2>
        <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
          <div className="space-y-4">
            {reviews.loading ? <Spinner /> : reviews.error ? <ErrorState message={reviews.error} onRetry={reviews.reload} /> :
              reviews.data!.items.length === 0 ? <EmptyState icon="⭐" title={t('product.noReviews')} /> :
                reviews.data!.items.map((r) => (
                  <article key={r.id} className="card p-4">
                    <div className="flex items-center justify-between"><Rating value={r.rating} /><time className="text-xs text-slate-500">{shortDate(r.createdAtUtc, lang)}</time></div>
                    <h3 className="mt-1 font-semibold">{r.title}</h3>
                    <p className="mt-1 text-sm text-slate-700">{r.body}</p>
                    <p className="mt-2 text-xs text-slate-500">{r.userName}{r.verifiedPurchase && ' · Verified purchase'}</p>
                  </article>
                ))}
          </div>
          {token ? <ReviewForm productId={p.id} onDone={() => { reviews.reload(); data.reload(); }} /> : (
            <div className="card h-fit p-4 text-sm text-slate-600"><Link className="font-semibold text-brand-700 hover:underline" to="/login" state={{ from: location.pathname }}>{t('nav.login')}</Link> {t('product.writeReview').toLowerCase()}.</div>
          )}
        </div>
      </section>
    </div>
  );
}

function ReviewForm({ productId, onDone }: { productId: string; onDone: () => void }) {
  const { t } = useT();
  const [rating, setRating] = useState(5);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      await api('/api/reviews', { method: 'POST', body: { productId, rating, title, body } });
      toast.success('Thank you for your review!');
      setTitle(''); setBody(''); onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not submit the review.');
    } finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="card h-fit space-y-3 p-4">
      <h3 className="font-semibold">{t('product.writeReview')}</h3>
      <p className="text-xs text-slate-500">Only customers who purchased this product can review it.</p>
      <Field label="Rating" htmlFor="rv-r"><select id="rv-r" className="input" value={rating} onChange={(e) => setRating(Number(e.target.value))}>{[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{'★'.repeat(n)}</option>)}</select></Field>
      <Field label="Title" htmlFor="rv-t"><input id="rv-t" className="input" required minLength={2} maxLength={120} value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
      <Field label="Review" htmlFor="rv-b"><textarea id="rv-b" className="input min-h-24" required minLength={5} maxLength={2000} value={body} onChange={(e) => setBody(e.target.value)} /></Field>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      <button className="btn-primary w-full" disabled={busy}>{t('product.writeReview')}</button>
    </form>
  );
}
