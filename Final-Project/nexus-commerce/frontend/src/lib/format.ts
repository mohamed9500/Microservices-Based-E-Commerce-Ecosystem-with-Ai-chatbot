export function money(value: number, currency = 'USD', lang = 'en'): string {
  try {
    return new Intl.NumberFormat(lang === 'ar' ? 'ar-EG' : 'en-US', { style: 'currency', currency }).format(value);
  } catch {
    return `${value.toFixed(2)} ${currency}`;
  }
}

export function shortDate(iso: string, lang = 'en'): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return new Intl.DateTimeFormat(lang === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(d);
}

export const shortId = (id: string) => id.slice(0, 8).toUpperCase();

export function clamp(n: number, min: number, max: number) { return Math.min(max, Math.max(min, n)); }
