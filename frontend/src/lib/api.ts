import { createSession, loadAuthSession, saveAuthSession, type AuthSession } from "./storage";
import type {
  AuthTokensResponse,
  ChallengeAttemptResult,
  CipherCard,
  CipherCatalogItem,
  Collection,
  CryptoTransformRequest,
  CryptoTransformResponse,
  DailyChallenge,
  HistoricalEvent,
  LeaderboardEntry,
  LoginRequest,
  OperationErrorPayload,
  RegisterRequest,
  ShiftReport,
  ShiftResolution,
  ShiftSession,
  TrainingChallenge,
  UpsertCipherCardRequest,
  UpsertCollectionRequest,
  UpsertHistoricalEventRequest,
  UserInfoResponse,
  UserProfile
} from "./types";

type QueryValue = string | number | boolean | null | undefined;

interface RequestOptions {
  method?: "GET" | "POST" | "PUT";
  body?: unknown;
  auth?: boolean;
  query?: Record<string, QueryValue>;
  headers?: HeadersInit;
  signal?: AbortSignal;
  retryOnUnauthorized?: boolean;
}

type SessionGetter = () => AuthSession | null;
type SessionSetter = (session: AuthSession | null) => void;

const runtimeOrigin = typeof window !== "undefined" ? window.location.origin : "";
const isProxyFriendlyLocal =
  runtimeOrigin.includes("localhost") &&
  !runtimeOrigin.endsWith(":7000") &&
  !runtimeOrigin.endsWith(":4173");

const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ??
  (isProxyFriendlyLocal ? "" : "http://localhost:7000");

let getSession: SessionGetter = () => loadAuthSession();
let setSession: SessionSetter = (session) => saveAuthSession(session);
let refreshPromise: Promise<AuthSession | null> | null = null;

export class ApiError extends Error {
  status: number;
  code?: string;
  details?: unknown;

  constructor(status: number, message: string, code?: string, details?: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

export function registerAuthSessionAccessors(getter: SessionGetter, setter: SessionSetter): void {
  getSession = getter;
  setSession = setter;
}

function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const url = new URL(`${apiBaseUrl}${path}`, apiBaseUrl ? undefined : runtimeOrigin || "http://localhost");
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== "") {
        url.searchParams.set(key, String(value));
      }
    }
  }

  if (!apiBaseUrl) {
    return `${url.pathname}${url.search}`;
  }

  return url.toString();
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return null as T;
  }

  const text = await response.text();
  if (!text) {
    return null as T;
  }

  return JSON.parse(text) as T;
}

function mapStatusMessage(status: number): string {
  if (status === 400) {
    return "Запрос отклонён gateway или сервисом. Проверь введённые данные.";
  }

  if (status === 401) {
    return "Сессия истекла или токен недействителен.";
  }

  if (status === 404) {
    return "Запрошенный ресурс не найден.";
  }

  return "Gateway вернул ошибку.";
}

async function toApiError(response: Response): Promise<ApiError> {
  const payload = await parseResponse<OperationErrorPayload | null>(response).catch(() => null);
  return new ApiError(
    response.status,
    payload?.message || payload?.error || mapStatusMessage(response.status),
    payload?.code,
    payload
  );
}

async function refreshSession(): Promise<AuthSession | null> {
  if (refreshPromise) {
    return refreshPromise;
  }

  const current = getSession();
  if (!current?.refreshToken) {
    setSession(null);
    return null;
  }

  refreshPromise = (async () => {
    try {
      const tokens = await request<AuthTokensResponse>("/api/auth/refresh", {
        method: "POST",
        body: { refreshToken: current.refreshToken },
        auth: false,
        retryOnUnauthorized: false
      });

      const next = createSession(tokens, current.email);
      setSession(next);
      return next;
    } catch {
      setSession(null);
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const session = getSession();
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  let body: BodyInit | undefined;
  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
    body = JSON.stringify(options.body);
  }

  if (options.auth !== false && session?.accessToken) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  const response = await fetch(buildUrl(path, options.query), {
    method: options.method ?? "GET",
    headers,
    body,
    signal: options.signal
  });

  if (response.status === 401 && options.auth !== false && options.retryOnUnauthorized !== false) {
    const refreshed = await refreshSession();
    if (refreshed) {
      return request<T>(path, { ...options, retryOnUnauthorized: false });
    }

    throw new ApiError(401, "Сессия истекла. Выполните вход заново.");
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return parseResponse<T>(response);
}

export const api = {
  auth: {
    register(payload: RegisterRequest) {
      return request<AuthTokensResponse>("/api/auth/register", {
        method: "POST",
        body: payload,
        auth: false
      });
    },
    login(payload: LoginRequest) {
      return request<AuthTokensResponse>("/api/auth/login", {
        method: "POST",
        body: payload,
        auth: false
      });
    },
    me() {
      return request<UserInfoResponse>("/api/auth/me");
    },
    logout(refreshToken: string) {
      return request<void>("/api/auth/logout", {
        method: "POST",
        body: { refreshToken },
        auth: false
      });
    }
  },
  content: {
    getCiphers(filters: { search?: string; category?: string; era?: string; publishedOnly?: boolean } = {}) {
      return request<CipherCard[]>("/api/content/ciphers", {
        auth: false,
        query: {
          search: filters.search,
          category: filters.category,
          era: filters.era,
          publishedOnly: filters.publishedOnly ?? true
        }
      });
    },
    getCipher(id: string) {
      return request<CipherCard>(`/api/content/ciphers/${id}`, { auth: false });
    },
    getEvents(filters: { region?: string; year?: number; topic?: string; publishedOnly?: boolean } = {}) {
      return request<HistoricalEvent[]>("/api/content/events", {
        auth: false,
        query: {
          region: filters.region,
          year: filters.year,
          topic: filters.topic,
          publishedOnly: filters.publishedOnly ?? true
        }
      });
    },
    getEvent(id: string) {
      return request<HistoricalEvent>(`/api/content/events/${id}`, { auth: false });
    },
    getCollections(publishedOnly = true) {
      return request<Collection[]>("/api/content/collections", {
        auth: false,
        query: { publishedOnly }
      });
    },
    getCollection(id: string) {
      return request<Collection>(`/api/content/collections/${id}`, { auth: false });
    },
    createCipher(payload: UpsertCipherCardRequest) {
      return request<CipherCard>("/api/content/ciphers", {
        method: "POST",
        body: payload
      });
    },
    updateCipher(id: string, payload: UpsertCipherCardRequest) {
      return request<CipherCard>(`/api/content/ciphers/${id}`, {
        method: "PUT",
        body: payload
      });
    },
    publishCipher(id: string, status: "Published" | "Draft") {
      return request<void>(`/api/content/ciphers/${id}/publication/${status}`, {
        method: "POST"
      });
    },
    createEvent(payload: UpsertHistoricalEventRequest) {
      return request<HistoricalEvent>("/api/content/events", {
        method: "POST",
        body: payload
      });
    },
    updateEvent(id: string, payload: UpsertHistoricalEventRequest) {
      return request<HistoricalEvent>(`/api/content/events/${id}`, {
        method: "PUT",
        body: payload
      });
    },
    publishEvent(id: string, status: "Published" | "Draft") {
      return request<void>(`/api/content/events/${id}/publication/${status}`, {
        method: "POST"
      });
    },
    createCollection(payload: UpsertCollectionRequest) {
      return request<Collection>("/api/content/collections", {
        method: "POST",
        body: payload
      });
    },
    updateCollection(id: string, payload: UpsertCollectionRequest) {
      return request<Collection>(`/api/content/collections/${id}`, {
        method: "PUT",
        body: payload
      });
    },
    publishCollection(id: string, status: "Published" | "Draft") {
      return request<void>(`/api/content/collections/${id}/publication/${status}`, {
        method: "POST"
      });
    }
  },
  crypto: {
    getCatalog() {
      return request<CipherCatalogItem[]>("/api/crypto/catalog", { auth: false });
    },
    transform(payload: CryptoTransformRequest) {
      return request<CryptoTransformResponse>("/api/crypto/transform", {
        method: "POST",
        body: payload
      });
    }
  },
  game: {
    generateTraining(cipherCode: string, difficulty: string) {
      return request<TrainingChallenge>("/api/game/training/generate", {
        method: "POST",
        query: { cipherCode, difficulty }
      });
    },
    submitTraining(challengeId: string, answer: string, usedHint: boolean) {
      return request<ChallengeAttemptResult>(`/api/game/training/${challengeId}/submit`, {
        method: "POST",
        body: { answer, usedHint }
      });
    },
    getDaily() {
      return request<DailyChallenge>("/api/game/daily", { auth: false });
    },
    startShift(difficulty: string) {
      return request<ShiftSession>("/api/game/shifts/start", {
        method: "POST",
        body: { difficulty }
      });
    },
    resolveShift(shiftId: string, messageId: string, decision: string, decodedMessage: string | null) {
      return request<ShiftResolution>(`/api/game/shifts/${shiftId}/resolve`, {
        method: "POST",
        body: { messageId, decision, decodedMessage }
      });
    },
    async getShiftReport(shiftId: string) {
      return request<ShiftReport | null>(`/api/game/shifts/${shiftId}/report`);
    }
  },
  progress: {
    getProfile() {
      return request<UserProfile>("/api/progress/profile");
    },
    getLeaderboard() {
      return request<LeaderboardEntry[]>("/api/progress/leaderboard");
    }
  }
};
