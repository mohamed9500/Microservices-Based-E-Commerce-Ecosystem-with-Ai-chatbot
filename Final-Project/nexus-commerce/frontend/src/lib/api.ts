import { useAuth } from '../store/auth';

const BASE: string = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '';

export class ApiError extends Error {
  status: number;
  errors?: Record<string, string[]>;
  correlationId?: string;
  constructor(status: number, message: string, errors?: Record<string, string[]>, correlationId?: string) {
    super(message);
    this.status = status;
    this.errors = errors;
    this.correlationId = correlationId;
  }
}

interface Options { method?: string; body?: unknown; headers?: Record<string, string>; signal?: AbortSignal }

/** Typed fetch wrapper: attaches the JWT, parses problem+json errors, signs the user out on expired tokens. */
export async function api<T>(path: string, opts: Options = {}): Promise<T> {
  const token = useAuth.getState().token;
  const headers: Record<string, string> = { Accept: 'application/json', ...opts.headers };
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;

  let res: Response;
  try {
    res = await fetch(`${BASE}${path}`, {
      method: opts.method ?? 'GET',
      headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      signal: opts.signal,
    });
  } catch (e) {
    if ((e as Error).name === 'AbortError') throw e;
    throw new ApiError(0, 'Cannot reach the server. Check your connection and try again.');
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  let data: unknown = undefined;
  if (text) { try { data = JSON.parse(text); } catch { data = undefined; } }

  if (!res.ok) {
    const p = (data ?? {}) as { detail?: string; title?: string; errors?: Record<string, string[]>; correlationId?: string };
    if (res.status === 401 && token) useAuth.getState().logout();
    const firstFieldError = p.errors ? Object.values(p.errors)[0]?.[0] : undefined;
    throw new ApiError(res.status, firstFieldError ?? p.detail ?? p.title ?? `Request failed (${res.status})`, p.errors, p.correlationId);
  }
  return data as T;
}

export const qs = (params: Record<string, string | number | boolean | undefined | null>): string => {
  const s = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') s.set(k, String(v));
  const out = s.toString();
  return out ? `?${out}` : '';
};
