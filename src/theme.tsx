import { ConfigProvider, theme as antTheme } from "antd";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

export type ThemeMode = "dark" | "light";

const STORAGE_KEY = "shutdown-pc-theme";

// Map of theme → body background color
const BODY_BG: Record<ThemeMode, string> = {
  dark: "#0f0f13",
  light: "#f5f5f5",
};

function loadTheme(): ThemeMode {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === "dark" || saved === "light") return saved;
  } catch { /* localStorage not available */ }
  return "dark";
}

interface ThemeContextValue {
  mode: ThemeMode;
  toggle: () => void;
  setMode: (mode: ThemeMode) => void;
}

const ThemeCtx = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>(loadTheme);

  useEffect(() => {
    // Persist
    try { localStorage.setItem(STORAGE_KEY, mode); } catch { /* noop */ }

    // Sync CSS variables via data attribute
    document.documentElement.setAttribute("data-theme", mode);
    // Force body background (Ant Design's darkAlgorithm overrides body bg)
    document.body.style.backgroundColor = BODY_BG[mode];
  }, [mode]);

  const toggle = () => setMode((m) => (m === "dark" ? "light" : "dark"));

  return (
    <ThemeCtx.Provider value={{ mode, toggle, setMode }}>
      <ConfigProvider
        theme={{
          algorithm: mode === "dark" ? antTheme.darkAlgorithm : antTheme.defaultAlgorithm,
          components: {
            Tag: {
              defaultBg: "transparent",
              defaultColor: mode === "dark" ? "#e4e4ec" : "#595959",
            },
            Message: {
              contentBg: mode === "dark" ? "#1a1a24" : "#ffffff",
            },
          },
          token: {
            colorBgLayout: BODY_BG[mode],
          },
        }}
      >
        {children}
      </ConfigProvider>
    </ThemeCtx.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeCtx);
  if (!ctx) throw new Error("useTheme must be used within a ThemeProvider");
  return ctx;
}
