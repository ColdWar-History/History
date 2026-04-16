import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { Badge, EmptyState, ErrorBlock, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { ApiError, api } from "../../lib/api";
import type { CipherCard, Collection, HistoricalEvent } from "../../lib/types";
import { formatApiError, formatDate, formatDateTime, publicationLabel, publicationTone } from "../../lib/utils";

interface CipherDetailsState {
  cipher: CipherCard;
  events: HistoricalEvent[];
  collections: Collection[];
}

export function CipherDetailsPage() {
  const { cipherId } = useParams();
  const auth = useAuth();
  const [state, setState] = useState<CipherDetailsState | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!cipherId) {
      setLoading(false);
      setError("Идентификатор шифра не передан.");
      return;
    }

    const resolvedCipherId = cipherId;
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const showDrafts = auth.hasRole("editor", "admin");
        const [cipher, events, collections] = await Promise.all([
          api.content.getCipher(resolvedCipherId),
          api.content.getEvents({ publishedOnly: !showDrafts }),
          api.content.getCollections(!showDrafts)
        ]);

        if (!showDrafts && cipher.publicationStatus !== "Published") {
          throw new ApiError(404, "Карточка не опубликована.");
        }

        if (!ignore) {
          setState({
            cipher,
            events: events.filter((item) => cipher.relatedEventIds.includes(item.id)),
            collections: collections.filter((item) => item.cipherCodes.includes(cipher.code))
          });
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
  }, [auth, cipherId]);

  if (loading) {
    return <LoadingBlock label="Поднимаю досье шифра..." />;
  }

  if (error) {
    return <ErrorBlock message={error} />;
  }

  if (!state) {
    return <EmptyState actionLabel="Вернуться в каталог" actionTo="/ciphers" description="Карточка не найдена." title="Нет данных" />;
  }

  const { cipher, events, collections } = state;

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow={cipher.code.toUpperCase()}
        title={cipher.name}
        description={cipher.summary}
        actions={
          <>
            <Badge tone={publicationTone(cipher.publicationStatus)}>{publicationLabel(cipher.publicationStatus)}</Badge>
            <Link className="button secondary" to={`/lab?cipher=${encodeURIComponent(cipher.code)}`}>
              Открыть в лаборатории
            </Link>
          </>
        }
      />

      <section className="metric-grid">
        <div className="metric-card">
          <span>Категория</span>
          <strong>{cipher.category}</strong>
        </div>
        <div className="metric-card">
          <span>Эпоха</span>
          <strong>{cipher.era}</strong>
        </div>
        <div className="metric-card">
          <span>Сложность</span>
          <strong>{cipher.difficulty}/3</strong>
        </div>
      </section>

      <div className="split-grid">
        <Panel subtitle="Историческая справка и принцип работы" title="Описание">
          <div className="rich-copy">
            <p>{cipher.description}</p>
            <blockquote>{cipher.example}</blockquote>
          </div>
        </Panel>

        <Panel subtitle="Связанные точки таймлайна" title="Исторические события">
          {events.length === 0 ? (
            <EmptyState description="Для этой карточки пока не привязаны опубликованные события." title="Связей нет" />
          ) : (
            <div className="stack-list">
              {events.map((event) => (
                <article className="stack-card" key={event.id}>
                  <div className="inline-meta">
                    <span>{formatDate(event.date)}</span>
                    <span>{event.region}</span>
                    <span>{event.topic}</span>
                  </div>
                  <h3>{event.title}</h3>
                  <p>{event.summary}</p>
                </article>
              ))}
            </div>
          )}
        </Panel>
      </div>

      <div className="split-grid">
        <Panel subtitle="Где этот шифр включён в подборки" title="Кураторские наборы">
          {collections.length === 0 ? (
            <EmptyState description="Подборки для этой карточки пока не опубликованы." title="Подборок нет" />
          ) : (
            <div className="stack-list">
              {collections.map((collection) => (
                <article className="stack-card" key={collection.id}>
                  <h3>{collection.title}</h3>
                  <p>{collection.summary}</p>
                </article>
              ))}
            </div>
          )}
        </Panel>

        <Panel subtitle="Версионность контентной карточки" title="История правок">
          <div className="stack-list">
            {cipher.versions.map((version) => (
              <article className="stack-card" key={version.versionNumber}>
                <div className="inline-meta">
                  <span>Версия {version.versionNumber}</span>
                  <span>{version.editedBy}</span>
                  <span>{formatDateTime(version.updatedAt)}</span>
                </div>
                <p>{version.changeSummary}</p>
              </article>
            ))}
          </div>
        </Panel>
      </div>
    </div>
  );
}
