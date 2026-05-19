import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { Badge, ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { api } from "../../lib/api";
import type { CipherCatalogItem, CryptoTransformResponse } from "../../lib/types";
import { formatApiError, formatDateTime, modeLabel } from "../../lib/utils";

const limitationTranslations: Record<string, string> = {
  "Works with the Latin A-Z alphabet; letters are uppercased.":
    "Работает с латинским A-Z и русским А-Я/Ё алфавитами; буквы приводятся к верхнему регистру.",
  "Non-Latin letters are left unchanged because they are outside the alphabet table.":
    "Символы вне поддерживаемых алфавитов остаются без изменений.",
  "This is a historical teaching cipher and is trivial to break by frequency analysis or brute force.":
    "Это учебный исторический шифр: его легко взломать перебором сдвига или частотным анализом.",
  "There is no secret key, so anyone who recognizes Atbash can reverse it immediately.":
    "Секретного ключа нет, поэтому распознанный Атбаш сразу обращается обратно.",
  "The key must contain at least one Latin letter; other key characters are ignored.":
    "Ключ должен содержать хотя бы одну латинскую или русскую букву; остальные символы ключа игнорируются.",
  "Only Latin A-Z letters are shifted; punctuation, spaces and digits are preserved and do not advance the key.":
    "Сдвигаются латинские A-Z и русские А-Я/Ё буквы; пробелы, цифры и пунктуация сохраняются и не продвигают ключ.",
  "A repeated key is vulnerable to classical cryptanalysis when enough text is available.":
    "Повторяющийся ключ уязвим к классическому криптоанализу при достаточном объёме текста.",
  "The rails parameter must be an integer greater than 1.": "Параметр рельсов должен быть целым числом больше 1.",
  "Characters are uppercased, but spaces, punctuation and digits are still transposed.":
    "Символы приводятся к верхнему регистру, но пробелы, пунктуация и цифры тоже участвуют в перестановке.",
  "This is a transposition cipher only; if the rail count is known, recovery is straightforward.":
    "Это только шифр перестановки: если известно число рельсов, восстановление обычно несложное.",
  "The key must contain at least one Latin letter; repeated key letters are ordered from left to right.":
    "Ключ должен содержать хотя бы одну латинскую или русскую букву; повторяющиеся буквы упорядочиваются слева направо.",
  "Input is normalized to Latin letters only, so spaces, punctuation, digits and non-Latin letters are removed.":
    "Входной текст нормализуется до латинских или русских букв, поэтому пробелы, пунктуация и цифры удаляются.",
  "The implementation uses an unpadded ragged grid, so decrypt can preserve a real trailing X.":
    "Реализация использует неровную таблицу без добивки, поэтому расшифровка сохраняет настоящий завершающий X."
};

function localizeLimitation(limitation: string): string {
  return limitationTranslations[limitation] ?? limitation;
}

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
        eyebrow="Криптолаборатория"
        title="Параметризуемая лаборатория шифрования и дешифрования."
        description="Выберите алгоритм, заполните параметры и получите результат с пошаговым объяснением преобразования."
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
          <Panel subtitle="Настройте алгоритм и исходное сообщение" title="Операция">
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

              {(currentCipher.limitations?.length ?? 0) > 0 ? (
                <div className="status-block">
                  <strong>Ограничения алгоритма</strong>
                  <ul className="simple-list">
                    {(currentCipher.limitations ?? []).map((limitation) => (
                      <li key={limitation}>{localizeLimitation(limitation)}</li>
                    ))}
                  </ul>
                </div>
              ) : null}

              <Field hint="Произвольный текст для шифрования или расшифровки." label="Сообщение">
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

          <Panel subtitle="Готовый текст и объяснение преобразования" title="Результат">
            {result ? (
              <div className="stack-list">
                <article className="stack-card result-card">
                  <div className="inline-meta">
                    <Badge>{modeLabel(result.mode)}</Badge>
                    <span>{formatDateTime(result.processedAt)}</span>
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
                    <h3>Сообщения проверки</h3>
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
                <p>После отправки формы здесь появятся готовый текст, диагностические сообщения и раскладка шагов алгоритма.</p>
              </div>
            )}
          </Panel>
        </div>
      ) : null}
    </div>
  );
}
