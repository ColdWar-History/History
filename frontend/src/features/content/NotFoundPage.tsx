import { Link } from "react-router-dom";

import { PageIntro } from "../../components/Ui";

export function NotFoundPage() {
  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="404"
        title="Маршрут не найден"
        description="Похоже, такой страницы нет. Вернитесь на главную или откройте каталог материалов."
        actions={
          <>
            <Link className="button" to="/">
              На главную
            </Link>
            <Link className="button secondary" to="/ciphers">
              В каталог
            </Link>
          </>
        }
      />
    </div>
  );
}
