import { create } from 'zustand';
import { api } from '../lib/api';
import type { Cart } from '../lib/types';

interface CartState {
  cart: Cart | null;
  loading: boolean;
  refresh: () => Promise<void>;
  add: (productId: string, quantity?: number) => Promise<void>;
  setQuantity: (productId: string, quantity: number) => Promise<void>;
  remove: (productId: string) => Promise<void>;
  reset: () => void;
}

export const useCart = create<CartState>((set) => ({
  cart: null,
  loading: false,
  refresh: async () => {
    set({ loading: true });
    try { set({ cart: await api<Cart>('/api/cart') }); } finally { set({ loading: false }); }
  },
  add: async (productId, quantity = 1) => set({ cart: await api<Cart>('/api/cart/items', { method: 'POST', body: { productId, quantity } }) }),
  setQuantity: async (productId, quantity) => set({ cart: await api<Cart>(`/api/cart/items/${productId}`, { method: 'PUT', body: { quantity } }) }),
  remove: async (productId) => set({ cart: await api<Cart>(`/api/cart/items/${productId}`, { method: 'DELETE' }) }),
  reset: () => set({ cart: null }),
}));
