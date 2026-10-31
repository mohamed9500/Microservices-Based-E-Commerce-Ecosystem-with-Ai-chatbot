import { create } from 'zustand';

export interface ToastItem { id: number; kind: 'success' | 'error' | 'info'; message: string }
interface ToastState { toasts: ToastItem[]; push: (kind: ToastItem['kind'], message: string) => void; dismiss: (id: number) => void }
let nextId = 1;

export const useToast = create<ToastState>((set, get) => ({
  toasts: [],
  push: (kind, message) => {
    const id = nextId++;
    set({ toasts: [...get().toasts, { id, kind, message }].slice(-4) });
    setTimeout(() => get().dismiss(id), 4500);
  },
  dismiss: (id) => set({ toasts: get().toasts.filter((t) => t.id !== id) }),
}));

export const toast = {
  success: (m: string) => useToast.getState().push('success', m),
  error: (m: string) => useToast.getState().push('error', m),
  info: (m: string) => useToast.getState().push('info', m),
};
