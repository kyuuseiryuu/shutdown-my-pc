import {
  Button,
  Flex,
  InputNumber,
  Radio,
  Tag,
  Typography,
  message,
} from "antd";
import { useCallback, useState } from "react";
import { WarningOutlined } from "@ant-design/icons";

import { ActionSelector, type PowerAction, ACTION_CONFIG, ACTION_I18N_KEY, ACTION_META } from "./ActionSelector";
import { useI18n } from "../i18n";
import { useTheme } from "../theme";

const { Title, Text } = Typography;

export function PowerPanel() {
  const { t } = useI18n();
  useTheme(); // keep for dark/light awareness on CSS variables

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
        const actionLabel = t(ACTION_I18N_KEY[action]);
        messageApi.success(t("msg.scheduled", { action: actionLabel, seconds: timeout }));
      } else {
        messageApi.error(data.message);
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : t("msg.request.fail");
      setResult({ ok: false, message: msg });
      messageApi.error(msg);
    } finally {
      setLoading(false);
    }
  }, [action, timeout, force, messageApi, t]);

  const handleCancel = useCallback(async () => {
    setLoading(true);
    setResult(null);
    try {
      const res = await fetch("/api/cancel");
      const data = await res.json();
      setResult(data);
      if (data.ok) {
        messageApi.success(t("msg.cancelled"));
      } else {
        messageApi.warning(t("msg.cancel.fail"));
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : t("msg.request.fail");
      setResult({ ok: false, message: msg });
      messageApi.error(msg);
    } finally {
      setLoading(false);
    }
  }, [messageApi, t]);

  const cfg = ACTION_CONFIG[action];
  const meta = ACTION_META[action];
  const disableTimer = meta.noTimeout ?? meta.noTimer ?? false;
  const disableForce = meta.noForce ?? false;

  return (
    <div className="container">
      {contextHolder}

      {/* Header */}
      <div className="header">
        <span className="header-icon">⚡</span>
        <Title level={2} className="header-title">
          {t("app.title")}
        </Title>
        <Text type="secondary" className="header-subtitle">
          {t("app.subtitle")}
        </Text>
      </div>

      {/* Action selector */}
      <ActionSelector action={action} onChange={setAction} sectionTitle={t("action.section")} />

      {/* Options */}
      <div className="section-card">
        <div className="section-title">{t("options.section")}</div>
        <div className="options-grid">
          <div className="option-row">
            <Text strong>{t("options.timer")}</Text>
            <InputNumber
              min={0}
              max={600}
              value={timeout}
              onChange={(v) => setTimeout_(v ?? 30)}
              disabled={disableTimer}
              style={{ width: 160 }}
              suffix="s"
            />
          </div>
          <div className="option-row">
            <Text strong>{t("options.force")}</Text>
            <Radio.Group value={force} onChange={(e) => setForce(e.target.value)} size="small" disabled={disableForce}>
              <Radio.Button value={true}>{t("options.yes")}</Radio.Button>
              <Radio.Button value={false}>{t("options.no")}</Radio.Button>
            </Radio.Group>
          </div>
        </div>
      </div>

      {/* Execute */}
      <Flex vertical gap={8}>
        <Button
          block
          danger
          size="large"
          color="default"
          className="execute-btn"
          style={{ background: cfg.color, borderColor: cfg.color }}
          disabled={loading}
          onClick={handlePower}
          icon={cfg.icon}
          loading={loading}
        >
          <span>
            {!disableTimer
              ? t("execute.label", { action: t(ACTION_I18N_KEY[action]), timeout })
              : t("execute.label_simple", { action: t(ACTION_I18N_KEY[action]) })}</span>
        </Button>

        <Button block size="large" disabled={loading} onClick={handleCancel}>
          <WarningOutlined /> {t("execute.cancel")}
        </Button>
      </Flex>

      {/* Result */}
      {result && (
        <div className="section-card result-card">
          <Tag color={result.ok ? "success" : "error"}>
            {result.ok ? t("result.success") : t("result.failed")}
          </Tag>
          <Text>{result.message}</Text>
        </div>
      )}

      {/* Footer */}
      <div className="footer">
        <Text type="secondary">
          {t("footer.text")}
        </Text>
      </div>
    </div>
  );
}
