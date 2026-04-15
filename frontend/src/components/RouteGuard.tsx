import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";

import { useAuth } from "../contexts/AuthContext";
import type { Role } from "../lib/types";
import { LoadingBlock } from "./Ui";

export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const location = useLocation();

  if (auth.status === "booting") {
    return <LoadingBlock label="Проверяю сессию..." />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate replace to={`/auth?redirect=${encodeURIComponent(`${location.pathname}${location.search}`)}`} />;
  }

  return <>{children}</>;
}

export function RequireRole({
  roles,
  children
}: {
  roles: Role[];
  children: ReactNode;
}) {
  const auth = useAuth();

  if (auth.status === "booting") {
    return <LoadingBlock label="Проверяю права доступа..." />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate replace to="/auth" />;
  }

  if (!auth.hasRole(...roles)) {
    return <Navigate replace to="/" />;
  }

  return <>{children}</>;
}
