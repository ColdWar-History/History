import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Badge, EmptyState, ErrorBlock, LoadingBlock, MetricCard, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { api } from "../../lib/api";
import type { UserProfile } from "../../lib/types";
import { formatApiError, formatDateTime, modeLabel } from "../../lib/utils";

export function ProfilePage() {
  const auth = useAuth();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const data = await api.progress.getProfile();
        if (!ignore) {
          setProfile(data);
        }
      } catch (loadError) {
        if (!ignore) {
          setError(formatApiError(loadError));
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      ignore = true;
    };
  }, []);

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Профиль"
        title={auth.user ? `Профиль агента ${auth.user.userName}` : "Профиль пользователя"}
        description="Панель прогресса показывает последние криптооперации, достижения, общие метрики и суммарный рейтинг."
      />

      {loading ? <LoadingBlock label="Поднимаю профиль и историю операций..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {profile ? (
        <>
          <section className="metric-grid">
            <MetricCard detail="Общий рейтинг" label="Очки" value={profile.metrics.totalScore} />
            <MetricCard detail="Сколько заданий завершено" label="Задания" value={profile.metrics.challengesCompleted} />
            <MetricCard detail="Количество верных ответов" label="Верные ответы" value={profile.metrics.correctChallenges} />
            <MetricCard detail="Смены инспектора" label="Смены" value={profile.metrics.shiftReportsCompleted} />
            <MetricCard detail="История лаборатории" label="Криптооперации" value={profile.metrics.cryptoOperations} />
          </section>

          <div className="split-grid">
            <Panel subtitle="Последние действия в лаборатории" title="Криптооперации">
              {profile.recentOperations.length === 0 ? (
                <EmptyState description="История появится после первой операции в лаборатории." title="Пока пусто" />
              ) : (
                <div className="stack-list">
                  {profile.recentOperations.map((operation) => (
                    <article className="stack-card" key={operation.operationId}>
                      <div className="inline-meta">
                        <Badge>{operation.cipherCode}</Badge>
                        <span>{modeLabel(operation.mode)}</span>
                        <span>{formatDateTime(operation.processedAt)}</span>
                      </div>
                      <div className="comparison-grid">
                        <div>
                          <strong>Исходный текст</strong>
                          <pre>{operation.input}</pre>
                        </div>
                        <div>
                          <strong>Результат</strong>
                          <pre>{operation.output}</pre>
                        </div>
                      </div>
                      <div className="card-actions">
                        <Link
                          className="button ghost"
                          to={`/lab?cipher=${encodeURIComponent(operation.cipherCode)}&mode=${encodeURIComponent(
                            operation.mode
                          )}&input=${encodeURIComponent(operation.input)}`}
                        >
                          Повторить в лаборатории
                        </Link>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </Panel>

            <Panel subtitle="Разблокировки и milestones" title="Ачивки">
              {profile.achievements.length === 0 ? (
                <EmptyState description="Ачивки появятся после первых действий в тренировках, игре и лаборатории." title="Пока нет достижений" />
              ) : (
                <div className="stack-list">
                  {profile.achievements.map((achievement) => (
                    <article className="stack-card" key={achievement.code}>
                      <div className="inline-meta">
                        <Badge tone="success">{achievement.title}</Badge>
                        {achievement.unlockedAt ? <span>{formatDateTime(achievement.unlockedAt)}</span> : null}
                      </div>
                      <p>{achievement.description}</p>
                    </article>
                  ))}
                </div>
              )}
            </Panel>
          </div>
        </>
      ) : null}
    </div>
  );
}
