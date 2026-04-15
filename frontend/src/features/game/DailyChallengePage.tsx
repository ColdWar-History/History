import { useEffect, useState } from "react";

import { Badge, ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { api } from "../../lib/api";
import type { ChallengeAttemptResult, DailyChallenge } from "../../lib/types";
import { formatApiError, formatDate, formatDateTime } from "../../lib/utils";

export function DailyChallengePage() {
  const [daily, setDaily] = useState<DailyChallenge | null>(null);
  const [answer, setAnswer] = useState("");
  const [usedHint, setUsedHint] = useState(false);
  const [showHint, setShowHint] = useState(false);
  const [result, setResult] = useState<ChallengeAttemptResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function loadDaily() {
      setLoading(true);
      setError(null);

      try {
        const payload = await api.game.getDaily();
        if (!ignore) {
          setDaily(payload);
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

    void loadDaily();

    return () => {
      ignore = true;
    };
  }, []);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!daily) {
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const nextResult = await api.game.submitTraining(daily.challenge.id, answer, usedHint);
      setResult(nextResult);
    } catch (submitError) {
      setError(formatApiError(submitError));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Daily Challenge"
        title="Ежедневный вызов, который меняется по дате."
        description="Daily challenge тянется из game-сервиса и строится вокруг сегодняшнего шаблона cipherCode. Если backend обновил день, фронт сразу покажет новую задачу."
      />

      {loading ? <LoadingBlock label="Запрашиваю daily challenge..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {daily ? (
        <div className="split-grid">
          <Panel subtitle="Дневной пакет" title={daily.theme}>
            <article className="stack-card">
              <div className="inline-meta">
                <Badge>{daily.challenge.cipherCode}</Badge>
                <span>{formatDate(daily.date)}</span>
                <span>Base score: {daily.challenge.baseScore}</span>
              </div>
              <h3>{daily.challenge.prompt}</h3>
              <pre className="output-box">{daily.challenge.input}</pre>
              <small>Сгенерировано: {formatDateTime(daily.challenge.generatedAt)}</small>
            </article>
          </Panel>

          <Panel subtitle="Ответ сохраняется тем же submit-endpoint" title="Решение">
            <form className="stack-form" onSubmit={submit}>
              <Field label="Ответ">
                <input onChange={(event) => setAnswer(event.target.value)} placeholder="Введите plaintext..." value={answer} />
              </Field>

              {showHint ? (
                <div className="status-inline">
                  Подсказка: режим `{daily.challenge.expectedMode}`, параметры `{JSON.stringify(daily.challenge.parameters)}`.
                </div>
              ) : null}

              <div className="card-actions">
                <button className="button" disabled={busy} type="submit">
                  {busy ? "Проверяю..." : "Отправить"}
                </button>
                <button
                  className="button ghost"
                  onClick={() => {
                    setShowHint(true);
                    setUsedHint(true);
                  }}
                  type="button"
                >
                  Показать подсказку
                </button>
              </div>
            </form>

            {result ? (
              <article className="stack-card">
                <div className="inline-meta">
                  <Badge tone={result.isCorrect ? "success" : "danger"}>{result.isCorrect ? "Верно" : "Неверно"}</Badge>
                  <span>Очки: {result.awardedScore}</span>
                </div>
                <p>{result.explanation}</p>
                <div className="comparison-grid">
                  <div>
                    <strong>Ожидалось</strong>
                    <pre>{result.expectedAnswer}</pre>
                  </div>
                  <div>
                    <strong>Отправлено</strong>
                    <pre>{result.userAnswer}</pre>
                  </div>
                </div>
              </article>
            ) : null}
          </Panel>
        </div>
      ) : null}
    </div>
  );
}
