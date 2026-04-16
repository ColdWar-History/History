import { useEffect, useState } from "react";

import { Badge, ErrorBlock, Field, LoadingBlock, PageIntro, Panel } from "../../components/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { api } from "../../lib/api";
import type {
  CipherCard,
  Collection,
  HistoricalEvent,
  UpsertCipherCardRequest,
  UpsertCollectionRequest,
  UpsertHistoricalEventRequest
} from "../../lib/types";
import { formatApiError, fromCsv, publicationLabel, publicationTone, toggleListValue, toCsv } from "../../lib/utils";

type AdminTab = "ciphers" | "events" | "collections";

function createCipherDraft(editedBy: string): UpsertCipherCardRequest {
  return {
    code: "",
    name: "",
    category: "",
    era: "",
    difficulty: 1,
    summary: "",
    description: "",
    example: "",
    relatedEventIds: [],
    editedBy,
    changeSummary: "Создано из фронтенда"
  };
}

function createEventDraft(): UpsertHistoricalEventRequest {
  return {
    title: "",
    date: "",
    region: "",
    topic: "",
    summary: "",
    description: "",
    participants: [],
    cipherCodes: []
  };
}

function createCollectionDraft(): UpsertCollectionRequest {
  return {
    title: "",
    theme: "",
    summary: "",
    eventIds: [],
    cipherCodes: []
  };
}

export function AdminPage() {
  const auth = useAuth();
  const editorName = auth.user?.userName ?? "editor";
  const [tab, setTab] = useState<AdminTab>("ciphers");
  const [ciphers, setCiphers] = useState<CipherCard[]>([]);
  const [events, setEvents] = useState<HistoricalEvent[]>([]);
  const [collections, setCollections] = useState<Collection[]>([]);
  const [selectedCipherId, setSelectedCipherId] = useState<string | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [selectedCollectionId, setSelectedCollectionId] = useState<string | null>(null);
  const [cipherForm, setCipherForm] = useState<UpsertCipherCardRequest>(() => createCipherDraft(editorName));
  const [eventForm, setEventForm] = useState<UpsertHistoricalEventRequest>(() => createEventDraft());
  const [collectionForm, setCollectionForm] = useState<UpsertCollectionRequest>(() => createCollectionDraft());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function loadAll() {
    setLoading(true);
    setError(null);

    try {
      const [nextCiphers, nextEvents, nextCollections] = await Promise.all([
        api.content.getCiphers({ publishedOnly: false }),
        api.content.getEvents({ publishedOnly: false }),
        api.content.getCollections(false)
      ]);
      setCiphers(nextCiphers);
      setEvents(nextEvents);
      setCollections(nextCollections);
    } catch (loadError) {
      setError(formatApiError(loadError));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadAll();
  }, []);

  useEffect(() => {
    const selected = ciphers.find((item) => item.id === selectedCipherId);
    if (!selected) {
      setCipherForm(createCipherDraft(editorName));
      return;
    }

    setCipherForm({
      code: selected.code,
      name: selected.name,
      category: selected.category,
      era: selected.era,
      difficulty: selected.difficulty,
      summary: selected.summary,
      description: selected.description,
      example: selected.example,
      relatedEventIds: selected.relatedEventIds,
      editedBy: editorName,
      changeSummary: "Обновлено из фронтенда"
    });
  }, [ciphers, editorName, selectedCipherId]);

  useEffect(() => {
    const selected = events.find((item) => item.id === selectedEventId);
    if (!selected) {
      setEventForm(createEventDraft());
      return;
    }

    setEventForm({
      title: selected.title,
      date: selected.date,
      region: selected.region,
      topic: selected.topic,
      summary: selected.summary,
      description: selected.description,
      participants: selected.participants,
      cipherCodes: selected.cipherCodes
    });
  }, [events, selectedEventId]);

  useEffect(() => {
    const selected = collections.find((item) => item.id === selectedCollectionId);
    if (!selected) {
      setCollectionForm(createCollectionDraft());
      return;
    }

    setCollectionForm({
      title: selected.title,
      theme: selected.theme,
      summary: selected.summary,
      eventIds: selected.eventIds,
      cipherCodes: selected.cipherCodes
    });
  }, [collections, selectedCollectionId]);

  async function saveCipher() {
    setSaving(true);
    setError(null);
    setNotice(null);

    try {
      const saved = selectedCipherId
        ? await api.content.updateCipher(selectedCipherId, cipherForm)
        : await api.content.createCipher(cipherForm);
      setSelectedCipherId(saved.id);
      setNotice("Карточка шифра сохранена.");
      await loadAll();
    } catch (saveError) {
      setError(formatApiError(saveError));
    } finally {
      setSaving(false);
    }
  }

  async function saveEvent() {
    setSaving(true);
    setError(null);
    setNotice(null);

    try {
      const saved = selectedEventId
        ? await api.content.updateEvent(selectedEventId, eventForm)
        : await api.content.createEvent(eventForm);
      setSelectedEventId(saved.id);
      setNotice("Историческое событие сохранено.");
      await loadAll();
    } catch (saveError) {
      setError(formatApiError(saveError));
    } finally {
      setSaving(false);
    }
  }

  async function saveCollection() {
    setSaving(true);
    setError(null);
    setNotice(null);

    try {
      const saved = selectedCollectionId
        ? await api.content.updateCollection(selectedCollectionId, collectionForm)
        : await api.content.createCollection(collectionForm);
      setSelectedCollectionId(saved.id);
      setNotice("Подборка сохранена.");
      await loadAll();
    } catch (saveError) {
      setError(formatApiError(saveError));
    } finally {
      setSaving(false);
    }
  }

  async function changePublication(type: AdminTab, id: string, status: "Published" | "Draft") {
    setSaving(true);
    setError(null);
    setNotice(null);

    try {
      if (type === "ciphers") {
        await api.content.publishCipher(id, status);
      } else if (type === "events") {
        await api.content.publishEvent(id, status);
      } else {
        await api.content.publishCollection(id, status);
      }

      setNotice(`Статус изменён на ${status}.`);
      await loadAll();
    } catch (publishError) {
      setError(formatApiError(publishError));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="Editor Console"
        title="Контентный CRUD для ролей editor и admin."
        description="Обычному пользователю экран не виден. Здесь можно создавать, редактировать и публиковать шифры, события и подборки через gateway."
      />

      {loading ? <LoadingBlock label="Загружаю весь контент без фильтра publishedOnly..." /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? <div className="status-inline">{notice}</div> : null}

      <div className="tab-strip">
        <button className={tab === "ciphers" ? "tab-button is-active" : "tab-button"} onClick={() => setTab("ciphers")} type="button">
          Шифры
        </button>
        <button className={tab === "events" ? "tab-button is-active" : "tab-button"} onClick={() => setTab("events")} type="button">
          События
        </button>
        <button className={tab === "collections" ? "tab-button is-active" : "tab-button"} onClick={() => setTab("collections")} type="button">
          Подборки
        </button>
      </div>

      {tab === "ciphers" ? (
        <div className="split-grid admin-layout">
          <Panel subtitle="Список карточек" title="Шифры">
            <div className="card-actions">
              <button
                className="button ghost"
                onClick={() => {
                  setSelectedCipherId(null);
                  setCipherForm(createCipherDraft(editorName));
                }}
                type="button"
              >
                Новая карточка
              </button>
            </div>

            <div className="queue-list">
              {ciphers.map((cipher) => (
                <button className={`queue-item ${cipher.id === selectedCipherId ? "is-active" : ""}`} key={cipher.id} onClick={() => setSelectedCipherId(cipher.id)} type="button">
                  <strong>{cipher.name}</strong>
                  <small>{publicationLabel(cipher.publicationStatus)}</small>
                </button>
              ))}
            </div>
          </Panel>

          <Panel subtitle="Редактирование карточки шифра" title={selectedCipherId ? "Изменить шифр" : "Новый шифр"}>
            <div className="stack-form">
              <div className="form-grid form-grid-2">
                <Field label="Code">
                  <input
                    disabled={Boolean(selectedCipherId)}
                    onChange={(event) => setCipherForm((current) => ({ ...current, code: event.target.value }))}
                    value={cipherForm.code}
                  />
                </Field>
                <Field label="Название">
                  <input onChange={(event) => setCipherForm((current) => ({ ...current, name: event.target.value }))} value={cipherForm.name} />
                </Field>
                <Field label="Категория">
                  <input onChange={(event) => setCipherForm((current) => ({ ...current, category: event.target.value }))} value={cipherForm.category} />
                </Field>
                <Field label="Эпоха">
                  <input onChange={(event) => setCipherForm((current) => ({ ...current, era: event.target.value }))} value={cipherForm.era} />
                </Field>
                <Field label="Сложность">
                  <input
                    min={1}
                    onChange={(event) => setCipherForm((current) => ({ ...current, difficulty: Number(event.target.value) || 1 }))}
                    type="number"
                    value={cipherForm.difficulty}
                  />
                </Field>
                <Field label="Edited by">
                  <input onChange={(event) => setCipherForm((current) => ({ ...current, editedBy: event.target.value }))} value={cipherForm.editedBy} />
                </Field>
              </div>

              <Field label="Summary">
                <textarea onChange={(event) => setCipherForm((current) => ({ ...current, summary: event.target.value }))} rows={3} value={cipherForm.summary} />
              </Field>
              <Field label="Description">
                <textarea onChange={(event) => setCipherForm((current) => ({ ...current, description: event.target.value }))} rows={6} value={cipherForm.description} />
              </Field>
              <Field label="Example">
                <textarea onChange={(event) => setCipherForm((current) => ({ ...current, example: event.target.value }))} rows={3} value={cipherForm.example} />
              </Field>
              <Field label="Change summary">
                <input
                  onChange={(event) => setCipherForm((current) => ({ ...current, changeSummary: event.target.value }))}
                  value={cipherForm.changeSummary}
                />
              </Field>

              <Field hint="GUID-список задаётся чекбоксами из исторических событий." label="Связанные события">
                <div className="checkbox-grid">
                  {events.map((event) => (
                    <label className="checkbox-row" key={event.id}>
                      <input
                        checked={cipherForm.relatedEventIds.includes(event.id)}
                        onChange={() =>
                          setCipherForm((current) => ({
                            ...current,
                            relatedEventIds: toggleListValue(current.relatedEventIds, event.id)
                          }))
                        }
                        type="checkbox"
                      />
                      <span>{event.title}</span>
                    </label>
                  ))}
                </div>
              </Field>

              <div className="card-actions">
                <button className="button" disabled={saving} onClick={() => void saveCipher()} type="button">
                  {saving ? "Сохраняю..." : "Сохранить"}
                </button>
                {selectedCipherId ? (
                  <button
                    className="button secondary"
                    disabled={saving}
                    onClick={() =>
                      void changePublication(
                        "ciphers",
                        selectedCipherId,
                        ciphers.find((item) => item.id === selectedCipherId)?.publicationStatus === "Published" ? "Draft" : "Published"
                      )
                    }
                    type="button"
                  >
                    {ciphers.find((item) => item.id === selectedCipherId)?.publicationStatus === "Published" ? "Снять с публикации" : "Опубликовать"}
                  </button>
                ) : null}
              </div>
            </div>
          </Panel>
        </div>
      ) : null}

      {tab === "events" ? (
        <div className="split-grid admin-layout">
          <Panel subtitle="Список исторических событий" title="События">
            <div className="card-actions">
              <button
                className="button ghost"
                onClick={() => {
                  setSelectedEventId(null);
                  setEventForm(createEventDraft());
                }}
                type="button"
              >
                Новое событие
              </button>
            </div>

            <div className="queue-list">
              {events.map((event) => (
                <button className={`queue-item ${event.id === selectedEventId ? "is-active" : ""}`} key={event.id} onClick={() => setSelectedEventId(event.id)} type="button">
                  <strong>{event.title}</strong>
                  <small>{publicationLabel(event.publicationStatus)}</small>
                </button>
              ))}
            </div>
          </Panel>

          <Panel subtitle="Редактирование исторического события" title={selectedEventId ? "Изменить событие" : "Новое событие"}>
            <div className="stack-form">
              <div className="form-grid form-grid-2">
                <Field label="Заголовок">
                  <input onChange={(event) => setEventForm((current) => ({ ...current, title: event.target.value }))} value={eventForm.title} />
                </Field>
                <Field label="Дата">
                  <input onChange={(event) => setEventForm((current) => ({ ...current, date: event.target.value }))} type="date" value={eventForm.date} />
                </Field>
                <Field label="Регион">
                  <input onChange={(event) => setEventForm((current) => ({ ...current, region: event.target.value }))} value={eventForm.region} />
                </Field>
                <Field label="Тема">
                  <input onChange={(event) => setEventForm((current) => ({ ...current, topic: event.target.value }))} value={eventForm.topic} />
                </Field>
              </div>

              <Field label="Summary">
                <textarea onChange={(event) => setEventForm((current) => ({ ...current, summary: event.target.value }))} rows={3} value={eventForm.summary} />
              </Field>
              <Field label="Description">
                <textarea onChange={(event) => setEventForm((current) => ({ ...current, description: event.target.value }))} rows={6} value={eventForm.description} />
              </Field>
              <Field hint="Через запятую" label="Participants">
                <input onChange={(event) => setEventForm((current) => ({ ...current, participants: fromCsv(event.target.value) }))} value={toCsv(eventForm.participants)} />
              </Field>
              <Field hint="Через запятую, например `caesar, vigenere`" label="Cipher codes">
                <input onChange={(event) => setEventForm((current) => ({ ...current, cipherCodes: fromCsv(event.target.value) }))} value={toCsv(eventForm.cipherCodes)} />
              </Field>

              <div className="card-actions">
                <button className="button" disabled={saving} onClick={() => void saveEvent()} type="button">
                  {saving ? "Сохраняю..." : "Сохранить"}
                </button>
                {selectedEventId ? (
                  <button
                    className="button secondary"
                    disabled={saving}
                    onClick={() =>
                      void changePublication(
                        "events",
                        selectedEventId,
                        events.find((item) => item.id === selectedEventId)?.publicationStatus === "Published" ? "Draft" : "Published"
                      )
                    }
                    type="button"
                  >
                    {events.find((item) => item.id === selectedEventId)?.publicationStatus === "Published" ? "Снять с публикации" : "Опубликовать"}
                  </button>
                ) : null}
              </div>
            </div>
          </Panel>
        </div>
      ) : null}

      {tab === "collections" ? (
        <div className="split-grid admin-layout">
          <Panel subtitle="Список подборок" title="Подборки">
            <div className="card-actions">
              <button
                className="button ghost"
                onClick={() => {
                  setSelectedCollectionId(null);
                  setCollectionForm(createCollectionDraft());
                }}
                type="button"
              >
                Новая подборка
              </button>
            </div>

            <div className="queue-list">
              {collections.map((collection) => (
                <button
                  className={`queue-item ${collection.id === selectedCollectionId ? "is-active" : ""}`}
                  key={collection.id}
                  onClick={() => setSelectedCollectionId(collection.id)}
                  type="button"
                >
                  <strong>{collection.title}</strong>
                  <small>{publicationLabel(collection.publicationStatus)}</small>
                </button>
              ))}
            </div>
          </Panel>

          <Panel subtitle="Редактирование подборки" title={selectedCollectionId ? "Изменить подборку" : "Новая подборка"}>
            <div className="stack-form">
              <div className="form-grid form-grid-2">
                <Field label="Название">
                  <input onChange={(event) => setCollectionForm((current) => ({ ...current, title: event.target.value }))} value={collectionForm.title} />
                </Field>
                <Field label="Тема">
                  <input onChange={(event) => setCollectionForm((current) => ({ ...current, theme: event.target.value }))} value={collectionForm.theme} />
                </Field>
              </div>

              <Field label="Summary">
                <textarea onChange={(event) => setCollectionForm((current) => ({ ...current, summary: event.target.value }))} rows={4} value={collectionForm.summary} />
              </Field>

              <Field label="События">
                <div className="checkbox-grid">
                  {events.map((event) => (
                    <label className="checkbox-row" key={event.id}>
                      <input
                        checked={collectionForm.eventIds.includes(event.id)}
                        onChange={() =>
                          setCollectionForm((current) => ({
                            ...current,
                            eventIds: toggleListValue(current.eventIds, event.id)
                          }))
                        }
                        type="checkbox"
                      />
                      <span>{event.title}</span>
                    </label>
                  ))}
                </div>
              </Field>

              <Field label="Cipher codes">
                <div className="checkbox-grid">
                  {ciphers.map((cipher) => (
                    <label className="checkbox-row" key={cipher.code}>
                      <input
                        checked={collectionForm.cipherCodes.includes(cipher.code)}
                        onChange={() =>
                          setCollectionForm((current) => ({
                            ...current,
                            cipherCodes: toggleListValue(current.cipherCodes, cipher.code)
                          }))
                        }
                        type="checkbox"
                      />
                      <span>{cipher.name}</span>
                    </label>
                  ))}
                </div>
              </Field>

              {selectedCollectionId ? (
                <Badge tone={publicationTone(collections.find((item) => item.id === selectedCollectionId)?.publicationStatus ?? "Draft")}>
                  {publicationLabel(collections.find((item) => item.id === selectedCollectionId)?.publicationStatus ?? "Draft")}
                </Badge>
              ) : null}

              <div className="card-actions">
                <button className="button" disabled={saving} onClick={() => void saveCollection()} type="button">
                  {saving ? "Сохраняю..." : "Сохранить"}
                </button>
                {selectedCollectionId ? (
                  <button
                    className="button secondary"
                    disabled={saving}
                    onClick={() =>
                      void changePublication(
                        "collections",
                        selectedCollectionId,
                        collections.find((item) => item.id === selectedCollectionId)?.publicationStatus === "Published" ? "Draft" : "Published"
                      )
                    }
                    type="button"
                  >
                    {collections.find((item) => item.id === selectedCollectionId)?.publicationStatus === "Published"
                      ? "Снять с публикации"
                      : "Опубликовать"}
                  </button>
                ) : null}
              </div>
            </div>
          </Panel>
        </div>
      ) : null}
    </div>
  );
}
