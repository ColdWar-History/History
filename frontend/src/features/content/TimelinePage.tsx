import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Badge, ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { api } from "../../lib/api";
import type { CipherCard, Collection, HistoricalEvent } from "../../lib/types";
import { formatApiError, formatDate } from "../../lib/utils";

interface TimelineState {
  events: HistoricalEvent[];
  collections: Collection[];
  ciphers: CipherCard[];
  allEvents: HistoricalEvent[];
}

export function TimelinePage() {
  const [region, setRegion] = useState("");
  const [year, setYear] = useState("");
  const [topic, setTopic] = useState("");
  const [state, setState] = useState<TimelineState | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const [events, collections, ciphers, allEvents] = await Promise.all([
          api.content.getEvents({
            region: region || undefined,
            year: year ? Number(year) : undefined,
            topic: topic || undefined,
            publishedOnly: true
          }),
          api.content.getCollections(),
          api.content.getCiphers(),
          api.content.getEvents()
        ]);

        if (!ignore) {
          setState({ events, collections, ciphers, allEvents });
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
  }, [region, topic, year]);

  const regions = Array.from(new Set(state?.allEvents.map((item) => item.region) ?? [])).sort();
  const topics = Array.from(new Set(state?.allEvents.map((item) => item.topic) ?? [])).sort();

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Historical Timeline"
        title="Фильтруемый таймлайн по региону, году и теме."
        description="Лента событий использует published-данные content-сервиса и связывает исторические карточки с шифрами и тематическими подборками."
      />

      <Panel subtitle="Фильтры `region`, `year`, `topic` идут напрямую в gateway" title="Навигация по эпохе">
        <div className="form-grid form-grid-4">
          <Field label="Регион">
            <select onChange={(event) => setRegion(event.target.value)} value={region}>
              <option value="">Все регионы</option>
              {regions.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Год">
            <input onChange={(event) => setYear(event.target.value)} placeholder="1962" value={year} />
          </Field>
          <Field label="Тема">
            <select onChange={(event) => setTopic(event.target.value)} value={topic}>
              <option value="">Все темы</option>
              {topics.map((item) => (
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
                setRegion("");
                setYear("");
                setTopic("");
              }}
              type="button"
            >
              Сбросить
            </button>
          </div>
        </div>
      </Panel>

      {loading ? <LoadingBlock label="Разворачиваю исторический таймлайн..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      {state ? (
        <>
          <Panel subtitle="Подборки сохраняют curated-маршрут по событиям" title="Тематические коллекции">
            <div className="cards-grid cards-grid-3">
              {state.collections.map((collection) => (
                <article className="card-item" key={collection.id}>
                  <div className="card-kicker">
                    <span>{collection.theme}</span>
                    <strong>{collection.eventIds.length} событий</strong>
                  </div>
                  <h3>{collection.title}</h3>
                  <p>{collection.summary}</p>
                  <div className="inline-meta">
                    {collection.cipherCodes.map((code) => (
                      <Badge key={code}>{code}</Badge>
                    ))}
                  </div>
                </article>
              ))}
            </div>
          </Panel>

          <Panel subtitle={`Найдено событий: ${state.events.length}`} title="Хроника">
            <div className="timeline-full">
              {state.events.map((event) => (
                <article className="timeline-card" key={event.id}>
                  <div className="timeline-stamp">
                    <strong>{formatDate(event.date)}</strong>
                    <span>{event.region}</span>
                  </div>
                  <div className="timeline-body">
                    <div className="inline-meta">
                      <Badge>{event.topic}</Badge>
                      {event.participants.map((participant) => (
                        <span className="meta-chip" key={participant}>
                          {participant}
                        </span>
                      ))}
                    </div>
                    <h3>{event.title}</h3>
                    <p>{event.description}</p>
                    <div className="inline-meta">
                      {event.cipherCodes.map((code) => {
                        const relatedCipher = state.ciphers.find((item) => item.code === code);
                        return relatedCipher ? (
                          <Link className="text-link" key={code} to={`/ciphers/${relatedCipher.id}`}>
                            {relatedCipher.name}
                          </Link>
                        ) : (
                          <span className="meta-chip" key={code}>
                            {code}
                          </span>
                        );
                      })}
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </Panel>
        </>
      ) : null}
    </div>
  );
}
