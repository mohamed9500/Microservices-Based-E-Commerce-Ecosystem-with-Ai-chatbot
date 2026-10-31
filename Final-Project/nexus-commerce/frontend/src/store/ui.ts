import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Lang } from "../lib/i18n";

interface UiState {
  lang: Lang;
  theme: "light" | "dark";
  chatOpen: boolean;
  setLang: (l: Lang) => void;
  toggleLang: () => void;
  toggleTheme: () => void;
  setChatOpen: (o: boolean) => void;
}

export const useUi = create<UiState>()(
  persist(
    (set, get) => ({
      lang: "en",
      theme: "light",
      chatOpen: false,
      setLang: (lang) => set({ lang }),
      toggleLang: () => set({ lang: get().lang === "en" ? "ar" : "en" }),
      toggleTheme: () =>
        set({ theme: get().theme === "light" ? "dark" : "light" }),
      setChatOpen: (chatOpen) => set({ chatOpen }),
    }),
    { name: "nexus-ui", partialize: (s) => ({ lang: s.lang, theme: s.theme }) },
  ),
);
