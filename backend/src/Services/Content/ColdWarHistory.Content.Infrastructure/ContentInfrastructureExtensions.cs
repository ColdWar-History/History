using ColdWarHistory.Content.Application;
using ColdWarHistory.Content.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ColdWarHistory.Content.Infrastructure;

public static class ContentInfrastructureExtensions
{
    public static IServiceCollection AddContentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IContentRepository, InMemoryContentRepository>();
        services.AddSingleton<IContentService, ContentService>();
        return services;
    }
}

internal sealed class InMemoryContentRepository : IContentRepository
{
    private readonly List<CipherCard> _ciphers;
    private readonly List<HistoricalEvent> _events;
    private readonly List<CuratedCollection> _collections;

    public InMemoryContentRepository()
    {
        _events = SeedEvents();
        _ciphers = SeedCiphers(_events);
        _collections = SeedCollections(_events);
    }

    public Task<IReadOnlyCollection<CipherCard>> GetCiphersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<CipherCard>>(_ciphers.ToArray());

    public Task<CipherCard?> GetCipherAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_ciphers.FirstOrDefault(item => item.Id == id));

    public Task AddCipherAsync(CipherCard cipher, CancellationToken cancellationToken)
    {
        _ciphers.Add(cipher);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<HistoricalEvent>> GetEventsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<HistoricalEvent>>(_events.ToArray());

    public Task<HistoricalEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_events.FirstOrDefault(item => item.Id == id));

    public Task AddEventAsync(HistoricalEvent historicalEvent, CancellationToken cancellationToken)
    {
        _events.Add(historicalEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<CuratedCollection>> GetCollectionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<CuratedCollection>>(_collections.ToArray());

    public Task<CuratedCollection?> GetCollectionAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_collections.FirstOrDefault(item => item.Id == id));

    public Task AddCollectionAsync(CuratedCollection collection, CancellationToken cancellationToken)
    {
        _collections.Add(collection);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static List<CipherCard> SeedCiphers(IReadOnlyCollection<HistoricalEvent> events)
    {
        var map = events.ToDictionary(item => item.Title, item => item.Id);

        return
        [
            CreateCipher("caesar", "Шифр Цезаря", "Подстановка", "Античность и переиспользование в XX веке", 1, "Сдвиг букв на фиксированное число позиций.", "Один из базовых моноалфавитных шифров, часто используется как вводный пример криптоанализа.", "HELLO -> KHOOR", [map["Берлинский кризис 1948 года"]]),
            CreateCipher("atbash", "Атбаш", "Подстановка", "Древность", 1, "Разворот алфавита в обратном порядке.", "Символический и учебный шифр, полезный для понимания симметричного преобразования.", "COLD -> XLOW", [map["Создание НАТО"]]),
            CreateCipher("vigenere", "Шифр Виженера", "Полиалфавитный", "XVI-XX века", 3, "Сдвиг меняется по ключевому слову.", "Классический полиалфавитный шифр, важный для объяснения частотного анализа и длины ключа.", "ATTACK + KEY -> KXRKGI", [map["Карибский кризис"], map["Инцидент с U-2"]]),
            CreateCipher("rail-fence", "Rail Fence", "Перестановка", "XIX-XX века", 2, "Текст пишется зигзагом по рельсам.", "Наглядный маршрутный шифр, хорошо подходит для тренировочных задач.", "WEAREDISCOVERED -> WECRLTEERDSOEEAIVD", [map["Создание Варшавского договора"]]),
            CreateCipher("columnar", "Колонная перестановка", "Перестановка", "XIX-XX века", 3, "Текст записывается в таблицу и читается по перестановке столбцов.", "Подходит для практики с параметризуемыми шифрами и объяснения ролей ключа.", "DEFENDTHEEASTWALL -> DTTFSAEALEHDELWN", [map["Операция АНАДЫРЬ"], map["Падение Берлинской стены"]])
        ];
    }

    private static CipherCard CreateCipher(string code, string name, string category, string era, int difficulty, string summary, string description, string example, IReadOnlyCollection<Guid> relatedEventIds)
    {
        var cipher = new CipherCard(Guid.NewGuid(), code, name, category, era, difficulty, summary, description, example, relatedEventIds, PublicationStatus.Published);
        cipher.AddVersion("system", "Initial MVP seed");
        return cipher;
    }

    private static List<HistoricalEvent> SeedEvents() =>
    [
        CreateEvent("Берлинский кризис 1948 года", new DateOnly(1948, 6, 24), "Европа", "Дипломатия", "Начало блокады Западного Берлина.", "Советский Союз перекрыл сухопутные пути к Западному Берлину, что привело к масштабному воздушному мосту союзников.", ["СССР", "США", "Великобритания"], ["caesar"]),
        CreateEvent("Создание НАТО", new DateOnly(1949, 4, 4), "Европа", "Дипломатия", "Формирование североатлантического союза.", "Военно-политический союз стал важнейшим институтом западного блока во время Холодной войны.", ["США", "Канада", "Великобритания"], ["atbash"]),
        CreateEvent("Корейская война", new DateOnly(1950, 6, 25), "Азия", "Конфликт", "Первый крупный горячий конфликт эпохи.", "Конфликт на Корейском полуострове сделал разведку и защищённые коммуникации критически важными.", ["КНДР", "Республика Корея", "США"], ["vigenere"]),
        CreateEvent("Смерть Сталина", new DateOnly(1953, 3, 5), "СССР", "Политика", "Смена политического курса в СССР.", "После смерти И.В. Сталина началась постепенная перестройка внутриполитического и внешнеполитического курса.", ["СССР"], ["caesar"]),
        CreateEvent("Создание Варшавского договора", new DateOnly(1955, 5, 14), "Европа", "Дипломатия", "Военный блок стран социалистического лагеря.", "Появление договора оформило военно-политическое деление Европы на два лагеря.", ["СССР", "Польша", "ГДР"], ["rail-fence"]),
        CreateEvent("Венгерское восстание", new DateOnly(1956, 10, 23), "Европа", "Конфликт", "Вооружённое выступление в Будапеште.", "События показали пределы либерализации в социалистическом блоке и усилили интерес к информационным операциям.", ["Венгрия", "СССР"], ["columnar"]),
        CreateEvent("Запуск Спутника-1", new DateOnly(1957, 10, 4), "СССР", "Технологии", "Старт космической гонки.", "Первый искусственный спутник Земли стал сильным политическим и научным сигналом всему миру.", ["СССР"], ["atbash"]),
        CreateEvent("Инцидент с U-2", new DateOnly(1960, 5, 1), "СССР", "Разведка", "Сбит американский разведывательный самолёт.", "История с самолётом U-2 усилила напряжение и подчеркнула значимость разведданных и защищённой связи.", ["США", "СССР"], ["vigenere"]),
        CreateEvent("Берлинская стена", new DateOnly(1961, 8, 13), "Европа", "Политика", "Физическое разделение Берлина.", "Возведение стены стало символом раскола Европы и усиления контроля над потоками людей и информации.", ["ГДР", "ФРГ", "СССР"], ["caesar"]),
        CreateEvent("Карибский кризис", new DateOnly(1962, 10, 16), "Карибы", "Конфликт", "Пик ядерного противостояния.", "Секретные переговоры и разведданные сыграли ключевую роль в предотвращении прямой войны.", ["США", "СССР", "Куба"], ["vigenere", "columnar"]),
        CreateEvent("Операция АНАДЫРЬ", new DateOnly(1962, 9, 1), "Карибы", "Разведка", "Скрытая переброска советских ракет на Кубу.", "Операция строилась на маскировке, дезинформации и контроле коммуникаций.", ["СССР", "Куба"], ["columnar"]),
        CreateEvent("Пражская весна", new DateOnly(1968, 1, 5), "Европа", "Политика", "Попытка либерализации в Чехословакии.", "Реформы и их подавление показали роль контроля информации в кризисных политических сценариях.", ["Чехословакия", "СССР"], ["rail-fence"]),
        CreateEvent("Разрядка и ОСВ-1", new DateOnly(1972, 5, 26), "Европа", "Дипломатия", "Подписание первых договоров об ограничении вооружений.", "Этап частичной разрядки сопровождался ростом значения дипломатических каналов и проверки достоверности сигналов.", ["США", "СССР"], ["atbash"]),
        CreateEvent("Афганская война", new DateOnly(1979, 12, 24), "Азия", "Конфликт", "Ввод советских войск в Афганистан.", "Война сопровождалась сложной логистикой, радиоперехватом и анализом сообщений в условиях неопределённости.", ["СССР", "Афганистан"], ["rail-fence"]),
        CreateEvent("Падение Берлинской стены", new DateOnly(1989, 11, 9), "Европа", "Политика", "Символический конец разделённой Европы.", "Падение стены стало одной из ключевых вех завершения Холодной войны.", ["ГДР", "ФРГ"], ["columnar"])
    ];

    private static HistoricalEvent CreateEvent(string title, DateOnly date, string region, string topic, string summary, string description, IReadOnlyCollection<string> participants, IReadOnlyCollection<string> cipherCodes) =>
        new(Guid.NewGuid(), title, date, region, topic, summary, description, participants, cipherCodes, PublicationStatus.Published);

    private static List<CuratedCollection> SeedCollections(IReadOnlyCollection<HistoricalEvent> events)
    {
        var byTitle = events.ToDictionary(item => item.Title, item => item.Id);
        return
        [
            new CuratedCollection(Guid.NewGuid(), "Карибский кризис", "Ядерное противостояние", "Материалы о дипломатии, разведке и скрытых операциях вокруг Карибского кризиса.", [byTitle["Карибский кризис"], byTitle["Операция АНАДЫРЬ"]], ["vigenere", "columnar"], PublicationStatus.Published),
            new CuratedCollection(Guid.NewGuid(), "Разведка и перехват", "Разведоперации", "Подборка кейсов, где ключевую роль сыграли разведданные и защищённая связь.", [byTitle["Инцидент с U-2"], byTitle["Афганская война"], byTitle["Операция АНАДЫРЬ"]], ["vigenere", "rail-fence", "columnar"], PublicationStatus.Published),
            new CuratedCollection(Guid.NewGuid(), "Европа Холодной войны", "Политика и разделение", "Материалы о расколе Европы и политических кризисах.", [byTitle["Берлинский кризис 1948 года"], byTitle["Берлинская стена"], byTitle["Падение Берлинской стены"]], ["caesar", "columnar"], PublicationStatus.Published)
        ];
    }
}
