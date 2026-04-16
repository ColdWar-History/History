import { Link } from "react-router-dom";

import { PageIntro } from "../../components/Ui";

export function NotFoundPage() {
  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="404"
        title="Маршрут не найден"
        description="Похоже, этого экрана нет в текущем фронтенде. Вернись на главную или открой каталог материалов."
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
