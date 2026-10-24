import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../lib/api';
import { money } from '../lib/format';
import { useT } from '../lib/i18n';
import type { AssistantStatus, ChatResponse, ProductCardData } from '../lib/types';
import { useAuth } from '../store/auth';
import { useUi } from '../store/ui';
import { StockBadge } from './Ui';

interface Msg { role: 'user' | 'assistant'; content: string; products?: ProductCardData[]; mode?: string; notice?: string | null }
interface ConversationSummary { id: string; title: string; updatedAtUtc: string }
interface StoredMessage { role: 'user' | 'assistant'; content: string; mode?: string; products: ProductCardData[] }

export function ChatWidget() {
  const { t, lang } = useT();
  const open = useUi((s) => s.chatOpen);
  const setOpen = useUi((s) => s.setChatOpen);
  const token = useAuth((s) => s.token);
  const [status, setStatus] = useState<AssistantStatus | null>(null);
  const [messages, setMessages] = useState<Msg[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const endRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    inputRef.current?.focus();
    api<AssistantStatus>('/api/assistant/status').then(setStatus).catch(() => setStatus(null));
    if (token) api<ConversationSummary[]>('/api/assistant/conversations').then(setConversations).catch(() => setConversations([]));
  }, [open, token]);

  useEffect(() => { endRef.current?.scrollIntoView({ block: 'end' }); }, [messages, busy]);
  useEffect(() => { if (!token) { setConversationId(null); setConversations([]); } }, [token]);

  const send = async (e: React.FormEvent) => {
    e.preventDefault();
    const text = input.trim();
    if (!text || busy) return;
    setInput('');
    const history = messages.slice(-10).map((m) => ({ role: m.role, content: m.content.slice(0, 1000) }));
    setMessages((m) => [...m, { role: 'user', content: text }]);
    setBusy(true);
    try {
      const res = await api<ChatResponse>('/api/assistant/chat', {
        method: 'POST',
        body: { message: text, conversationId: token ? conversationId : null, locale: lang, history: token ? undefined : history },
      });
      if (res.conversationId) setConversationId(res.conversationId);
      setMessages((m) => [...m, { role: 'assistant', content: res.reply, products: res.products, mode: res.mode, notice: res.notice }]);
    } catch (err) {
      const msg = err instanceof ApiError && err.status === 429 ? 'You are sending messages too quickly. Please wait a moment.'
        : err instanceof ApiError ? err.message : 'The assistant is unavailable right now.';
      setMessages((m) => [...m, { role: 'assistant', content: msg, mode: 'error' }]);
    } finally {
      setBusy(false);
    }
  };

  const loadConversation = async (id: string) => {
    if (!id) { setMessages([]); setConversationId(null); return; }
    try {
      const msgs = await api<StoredMessage[]>(`/api/assistant/conversations/${id}`);
      setMessages(msgs.map((m) => ({ role: m.role, content: m.content, products: m.products, mode: m.mode })));
      setConversationId(id);
    } catch { /* keep current view */ }
  };

  const newChat = () => { setMessages([]); setConversationId(null); };

  return (
    <>
      {!open && (
        <button onClick={() => setOpen(true)} aria-label={t('chat.open')}
          className="fixed bottom-5 end-5 z-50 flex h-14 items-center gap-2 rounded-full bg-brand-600 px-5 font-semibold text-white shadow-xl hover:bg-brand-700">
          <span aria-hidden>💬</span><span className="hidden sm:inline">{t('chat.title')}</span>
        </button>
      )}
      {open && (
        <section role="dialog" aria-label={t('chat.title')}
          className="fixed inset-x-0 bottom-0 z-50 flex h-[85vh] flex-col overflow-hidden rounded-t-2xl border border-slate-200 bg-white shadow-2xl sm:inset-x-auto sm:bottom-5 sm:end-5 sm:h-[640px] sm:w-[400px] sm:rounded-2xl">
          <header className="flex items-center justify-between gap-2 bg-brand-700 px-4 py-3 text-white">
            <div className="min-w-0">
              <h2 className="font-semibold">{t('chat.title')}</h2>
              <p className="truncate text-xs text-indigo-100">{status ? (status.mode === 'llm' ? `${t('chat.llm')} (${status.provider})` : t('chat.fallback')) : '…'}</p>
            </div>
            <div className="flex items-center gap-2">
              <button className="rounded-md bg-white/15 px-2 py-1 text-xs hover:bg-white/25" onClick={newChat}>{t('chat.new')}</button>
              <button aria-label={t('chat.close')} className="rounded-md px-2 py-1 hover:bg-white/20" onClick={() => setOpen(false)}>✕</button>
            </div>
          </header>
          {token && conversations.length > 0 && (
            <select aria-label="Previous conversations" className="m-2 rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={conversationId ?? ''} onChange={(e) => loadConversation(e.target.value)}>
              <option value="">{t('chat.new')}</option>
              {conversations.map((c) => <option key={c.id} value={c.id}>{c.title}</option>)}
            </select>
          )}
          <div className="flex-1 space-y-3 overflow-y-auto bg-slate-50 p-3" aria-live="polite">
            {messages.length === 0 && <p className="rounded-xl bg-white p-3 text-sm text-slate-600 shadow-sm">{t('chat.welcome')}</p>}
            {messages.map((m, i) => (
              <div key={i} className={`flex ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                <div className={`max-w-[92%] space-y-2 rounded-2xl px-3.5 py-2.5 text-sm shadow-sm ${m.role === 'user' ? 'bg-brand-600 text-white' : m.mode === 'error' ? 'bg-red-50 text-red-800' : 'bg-white text-slate-800'}`}>
                  {m.notice && <p className="rounded-md bg-amber-50 px-2 py-1 text-xs text-amber-800">{m.notice}</p>}
                  <p className="whitespace-pre-line break-words">{m.content}</p>
                  {m.products && m.products.length > 0 && (
                    <ul className="space-y-2">
                      {m.products.map((p) => (
                        <li key={p.id}>
                          <Link to={`/products/${p.id}`} onClick={() => window.innerWidth < 640 && setOpen(false)} className="flex items-center gap-3 rounded-xl border border-slate-200 p-2 text-slate-800 hover:border-brand-500">
                            <img src={p.imageUrl} alt="" className="h-14 w-14 rounded-lg object-cover" />
                            <span className="min-w-0 flex-1">
                              <span className="block truncate font-medium">{p.name}</span>
                              <span className="flex items-center gap-2"><span className="font-semibold">{money(p.price, p.currency, lang)}</span><StockBadge status={p.availability} /></span>
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            ))}
            {busy && <div className="flex justify-start"><div className="rounded-2xl bg-white px-3.5 py-2.5 text-sm text-slate-500 shadow-sm">{t('chat.thinking')}</div></div>}
            <div ref={endRef} />
          </div>
          <form onSubmit={send} className="flex gap-2 border-t border-slate-200 bg-white p-3">
            <input ref={inputRef} className="input" value={input} maxLength={1000} onChange={(e) => setInput(e.target.value)} placeholder={t('chat.placeholder')} aria-label={t('chat.placeholder')} />
            <button className="btn-primary" disabled={busy || !input.trim()}>{t('chat.send')}</button>
          </form>
        </section>
      )}
    </>
  );
}
