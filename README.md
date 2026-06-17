# Bank Transactions, вариант 9

Учебное приложение по теме "Банковские транзакции".

Группа: 1483-05.

Состав:

- `src/BankTransactions.Server` - консольный HTTP-сервер, работает с SQLite через EF Core ORM и параметризованные SQL-запросы.
- `src/BankTransactions.Client` - оконный Avalonia-клиент, общается с сервером по HTTP.
- `src/BankTransactions.Shared` - общие DTO-контракты клиента и сервера.
- `tests/BankTransactions.Tests` - xUnit unit и сквозные тесты.
- `docs/REPORT.md` - отчет, диаграммы, схема БД, таблица тестов.
- `docs/diagrams-html/index.html` - диаграммы, открываемые в браузере.

## Запуск

Требуется .NET SDK 8.

```bash
dotnet restore
dotnet build BankTransactions.sln
dotnet test BankTransactions.sln
```

Сервер:

```bash
dotnet run --project src/BankTransactions.Server --urls http://localhost:5055
```

Оконный клиент:

```bash
dotnet run --project src/BankTransactions.Client
```

Логин по умолчанию:

- username: `admin`
- password: `admin123`

## Режимы доступа к БД

Все CRUD/search endpoint-ы принимают query-параметр:

- `mode=orm` - EF Core ORM.
- `mode=sql` - параметризованные SQL-команды через ADO.NET connection EF Core.

Пример:

```bash
curl -H "Authorization: Bearer <token>" "http://localhost:5055/api/customers?mode=sql&search=Ivan"
```

## Windows и Visual Studio

Рекомендуемый запуск для защиты: Visual Studio Community 2026 или Visual Studio 2022, рабочие нагрузки `.NET desktop development` и `ASP.NET and web development`, SDK `.NET 8`.

## Диаграммы

Для просмотра диаграмм в браузере открой:

```text
docs/diagrams-html/index.html
```

Исходники диаграмм лежат в `docs/diagrams/*.mmd`.
