# Лабораторна 2

## 1. Види NoSQL баз даних

Сучасні NoSQL СУБД не дотримуються жорсткої реляційної моделі «таблиці-рядки-стовпці» і зазвичай жертвують частиною класичних можливостей SQL (транзакції, JOIN, строгі схеми) заради гнучкості, масштабованості та продуктивності. Основні типи NoSQL-баз:

**1. Key–Value (ключ–значення)**

**Модель: кожен запис — це пара key -> value, де value може бути рядком, масивом байтів або серіалізованим об’єктом.**

**Приклади: Redis, Riak, Amazon DynamoDB.**

**Сфери застосування: кеші, сесії користувачів, лічильники, тимчасові дані.**

**Для LMS: зберігання сесій студентів, токенів авторизації, кількості переглядів уроків.**

**2. Документні (Document Store)**

**Модель: дані зберігаються у вигляді документів (JSON/BSON), кожен документ може мати довільну структуру.**

**Приклади: MongoDB, CouchDB.**

**Сфери застосування: каталоги товарів, профілі користувачів, складні вкладені структури.**

**Для LMS: структура курсу — модулі, уроки, блоки контенту та питання тестів.**

**3. Колонкові / Wide-Column**

**Модель: таблиці з «колонковими сімействами»; різні рядки можуть мати різні набори колонок.**

**Приклади: Apache Cassandra, HBase.**

**Сфери застосування: великі обсяги логів, time-series, аналітичні події.**

**Для LMS: логування активності студентів (відкриття уроків, проходження тестів) з подальшою аналітикою.**

**4. Графові (Graph DB)**

**Модель: вузли (nodes) та ребра/зв’язки (relationships) з властивостями.**

**Приклади: Neo4j, JanusGraph.**

**Сфери застосування: соціальні мережі, рекомендаційні системи, аналіз зв’язностей.**

**Для LMS: зв’язки між курсами (передумови, «що після чого»), між студентами і курсами, побудова рекомендацій «який курс пройти наступним».**

У даній роботі я використовую:

MongoDB — як документну NoSQL базу;

Neo4j — як графову NoSQL базу для пункту 6.

## 2. Чи доцільно виносити частину даних LMS у NoSQL?

**Предметна область:** система управління навчальними курсами (LMS).

**Основні сутності:**

1)Користувачі (студенти, викладачі, адміністратори)

2)Курси

3)Модулі, уроки, блоки контенту, тести

4)Запис на курс (enrollment), спроби тестів, оцінки

**Що логічно залишити в реляційній БД?**

Для таких даних, як

User, Role, Enrollment, Attempt, Grade, Payment

важлива цілісність, транзакційність і чіткі зв’язки (FOREIGN KEY), тому:

- зручно робити JOIN-и,

- легко забезпечити унікальність і консистентність,

- потрібні складні фільтри («усі студенти групи X, які пройшли курс Y»).

Це класична зона відповідальності SQL (PostgreSQL / SQL Server):)

**Що вигідно винести в NoSQL (MongoDB)?**

Структура курсу:

курс -> модулі -> уроки -> блоки (text, video, quiz і тд) -> питання -> варіанти відповідей.

У реальному житті така структура часто змінюється:

- додаються нові типи блоків (опитування, код із автоперевіркою, інтерактиви),

- змінюється порядок блоків,

- в одних курсів є тести, в інших — ні,

- для відображення курсу на фронтенді часто потрібен цілий курс як одне дерево, а не десяток JOIN-ів.

У реляційній БД це перетворюється на велику кількість таблиць і JOIN-запитів.
У MongoDB це — один документ з вкладеними масивами.

**Отже, висновок:**

Для предметної області LMS доцільно:

- залишити транзакційні» дані в SQL (користувачі, записи на курс, оцінки);

- винести структуру курсів у документну NoSQL-базу (MongoDB).

І загалом виходить:

- гнучку схему та простіше додавання нових типів контенту;

- швидке читання повного курсу одним запитом;

- менше складних JOIN-ів.

 ## 3. Реалізація у NoSQL (MongoDB)

 ### 1. Обраний тип NoSQL

 Обрана документна NoSQL БД — **MongoDB**, оскільки:

- структура курсу природно лягає в JSON-документ;

- вкладені модулі / уроки / блоки зручно зберігати масивами;

- базі не потрібні жорсткі схеми, але можна додати індекси за основними полями.

### 2. Структура колекції courses

У базі lms створена колекція courses.
Один документ = один курс. Узагальнена структура:

 ```
{
  _id: ObjectId('692b41fa9b22881616bc2e9a'),
  title: 'Основи програмування на Python',
  slug: 'python-basics',
  language: 'uk',
  level: 'beginner',
  tags: [
    'programming',
    'python'
  ],
  modules: [
    {
      id: 'm1',
      title: 'Вступ',
      order: 1,
      lessons: [
        {
          id: 'l1',
          title: 'Огляд курсу',
          order: 1,
          blocks: [
            {
              id: 'b1',
              type: 'text',
              content: {
                html: '<p>Ласкаво просимо!</p>'
              }
            },
            {
              id: 'b2',
              type: 'video',
              content: {
                url: 'https://cdn.example.com/videos/welcome.mp4',
                duration_sec: 300
              }
            }
          ]
        },
        {
          id: 'l2',
          title: 'Що таке Python?',
          order: 2,
          blocks: [
            {
              id: 'b3',
              type: 'quiz',
              content: {
                shuffle_questions: true,
                questions: [
                  {
                    id: 'q1',
                    type: 'single_choice',
                    text: 'Для чого найчастіше використовують Python?',
                    options: [
                      {
                        id: 'o1',
                        text: 'Розробка веб-додатків'
                      },
                      {
                        id: 'o2',
                        text: 'Аналіз даних'
                      },
                      {
                        id: 'o3',
                        text: 'Скрипти автоматизації'
                      },
                      {
                        id: 'o4',
                        text: 'Усе з переліченого'
                      }
                    ],
                    correct_option_ids: [
                      'o4'
                    ]
                  }
                ]
              }
            }
          ]
        }
      ]
    }
  ],
  created_at: '2025-11-01T12:00:00Z',
  updated_at: '2025-11-05T10:00:00Z'
}
```
### 3. Індекси

**У MongoDB створені індекси:**

- slug (unique) — для швидкого пошуку курсу;

- tags — для фільтрації за тегами;

- при потребі — по modules.lessons.blocks.type (пошук курсів з тестами).

### 4. Приклади запитів (MongoDB)

**1. Знайти курс за slug:**
<img width="1455" height="758" alt="image" src="https://github.com/user-attachments/assets/129042ee-d5a9-4c38-928c-d3ef8e06d8c4" />
<img width="740" height="865" alt="image" src="https://github.com/user-attachments/assets/8cd6ebf8-d65f-4496-a89c-4c2d53c8668b" />
<img width="824" height="868" alt="image" src="https://github.com/user-attachments/assets/8ca6e44a-82eb-4405-9fe3-9b10f050d299" />
<img width="368" height="90" alt="image" src="https://github.com/user-attachments/assets/4f3b79d6-0c68-4dfa-9f49-f2067a00819f" />

**2. Курси, що містять хоч один блок типу quiz:**
<img width="472" height="273" alt="image" src="https://github.com/user-attachments/assets/63ddb48f-c11a-4d3f-b7a0-e1b4848384c6" />

**3. Отримати конкретний урок l2 курсу python-basics:**
<img width="742" height="482" alt="image" src="https://github.com/user-attachments/assets/21e9d2bd-f105-47cc-bb62-603a3b5e3c71" />
<img width="770" height="866" alt="image" src="https://github.com/user-attachments/assets/ccf2a5d8-fa7b-4e86-8c71-636d62ccd276" />
<img width="558" height="556" alt="image" src="https://github.com/user-attachments/assets/7aa82c30-9a6d-433a-886d-b70a1b946794" />

**4. Додати новий урок у модуль m1:**
```
db.courses.updateOne(
  { slug: "python-basics", "modules.id": "m1" },
  {
    $push: {
      "modules.$.lessons": {
        id: "l2",
        title: "Змінні та типи даних",
        order: 2,
        blocks: []
      }
    }
  }
);
```
Такі запити показують, що структура курсу зручно працює як один документ зі вкладеними масивами.
## 4. Реалізація в SQL (PostgreSQL)
### 1. Схема БД

Для реляційної реалізації використовується PostgreSQL.
Документ course розкладено на кілька пов’язаних таблиць:
```
CREATE TABLE Course (
    Id        SERIAL PRIMARY KEY,
    Title     VARCHAR(200) NOT NULL,
    Slug      VARCHAR(200) NOT NULL UNIQUE,
    Language  VARCHAR(10)  NOT NULL,
    Level     VARCHAR(50),
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE Module (
    Id         SERIAL PRIMARY KEY,
    CourseId   INT NOT NULL REFERENCES Course(Id),
    InternalId VARCHAR(50) NOT NULL,  -- 'm1'
    Title      VARCHAR(200) NOT NULL,
    "Order"    INT NOT NULL
);

CREATE TABLE Lesson (
    Id         SERIAL PRIMARY KEY,
    ModuleId   INT NOT NULL REFERENCES Module(Id),
    InternalId VARCHAR(50) NOT NULL,  -- 'l1'
    Title      VARCHAR(200) NOT NULL,
    "Order"    INT NOT NULL
);

CREATE TABLE Block (
    Id          SERIAL PRIMARY KEY,
    LessonId    INT NOT NULL REFERENCES Lesson(Id),
    InternalId  VARCHAR(50) NOT NULL, -- 'b1'
    Type        VARCHAR(50) NOT NULL, -- 'text', 'quiz' ...
    ContentText TEXT
);
```
### 2. SQL-запити — аналоги до MongoDB
**1. Повна структура курсу за slug:**
```
SELECT
    c.Id          AS CourseId,
    c.Title       AS CourseTitle,
    c.Slug,
    m.Id          AS ModuleId,
    m.InternalId  AS ModuleInternalId,
    m.Title       AS ModuleTitle,
    m."Order"     AS ModuleOrder,
    l.Id          AS LessonId,
    l.InternalId  AS LessonInternalId,
    l.Title       AS LessonTitle,
    l."Order"     AS LessonOrder,
    b.Id          AS BlockId,
    b.InternalId  AS BlockInternalId,
    b.Type        AS BlockType,
    b.ContentText
FROM Course c
LEFT JOIN Module m ON m.CourseId = c.Id
LEFT JOIN Lesson l ON l.ModuleId = m.Id
LEFT JOIN Block  b ON b.LessonId = l.Id
WHERE c.Slug = 'python-basics'
ORDER BY
    m."Order",
    l."Order",
    b.Id;
```
**2. Курси з блоками типу quiz:**
<img width="689" height="391" alt="image" src="https://github.com/user-attachments/assets/53b2425f-03ba-45cc-9187-b0fd30714975" />

**3. Додавання нового уроку до модуля m1:**
<img width="797" height="594" alt="image" src="https://github.com/user-attachments/assets/78984cea-87f2-49a9-8cdb-76cb9244e194" />
Так видно, як один документ MongoDB розкладається на 4 SQL-таблиці з відповідними JOIN-ами.

## 5. Тестування швидкодії (MongoDB vs PostgreSQL)
### 1. Мета:

Порівняти час роботи типових операцій для LMS у двох підходах:

- документна модель (MongoDB),

- реляційна модель (PostgreSQL).

Операції:

- Масова вставка курсів.

- Пошук курсів, що містять блок типу quiz.

Чому саме ці операції:

- вставка відображає сценарій, коли адміністратор імпортує/створює багато курсів;

- пошук курсів з тестами — поширений фільтр у реальній LMS;

- обидві операції присутні і в MongoDB, і в PostgreSQL, що дозволяє коректно порівнювати.

### 2. Тестові дані

Генерується N курсів (у коді легко змінити значення N, наприклад 1000):

Для кожного курсу:

- 1 модуль;

- 1 урок;

- 1 блок (quiz для половини курсів, text — для іншої половини).

Це дає:

1) в MongoDB — N документів у courses;

2) в PostgreSQL — N записів у Course, Module, Lesson, Block.

Обрано N≈1000, бо:

- цього достатньо, щоб побачити різницю у мілісекундах;

- не перевантажує локальну машину;

- при необхідності тест легко масштабувати до 5000/10000.

### 3. Методика вимірювання

Для вимірювання використовується C# + .NET (Stopwatch):

**MongoDB:**

- очищення колекції courses;

- генерація списку з N документів;

- запуск таймера → InsertMany → стоп таймера;

- запуск таймера → Find({"modules.lessons.blocks.type": "quiz"}) → ToList() → стоп таймера.

**PostgreSQL:**

- очищення таблиць Block, Lesson, Module, Course;

- у циклі N разів:

INSERT INTO Course … RETURNING Id;

INSERT INTO Module … RETURNING Id;

INSERT INTO Lesson … RETURNING Id;

INSERT INTO Block …;

- запуск таймера тільки на час вставки;

- для запиту — SELECT DISTINCT c.* FROM Course ... JOIN ... WHERE b.Type = 'quiz' з вимірюванням часу виконання.

Код реалізований у вигляді консольного застосунку, який виводить, наприклад:
<img width="522" height="270" alt="image" src="https://github.com/user-attachments/assets/ca2c472d-08fa-44b8-b127-6317eeb2abac" />
(Код до завдання закріпленний на GIT)

### 4. Висновки

**MongoDB** є більш ефективним у сценаріях, де потрібно швидко завантажувати великі обсяги структурованого навчального контенту (курси → модулі → уроки → блоки).

**PostgreSQL** є більш ефективним у сценаріях точкових аналітичних вибірок, особливо за наявності індексів та нормалізованих таблиць.

Таким чином, гібридна архітектура LMS (MongoDB + PostgreSQL) є оптимальною:
**MongoDB** зберігає структуру курсів, **PostgreSQL** зберігає користувачів, оцінки та транзакційні дані.

## 6. Інша NoSQL-база: графова Neo4j

### 1. Чому саме Neo4j

Для пункту 6 обрана графова NoSQL база Neo4j, оскільки вона добре підходить для моделювання:

зв’язків між курсами (передумови, «який курс після якого»), зв’язків між студентами та курсами, рекомендацій «який курс пройти наступним».

Це те, що в реляційній БД робиться через складні JOIN-и, а в графі виглядає природно.

### 2. Графова модель LMS

```
Вузли (nodes):

(:Course {id, title, level}) — курси;

(:Student {id, name}) — студенти;

(:Topic {name}) — теми/теги курсів.

Зв’язки (relationships):

(:Student)-[:ENROLLED_IN]->(:Course) — студент записаний на курс;

(:Course)-[:HAS_TOPIC]->(:Topic) — курс має тему;

(:Course)-[:PREREQUISITE]->(:Course) — один курс є передумовою іншого.
``` 
Приклад наповнення (Cypher):
```
MATCH (n) DETACH DELETE n;

CREATE
  (c1:Course {id: 1, title: "Основи програмування на Python", level: "beginner"}),
  (c2:Course {id: 2, title: "ООП на Python",                  level: "intermediate"}),
  (c3:Course {id: 3, title: "Основи C#",                      level: "beginner"});

CREATE
  (s1:Student {id: 101, name: "Андрій"}),
  (s2:Student {id: 102, name: "Олександра"}),
  (s3:Student {id: 103, name: "Марія"});

CREATE
  (tVar:Topic   {name: "Змінні"}),
  (tLoop:Topic  {name: "Цикли"}),
  (tFunc:Topic  {name: "Функції"}),
  (tOOP:Topic   {name: "ООП"}),
  (tTypes:Topic {name: "Типи даних"}),
  (tLinq:Topic  {name: "LINQ"});

CREATE
  (c1)-[:HAS_TOPIC]->(tVar),
  (c1)-[:HAS_TOPIC]->(tLoop),
  (c1)-[:HAS_TOPIC]->(tFunc),
  (c2)-[:HAS_TOPIC]->(tOOP),
  (c3)-[:HAS_TOPIC]->(tTypes),
  (c3)-[:HAS_TOPIC]->(tLinq);

CREATE
  (c1)-[:PREREQUISITE]->(c2);

CREATE
  (s1)-[:ENROLLED_IN]->(c1),
  (s2)-[:ENROLLED_IN]->(c1),
  (s2)-[:ENROLLED_IN]->(c3),
  (s3)-[:ENROLLED_IN]->(c3);

```
### 3. Базові запити

**1. Теми курсів:**
```
MATCH (c:Course)-[:HAS_TOPIC]->(t:Topic)
RETURN c.title, t.name;
```
<img width="1049" height="622" alt="image" src="https://github.com/user-attachments/assets/16e086d2-daea-4345-bce2-9c3c721f286e" />
**2. Передумови:**
```
MATCH (a:Course)-[:PREREQUISITE]->(b:Course)
RETURN a.title AS prerequisite, b.title AS next;
```
<img width="978" height="297" alt="image" src="https://github.com/user-attachments/assets/042a2e26-5f68-4fda-a61d-8008bf81787e" />

**3. Студенти, записані на курс "Основи C#":**
```
MATCH (s:Student)-[:ENROLLED_IN]->(c:Course {title: "Основи C#"})
RETURN s.name AS student, c.title AS course;
```
<img width="980" height="262" alt="image" src="https://github.com/user-attachments/assets/b4482c5c-f094-42ac-a6de-95d0c457ad35" />


**4. Рекомендовані наступні курси для студентки Олександри:**
```
MATCH (s:Student {name: "Олександра"})-[:ENROLLED_IN]->(c:Course)
MATCH (c)-[:PREREQUISITE]->(next:Course)
RETURN c.title AS completed, next.title AS recommended_next_course;
```
<img width="1019" height="256" alt="image" src="https://github.com/user-attachments/assets/b6297d0c-1d95-418f-abc7-5097bb8c1251" />
Цей запит демонструє сильну сторону графової БД:
легко знайти шляхи виду «студент → курс → курс-продовження» без складних JOIN-ів.

### 4. Висновок по Neo4j

Neo4j зручно використовувати в LMS для задач:

- побудова рекомендацій «який курс пройти далі»;

- аналіз навчальних траєкторій;

- візуалізація зв’язків між курсами та студентами.

У підсумку:

**MongoDB** — для структури курсів як вкладених документів;

**PostgreSQL** — для транзакційних даних і звітів;

**Neo4j** — для аналізу складних зв’язків та рекомендацій.


