import { Link } from 'react-router-dom';
import { ErrorState, PageTitle, Spinner } from '../components/Ui';
import { api } from '../lib/api';
import { money } from '../lib/format';
import { useAsync } from '../lib/hooks';
import type { AdminStock, OrderStats, Paged, Product, User } from '../lib/types';
import { Stat } from './shared';

export default function Dashboard() {
  const d = useAsync(async (signal) => {
    const [stats, users, products, low] = await Promise.all([
      api<OrderStats>('/api/admin/orders/stats?days=30', { signal }),
      api<Paged<User>>('/api/admin/users?pageSize=1', { signal }),
      api<Paged<Product>>('/api/products?includeInactive=true&pageSize=1', { signal }),
      api<Paged<AdminStock>>('/api/admin/inventory?lowOnly=true&pageSize=1', { signal }),
    ]);
    return { stats, users: users.totalCount, products: products.totalCount, low: low.totalCount };
  }, []);

  if (d.loading) return <Spinner />;
  if (d.error || !d.data) return <ErrorState message={d.error ?? 'Failed to load'} onRetry={d.reload} />;
  const { stats, users, products, low } = d.data;
  return (
    <>
      <PageTitle sub="Live figures from the Order, Identity, Catalog and Inventory services.">Overview</PageTitle>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <Stat label="Revenue (confirmed orders)" value={money(stats.revenue)} hint="Confirmed, shipped and delivered" />
        <Stat label="Orders" value={stats.totalOrders} hint={`${stats.byStatus.Pending + stats.byStatus.AwaitingPayment} in progress`} />
        <Stat label="Average order value" value={money(stats.averageOrderValue)} />
        <Stat label="Customers & staff" value={users} />
        <Stat label="Products" value={products} />
        <Stat label="Low / out of stock" value={<Link className="text-brand-700 hover:underline" to="/admin/inventory">{low}</Link>} />
      </div>
    </>
  );
}
