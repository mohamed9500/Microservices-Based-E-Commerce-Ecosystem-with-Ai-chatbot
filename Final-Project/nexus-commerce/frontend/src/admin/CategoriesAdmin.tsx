import { useState } from 'react';
import { ErrorState, PageTitle, Spinner } from '../components/Ui';
import { api, ApiError } from '../lib/api';
import { useAsync } from '../lib/hooks';
import type { Category } from '../lib/types';
import { toast } from '../store/toast';
import { Modal, Table } from './shared';

type Form = { id?: string; name: string; description: string; imageUrl: string; sortOrder: number };

export default function CategoriesAdmin() {
  const list = useAsync((signal) => api<Category[]>('/api/categories', { signal }), []);
  const [form, setForm] = useState<Form | null>(null);
  const [err, setErr] = useState('');

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    const { id, ...body } = form;
    try {
      await api(id ? `/api/categories/${id}` : '/api/categories', { method: id ? 'PUT' : 'POST', body: { ...body, sortOrder: Number(body.sortOrder), imageUrl: body.imageUrl || null } });
      toast.success('Category saved.'); setForm(null); list.reload();
    } catch (ex) { setErr(ex instanceof ApiError ? ex.message : 'Save failed.'); }
  };
  const remove = async (c: Category) => {
    if (!confirm(`Delete category "${c.name}"?`)) return;
    try { await api(`/api/categories/${c.id}`, { method: 'DELETE' }); toast.success('Category deleted.'); list.reload(); }
    catch (ex) { toast.error(ex instanceof ApiError ? ex.message : 'Failed.'); }
  };

  return (
    <>
      <PageTitle>Categories</PageTitle>
      <button className="btn-primary mb-4" onClick={() => { setErr(''); setForm({ name: '', description: '', imageUrl: '', sortOrder: (list.data?.length ?? 0) + 1 }); }}>New category</button>
      {list.loading ? <Spinner /> : list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : (
        <Table head={['Name', 'Slug', 'Order', '']} empty={list.data!.length === 0}>
          {list.data!.map((c) => (
            <tr key={c.id}><td className="td font-medium">{c.name}</td><td className="td">{c.slug}</td><td className="td">{c.sortOrder}</td>
              <td className="td whitespace-nowrap"><button className="me-3 text-brand-700 hover:underline" onClick={() => { setErr(''); setForm({ id: c.id, name: c.name, description: c.description ?? '', imageUrl: c.imageUrl ?? '', sortOrder: c.sortOrder }); }}>Edit</button><button className="text-red-600 hover:underline" onClick={() => remove(c)}>Delete</button></td></tr>
          ))}
        </Table>
      )}
      {form && (
        <Modal title={form.id ? 'Edit category' : 'New category'} onClose={() => setForm(null)}>
          <form onSubmit={save} className="space-y-3">
            <label className="block text-sm">Name<input className="input mt-1" required minLength={2} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
            <label className="block text-sm">Description<input className="input mt-1" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></label>
            <label className="block text-sm">Image URL<input className="input mt-1" value={form.imageUrl} onChange={(e) => setForm({ ...form, imageUrl: e.target.value })} /></label>
            <label className="block text-sm">Sort order<input className="input mt-1" type="number" min={0} value={form.sortOrder} onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) })} /></label>
            {err && <p role="alert" className="text-sm text-red-600">{err}</p>}
            <div className="flex justify-end gap-3"><button type="button" className="btn-secondary" onClick={() => setForm(null)}>Cancel</button><button className="btn-primary">Save</button></div>
          </form>
        </Modal>
      )}
    </>
  );
}
