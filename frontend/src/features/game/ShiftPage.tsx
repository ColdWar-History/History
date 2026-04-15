import { useState } from "react";

import { Badge, ErrorBlock, Field, PageIntro, Panel } from "../../components/Ui";
import { api } from "../../lib/api";
import type { ShiftReport, ShiftResolution, ShiftSession } from "../../lib/types";
import { decisionLabel, difficultyLabel, formatApiError, formatDateTime } from "../../lib/utils";

export function ShiftPage() {
  const [difficulty, setDifficulty] = useState("normal");
  const [session, setSession] = useState<ShiftSession | null>(null);
  const [resolutions, setResolutions] = useState<Record<string, ShiftResolution>>({});
  const [activeMessageId, setActiveMessageId] = useState<string | null>(null);
  const [decodedMessage, setDecodedMessage] = useState("");
  const [decision, setDecision] = useState("allow");
  const [report, setReport] = useState<ShiftReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeMessage = session?.messages.find((message) => message.messageId === activeMessageId) ?? null;

  async function startShift() {
    setBusy(true);
    setError(null);

    try {
      const nextSession = await api.game.startShift(difficulty);
      setSession(nextSession);
      setResolutions({});
      setActiveMessageId(nextSession.messages[0]?.messageId ?? null);
      setReport(null);
      setDecodedMessage("");
      setDecision("allow");
    } catch (loadError) {
      setError(formatApiError(loadError));
    } finally {
      setBusy(false);
    }
  }

  async function resolveCurrent() {
    if (!session || !activeMessage) {
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const resolution = await api.game.resolveShift(session.shiftId, activeMessage.messageId, decision, decodedMessage || null);
      const nextResolutions = {
        ...resolutions,
        [resolution.messageId]: resolution
      };
      setResolutions(nextResolutions);
      setDecodedMessage("");

      const nextMessage = session.messages.find((message) => !nextResolutions[message.messageId]);
      setActiveMessageId(nextMessage?.messageId ?? null);

      const nextReport = await api.game.getShiftReport(session.shiftId);
      if (nextReport) {
        setReport(nextReport);
      }
    } catch (resolveError) {
      setError(formatApiError(resolveError));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Inspector Shift"
        title="Игровой режим «Инспектор связи»."
        description="Фронт стартует смену, показывает поток сообщений, принимает решение `allow/reject/escalate`, отправляет его в backend и строит финальный отчёт по смене."
      />

      {error ? <ErrorBlock message={error} /> : null}

      <div className="split-grid">
        <Panel subtitle="Старт новой смены" title="Оперативный запуск">
          <div className="stack-form">
            <Field label="Сложность">
              <select onChange={(event) => setDifficulty(event.target.value)} value={difficulty}>
                <option value="easy">Легко</option>
                <option value="normal">Нормально</option>
                <option value="hard">Сложно</option>
              </select>
            </Field>
            <button className="button" disabled={busy} onClick={() => void startShift()} type="button">
              {busy ? "Запускаю..." : "Начать смену"}
            </button>
          </div>

          {session ? (
            <article className="stack-card">
              <div className="inline-meta">
                <Badge>{difficultyLabel(session.difficulty)}</Badge>
                <span>{formatDateTime(session.startedAt)}</span>
                <span>{session.messages.length} сообщений</span>
              </div>
              <h3>Очередь перехватов</h3>
              <div className="queue-list">
                {session.messages.map((message, index) => {
                  const resolution = resolutions[message.messageId];
                  return (
                    <button
                      className={`queue-item ${message.messageId === activeMessageId ? "is-active" : ""}`}
                      key={message.messageId}
                      onClick={() => setActiveMessageId(message.messageId)}
                      type="button"
                    >
                      <span>{String(index + 1).padStart(2, "0")}</span>
                      <strong>{message.headline}</strong>
                      <small>{resolution ? decisionLabel(resolution.decision) : "Ожидает решения"}</small>
                    </button>
                  );
                })}
              </div>
            </article>
          ) : null}
        </Panel>

        <Panel subtitle="Анализ текущего сообщения" title="Консоль инспектора">
          {activeMessage ? (
            <div className="stack-list">
              <article className="stack-card">
                <div className="inline-meta">
                  <Badge>{activeMessage.cipherCode}</Badge>
                  <span>{activeMessage.briefing}</span>
                </div>
                <h3>{activeMessage.headline}</h3>
                <pre className="output-box">{activeMessage.encodedMessage}</pre>
              </article>

              <article className="stack-card">
                <Field hint="Поле уходит в `decodedMessage`, даже если backend пока не валидирует его содержимое." label="Черновик расшифровки">
                  <textarea onChange={(event) => setDecodedMessage(event.target.value)} rows={5} value={decodedMessage} />
                </Field>

                <Field label="Решение">
                  <select onChange={(event) => setDecision(event.target.value)} value={decision}>
                    <option value="allow">Пропустить</option>
                    <option value="reject">Отклонить</option>
                    <option value="escalate">Эскалировать</option>
                  </select>
                </Field>

                <div className="card-actions">
                  <button className="button" disabled={busy} onClick={() => void resolveCurrent()} type="button">
                    {busy ? "Отправляю..." : "Зафиксировать решение"}
                  </button>
                </div>
              </article>
            </div>
          ) : (
            <div className="status-block">
              <strong>Нет активного сообщения</strong>
              <p>Запусти смену, чтобы открыть очередь сообщений. Когда все решения будут отправлены, backend вернёт отчёт по смене.</p>
            </div>
          )}
        </Panel>
      </div>

      {report ? (
        <Panel subtitle="Финальный отчёт из `/api/game/shifts/{shiftId}/report`" title="Итоги смены">
          <section className="metric-grid">
            <div className="metric-card">
              <span>Общий счёт</span>
              <strong>{report.totalScore}</strong>
            </div>
            <div className="metric-card">
              <span>Верные решения</span>
              <strong>{report.correctDecisions}</strong>
            </div>
            <div className="metric-card">
              <span>Ошибки</span>
              <strong>{report.incorrectDecisions}</strong>
            </div>
          </section>

          <div className="stack-list">
            {report.resolutions.map((resolution) => (
              <article className="stack-card" key={resolution.messageId}>
                <div className="inline-meta">
                  <Badge tone={resolution.isCorrect ? "success" : "danger"}>{resolution.isCorrect ? "Верно" : "Ошибка"}</Badge>
                  <span>{decisionLabel(resolution.decision)}</span>
                  <span>Δ {resolution.scoreDelta}</span>
                </div>
                <p>{resolution.explanation}</p>
              </article>
            ))}
          </div>

          <div className="status-inline">{report.recommendation}</div>
        </Panel>
      ) : null}
    </div>
  );
}
