import { NavLink, Outlet } from 'react-router-dom';

const links = [
  ['/admin', 'Overview'], ['/admin/products', 'Products'], ['/admin/categories', 'Categories'], ['/admin/inventory', 'Inventory'],
  ['/admin/orders', 'Orders'], ['/admin/customers', 'Customers'], ['/admin/reviews', 'Reviews'], ['/admin/reports', 'Sales report'], ['/admin/monitoring', 'Monitoring'],
] as const;

export function AdminLayout() {
  return (
    <div className="container-page grid gap-6 py-8 md:grid-cols-[13rem_1fr]">
      <nav aria-label="Admin" className="card h-fit p-2">
        <ul className="flex gap-1 overflow-x-auto md:flex-col">
          {links.map(([to, label]) => (
            <li key={to}>
              <NavLink to={to} end={to === '/admin'} className={({ isActive }) => `block whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium ${isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-600 hover:bg-slate-100'}`}>{label}</NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <div className="min-w-0"><Outlet /></div>
    </div>
  );
}
