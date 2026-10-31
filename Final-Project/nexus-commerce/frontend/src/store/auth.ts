import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import type { User } from '../lib/types';

interface AuthState {
  token: string | null;
  user: User | null;
  setSession: (token: string, user: User) => void;
  setUser: (user: User) => void;
  logout: () => void;
}

// The JWT is kept in sessionStorage (cleared when the tab closes) to limit exposure; role checks in the UI are
// convenience only - every API enforces authorization again on the server.
export const useAuth = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      user: null,
      setSession: (token, user) => set({ token, user }),
      setUser: (user) => set({ user }),
      logout: () => set({ token: null, user: null }),
    }),
    { name: 'nexus-auth', storage: createJSONStorage(() => sessionStorage) },
  ),
);

export const isAdmin = () => useAuth.getState().user?.role === 'Admin';
