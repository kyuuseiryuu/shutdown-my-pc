import { Flex } from "antd";
import type { ReactNode } from "react";

import {
  PoweroffOutlined,
  ReloadOutlined,
  CloudOutlined,
  MoonOutlined,
  LogoutOutlined,
} from "@ant-design/icons";

import { useI18n } from "../i18n";

export type PowerAction = "shutdown" | "restart" | "hibernate" | "sleep" | "logout";

// i18n key for each action label
export const ACTION_I18N_KEY: Record<PowerAction, string> = {
  shutdown:  "action.shutdown",
  restart:   "action.restart",
  hibernate: "action.hibernate",
  sleep:     "action.sleep",
  logout:    "action.logout",
};

// Per-action metadata: whether timer and force are supported
export const ACTION_META: Record<PowerAction, { noTimer?: boolean; noForce?: boolean; noTimeout?: boolean }> = {
  shutdown:  {},
  restart:   {},
  hibernate: { noTimer: true, noForce: true },
  sleep:     { noTimer: true, noForce: true },
  logout:    { noTimeout: true },
};

// Display config
export const ACTION_CONFIG: Record<PowerAction, { icon: ReactNode; color: string }> = {
  shutdown:  { icon: <PoweroffOutlined />,        color: "#ff4d4f" },
  restart:   { icon: <ReloadOutlined />,          color: "#fa8c16" },
  hibernate: { icon: <CloudOutlined />,   color: "#722ed1" },
  sleep:     { icon: <MoonOutlined />,    color: "#13c2c2" },
  logout:    { icon: <LogoutOutlined />,         color: "#eb2f96" },
};

interface ActionSelectorProps {
  action: PowerAction;
  onChange: (action: PowerAction) => void;
  sectionTitle?: string;
}

export function ActionSelector({ action, onChange, sectionTitle = "Action" }: ActionSelectorProps) {
  const { t } = useI18n();

  return (
    <div className="section-card">
      <div className="section-title">{sectionTitle}</div>
      <div className="action-grid">
        {(Object.entries(ACTION_CONFIG) as [PowerAction, typeof ACTION_CONFIG[PowerAction]][]).map(([key, v]) => (
          <Flex
            key={key}
            vertical
            gap={4}
            className={`action-btn ${action === key ? "active" : ""}`}
            style={{
              "--accent": v.color,
              borderColor: action === key ? v.color : "transparent",
            } as React.CSSProperties}
            onClick={() => onChange(key)}
          >
            <div className="action-icon" style={{ color: v.color }}>
              {v.icon}
            </div>
            <span className="action-label">{t(ACTION_I18N_KEY[key])}</span>
          </Flex>
        ))}
      </div>
    </div>
  );
}

