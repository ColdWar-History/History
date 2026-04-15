import { NavLink, Outlet, useLocation } from "react-router-dom";

import { useAuth } from "../contexts/AuthContext";
import { initials, roleLabel } from "../lib/utils";

const primaryLinks = [
  { to: "/", label: "Главная" },
  { to: "/ciphers", label: "Каталог" },
  { to: "/timeline", label: "Таймлайн" },
  { to: "/lab", label: "Криптолаб" },
  { to: "/training", label: "Тренировка" },
  { to: "/daily", label: "Daily" },
  { to: "/shift", label: "Инспектор" }
];

export function Layout() {
  const auth = useAuth();
  const location = useLocation();

  return (
    <div className="app-shell">
      <div className="background-grid" />
      <header className="topbar">
        <div className="brand-block">
          <NavLink className="brand-mark" to="/">
            <span>Cold War</span>
            <strong>History</strong>
          </NavLink>
          <p>Интерактивный фронтенд для исторического контента, криптографии и игровых сценариев.</p>
        </div>

        <nav className="nav-cluster" aria-label="Основная навигация">
          {primaryLinks.map((item) => (
            <NavLink
              key={item.to}
              className={({ isActive }) => (isActive ? "nav-link is-active" : "nav-link")}
              to={item.to}
            >
              {item.label}
            </NavLink>
          ))}

          {auth.isAuthenticated ? (
            <>
              <NavLink className={({ isActive }) => (isActive ? "nav-link is-active" : "nav-link")} to="/leaderboard">
                Лидерборд
              </NavLink>
              <NavLink className={({ isActive }) => (isActive ? "nav-link is-active" : "nav-link")} to="/profile">
                Профиль
              </NavLink>
            </>
          ) : null}

          {auth.hasRole("editor", "admin") ? (
            <NavLink className={({ isActive }) => (isActive ? "nav-link is-active" : "nav-link")} to="/admin">
              Редактор
            </NavLink>
          ) : null}
        </nav>

        <div className="topbar-status">
          {auth.isAuthenticated && auth.user ? (
            <>
              <div className="identity-pill">
                <div className="identity-avatar">{initials(auth.user.userName)}</div>
                <div>
                  <strong>{auth.user.userName}</strong>
                  <span>{auth.roles.map(roleLabel).join(" · ")}</span>
                </div>
              </div>
              <button className="button ghost" onClick={() => void auth.logout()} type="button">
                Выйти
              </button>
            </>
          ) : (
            <>
              <div className="identity-pill identity-pill-guest">
                <div className="identity-avatar">GW</div>
                <div>
                  <strong>Гостевой режим</strong>
                  <span>Прогресс не сохраняется без входа</span>
                </div>
              </div>
              <NavLink className="button" to={`/auth?redirect=${encodeURIComponent(location.pathname + location.search)}`}>
                Войти
              </NavLink>
            </>
          )}
        </div>
      </header>

      <main className="content-shell">
        <Outlet />
      </main>

      <footer className="footer-bar">
        <span>Gateway target: `http://localhost:7000`</span>
        <span>Frontend honours opaque tokens, refresh flow and role-based UI gates.</span>
      </footer>
    </div>
  );
}
