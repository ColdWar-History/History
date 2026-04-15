import { useEffect, useState } from "react";

import { Badge, ErrorBlock, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { api } from "../../lib/api";
import type { LeaderboardEntry } from "../../lib/types";
import { formatApiError } from "../../lib/utils";

export function LeaderboardPage() {
  const auth = useAuth();
  const [entries, setEntries] = useState<LeaderboardEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const data = await api.progress.getLeaderboard();
        if (!ignore) {
          setEntries(data);
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

  const leaders = entries.slice(0, 3);

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Leaderboard"
        title="Рейтинг пользователей по score и количеству верных тренировок."
        description="Эта страница требует авторизацию и показывает агрегированные данные из progress-сервиса. Сортировка повторяет backend: score, затем correctChallenges."
      />

      {loading ? <LoadingBlock label="Собираю таблицу лидеров..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {!loading && !error ? (
        <>
          <section className="cards-grid cards-grid-3">
            {leaders.map((entry, index) => (
              <article className={`podium-card podium-${index + 1}`} key={entry.userId}>
                <span>#{entry.rank}</span>
                <h3>{entry.userName}</h3>
                <strong>{entry.score}</strong>
                <small>{entry.correctChallenges} correct challenge(s)</small>
              </article>
            ))}
          </section>

          <Panel subtitle="Полная таблица лидеров" title="Рейтинг">
            <div className="table-shell">
              <table>
                <thead>
                  <tr>
                    <th>Место</th>
                    <th>Пользователь</th>
                    <th>Score</th>
                    <th>Correct</th>
                    <th>Статус</th>
                  </tr>
                </thead>
                <tbody>
                  {entries.map((entry) => (
                    <tr className={entry.userId === auth.user?.userId ? "is-current-row" : ""} key={entry.userId}>
                      <td>{entry.rank}</td>
                      <td>{entry.userName}</td>
                      <td>{entry.score}</td>
                      <td>{entry.correctChallenges}</td>
                      <td>{entry.userId === auth.user?.userId ? <Badge tone="success">Вы</Badge> : <Badge>Игрок</Badge>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>
        </>
      ) : null}
    </div>
  );
}
