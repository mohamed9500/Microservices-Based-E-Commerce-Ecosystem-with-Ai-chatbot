import { Link } from 'react-router-dom';
import { useT } from '../lib/i18n';
import { money } from '../lib/format';
import type { Product, StockInfo, RatingSummary } from '../lib/types';
import { useWishlist } from '../store/wishlist';
import { Rating, StockBadge } from './Ui';

interface Props { product: Product; stock?: StockInfo; rating?: RatingSummary; onAdd?: (p: Product) => void }

export function ProductCard({ product: p, stock, rating, onAdd }: Props) {
  const { t, lang } = useT();
  const wished = useWishlist((s) => s.ids.includes(p.id));
  const toggle = useWishlist((s) => s.toggle);
  const out = stock?.status === 'out_of_stock';

  return (
    <article className="card group relative flex flex-col overflow-hidden transition hover:-translate-y-0.5 hover:shadow-md">
      <button
        type="button"
        onClick={() => toggle(p.id)}
        aria-pressed={wished}
        aria-label={wished ? t('product.wishlistRemove') : t('product.wishlistAdd')}
        className="absolute end-3 top-3 z-10 flex h-9 w-9 items-center justify-center rounded-full bg-white/90 text-lg shadow hover:bg-white"
      >
        <span className={wished ? 'text-rose-500' : 'text-slate-400'}>{wished ? '♥' : '♡'}</span>
      </button>
      <Link to={`/products/${p.id}`} className="block overflow-hidden bg-slate-100">
        <img src={p.imageUrl} alt={p.name} loading="lazy" className="aspect-square w-full object-cover transition duration-300 group-hover:scale-105" />
      </Link>
      <div className="flex flex-1 flex-col gap-2 p-4">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-600">{p.brand} · {p.categoryName}</p>
        <Link to={`/products/${p.id}`} className="line-clamp-2 font-semibold leading-snug hover:text-brand-700">{p.name}</Link>
        {rating && rating.count > 0 && <Rating value={rating.average} count={rating.count} />}
        <div className="mt-auto flex items-center justify-between gap-2 pt-2">
          <span className="text-lg font-bold">{money(p.price, p.currency, lang)}</span>
          {stock && <StockBadge status={stock.status} />}
        </div>
        {onAdd && (
          <button className="btn-primary mt-2 w-full" disabled={out} onClick={() => onAdd(p)}>
            {out ? t('product.outOfStock') : t('product.add')}
          </button>
        )}
      </div>
    </article>
  );
}
