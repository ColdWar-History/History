import type { ReactNode } from "react";
import { Link } from "react-router-dom";

import { cn } from "../lib/utils";

export function PageIntro({
  eyebrow,
  title,
  description,
  actions
}: {
  eyebrow: string;
  title: string;
  description: string;
  actions?: ReactNode;
}) {
  return (
    <section className="hero-panel">
      <div className="hero-copy">
        <span className="hero-eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {actions ? <div className="hero-actions">{actions}</div> : null}
    </section>
  );
}

export function Panel({
  title,
  subtitle,
  className,
  children
}: {
  title?: string;
  subtitle?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section className={cn("panel", className)}>
      {title ? (
        <header className="panel-header">
          <div>
            <h2>{title}</h2>
            {subtitle ? <p>{subtitle}</p> : null}
          </div>
        </header>
      ) : null}
      {children}
    </section>
  );
}

export function Badge({
  children,
  tone = "default"
}: {
  children: ReactNode;
  tone?: "default" | "success" | "warning" | "danger";
}) {
  return <span className={cn("badge", `badge-${tone}`)}>{children}</span>;
}

export function MetricCard({
  label,
  value,
  detail
}: {
  label: string;
  value: ReactNode;
  detail?: ReactNode;
}) {
  return (
    <article className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
      {detail ? <small>{detail}</small> : null}
    </article>
  );
}

export function Field({
  label,
  hint,
  children
}: {
  label: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {hint ? <small>{hint}</small> : null}
    </label>
  );
}

export function LoadingBlock({ label = "Загрузка..." }: { label?: string }) {
  return <div className="status-block">{label}</div>;
}

export function EmptyState({
  title,
  description,
  actionLabel,
  actionTo
}: {
  title: string;
  description: string;
  actionLabel?: string;
  actionTo?: string;
}) {
  return (
    <div className="status-block">
      <strong>{title}</strong>
      <p>{description}</p>
      {actionLabel && actionTo ? (
        <Link className="button ghost" to={actionTo}>
          {actionLabel}
        </Link>
      ) : null}
    </div>
  );
}

export function ErrorBlock({
  message,
  retryLabel,
  onRetry
}: {
  message: string;
  retryLabel?: string;
  onRetry?: () => void;
}) {
  return (
    <div className="status-block status-error">
      <strong>Ошибка запроса</strong>
      <p>{message}</p>
      {retryLabel && onRetry ? (
        <button className="button ghost" onClick={onRetry} type="button">
          {retryLabel}
        </button>
      ) : null}
    </div>
  );
}
