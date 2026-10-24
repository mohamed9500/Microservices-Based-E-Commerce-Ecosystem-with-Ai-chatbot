import { Link } from 'react-router-dom';
import { ProductCard } from '../components/ProductCard';
import { EmptyState, ErrorState, PageTitle, ProductGridSkeleton } from '../components/Ui';
import { api, qs } from '../lib/api';
import { useAsync } from '../lib/hooks';
import { useT } from '../lib/i18n';
import type { Product } from '../lib/types';
import { useAddToCart } from '../lib/useAddToCart';
import { useWishlist } from '../store/wishlist';
import { loadEnrichment } from './Home';

export default function WishlistPage() {
  const { t } = useT();
  const ids = useWishlist((s) => s.ids);
  const addToCart = useAddToCart();
  const data = useAsync(async (signal) => {
    if (ids.length === 0) return { items: [] as Product[], stock: {}, ratings: {} };
    const items = await api<Product[]>(`/api/products/by-ids${qs({ ids: ids.slice(0, 50).join(',') })}`, { signal });
    return { items, ...(await loadEnrichment(items.map((p) => p.id))) };
  }, [ids.join(',')]);

  return (
    <div className="container-page py-8">
      <PageTitle>{t('wishlist.title')}</PageTitle>
      {data.loading ? <ProductGridSkeleton count={4} /> : data.error ? <ErrorState message={data.error} onRetry={data.reload} /> :
        data.data!.items.length === 0 ? <EmptyState icon="♡" title={t('wishlist.empty')} action={<Link to="/products" className="btn-primary">{t('cart.continue')}</Link>} /> : (
          <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-3 xl:grid-cols-4">
            {data.data!.items.map((p) => <ProductCard key={p.id} product={p} stock={(data.data!.stock as Record<string, never>)[p.id]} rating={(data.data!.ratings as Record<string, never>)[p.id]} onAdd={(x) => addToCart(x.id, x.name)} />)}
          </div>
        )}
    </div>
  );
}
