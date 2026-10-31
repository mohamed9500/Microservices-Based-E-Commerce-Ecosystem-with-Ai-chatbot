import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface WishlistState { ids: string[]; toggle: (id: string) => boolean; has: (id: string) => boolean; clear: () => void }

// Wishlist is a device-local convenience (localStorage); it is intentionally not sent to the server.
export const useWishlist = create<WishlistState>()(
  persist(
    (set, get) => ({
      ids: [],
      toggle: (id) => {
        const exists = get().ids.includes(id);
        set({ ids: exists ? get().ids.filter((x) => x !== id) : [...get().ids, id] });
        return !exists;
      },
      has: (id) => get().ids.includes(id),
      clear: () => set({ ids: [] }),
    }),
    { name: 'nexus-wishlist' },
  ),
);
