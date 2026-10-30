import { Link } from 'react-router-dom';
import { ProductCard } from '../components/ProductCard';
import { ErrorState, ProductGridSkeleton } from '../components/Ui';
import { api, qs } from '../lib/api';
import { useAsync } from '../lib/hooks';
import { useT } from '../lib/i18n';
import type { Category, Paged, Product, RatingSummary, StockInfo } from '../lib/types';
import { useUi } from '../store/ui';
import { useAddToCart } from '../lib/useAddToCart';

export async function loadEnrichment(ids: string[]) {
  if (ids.length === 0) return { stock: {} as Record<string, StockInfo>, ratings: {} as Record<string, RatingSummary> };
  const [stock, ratings] = await Promise.all([
    api<StockInfo[]>(`/api/inventory/stock${qs({ ids: ids.join(',') })}`).catch(() => [] as StockInfo[]),
    api<RatingSummary[]>(`/api/reviews/summary${qs({ ids: ids.join(',') })}`).catch(() => [] as RatingSummary[]),
  ]);
  return {
    stock: Object.fromEntries(stock.map((s) => [s.productId, s])),
    ratings: Object.fromEntries(ratings.map((r) => [r.productId, r])),
  };
}

export default function Home() {
  const { t } = useT();
  const setChatOpen = useUi((s) => s.setChatOpen);
  const addToCart = useAddToCart();
  const featured = useAsync(async (signal) => {
    const page = await api<Paged<Product>>(`/api/products${qs({ featured: true, pageSize: 8 })}`, { signal });
    return { page, ...(await loadEnrichment(page.items.map((p) => p.id))) };
  }, []);
  const categories = useAsync((signal) => api<Category[]>('/api/categories', { signal }), []);

  return (
    <>
      <section className="relative overflow-hidden bg-brand-900 text-white">
        
<img
  src="/images/hero-products.png"
  alt=""
  aria-hidden="true"
  className="pointer-events-none absolute right-0 top-1/2 hidden h-auto w-[48%] max-h-[90%] -translate-y-1/2 object-contain object-right md:block"
/>

        <div className="container-page relative py-16 sm:py-24">
          <div className="max-w-2xl animate-fadeIn">
            <h1 className="text-4xl font-extrabold tracking-tight sm:text-5xl">{t('home.hero.title')}</h1>
            <p className="mt-4 text-lg text-indigo-100">{t('home.hero.sub')}</p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link to="/products" className="btn bg-white text-brand-900 hover:bg-indigo-50">{t('home.hero.cta')}</Link>
              <button className="btn border border-white/40 text-white hover:bg-white/10" onClick={() => setChatOpen(true)}>💬 {t('home.hero.ask')}</button>
            </div>
          </div>
        </div>
      </section>

      <section className="container-page mt-12">
        <h2 className="mb-5 text-2xl font-bold">{t('home.collections')}</h2>
        {categories.error ? <ErrorState message={categories.error} onRetry={categories.reload} /> : (
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {(categories.data ?? Array.from({ length: 8 }, () => null)).map((c, i) => c ? (
              <Link key={c.id} to={`/products?category=${c.slug}`} className="group relative overflow-hidden rounded-2xl">
                <img src={c.imageUrl ?? ''} alt="" loading="lazy" className="aspect-[3/2] w-full object-cover transition group-hover:scale-105" />
                <span className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/70 to-transparent p-3 text-sm font-semibold text-white">{c.name}</span>
              </Link>
            ) : <div key={i} className="aspect-[3/2] animate-pulse rounded-2xl bg-slate-200" />)}
          </div>
        )}
      </section>

      <section className="container-page mt-14">
        <div className="mb-5 flex items-end justify-between">
          <h2 className="text-2xl font-bold">{t('home.featured')}</h2>
          <Link to="/products" className="text-sm font-semibold text-brand-700 hover:underline">{t('home.viewAll')} →</Link>
        </div>
        {featured.loading ? <ProductGridSkeleton /> : featured.error ? <ErrorState message={featured.error} onRetry={featured.reload} /> : (
          <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-3 xl:grid-cols-4">
            {featured.data!.page.items.map((p) => (
              <ProductCard key={p.id} product={p} stock={featured.data!.stock[p.id]} rating={featured.data!.ratings[p.id]} onAdd={(x) => addToCart(x.id, x.name)} />
            ))}
          </div>
        )}
      </section>
    </>
  );
}
