import { useState } from 'react';
import { ErrorState, PageTitle, Pagination, Spinner } from '../components/Ui';
import { api, ApiError, qs } from '../lib/api';
import { money } from '../lib/format';
import { useAsync, useDebounced } from '../lib/hooks';
import type { Category, Paged, Product } from '../lib/types';
import { toast } from '../store/toast';
import { Modal, Table } from './shared';

const blank = { sku: '', name: '', description: '', price: 0, categoryId: '', brand: '', imageUrl: '/images/products/p01.svg', isActive: true, isFeatured: false, specsText: '' };

export default function ProductsAdmin() {
  const [page, setPage] = useState(1);
  const [q, setQ] = useState('');
  const dq = useDebounced(q);
  const [editing, setEditing] = useState<(typeof blank & { id?: string }) | null>(null);
  const [err, setErr] = useState('');
  const cats = useAsync((s) => api<Category[]>('/api/categories', { signal: s }), []);
  const list = useAsync((signal) => api<Paged<Product>>(`/api/products${qs({ includeInactive: true, q: dq, page, pageSize: 15, sort: 'name' })}`, { signal }), [dq, page]);

  const edit = (p?: Product) => {
    setErr('');
    setEditing(p ? { id: p.id, sku: p.sku, name: p.name, description: p.description, price: p.price, categoryId: p.categoryId, brand: p.brand, imageUrl: p.imageUrl, isActive: p.isActive, isFeatured: p.isFeatured, specsText: Object.entries(p.specs).map(([k, v]) => `${k}: ${v}`).join('\n') }
      : { ...blank, categoryId: cats.data?.[0]?.id ?? '' });
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    const specs: Record<string, string> = {};
    for (const line of editing.specsText.split('\n')) { const i = line.indexOf(':'); if (i > 0) specs[line.slice(0, i).trim()] = line.slice(i + 1).trim(); }
    const { id, specsText, ...rest } = editing;
    try {
      await api(id ? `/api/products/${id}` : '/api/products', { method: id ? 'PUT' : 'POST', body: { ...rest, price: Number(rest.price), specs } });
      toast.success('Product saved.'); setEditing(null); list.reload();
    } catch (ex) { setErr(ex instanceof ApiError ? ex.message : 'Save failed.'); }
  };

  const deactivate = async (p: Product) => {
    if (!confirm(`Deactivate "${p.name}"? It will be hidden from the store (order history is kept).`)) return;
    try { await api(`/api/products/${p.id}`, { method: 'DELETE' }); toast.success('Product deactivated.'); list.reload(); }
    catch (ex) { toast.error(ex instanceof ApiError ? ex.message : 'Failed.'); }
  };

  const set = (k: string, v: string | number | boolean) => setEditing((c) => (c ? { ...c, [k]: v } : c));

  return (
    <>
      <PageTitle>Products</PageTitle>
      <div className="mb-4 flex gap-3"><input className="input max-w-xs" placeholder="Search products" aria-label="Search products" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} /><button className="btn-primary" onClick={() => edit()}>New product</button></div>
      {list.loading ? <Spinner /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : (
        <>
          <Table head={['Product', 'SKU', 'Category', 'Price', 'Status', '']} empty={list.data!.items.length === 0}>
            {list.data!.items.map((p) => (
              <tr key={p.id}>
                <td className="td"><div className="flex items-center gap-3"><img src={p.imageUrl} alt="" className="h-10 w-10 rounded object-cover" /><span className="font-medium">{p.name}</span></div></td>
                <td className="td">{p.sku}</td><td className="td">{p.categoryName}</td><td className="td">{money(p.price, p.currency)}</td>
                <td className="td">{p.isActive ? 'Active' : 'Inactive'}{p.isFeatured && ' · Featured'}</td>
                <td className="td whitespace-nowrap"><button className="me-3 text-brand-700 hover:underline" onClick={() => edit(p)}>Edit</button>{p.isActive && <button className="text-red-600 hover:underline" onClick={() => deactivate(p)}>Deactivate</button>}</td>
              </tr>
            ))}
          </Table>
          <Pagination page={list.data!.page} totalPages={list.data!.totalPages} onChange={setPage} />
        </>
      )}
      {editing && (
        <Modal title={editing.id ? 'Edit product' : 'New product'} onClose={() => setEditing(null)}>
          <form onSubmit={save} className="grid gap-3 sm:grid-cols-2">
            <label className="text-sm">SKU<input className="input mt-1" required value={editing.sku} onChange={(e) => set('sku', e.target.value)} /></label>
            <label className="text-sm">Brand<input className="input mt-1" required value={editing.brand} onChange={(e) => set('brand', e.target.value)} /></label>
            <label className="text-sm sm:col-span-2">Name<input className="input mt-1" required value={editing.name} onChange={(e) => set('name', e.target.value)} /></label>
            <label className="text-sm sm:col-span-2">Description<textarea className="input mt-1 min-h-24" required minLength={10} value={editing.description} onChange={(e) => set('description', e.target.value)} /></label>
            <label className="text-sm">Price (USD)<input className="input mt-1" type="number" min="0.01" step="0.01" required value={editing.price} onChange={(e) => set('price', e.target.value)} /></label>
            <label className="text-sm">Category<select className="input mt-1" required value={editing.categoryId} onChange={(e) => set('categoryId', e.target.value)}>{cats.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
            <label className="text-sm sm:col-span-2">Image URL (relative path or https)<input className="input mt-1" required value={editing.imageUrl} onChange={(e) => set('imageUrl', e.target.value)} /></label>
            <label className="text-sm sm:col-span-2">Specifications (one "Name: value" per line)<textarea className="input mt-1 min-h-20" value={editing.specsText} onChange={(e) => set('specsText', e.target.value)} /></label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={editing.isActive} onChange={(e) => set('isActive', e.target.checked)} /> Active</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={editing.isFeatured} onChange={(e) => set('isFeatured', e.target.checked)} /> Featured on homepage</label>
            {err && <p role="alert" className="text-sm text-red-600 sm:col-span-2">{err}</p>}
            <div className="flex justify-end gap-3 sm:col-span-2"><button type="button" className="btn-secondary" onClick={() => setEditing(null)}>Cancel</button><button className="btn-primary">Save product</button></div>
          </form>
        </Modal>
      )}
    </>
  );
}
