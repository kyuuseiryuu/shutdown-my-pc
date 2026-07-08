import i18n from "i18next";
import { initReactI18next, useTranslation } from "react-i18next";

// ─── English ────────────────────────────────────────────────────────

const en = {
  translation: {
    // Header
    "app.title": "Shutdown My PC",
    "app.subtitle": "Power management from your browser",
    // Actions
    "action.shutdown": "Shut Down",
    "action.restart": "Restart",
    "action.hibernate": "Hibernate",
    "action.sleep": "Sleep",
    "action.logout": "Log Off",
    // Section titles
    "action.section": "Action",
    "options.section": "Options",
    "options.timer": "Timer",
    "options.force": "Force close apps",
    "options.yes": "Yes",
    "options.no": "No",
    // Execute buttons
    "execute.label": "{{action}} in {{timeout}}s",
    "execute.label_simple": "{{action}}",
    "execute.cancel": "Cancel Scheduled Operation",
    // Result
    "result.success": "Success",
    "result.failed": "Failed",
    // Footer
    "footer.text": "Windows only &middot; Uses shutdown.exe",
    // Messages
    "msg.scheduled": "{{action}} scheduled in {{seconds}} seconds",
    "msg.cancelled": "Scheduled operation has been cancelled",
    "msg.cancel.fail": "No pending operation to cancel",
    "msg.request.fail": "Request failed",
    // Theme
    "theme.light": "Light",
    "theme.dark": "Dark",
    // Lang
    "lang.en": "English",
    "lang.zh": "中文",
  },
};

// ─── 中文 ──────────────────────────────────────────────────────────

const zh = {
  translation: {
    "app.title": "Shutdown My PC",
    "app.subtitle": "从浏览器管理电源",
    "action.shutdown": "关机",
    "action.restart": "重启",
    "action.hibernate": "休眠",
    "action.sleep": "睡眠",
    "action.logout": "注销",
    "action.section": "操作",
    "options.section": "选项",
    "options.timer": "定时器",
    "options.force": "强制关闭应用",
    "options.yes": "是",
    "options.no": "否",
    "execute.label": "{{action}}（{{timeout}} 秒后）",
    "execute.label_simple": "{{action}}",
    "execute.cancel": "取消计划操作",
    "result.success": "成功",
    "result.failed": "失败",
    "footer.text": "仅 Windows &middot; 使用 shutdown.exe",
    "msg.scheduled": "已计划 {{action}}，{{seconds}} 秒后执行",
    "msg.cancelled": "已取消计划操作",
    "msg.cancel.fail": "没有待取消的操作",
    "msg.request.fail": "请求失败",
    "theme.light": "浅色",
    "theme.dark": "深色",
    "lang.en": "English",
    "lang.zh": "中文",
  },
};

// ─── Init ───────────────────────────────────────────────────────────

i18n.use(initReactI18next).init({
  resources: { en, zh },
  lng: "zh",           // default language
  fallbackLng: "en",
  interpolation: {
    escapeValue: false, // React already escapes
  },
});

export type Lang = "en" | "zh";

export function useI18n() {
  const { t, i18n } = useTranslation();

  const lang = i18n.language as Lang;
  const setLang = (l: Lang) => i18n.changeLanguage(l);

  return { t, lang, setLang };
}

export default i18n;
