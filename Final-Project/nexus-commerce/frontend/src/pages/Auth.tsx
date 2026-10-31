import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Field } from '../components/Ui';
import { api, ApiError } from '../lib/api';
import { useT } from '../lib/i18n';
import type { AuthResponse, User } from '../lib/types';
import { useAuth } from '../store/auth';
import { toast } from '../store/toast';

function AuthForm({ mode }: { mode: 'login' | 'register' }) {
  const { t } = useT();
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuth((s) => s.setSession);
  const [form, setForm] = useState({ email: '', password: '', fullName: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      const res = await api<AuthResponse>(mode === 'login' ? '/api/auth/login' : '/api/auth/register', {
        method: 'POST', body: mode === 'login' ? { email: form.email, password: form.password } : form,
      });
      setSession(res.accessToken, res.user);
      toast.success(`Welcome, ${res.user.fullName.split(' ')[0]}!`);
      navigate(res.user.role === 'Admin' && from === '/' ? '/admin' : from, { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.');
    } finally { setBusy(false); }
  };

  return (
    <div className="container-page flex justify-center py-12">
      <form onSubmit={submit} className="card w-full max-w-md space-y-4 p-6" noValidate={false}>
        <h1 className="text-2xl font-bold">{mode === 'login' ? t('auth.loginTitle') : t('auth.registerTitle')}</h1>
        {mode === 'register' && <Field label={t('field.fullName')} htmlFor="a-name"><input id="a-name" className="input" required minLength={2} maxLength={150} autoComplete="name" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} /></Field>}
        <Field label={t('field.email')} htmlFor="a-email"><input id="a-email" className="input" type="email" required autoComplete="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
        <Field label={t('field.password')} htmlFor="a-pw"><input id="a-pw" className="input" type="password" required minLength={mode === 'register' ? 8 : 1} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></Field>
        {mode === 'register' && <p className="text-xs text-slate-500">At least 8 characters, including a letter and a digit.</p>}
        {error && <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
        <button className="btn-primary w-full" disabled={busy}>{busy ? t('common.loading') : mode === 'login' ? t('nav.login') : t('nav.register')}</button>
        <p className="text-center text-sm text-slate-600">
          {mode === 'login' ? t('auth.noAccount') : t('auth.haveAccount')}{' '}
          <Link className="font-semibold text-brand-700 hover:underline" to={mode === 'login' ? '/register' : '/login'} state={location.state}>{mode === 'login' ? t('nav.register') : t('nav.login')}</Link>
        </p>
      </form>
    </div>
  );
}

export const LoginPage = () => <AuthForm mode="login" />;
export const RegisterPage = () => <AuthForm mode="register" />;

export function ProfilePage() {
  const { t } = useT();
  const { user, setUser } = useAuth();
  const [form, setForm] = useState({ fullName: user?.fullName ?? '', phone: user?.phone ?? '', addressLine: user?.addressLine ?? '', city: user?.city ?? '', postalCode: user?.postalCode ?? '', country: user?.country ?? '' });
  const [pw, setPw] = useState({ currentPassword: '', newPassword: '' });
  const [busy, setBusy] = useState(false);

  const save = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true);
    try { setUser(await api<User>('/api/profile', { method: 'PUT', body: form })); toast.success(t('profile.saved')); }
    catch (err) { toast.error(err instanceof ApiError ? err.message : 'Could not save.'); }
    finally { setBusy(false); }
  };
  const changePw = async (e: React.FormEvent) => {
    e.preventDefault();
    try { await api('/api/profile/change-password', { method: 'POST', body: pw }); setPw({ currentPassword: '', newPassword: '' }); toast.success('Password changed.'); }
    catch (err) { toast.error(err instanceof ApiError ? err.message : 'Could not change the password.'); }
  };
  const inp = (k: keyof typeof form, label: string) => <Field label={label} htmlFor={`p-${k}`}><input id={`p-${k}`} className="input" value={form[k]} onChange={(e) => setForm({ ...form, [k]: e.target.value })} /></Field>;

  return (
    <div className="container-page grid gap-8 py-8 lg:grid-cols-2">
      <form onSubmit={save} className="card space-y-4 p-6">
        <h1 className="text-2xl font-bold">{t('profile.title')}</h1>
        <p className="text-sm text-slate-500">{user?.email}</p>
        {inp('fullName', t('field.fullName'))}{inp('phone', t('field.phone'))}{inp('addressLine', t('field.address'))}
        <div className="grid grid-cols-2 gap-4">{inp('city', t('field.city'))}{inp('postalCode', t('field.postal'))}</div>
        {inp('country', t('field.country'))}
        <button className="btn-primary" disabled={busy}>{t('profile.save')}</button>
      </form>
      <form onSubmit={changePw} className="card h-fit space-y-4 p-6">
        <h2 className="text-xl font-bold">Change password</h2>
        <Field label="Current password" htmlFor="pw-c"><input id="pw-c" type="password" required className="input" autoComplete="current-password" value={pw.currentPassword} onChange={(e) => setPw({ ...pw, currentPassword: e.target.value })} /></Field>
        <Field label="New password" htmlFor="pw-n"><input id="pw-n" type="password" required minLength={8} className="input" autoComplete="new-password" value={pw.newPassword} onChange={(e) => setPw({ ...pw, newPassword: e.target.value })} /></Field>
        <button className="btn-secondary">Change password</button>
      </form>
    </div>
  );
}
