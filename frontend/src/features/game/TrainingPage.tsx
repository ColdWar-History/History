import { useEffect, useState } from "react";

import { Badge, ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { api } from "../../lib/api";
import type { ChallengeAttemptResult, CipherCatalogItem, TrainingChallenge } from "../../lib/types";
import { difficultyLabel, formatApiError, formatDateTime } from "../../lib/utils";

export function TrainingPage() {
  const [catalog, setCatalog] = useState<CipherCatalogItem[]>([]);
  const [cipherCode, setCipherCode] = useState("caesar");
  const [difficulty, setDifficulty] = useState("normal");
  const [challenge, setChallenge] = useState<TrainingChallenge | null>(null);
  const [answer, setAnswer] = useState("");
  const [usedHint, setUsedHint] = useState(false);
  const [showHint, setShowHint] = useState(false);
  const [result, setResult] = useState<ChallengeAttemptResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function loadCatalog() {
      try {
        const data = await api.crypto.getCatalog();
        if (!ignore) {
          setCatalog(data);
          setCipherCode(data[0]?.code || "caesar");
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

    void loadCatalog();

    return () => {
      ignore = true;
    };
  }, []);

  async function generateChallenge() {
    setBusy(true);
    setError(null);

    try {
      const nextChallenge = await api.game.generateTraining(cipherCode, difficulty);
      setChallenge(nextChallenge);
      setAnswer("");
      setUsedHint(false);
      setShowHint(false);
      setResult(null);
    } catch (loadError) {
      setError(formatApiError(loadError));
    } finally {
      setBusy(false);
    }
  }

  async function submitAnswer(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!challenge) {
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const attempt = await api.game.submitTraining(challenge.id, answer, usedHint);
      setResult(attempt);
    } catch (submitError) {
      setError(formatApiError(submitError));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Training Mode"
        title="Генерация тренировок по выбранному шифру и сложности."
        description="Экран учитывает MVP-особенность backend: `training/generate` получает `cipherCode` и `difficulty` через query string, а ответ на задание уходит отдельным submit-запросом."
      />

      {loading ? <LoadingBlock label="Загружаю каталог для тренировок..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {!loading ? (
        <div className="split-grid">
          <Panel subtitle="Сначала поднимаем задание, затем отправляем ответ" title="Генерация задания">
            <div className="stack-form">
              <div className="form-grid form-grid-2">
                <Field label="Шифр">
                  <select onChange={(event) => setCipherCode(event.target.value)} value={cipherCode}>
                    {catalog.map((cipher) => (
                      <option key={cipher.code} value={cipher.code}>
                        {cipher.name}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Сложность">
                  <select onChange={(event) => setDifficulty(event.target.value)} value={difficulty}>
                    <option value="easy">Легко</option>
                    <option value="normal">Нормально</option>
                    <option value="hard">Сложно</option>
                  </select>
                </Field>
              </div>

              <button className="button" disabled={busy} onClick={() => void generateChallenge()} type="button">
                {busy ? "Генерирую..." : "Сгенерировать миссию"}
              </button>
            </div>
          </Panel>

          <Panel subtitle="Показываем prompt, input и результат проверки" title="Оперативная панель">
            {challenge ? (
              <>
                <article className="stack-card">
                  <div className="inline-meta">
                    <Badge>{challenge.cipherCode}</Badge>
                    <Badge>{difficultyLabel(challenge.difficulty)}</Badge>
                    <span>{formatDateTime(challenge.generatedAt)}</span>
                    <span>Base score: {challenge.baseScore}</span>
                  </div>
                  <h3>{challenge.prompt}</h3>
                  <pre className="output-box">{challenge.input}</pre>
                </article>

                <form className="stack-form" onSubmit={submitAnswer}>
                  <Field hint="Введи предполагаемый plaintext." label="Ответ">
                    <input onChange={(event) => setAnswer(event.target.value)} placeholder="Введите расшифровку..." value={answer} />
                  </Field>

                  {showHint ? (
                    <div className="status-inline">
                      Подсказка: режим `{challenge.expectedMode}`, параметры `{JSON.stringify(challenge.parameters)}`. За подсказку backend спишет 20 очков.
                    </div>
                  ) : null}

                  <div className="card-actions">
                    <button className="button secondary" disabled={busy} type="submit">
                      Проверить
                    </button>
                    <button
                      className="button ghost"
                      onClick={() => {
                        setShowHint(true);
                        setUsedHint(true);
                      }}
                      type="button"
                    >
                      Нужна подсказка
                    </button>
                  </div>
                </form>

                {result ? (
                  <article className="stack-card">
                    <div className="inline-meta">
                      <Badge tone={result.isCorrect ? "success" : "danger"}>{result.isCorrect ? "Верно" : "Ошибка"}</Badge>
                      <span>Очки: {result.awardedScore}</span>
                      <span>{formatDateTime(result.evaluatedAt)}</span>
                    </div>
                    <p>{result.explanation}</p>
                    <div className="comparison-grid">
                      <div>
                        <strong>Ожидалось</strong>
                        <pre>{result.expectedAnswer}</pre>
                      </div>
                      <div>
                        <strong>Ответ пользователя</strong>
                        <pre>{result.userAnswer}</pre>
                      </div>
                    </div>
                  </article>
                ) : null}
              </>
            ) : (
              <div className="status-block">
                <strong>Задание ещё не создано</strong>
                <p>Выбери шифр и сложность, затем создай миссию. После submit правильный результат запишется в progress для авторизованного пользователя.</p>
              </div>
            )}
          </Panel>
        </div>
      ) : null}
    </div>
  );
}
