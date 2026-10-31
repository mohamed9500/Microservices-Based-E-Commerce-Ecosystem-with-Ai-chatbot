import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ProductCard } from '../components/ProductCard';
import { EmptyState, ErrorState, Pagination, PageTitle, ProductGridSkeleton } from '../components/Ui';
import { api, qs } from '../lib/api';
import { useAddToCart } from '../lib/useAddToCart';
import { useAsync, useDebounced } from '../lib/hooks';
import { useT, type TKey } from '../lib/i18n';
import type { Category, Paged, Product } from '../lib/types';
import { loadEnrichment } from './Home';

const SORTS = ['featured', 'price_asc', 'price_desc', 'newest', 'name'] as const;

export default function Products() {
  const { t } = useT();
  const [params, setParams] = useSearchParams();
  const addToCart = useAddToCart();
  const q = params.get('q') ?? '';
  const category = params.get('category') ?? '';
  const sort = params.get('sort') ?? 'featured';
  const page = Number(params.get('page') ?? '1') || 1;
  const [minP, setMinP] = useState(params.get('minPrice') ?? '');
  const [maxP, setMaxP] = useState(params.get('maxPrice') ?? '');
  const dMin = useDebounced(minP), dMax = useDebounced(maxP);

  const update = (patch: Record<string, string | undefined>, resetPage = true) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k); }
    if (resetPage) next.delete('page');
    setParams(next, { replace: true });
  };

  useEffect(() => { update({ minPrice: dMin || undefined, maxPrice: dMax || undefined }); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [dMin, dMax]);

  const categories = useAsync((signal) => api<Category[]>('/api/categories', { signal }), []);
  const minPrice = params.get('minPrice') ?? '', maxPrice = params.get('maxPrice') ?? '';
  const list = useAsync(async (signal) => {
    const result = await api<Paged<Product>>(`/api/products${qs({ q, category, sort, page, pageSize: 12, minPrice, maxPrice })}`, { signal });
    return { result, ...(await loadEnrichment(result.items.map((p) => p.id))) };
  }, [q, category, sort, page, minPrice, maxPrice]);

  const clear = () => { setMinP(''); setMaxP(''); setParams({}, { replace: true }); };

  return (
    <div className="container-page py-8">
      <PageTitle sub={list.data ? `${list.data.result.totalCount} ${t('shop.results')}` : undefined}>{q ? `“${q}”` : t('shop.title')}</PageTitle>
      <div className="grid gap-8 lg:grid-cols-[16rem_1fr]">
        <aside className="card h-fit space-y-5 p-4" aria-label="Filters">
          <div>
            <label htmlFor="f-q" className="mb-1 block text-sm font-medium">{t('shop.search')}</label>
            <input id="f-q" className="input" type="search" defaultValue={q} maxLength={100} onKeyDown={(e) => { if (e.key === 'Enter') update({ q: (e.target as HTMLInputElement).value.trim() || undefined }); }} onBlur={(e) => update({ q: e.target.value.trim() || undefined })} />
          </div>
          <div>
            <label htmlFor="f-cat" className="mb-1 block text-sm font-medium">{t('shop.category')}</label>
            <select id="f-cat" className="input" value={category} onChange={(e) => update({ category: e.target.value || undefined })}>
              <option value="">{t('shop.allCategories')}</option>
              {categories.data?.map((c) => <option key={c.id} value={c.slug}>{c.name}</option>)}
            </select>
          </div>
          <fieldset>
            <legend className="mb-1 text-sm font-medium">{t('shop.price')}</legend>
            <div className="flex gap-2">
              <input aria-label={t('shop.min')} className="input" type="number" min={0} placeholder={t('shop.min')} value={minP} onChange={(e) => setMinP(e.target.value)} />
              <input aria-label={t('shop.max')} className="input" type="number" min={0} placeholder={t('shop.max')} value={maxP} onChange={(e) => setMaxP(e.target.value)} />
            </div>
          </fieldset>
          <div>
            <label htmlFor="f-sort" className="mb-1 block text-sm font-medium">{t('shop.sort')}</label>
            <select id="f-sort" className="input" value={sort} onChange={(e) => update({ sort: e.target.value === 'featured' ? undefined : e.target.value })}>
              {SORTS.map((s) => <option key={s} value={s}>{t(`shop.sort.${s}` as TKey)}</option>)}
            </select>
          </div>
          <button className="btn-secondary w-full" onClick={clear}>{t('shop.clear')}</button>
        </aside>
        <section>
          {list.loading ? <ProductGridSkeleton count={6} /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> :
            list.data!.result.items.length === 0 ? <EmptyState icon="🔍" title={t('shop.empty')} action={<button className="btn-primary" onClick={clear}>{t('shop.clear')}</button>} /> : (
              <>
                <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-3">
                  {list.data!.result.items.map((p) => <ProductCard key={p.id} product={p} stock={list.data!.stock[p.id]} rating={list.data!.ratings[p.id]} onAdd={(x) => addToCart(x.id, x.name)} />)}
                </div>
                <Pagination page={list.data!.result.page} totalPages={list.data!.result.totalPages} onChange={(p) => { update({ page: String(p) }, false); window.scrollTo({ top: 0 }); }} />
              </>
            )}
        </section>
      </div>
    </div>
  );
}
