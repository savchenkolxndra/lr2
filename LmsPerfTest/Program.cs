using System;
using System.Collections.Generic;
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;

namespace LmsPerfTest
{
    class Program
    {
        // Кількість курсів для тесту
        const int CoursesCount = 1000;

        static void Main(string[] args)
        {
            Console.WriteLine("=== Тест MongoDB ===");
            RunMongoTests();

            Console.WriteLine();
            Console.WriteLine("=== Тест PostgreSQL ===");
            RunPostgresTests();

            Console.WriteLine();
            Console.WriteLine("Натисніть Enter для виходу...");
            Console.ReadLine();
        }

        // ------------------------ MONGO ------------------------

        static void RunMongoTests()
        {
            // 1. Підключення до MongoDB
            var mongoConnectionString = "mongodb://localhost:27017"; // за потреби змінити
            var client = new MongoClient(mongoConnectionString);
            var db = client.GetDatabase("lms");
            var courses = db.GetCollection<BsonDocument>("courses");

            // 2. Очистити колекцію
            courses.DeleteMany(FilterDefinition<BsonDocument>.Empty);

            // 3. Тест вставки
            TestMongoInsert(courses);

            // 4. Тест запиту
            TestMongoQuery(courses);
        }

        static void TestMongoInsert(IMongoCollection<BsonDocument> collection)
        {
            var docs = new List<BsonDocument>();

            for (int i = 0; i < CoursesCount; i++)
            {
                // Будуємо спрощений документ курсу з вкладеними структурами
                var doc = new BsonDocument
                {
                    { "title", $"Курс {i}" },
                    { "slug", $"course-{i}" },
                    { "language", "uk" },
                    { "level", i % 2 == 0 ? "beginner" : "intermediate" },
                    { "tags", new BsonArray { "programming", "example" } },
                    {
                        "modules", new BsonArray
                        {
                            new BsonDocument
                            {
                                { "id", "m1" },
                                { "title", "Модуль 1" },
                                { "order", 1 },
                                {
                                    "lessons", new BsonArray
                                    {
                                        new BsonDocument
                                        {
                                            { "id", "l1" },
                                            { "title", "Урок 1" },
                                            { "order", 1 },
                                            {
                                                "blocks", new BsonArray
                                                {
                                                    new BsonDocument
                                                    {
                                                        { "id", "b1" },
                                                        // половина курсів – quiz, половина – text
                                                        { "type", i % 2 == 0 ? "quiz" : "text" },
                                                        {
                                                            "content", new BsonDocument
                                                            {
                                                                { "text", "demo content" }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                docs.Add(doc);
            }

            var sw = Stopwatch.StartNew();
            collection.InsertMany(docs);
            sw.Stop();

            Console.WriteLine($"[MongoDB] Insert {CoursesCount} курсів: {sw.ElapsedMilliseconds} ms");
        }

        static void TestMongoQuery(IMongoCollection<BsonDocument> collection)
        {
            // Фільтр: курси, де є блок типу 'quiz'
            var filter = Builders<BsonDocument>.Filter.Eq("modules.lessons.blocks.type", "quiz");

            var sw = Stopwatch.StartNew();
            var result = collection.Find(filter).ToList();
            sw.Stop();

            Console.WriteLine($"[MongoDB] Query (курси з quiz): {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"[MongoDB] Знайдено курсів з quiz: {result.Count}");
        }

        // ------------------------ POSTGRES ------------------------

        static void RunPostgresTests()
        {
            // Рядок підключення до PostgreSQL
            var connString = "Host=localhost;Port=5432;Database=LmsSql;Username=postgres;Password=password";

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                // 1. Очистити таблиці
                ClearPostgresTables(conn);

                // 2. Тест вставки
                TestPostgresInsert(conn);

                // 3. Тест запиту
                TestPostgresQuery(conn);
            }
        }

        static void ClearPostgresTables(NpgsqlConnection conn)
        {
            using (var cmd = new NpgsqlCommand(
                @"DELETE FROM Block;
                  DELETE FROM Lesson;
                  DELETE FROM Module;
                  DELETE FROM Course;", conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        static void TestPostgresInsert(NpgsqlConnection conn)
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < CoursesCount; i++)
            {
                int courseId;
                int moduleId;
                int lessonId;

                // 1. Вставка Course
                using (var cmd = new NpgsqlCommand(
                    @"INSERT INTO Course (Title, Slug, Language, Level)
                      VALUES (@t, @s, @lang, @lvl)
                      RETURNING Id;", conn))
                {
                    cmd.Parameters.AddWithValue("t", $"Курс {i}");
                    cmd.Parameters.AddWithValue("s", $"course-{i}");
                    cmd.Parameters.AddWithValue("lang", "uk");
                    cmd.Parameters.AddWithValue("lvl", i % 2 == 0 ? "beginner" : "intermediate");

                    courseId = (int)cmd.ExecuteScalar();
                }

                // 2. Вставка Module
                using (var cmd = new NpgsqlCommand(
                    @"INSERT INTO Module (CourseId, InternalId, Title, ""Order"")
                      VALUES (@cid, @iid, @title, @ord)
                      RETURNING Id;", conn))
                {
                    cmd.Parameters.AddWithValue("cid", courseId);
                    cmd.Parameters.AddWithValue("iid", "m1");
                    cmd.Parameters.AddWithValue("title", "Модуль 1");
                    cmd.Parameters.AddWithValue("ord", 1);

                    moduleId = (int)cmd.ExecuteScalar();
                }

                // 3. Вставка Lesson
                using (var cmd = new NpgsqlCommand(
                    @"INSERT INTO Lesson (ModuleId, InternalId, Title, ""Order"")
                      VALUES (@mid, @iid, @title, @ord)
                      RETURNING Id;", conn))
                {
                    cmd.Parameters.AddWithValue("mid", moduleId);
                    cmd.Parameters.AddWithValue("iid", "l1");
                    cmd.Parameters.AddWithValue("title", "Урок 1");
                    cmd.Parameters.AddWithValue("ord", 1);

                    lessonId = (int)cmd.ExecuteScalar();
                }

                // 4. Вставка Block
                using (var cmd = new NpgsqlCommand(
                    @"INSERT INTO Block (LessonId, InternalId, Type, ContentText)
                      VALUES (@lid, @iid, @type, @content);", conn))
                {
                    cmd.Parameters.AddWithValue("lid", lessonId);
                    cmd.Parameters.AddWithValue("iid", "b1");
                    cmd.Parameters.AddWithValue("type", i % 2 == 0 ? "quiz" : "text");
                    cmd.Parameters.AddWithValue("content", "demo content");

                    cmd.ExecuteNonQuery();
                }
            }

            sw.Stop();
            Console.WriteLine($"[Postgres] Insert {CoursesCount} курсів: {sw.ElapsedMilliseconds} ms");
        }

        static void TestPostgresQuery(NpgsqlConnection conn)
        {
            string sql = @"
                SELECT DISTINCT
                    c.Id, c.Title, c.Slug
                FROM Course c
                JOIN Module m ON m.CourseId = c.Id
                JOIN Lesson l ON l.ModuleId = m.Id
                JOIN Block  b ON b.LessonId = l.Id
                WHERE b.Type = 'quiz';";

            int count = 0;
            var sw = Stopwatch.StartNew();

            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    count++;
                }
            }

            sw.Stop();

            Console.WriteLine($"[Postgres] Query (курси з quiz): {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"[Postgres] Знайдено курсів з quiz: {count}");
        }
    }
}
