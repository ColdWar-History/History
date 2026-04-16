export function cn(...values: Array<string | false | null | undefined>): string {
  return values.filter(Boolean).join(" ");
}

export function formatDate(value: string): string {
  return new Intl.DateTimeFormat("ru-RU", {
    day: "2-digit",
    month: "long",
    year: "numeric"
  }).format(new Date(`${value}T00:00:00`));
}

export function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("ru-RU", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

export function formatApiError(error: unknown): string {
  if (typeof error === "object" && error && "message" in error && typeof error.message === "string") {
    return error.message;
  }

  return "Не удалось выполнить запрос к gateway.";
}

export function roleLabel(role: string): string {
  return (
    {
      guest: "Гость",
      user: "Пользователь",
      editor: "Редактор",
      admin: "Администратор"
    }[role] ?? role
  );
}

export function difficultyLabel(value: string): string {
  return (
    {
      easy: "Легко",
      normal: "Нормально",
      hard: "Сложно"
    }[value] ?? value
  );
}

export function publicationLabel(value: string): string {
  return value === "Published" ? "Опубликовано" : "Черновик";
}

export function publicationTone(value: string): "success" | "warning" {
  return value === "Published" ? "success" : "warning";
}

export function modeLabel(value: string): string {
  return value === "decrypt" ? "Расшифровка" : "Шифрование";
}

export function decisionLabel(value: string): string {
  return (
    {
      allow: "Пропустить",
      reject: "Отклонить",
      escalate: "Эскалировать"
    }[value] ?? value
  );
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

export function toCsv(values: string[]): string {
  return values.join(", ");
}

export function fromCsv(value: string): string[] {
  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

export function toggleListValue(list: string[], value: string): string[] {
  return list.includes(value) ? list.filter((item) => item !== value) : [...list, value];
}
