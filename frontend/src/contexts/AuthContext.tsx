import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from "react";

import { api, registerAuthSessionAccessors } from "../lib/api";
import { createSession, loadAuthSession, saveAuthSession, type AuthSession } from "../lib/storage";
import type { LoginRequest, RegisterRequest, Role, UserInfoResponse } from "../lib/types";

type AuthStatus = "booting" | "ready";

interface AuthContextValue {
  session: AuthSession | null;
  user: UserInfoResponse | null;
  roles: Role[];
  isAuthenticated: boolean;
  status: AuthStatus;
  login: (payload: LoginRequest) => Promise<void>;
  register: (payload: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
  hasRole: (...roles: Role[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => loadAuthSession());
  const [user, setUser] = useState<UserInfoResponse | null>(() => {
    const stored = loadAuthSession();
    if (!stored) {
      return null;
    }

    return {
      userId: stored.userId,
      userName: stored.userName,
      email: stored.email ?? "",
      roles: stored.roles
    };
  });
  const [status, setStatus] = useState<AuthStatus>("booting");
  const sessionRef = useRef<AuthSession | null>(session);

  function applySession(next: AuthSession | null): void {
    sessionRef.current = next;
    setSession(next);
    saveAuthSession(next);
    if (!next) {
      setUser(null);
    }
  }

  useEffect(() => {
    sessionRef.current = session;
  }, [session]);

  useEffect(() => {
    registerAuthSessionAccessors(
      () => sessionRef.current,
      (next) => applySession(next)
    );
  }, []);

  useEffect(() => {
    let ignore = false;

    async function bootstrap() {
      if (!sessionRef.current) {
        setStatus("ready");
        return;
      }

      try {
        const profile = await api.auth.me();
        if (ignore) {
          return;
        }

        setUser(profile);
        if (sessionRef.current) {
          applySession({
            ...sessionRef.current,
            userId: profile.userId,
            userName: profile.userName,
            email: profile.email || sessionRef.current.email,
            roles: profile.roles
          });
        }
      } catch {
        if (!ignore) {
          applySession(null);
        }
      } finally {
        if (!ignore) {
          setStatus("ready");
        }
      }
    }

    void bootstrap();

    return () => {
      ignore = true;
    };
  }, []);

  async function refreshProfile(): Promise<void> {
    const profile = await api.auth.me();
    setUser(profile);
    if (sessionRef.current) {
      applySession({
        ...sessionRef.current,
        userId: profile.userId,
        userName: profile.userName,
        email: profile.email || sessionRef.current.email,
        roles: profile.roles
      });
    }
  }

  async function login(payload: LoginRequest): Promise<void> {
    const tokens = await api.auth.login(payload);
    const next = createSession(tokens);
    applySession(next);
    setUser({
      userId: next.userId,
      userName: next.userName,
      email: next.email ?? "",
      roles: next.roles
    });
  }

  async function register(payload: RegisterRequest): Promise<void> {
    const tokens = await api.auth.register(payload);
    const next = createSession(tokens, payload.email);
    applySession(next);
    setUser({
      userId: next.userId,
      userName: next.userName,
      email: payload.email,
      roles: next.roles
    });
  }

  async function logout(): Promise<void> {
    const current = sessionRef.current;
    if (current?.refreshToken) {
      try {
        await api.auth.logout(current.refreshToken);
      } catch {
        // Local logout still wins if backend token is already invalidated.
      }
    }

    applySession(null);
  }

  function hasRole(...rolesToCheck: Role[]): boolean {
    if (rolesToCheck.length === 0) {
      return true;
    }

    return rolesToCheck.some((role) => (user?.roles ?? session?.roles ?? []).includes(role));
  }

  return (
    <AuthContext.Provider
      value={{
        session,
        user,
        roles: user?.roles ?? session?.roles ?? [],
        isAuthenticated: Boolean(session?.accessToken),
        status,
        login,
        register,
        logout,
        refreshProfile,
        hasRole
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("AuthContext is not mounted.");
  }

  return value;
}
