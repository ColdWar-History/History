import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { Badge, ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { api } from "../../lib/api";
import type { CipherCatalogItem, CryptoTransformResponse } from "../../lib/types";
import { formatApiError, formatDateTime, modeLabel } from "../../lib/utils";

export function CryptoLabPage() {
  const auth = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [catalog, setCatalog] = useState<CipherCatalogItem[]>([]);
  const [cipherCode, setCipherCode] = useState("");
  const [mode, setMode] = useState<"encrypt" | "decrypt">("encrypt");
  const [input, setInput] = useState("");
  const [parameters, setParameters] = useState<Record<string, string>>({});
  const [result, setResult] = useState<CryptoTransformResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const data = await api.crypto.getCatalog();
        if (!ignore) {
          setCatalog(data);
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

  useEffect(() => {
    if (catalog.length === 0) {
      return;
    }

    setCipherCode(searchParams.get("cipher") || catalog[0]?.code || "");
    setMode(searchParams.get("mode") === "decrypt" ? "decrypt" : "encrypt");
    setInput(searchParams.get("input") || "");
  }, [catalog, searchParams]);

  const currentCipher = catalog.find((item) => item.code === cipherCode) ?? catalog[0] ?? null;

  useEffect(() => {
    if (!currentCipher) {
      return;
    }

    setParameters((current) => {
      const next: Record<string, string> = {};
      for (const parameter of currentCipher.parameters) {
        next[parameter.name] = current[parameter.name] ?? searchParams.get(parameter.name) ?? "";
      }
      return next;
    });
  }, [currentCipher, searchParams]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!currentCipher) {
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const response = await api.crypto.transform({
        cipherCode: currentCipher.code,
        mode,
        input,
        parameters
      });

      setResult(response);
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        next.set("cipher", currentCipher.code);
        next.set("mode", mode);
        next.set("input", input);
        return next;
      });
    } catch (submitError) {
      setError(formatApiError(submitError));
      setResult(null);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCopy() {
    if (!result?.output) {
      return;
    }

    await navigator.clipboard.writeText(result.output);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1800);
  }

  function handleSwap() {
    const nextInput = result?.output || input;
    setInput(nextInput);
    setMode((current) => (current === "encrypt" ? "decrypt" : "encrypt"));
    setResult(null);
  }

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Crypto Lab"
        title="Параметризуемая лаборатория шифрования и дешифрования."
        description="Экран использует `/api/crypto/catalog` для динамической формы и `/api/crypto/transform` для результата со steps[]. Если пользователь вошёл, операция автоматически попадёт в историю профиля."
        actions={
          auth.isAuthenticated ? (
            <Link className="button secondary" to="/profile">
              Открыть профиль
            </Link>
          ) : (
            <Link className="button secondary" to="/auth?redirect=/lab">
              Войти для сохранения истории
            </Link>
          )
        }
      />

      {loading ? <LoadingBlock label="Загружаю каталог алгоритмов..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {!loading && currentCipher ? (
        <div className="split-grid lab-layout">
          <Panel subtitle="Форма строится из catalog parameters" title="Операция">
            <form className="stack-form" onSubmit={handleSubmit}>
              <div className="form-grid form-grid-2">
                <Field label="Шифр">
                  <select onChange={(event) => setCipherCode(event.target.value)} value={currentCipher.code}>
                    {catalog.map((item) => (
                      <option key={item.code} value={item.code}>
                        {item.name}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Режим">
                  <select onChange={(event) => setMode(event.target.value as "encrypt" | "decrypt")} value={mode}>
                    <option value="encrypt">Шифровать</option>
                    <option value="decrypt">Расшифровывать</option>
                  </select>
                </Field>
              </div>

              <div className="inline-meta">
                <Badge>{currentCipher.category}</Badge>
                <Badge>{currentCipher.era}</Badge>
                <Badge>Сложность {currentCipher.difficulty}/3</Badge>
              </div>

              <Field hint="Произвольный текст, который уйдёт в body как `input`." label="Сообщение">
                <textarea onChange={(event) => setInput(event.target.value)} placeholder="Введите текст..." rows={7} value={input} />
              </Field>

              {currentCipher.parameters.length > 0 ? (
                <div className="form-grid form-grid-2">
                  {currentCipher.parameters.map((parameter) => (
                    <Field hint={parameter.description || undefined} key={parameter.name} label={parameter.label}>
                      <input
                        onChange={(event) =>
                          setParameters((current) => ({
                            ...current,
                            [parameter.name]: event.target.value
                          }))
                        }
                        placeholder={parameter.type === "integer" ? "Введите число" : "Введите значение"}
                        value={parameters[parameter.name] ?? ""}
                      />
                    </Field>
                  ))}
                </div>
              ) : (
                <div className="status-inline">У выбранного шифра нет дополнительных параметров.</div>
              )}

              <div className="card-actions">
                <button className="button" disabled={submitting} type="submit">
                  {submitting ? "Выполняю..." : mode === "encrypt" ? "Зашифровать" : "Расшифровать"}
                </button>
                <button className="button ghost" onClick={handleSwap} type="button">
                  Поменять местами
                </button>
              </div>
            </form>
          </Panel>

          <Panel subtitle="Backend возвращает output, steps и validationMessages" title="Результат">
            {result ? (
              <div className="stack-list">
                <article className="stack-card result-card">
                  <div className="inline-meta">
                    <Badge>{modeLabel(result.mode)}</Badge>
                    <span>{formatDateTime(result.processedAt)}</span>
                    {result.operationId ? <span>operationId: {result.operationId.slice(0, 8)}</span> : null}
                  </div>
                  <h3>Выход</h3>
                  <pre className="output-box">{result.output}</pre>
                  <div className="card-actions">
                    <button className="button secondary" onClick={() => void handleCopy()} type="button">
                      {copied ? "Скопировано" : "Копировать"}
                    </button>
                  </div>
                </article>

                {result.validationMessages.length > 0 ? (
                  <article className="stack-card">
                    <h3>Validation messages</h3>
                    <ul className="simple-list">
                      {result.validationMessages.map((message) => (
                        <li key={message}>{message}</li>
                      ))}
                    </ul>
                  </article>
                ) : null}

                <article className="stack-card">
                  <h3>Пошаговое объяснение</h3>
                  <div className="steps-list">
                    {result.steps.map((step) => (
                      <article className="step-card" key={step.order}>
                        <span>{step.order}</span>
                        <div>
                          <strong>{step.title}</strong>
                          <p>{step.description}</p>
                          <pre>{step.snapshot}</pre>
                        </div>
                      </article>
                    ))}
                  </div>
                </article>
              </div>
            ) : (
              <div className="status-block">
                <strong>Ожидание операции</strong>
                <p>После отправки формы здесь появятся `output`, диагностические сообщения и раскладка шагов алгоритма.</p>
              </div>
            )}
          </Panel>
        </div>
      ) : null}
    </div>
  );
}
