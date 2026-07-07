import { Button, ConfigProvider, Flex, InputNumber, Radio, Tag, Typography, message, theme } from "antd";
import { useCallback, useState, type ReactNode } from "react";

import {
  PoweroffOutlined,
  ReloadOutlined,
  StopOutlined,
  EyeInvisibleOutlined,
  LogoutOutlined,
  WarningOutlined,
  UpCircleOutlined,
} from "@ant-design/icons";

import "./index.css";

const { Title, Text } = Typography;

const darkTheme = {
  components: {
    Tag: {
      defaultBg: "transparent",
      defaultColor: "#e4e4ec",
    },
    Message: {
      contentBg: "#1a1a24",
    },
  },
};

type PowerAction = "shutdown" | "restart" | "hibernate" | "sleep" | "logout";

const ACTION_CONFIG: Record<PowerAction, { label: string; icon: ReactNode; color: string }> = {
  shutdown:  { label: "Shut Down",  icon: <PoweroffOutlined />,        color: "#ff4d4f" },
  restart:   { label: "Restart",    icon: <ReloadOutlined />,          color: "#fa8c16" },
  hibernate: { label: "Hibernate",  icon: <EyeInvisibleOutlined />,   color: "#722ed1" },
  sleep:     { label: "Sleep",      icon: <StopOutlined />,           color: "#13c2c2" },
  logout:    { label: "Log Off",    icon: <LogoutOutlined />,         color: "#eb2f96" },
};

export function App() {
  const [action, setAction] = useState<PowerAction>("shutdown");
  const [timeout, setTimeout_] = useState(30);
  const [force, setForce] = useState(true);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  const handlePower = useCallback(async () => {
    setLoading(true);
    setResult(null);
    try {
      const params = new URLSearchParams({ action, timeout: String(timeout), force: String(force) });
      const res = await fetch(`/api/power?${params}`);
      const data = await res.json();
      setResult(data);
      if (data.ok) {
        messageApi.success(data.message);
      } else {
        messageApi.error(data.message);
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Request failed";
      setResult({ ok: false, message: msg });
      messageApi.error(msg);
    } finally {
      setLoading(false);
    }
  }, [action, timeout, force, messageApi]);

  const handleCancel = useCallback(async () => {
    setLoading(true);
    setResult(null);
    try {
      const res = await fetch("/api/cancel");
      const data = await res.json();
      setResult(data);
      if (data.ok) {
        messageApi.success(data.message);
      } else {
        messageApi.warning(data.message);
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Request failed";
      setResult({ ok: false, message: msg });
      messageApi.error(msg);
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  const cfg = ACTION_CONFIG[action];
  const isLogout = action === "logout";

  return (
    <ConfigProvider theme={{
      ...darkTheme,
      algorithm: theme.darkAlgorithm,
    }}>
      <div className="app">
        {contextHolder}
        <div className="container">
          {/* Header */}
          <div className="header">
            <span className="header-icon">⚡</span>
            <Title level={2} className="header-title">
              Shutdown My PC
            </Title>
            <Text type="secondary" className="header-subtitle">
              Power management from your browser
            </Text>
          </div>

          {/* Action selector */}
          <div className="section-card">
            <div className="section-title">Action</div>
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
                  onClick={() => setAction(key)}
                >
                  <div className="action-icon" style={{ color: v.color }}>
                    {v.icon}
                  </div>
                  <span className="action-label">{v.label}</span>
                </Flex>
              ))}
            </div>
          </div>

          {/* Options */}
          <div className="section-card">
            <div className="section-title">Options</div>
            <div className="options-grid">
              <div className="option-row">
                <Text strong>Timer</Text>
                <InputNumber
                  min={0}
                  max={600}
                  value={timeout}
                  onChange={(v) => setTimeout_(v ?? 30)}
                  disabled={isLogout}
                  style={{ width: 160 }}
                  suffix="seconds"
                />
              </div>
              <div className="option-row">
                <Text strong>Force close apps</Text>
                <Radio.Group value={force} onChange={(e) => setForce(e.target.value)} size="small">
                  <Radio.Button value={true}>Yes</Radio.Button>
                  <Radio.Button value={false}>No</Radio.Button>
                </Radio.Group>
              </div>
            </div>
          </div>

          {/* Execute */}
          <Flex vertical gap={8}>
            <Button
              block
              size="large"
              color="default"
              style={{ background: cfg.color, borderColor: cfg.color }}
              disabled={loading}
              onClick={handlePower}
            >
              {loading ? <span className="spinner" /> : cfg.icon}
              <span>
                {cfg.label}{!isLogout ? " in " + timeout + "s" : ""}
              </span>
            </Button>

            <Button
              block
              size="large"
              disabled={loading}
              onClick={handleCancel}
            >
              <WarningOutlined /> Cancel Scheduled Operation
            </Button>
          </Flex>

          {/* Result */}
          {result && (
            <div className="section-card result-card">
              <Tag color={result.ok ? "success" : "error"}>
                {result.ok ? "Success" : "Failed"}
              </Tag>
              <Text>{result.message}</Text>
            </div>
          )}

          <div className="footer">
            <Text type="secondary">Windows only &middot; Uses shutdown.exe</Text>
          </div>
        </div>
      </div>
    </ConfigProvider>
  );
}

export default App;
