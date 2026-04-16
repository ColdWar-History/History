import { useDeferredValue, useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { api } from "../../lib/api";
import type { CipherCard } from "../../lib/types";
import { formatApiError } from "../../lib/utils";

export function CiphersPage() {
  const [ciphers, setCiphers] = useState<CipherCard[]>([]);
  const [allCiphers, setAllCiphers] = useState<CipherCard[]>([]);
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [era, setEra] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const deferredSearch = useDeferredValue(search);

  useEffect(() => {
    let ignore = false;

    async function loadOptions() {
      try {
        const data = await api.content.getCiphers();
        if (!ignore) {
          setAllCiphers(data);
        }
      } catch {
        // Options are secondary; the main request below handles user-visible failure.
      }
    }

    void loadOptions();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const data = await api.content.getCiphers({
          search: deferredSearch || undefined,
          category: category || undefined,
          era: era || undefined,
          publishedOnly: true
        });

        if (!ignore) {
          setCiphers(data);
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
  }, [category, deferredSearch, era]);

  const categories = Array.from(new Set(allCiphers.map((item) => item.category))).sort();
  const eras = Array.from(new Set(allCiphers.map((item) => item.era))).sort();

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Cipher Catalog"
        title="Каталог шифров с фильтрами по поиску, категории и эпохе."
        description="Публичная витрина использует только published-данные из content-сервиса. Карточки ведут в детальное досье и прямо в криптолабораторию."
      />

      <Panel subtitle="Фильтры работают через gateway query params" title="Поиск">
        <div className="form-grid form-grid-4">
          <Field label="Поиск">
            <input onChange={(event) => setSearch(event.target.value)} placeholder="Caesar, rail, разведка..." value={search} />
          </Field>

          <Field label="Категория">
            <select onChange={(event) => setCategory(event.target.value)} value={category}>
              <option value="">Все категории</option>
              {categories.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </Field>

          <Field label="Эпоха">
            <select onChange={(event) => setEra(event.target.value)} value={era}>
              <option value="">Все эпохи</option>
              {eras.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </Field>

          <div className="field actions-end">
            <span>Сброс</span>
            <button
              className="button ghost"
              onClick={() => {
                setSearch("");
                setCategory("");
                setEra("");
              }}
              type="button"
            >
              Очистить
            </button>
          </div>
        </div>
      </Panel>

      {loading ? <LoadingBlock label="Формирую каталог шифров..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {!loading && !error ? (
        <section className="cards-grid cards-grid-3">
          {ciphers.map((cipher) => (
            <article className="card-item" key={cipher.id}>
              <div className="card-kicker">
                <span>{cipher.category}</span>
                <strong>Уровень {cipher.difficulty}/3</strong>
              </div>
              <h2>{cipher.name}</h2>
              <p>{cipher.summary}</p>
              <div className="inline-meta">
                <span>{cipher.era}</span>
                <span>{cipher.relatedEventIds.length} связ. событий</span>
              </div>
              <div className="card-actions">
                <Link className="button ghost" to={`/ciphers/${cipher.id}`}>
                  Досье
                </Link>
                <Link className="button secondary" to={`/lab?cipher=${encodeURIComponent(cipher.code)}`}>
                  В лабораторию
                </Link>
              </div>
            </article>
          ))}
        </section>
      ) : null}
    </div>
  );
}
