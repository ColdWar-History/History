import { startTransition, useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { ErrorBlock, Field, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { formatApiError } from "../../lib/utils";

type Mode = "login" | "register";

export function AuthPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const redirect = searchParams.get("redirect") || "/profile";
  const [mode, setMode] = useState<Mode>("login");
  const [userNameOrEmail, setUserNameOrEmail] = useState("");
  const [password, setPassword] = useState("");
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (auth.isAuthenticated && auth.status === "ready") {
      startTransition(() => navigate(redirect, { replace: true }));
    }
  }, [auth.isAuthenticated, auth.status, navigate, redirect]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      if (mode === "login") {
        await auth.login({
          userNameOrEmail,
          password
        });
      } else {
        await auth.register({
          userName,
          email,
          password
        });
      }

      startTransition(() => navigate(redirect, { replace: true }));
    } catch (submitError) {
      setError(formatApiError(submitError));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Вход"
        title="Войдите, чтобы сохранять прогресс и открывать профиль."
        description="Создайте аккаунт или используйте тестовый доступ для проверки редакторского раздела."
      />

      <div className="split-grid auth-layout">
        <Panel subtitle="Доступ к прогрессу, профилю и рейтингам" title={mode === "login" ? "Вход" : "Регистрация"}>
          <div className="tab-strip">
            <button className={mode === "login" ? "tab-button is-active" : "tab-button"} onClick={() => setMode("login")} type="button">
              Логин
            </button>
            <button className={mode === "register" ? "tab-button is-active" : "tab-button"} onClick={() => setMode("register")} type="button">
              Регистрация
            </button>
          </div>

          {error ? <ErrorBlock message={error} /> : null}

          <form className="stack-form" onSubmit={handleSubmit}>
            {mode === "register" ? (
              <>
                <Field label="User name">
                  <input onChange={(event) => setUserName(event.target.value)} placeholder="agent.kim" value={userName} />
                </Field>
                <Field label="Email">
                  <input onChange={(event) => setEmail(event.target.value)} placeholder="agent@history.local" type="email" value={email} />
                </Field>
              </>
            ) : (
              <Field label="User name или Email">
                <input
                  onChange={(event) => setUserNameOrEmail(event.target.value)}
                  placeholder="admin или admin@example.com"
                  value={userNameOrEmail}
                />
              </Field>
            )}

            <Field label="Пароль">
              <input onChange={(event) => setPassword(event.target.value)} placeholder="Введите пароль" type="password" value={password} />
            </Field>

            <button className="button" disabled={loading} type="submit">
              {loading ? "Отправляю..." : mode === "login" ? "Войти" : "Создать аккаунт"}
            </button>
          </form>
        </Panel>

        <Panel subtitle="Доступные возможности" title="Быстрый старт">
          <div className="stack-list">
            <article className="stack-card">
              <h3>Тестовый админ</h3>
              <p>Логин admin и пароль Admin123! подходят для проверки редакторского раздела.</p>
            </article>
            <article className="stack-card">
              <h3>Гостевой режим остаётся доступным</h3>
              <p>Каталог, таймлайн и часть игровых сценариев работают без входа, но профиль и лидерборд требуют авторизацию.</p>
            </article>
          </div>

          <Link className="text-link" to="/lab">
            Вернуться в лабораторию
          </Link>
        </Panel>
      </div>
    </div>
  );
}
