import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { ErrorBlock, LoadingBlock, MetricCard, PageIntro, Panel } from "../../components/Ui";
import { api } from "../../lib/api";
import type { CipherCard, Collection, HistoricalEvent } from "../../lib/types";
import { formatApiError, formatDate } from "../../lib/utils";

interface Snapshot {
  ciphers: CipherCard[];
  events: HistoricalEvent[];
  collections: Collection[];
}

export function HomePage() {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const [ciphers, events, collections] = await Promise.all([
          api.content.getCiphers(),
          api.content.getEvents(),
          api.content.getCollections()
        ]);

        if (!ignore) {
          setSnapshot({ ciphers, events, collections });
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
        eyebrow="Cold War Operational Frontend"
        title="История шифров, таймлайн Холодной войны и игровые миссии в одном интерфейсе."
        description="Фронтенд собран вокруг gateway-контрактов MVP: публичный контент, криптолаборатория, тренировки, daily, инспектор связи, профиль и редакторские действия по ролям."
        actions={
          <>
            <Link className="button" to="/lab">
              Открыть лабораторию
            </Link>
            <Link className="button secondary" to="/ciphers">
              Смотреть каталог
            </Link>
          </>
        }
      />

      {loading ? <LoadingBlock label="Подтягиваю содержимое из content-сервиса..." /> : null}
      {error ? <ErrorBlock message={error} retryLabel="Повторить" onRetry={() => window.location.reload()} /> : null}

      {snapshot ? (
        <>
          <section className="metric-grid">
            <MetricCard detail="Опубликованные карточки" label="Шифры" value={snapshot.ciphers.length} />
            <MetricCard detail="События таймлайна" label="Исторические события" value={snapshot.events.length} />
            <MetricCard detail="Готовые наборы материалов" label="Подборки" value={snapshot.collections.length} />
          </section>

          <section className="cards-grid cards-grid-3">
            <article className="feature-card">
              <span>01</span>
              <h3>Контент и связи</h3>
              <p>Карточки шифров связаны с историческими событиями и подборками, поэтому навигация строится как оперативное досье, а не как разрозненный каталог.</p>
              <Link className="text-link" to="/timeline">
                Открыть таймлайн
              </Link>
            </article>
            <article className="feature-card">
              <span>02</span>
              <h3>Криптолаборатория</h3>
              <p>Параметры тянутся из `/api/crypto/catalog`, а результат отображает выход, шаги алгоритма и ошибки валидации из backend.</p>
              <Link className="text-link" to="/lab">
                Запустить преобразование
              </Link>
            </article>
            <article className="feature-card">
              <span>03</span>
              <h3>Игровой контур</h3>
              <p>Training, daily и смена инспектора работают поверх game-сервиса. При авторизации очки и операции уходят в progress и видны в профиле.</p>
              <Link className="text-link" to="/shift">
                Начать смену
              </Link>
            </article>
          </section>

          <Panel subtitle="Пять опубликованных карточек из seed-набора" title="Ключевые шифры">
            <div className="cards-grid cards-grid-3">
              {snapshot.ciphers.slice(0, 3).map((cipher) => (
                <article className="card-item" key={cipher.id}>
                  <div className="card-kicker">
                    <span>{cipher.category}</span>
                    <strong>Уровень {cipher.difficulty}/3</strong>
                  </div>
                  <h3>{cipher.name}</h3>
                  <p>{cipher.summary}</p>
                  <div className="card-footer">
                    <span>{cipher.era}</span>
                    <Link className="text-link" to={`/ciphers/${cipher.id}`}>
                      Открыть досье
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          </Panel>

          <div className="split-grid">
            <Panel subtitle="Последние узловые точки таймлайна" title="Исторический контекст">
              <div className="timeline-list">
                {snapshot.events
                  .slice()
                  .sort((left, right) => right.date.localeCompare(left.date))
                  .slice(0, 4)
                  .map((event) => (
                    <article className="timeline-mini-item" key={event.id}>
                      <span>{formatDate(event.date)}</span>
                      <h3>{event.title}</h3>
                      <p>{event.summary}</p>
                    </article>
                  ))}
              </div>
            </Panel>

            <Panel subtitle="Кураторские маршруты по материалам" title="Подборки">
              <div className="stack-list">
                {snapshot.collections.map((collection) => (
                  <article className="stack-card" key={collection.id}>
                    <h3>{collection.title}</h3>
                    <p>{collection.summary}</p>
                    <div className="inline-meta">
                      <span>{collection.theme}</span>
                      <span>{collection.cipherCodes.length} шифр(ов)</span>
                    </div>
                  </article>
                ))}
              </div>
            </Panel>
          </div>
        </>
      ) : null}
    </div>
  );
}
