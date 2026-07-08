import { Button, Space } from "antd";
import { SunOutlined, MoonOutlined, GlobalOutlined } from "@ant-design/icons";

import { PowerPanel } from "./components/PowerPanel";
import { ThemeProvider, useTheme } from "./theme";
import { useI18n, type Lang } from "./i18n";

// ─── Settings Bar ────────────────────────────────────────────────────

function SettingsBar() {
  const { mode, toggle } = useTheme();
  const { lang, setLang, t } = useI18n();

  const nextLang: Lang = lang === "en" ? "zh" : "en";

  return (
    <div className="settings-bar">
      <Space>
        <Button
          size="small"
          type="text"
          icon={mode === "dark" ? <SunOutlined /> : <MoonOutlined />}
          onClick={toggle}
        >
          {mode === "dark" ? t("theme.light") : t("theme.dark")}
        </Button>
        <Button
          size="small"
          type="text"
          icon={<GlobalOutlined />}
          onClick={() => setLang(nextLang)}
        >
          {t(`lang.${nextLang}`)}
        </Button>
      </Space>
    </div>
  );
}

// ─── App Shell ───────────────────────────────────────────────────────

export default function App() {
  return (
    <ThemeProvider>
      <div className="app">
        <SettingsBar />
        <PowerPanel />
      </div>
    </ThemeProvider>
  );
}

