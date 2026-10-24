import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useT } from '../lib/i18n';
import { useAuth } from '../store/auth';
import { useCart } from '../store/cart';
import { useUi } from '../store/ui';
import { useWishlist } from '../store/wishlist';
import { ChatWidget } from './ChatWidget';
import { Toaster } from './Toaster';

function Logo() {
  return (
    <Link to="/" className="flex items-center gap-2 text-lg font-extrabold tracking-tight" aria-label="Nexus Commerce home">
      <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-600 text-white" aria-hidden>N</span>
      <span>Nexus<span className="text-brand-600">Commerce</span></span>
    </Link>
  );
}

export function Layout() {
  const { t, lang } = useT();
  const { user, token, logout } = useAuth();
  const cart = useCart();
  const toggleLang = useUi((s) => s.toggleLang);
  const wishCount = useWishlist((s) => s.ids.length);
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }, [lang]);

  useEffect(() => {
    if (token) cart.refresh().catch(() => undefined);
    else cart.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    navigate(`/products${q.trim() ? `?q=${encodeURIComponent(q.trim())}` : ''}`);
    setOpen(false);
  };

  const link = ({ isActive }: { isActive: boolean }) => `rounded-md px-3 py-2 text-sm font-medium ${isActive ? 'text-brand-700' : 'text-slate-600 hover:text-slate-900'}`;
  const count = cart.cart?.itemCount ?? 0;

  return (
    <div className="flex min-h-screen flex-col">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-white focus:p-3">Skip to content</a>
      <header className="sticky top-0 z-40 border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="container-page flex h-16 items-center gap-4">
          <button className="btn-secondary px-3 md:hidden" aria-label={t('nav.menu')} aria-expanded={open} onClick={() => setOpen(!open)}>☰</button>
          <Logo />
          <nav className="ms-4 hidden items-center gap-1 md:flex" aria-label="Main">
            <NavLink to="/" end className={link}>{t('nav.home')}</NavLink>
            <NavLink to="/products" className={link}>{t('nav.shop')}</NavLink>
            {user?.role === 'Admin' && <NavLink to="/admin" className={link}>{t('nav.admin')}</NavLink>}
          </nav>
          <form onSubmit={submit} role="search" className="ms-auto hidden max-w-sm flex-1 md:block">
            <input className="input" type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('nav.search')} aria-label={t('nav.search')} maxLength={100} />
          </form>
          <div className="ms-auto flex items-center gap-1 md:ms-0">
            <button className="btn-secondary px-3 py-2" onClick={toggleLang} aria-label="Switch language">{t('lang.switch')}</button>
            <Link to="/wishlist" className="relative rounded-lg p-2 hover:bg-slate-100" aria-label={`${t('nav.wishlist')} (${wishCount})`}>♡{wishCount > 0 && <span className="absolute -end-0.5 -top-0.5 rounded-full bg-rose-500 px-1.5 text-[10px] font-bold text-white">{wishCount}</span>}</Link>
            <Link to="/cart" className="relative rounded-lg p-2 hover:bg-slate-100" aria-label={`${t('nav.cart')} (${count})`}>🛒{count > 0 && <span className="absolute -end-0.5 -top-0.5 rounded-full bg-brand-600 px-1.5 text-[10px] font-bold text-white">{count}</span>}</Link>
            {user ? (
              <div className="relative hidden md:block">
                <details className="group">
                  <summary className="btn-secondary cursor-pointer list-none px-3 py-2">{user.fullName.split(' ')[0]} ▾</summary>
                  <div className="absolute end-0 mt-2 w-48 rounded-xl border border-slate-200 bg-white p-1 shadow-lg">
                    <Link className="block rounded-lg px-3 py-2 text-sm hover:bg-slate-100" to="/profile">{t('nav.profile')}</Link>
                    <Link className="block rounded-lg px-3 py-2 text-sm hover:bg-slate-100" to="/orders">{t('nav.orders')}</Link>
                    <button className="block w-full rounded-lg px-3 py-2 text-start text-sm hover:bg-slate-100" onClick={() => { logout(); navigate('/'); }}>{t('nav.logout')}</button>
                  </div>
                </details>
              </div>
            ) : (
              <Link to="/login" className="btn-primary hidden px-3 py-2 md:inline-flex">{t('nav.login')}</Link>
            )}
          </div>
        </div>
        {open && (
          <div className="border-t border-slate-200 bg-white p-4 md:hidden">
            <form onSubmit={submit} role="search" className="mb-3"><input className="input" type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('nav.search')} aria-label={t('nav.search')} /></form>
            <nav className="flex flex-col gap-1" aria-label="Mobile" onClick={() => setOpen(false)}>
              <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/">{t('nav.home')}</Link>
              <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/products">{t('nav.shop')}</Link>
              {user ? (<>
                <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/orders">{t('nav.orders')}</Link>
                <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/profile">{t('nav.profile')}</Link>
                {user.role === 'Admin' && <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/admin">{t('nav.admin')}</Link>}
                <button className="rounded-lg px-3 py-2 text-start hover:bg-slate-100" onClick={() => { logout(); navigate('/'); }}>{t('nav.logout')}</button>
              </>) : (<>
                <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/login">{t('nav.login')}</Link>
                <Link className="rounded-lg px-3 py-2 hover:bg-slate-100" to="/register">{t('nav.register')}</Link>
              </>)}
            </nav>
          </div>
        )}
      </header>
      <main id="main" className="flex-1"><Outlet /></main>
      <footer className="mt-16 border-t border-slate-200 bg-white py-8 text-sm text-slate-500">
        <div className="container-page flex flex-col items-center justify-between gap-3 sm:flex-row">
          <Logo /><p>{t('footer.tagline')}</p>
        </div>
      </footer>
      <ChatWidget />
      <Toaster />
    </div>
  );
}
