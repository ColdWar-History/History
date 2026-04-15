export type Role = "guest" | "user" | "editor" | "admin";

export interface AuthTokensResponse {
  userId: string;
  userName: string;
  roles: Role[];
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

export interface UserInfoResponse {
  userId: string;
  userName: string;
  email: string;
  roles: Role[];
}

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  userNameOrEmail: string;
  password: string;
}

export interface CipherVersion {
  versionNumber: number;
  editedBy: string;
  updatedAt: string;
  changeSummary: string;
}

export interface CipherCard {
  id: string;
  code: string;
  name: string;
  category: string;
  era: string;
  difficulty: number;
  summary: string;
  description: string;
  example: string;
  publicationStatus: string;
  relatedEventIds: string[];
  versions: CipherVersion[];
}

export interface HistoricalEvent {
  id: string;
  title: string;
  date: string;
  region: string;
  topic: string;
  summary: string;
  description: string;
  participants: string[];
  cipherCodes: string[];
  publicationStatus: string;
}

export interface Collection {
  id: string;
  title: string;
  theme: string;
  summary: string;
  eventIds: string[];
  cipherCodes: string[];
  publicationStatus: string;
}

export interface CipherParameterDefinition {
  name: string;
  label: string;
  type: string;
  isRequired: boolean;
  description: string | null;
}

export interface CipherCatalogItem {
  code: string;
  name: string;
  category: string;
  era: string;
  difficulty: number;
  parameters: CipherParameterDefinition[];
}

export interface CryptoTransformRequest {
  cipherCode: string;
  mode: "encrypt" | "decrypt";
  input: string;
  parameters: Record<string, string>;
  explanationLevel?: string | null;
}

export interface CryptoStep {
  order: number;
  title: string;
  description: string;
  snapshot: string;
}

export interface CryptoTransformResponse {
  cipherCode: string;
  mode: string;
  input: string;
  output: string;
  steps: CryptoStep[];
  validationMessages: string[];
  processedAt: string;
  operationId?: string | null;
}

export interface TrainingChallenge {
  id: string;
  cipherCode: string;
  difficulty: string;
  prompt: string;
  input: string;
  expectedMode: string;
  parameters: Record<string, string>;
  baseScore: number;
  generatedAt: string;
}

export interface ChallengeAttemptResult {
  challengeId: string;
  isCorrect: boolean;
  awardedScore: number;
  explanation: string;
  expectedAnswer: string;
  userAnswer: string;
  evaluatedAt: string;
}

export interface DailyChallenge {
  date: string;
  challenge: TrainingChallenge;
  theme: string;
}

export interface ShiftMessage {
  messageId: string;
  headline: string;
  encodedMessage: string;
  cipherCode: string;
  briefing: string;
}

export interface ShiftSession {
  shiftId: string;
  difficulty: string;
  messages: ShiftMessage[];
  startedAt: string;
}

export interface ShiftResolution {
  messageId: string;
  decision: string;
  isCorrect: boolean;
  scoreDelta: number;
  explanation: string;
}

export interface ShiftReport {
  shiftId: string;
  totalScore: number;
  correctDecisions: number;
  incorrectDecisions: number;
  resolutions: ShiftResolution[];
  recommendation: string;
  completedAt: string;
}

export interface Achievement {
  code: string;
  title: string;
  description: string;
  unlockedAt: string | null;
}

export interface CryptoOperationHistory {
  operationId: string;
  cipherCode: string;
  mode: string;
  input: string;
  output: string;
  processedAt: string;
}

export interface UserMetrics {
  totalScore: number;
  challengesCompleted: number;
  correctChallenges: number;
  shiftReportsCompleted: number;
  cryptoOperations: number;
}

export interface UserProfile {
  userId: string;
  userName: string;
  recentOperations: CryptoOperationHistory[];
  achievements: Achievement[];
  metrics: UserMetrics;
}

export interface LeaderboardEntry {
  rank: number;
  userId: string;
  userName: string;
  score: number;
  correctChallenges: number;
}

export interface OperationErrorPayload {
  code?: string;
  message?: string;
  error?: string;
}

export interface UpsertCipherCardRequest {
  code: string;
  name: string;
  category: string;
  era: string;
  difficulty: number;
  summary: string;
  description: string;
  example: string;
  relatedEventIds: string[];
  editedBy: string;
  changeSummary: string;
}

export interface UpsertHistoricalEventRequest {
  title: string;
  date: string;
  region: string;
  topic: string;
  summary: string;
  description: string;
  participants: string[];
  cipherCodes: string[];
}

export interface UpsertCollectionRequest {
  title: string;
  theme: string;
  summary: string;
  eventIds: string[];
  cipherCodes: string[];
}
