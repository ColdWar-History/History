import { BrowserRouter, Route, Routes } from "react-router-dom";

import { Layout } from "../components/Layout";
import { RequireAuth, RequireRole } from "../components/RouteGuard";
import { AuthProvider } from "../contexts/AuthContext";
import { AdminPage } from "../features/admin/AdminPage";
import { AuthPage } from "../features/auth/AuthPage";
import { CiphersPage } from "../features/content/CiphersPage";
import { CipherDetailsPage } from "../features/content/CipherDetailsPage";
import { HomePage } from "../features/content/HomePage";
import { NotFoundPage } from "../features/content/NotFoundPage";
import { TimelinePage } from "../features/content/TimelinePage";
import { CryptoLabPage } from "../features/crypto/CryptoLabPage";
import { DailyChallengePage } from "../features/game/DailyChallengePage";
import { ShiftPage } from "../features/game/ShiftPage";
import { TrainingPage } from "../features/game/TrainingPage";
import { LeaderboardPage } from "../features/profile/LeaderboardPage";
import { ProfilePage } from "../features/profile/ProfilePage";

export function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />} path="/">
            <Route element={<HomePage />} index />
            <Route element={<AuthPage />} path="auth" />
            <Route element={<CiphersPage />} path="ciphers" />
            <Route element={<CipherDetailsPage />} path="ciphers/:cipherId" />
            <Route element={<TimelinePage />} path="timeline" />
            <Route element={<CryptoLabPage />} path="lab" />
            <Route element={<TrainingPage />} path="training" />
            <Route element={<DailyChallengePage />} path="daily" />
            <Route element={<ShiftPage />} path="shift" />
            <Route
              element={
                <RequireAuth>
                  <ProfilePage />
                </RequireAuth>
              }
              path="profile"
            />
            <Route
              element={
                <RequireAuth>
                  <LeaderboardPage />
                </RequireAuth>
              }
              path="leaderboard"
            />
            <Route
              element={
                <RequireRole roles={["editor", "admin"]}>
                  <AdminPage />
                </RequireRole>
              }
              path="admin"
            />
            <Route element={<NotFoundPage />} path="*" />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
