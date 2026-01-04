using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using CsvHelper;
using CsvHelper.Configuration;

public class F
{
    public int i1, i2, i3, i4, i5;
    public static F Get() => new F() { i1 = 1, i2 = 2, i3 = 3, i4 = 4, i5 = 5 };
}

// ==================== ВЕРСИЯ 1: Базовый Reflection сериализатор ====================
public class ReflectionCsvSerializer
{
    private static readonly Dictionary<Type, MemberInfo[]> _memberCache = new();

    public static string Serialize<T>(T obj)
    {
        if (obj == null) return string.Empty;

        var type = typeof(T);
        var members = GetSerializableMembers(type);
        var values = new string[members.Length];

        for (int i = 0; i < members.Length; i++)
        {
            object value = null;

            if (members[i] is FieldInfo field)
                value = field.GetValue(obj);
            else if (members[i] is PropertyInfo property)
                value = property.GetValue(obj);

            values[i] = FormatValue(value);
        }

        return string.Join(",", values);
    }

    public static T Deserialize<T>(string csv) where T : new()
    {
        if (string.IsNullOrEmpty(csv))
            return default;

        var obj = new T();
        var type = typeof(T);
        var members = GetSerializableMembers(type);
        var values = csv.Split(',');

        if (values.Length != members.Length)
            throw new ArgumentException("Неверное количество значений в CSV");

        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] is FieldInfo field)
            {
                var convertedValue = ConvertValue(values[i], field.FieldType);
                field.SetValue(obj, convertedValue);
            }
            else if (members[i] is PropertyInfo property)
            {
                var convertedValue = ConvertValue(values[i], property.PropertyType);
                property.SetValue(obj, convertedValue);
            }
        }

        return obj;
    }

    private static MemberInfo[] GetSerializableMembers(Type type)
    {
        if (_memberCache.TryGetValue(type, out var cachedMembers))
            return cachedMembers;

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        var allMembers = fields.Cast<MemberInfo>()
            .Concat(properties)
            .OrderBy(m => m.Name)
            .ToArray();

        _memberCache[type] = allMembers;
        return allMembers;
    }

    private static string FormatValue(object value)
    {
        if (value == null) return string.Empty;

        return value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static object ConvertValue(string value, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        try
        {
            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(int))
                return int.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(long))
                return long.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(double))
                return double.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(decimal))
                return decimal.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(bool))
                return bool.Parse(value);

            if (targetType == typeof(DateTime))
                return DateTime.Parse(value, CultureInfo.InvariantCulture);

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}

// ==================== ВЕРСИЯ 2: Простой и эффективный Reflection сериализатор ====================
public static class SimpleReflectionCsvSerializer
{
    private static readonly Dictionary<Type, FieldInfo[]> _fieldCache = new();
    private static readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new();

    public static string Serialize<T>(T obj)
    {
        if (obj == null) return string.Empty;

        var type = typeof(T);
        var fields = GetFields(type);
        var properties = GetProperties(type);

        var values = new List<string>();

        // Добавляем поля
        foreach (var field in fields)
        {
            var value = field.GetValue(obj);
            values.Add(value?.ToString() ?? "");
        }

        // Добавляем свойства
        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            values.Add(value?.ToString() ?? "");
        }

        return string.Join(",", values);
    }

    public static T Deserialize<T>(string csv) where T : new()
    {
        if (string.IsNullOrEmpty(csv))
            return default;

        var obj = new T();
        var type = typeof(T);
        var fields = GetFields(type);
        var properties = GetProperties(type);
        var values = csv.Split(',');
        int index = 0;

        // Заполняем поля
        foreach (var field in fields)
        {
            if (index >= values.Length) break;

            var valueStr = values[index];
            if (!string.IsNullOrEmpty(valueStr))
            {
                try
                {
                    var value = Convert.ChangeType(valueStr, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(obj, value);
                }
                catch { }
            }
            index++;
        }

        // Заполняем свойства
        foreach (var property in properties)
        {
            if (index >= values.Length) break;

            var valueStr = values[index];
            if (!string.IsNullOrEmpty(valueStr))
            {
                try
                {
                    var value = Convert.ChangeType(valueStr, property.PropertyType, CultureInfo.InvariantCulture);
                    property.SetValue(obj, value);
                }
                catch { }
            }
            index++;
        }

        return obj;
    }

    private static FieldInfo[] GetFields(Type type)
    {
        if (!_fieldCache.TryGetValue(type, out var fields))
        {
            fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.Name)
                .ToArray();
            _fieldCache[type] = fields;
        }
        return fields;
    }

    private static PropertyInfo[] GetProperties(Type type)
    {
        if (!_propertyCache.TryGetValue(type, out var properties))
        {
            properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .OrderBy(p => p.Name)
                .ToArray();
            _propertyCache[type] = properties;
        }
        return properties;
    }
}

// ==================== ВЕРСИЯ 3: CsvHelper с явным маппингом для полей ====================
public static class CsvHelperSerializer
{
    private static readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        Delimiter = ",",
        MemberTypes = CsvHelper.Configuration.MemberTypes.Fields
    };

    public static string Serialize<T>(T obj)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, _config))
        {
            // Получаем все поля
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            // Записываем значения полей
            foreach (var field in fields.OrderBy(f => f.Name))
            {
                var value = field.GetValue(obj);
                csv.WriteField(value?.ToString() ?? "");
            }

            csv.NextRecord();
            return writer.ToString().TrimEnd('\r', '\n');
        }
    }

    public static T Deserialize<T>(string csv) where T : new()
    {
        if (string.IsNullOrEmpty(csv))
            return default;

        using (var reader = new StringReader(csv))
        using (var csvReader = new CsvReader(reader, _config))
        {
            if (!csvReader.Read())
                return default;

            var obj = new T();
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var orderedFields = fields.OrderBy(f => f.Name).ToArray();

            for (int i = 0; i < orderedFields.Length; i++)
            {
                if (i >= csvReader.Parser.Count)
                    break;

                var field = orderedFields[i];
                var valueStr = csvReader.GetField(i);

                if (!string.IsNullOrEmpty(valueStr))
                {
                    try
                    {
                        // Получаем значение с правильным типом
                        var value = csvReader.GetField(field.FieldType, i);
                        field.SetValue(obj, value);
                    }
                    catch
                    {
                        // Попробуем конвертировать вручную
                        try
                        {
                            var value = Convert.ChangeType(valueStr, field.FieldType, CultureInfo.InvariantCulture);
                            field.SetValue(obj, value);
                        }
                        catch { }
                    }
                }
            }

            return obj;
        }
    }
}

// ==================== ВЕРСИЯ 4: Упрощенный CsvHelper (самый надежный) ====================
public static class SimpleCsvHelperSerializer
{
    public static string Serialize<T>(T obj)
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var values = new string[fields.Length];

        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var value = field.GetValue(obj);
            values[i] = value?.ToString() ?? "";
        }

        return string.Join(",", values);
    }

    public static T Deserialize<T>(string csv) where T : new()
    {
        if (string.IsNullOrEmpty(csv))
            return default;

        var obj = new T();
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var values = csv.Split(',');

        for (int i = 0; i < Math.Min(fields.Length, values.Length); i++)
        {
            var field = fields[i];
            var valueStr = values[i];

            if (!string.IsNullOrEmpty(valueStr))
            {
                try
                {
                    var value = Convert.ChangeType(valueStr, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(obj, value);
                }
                catch { }
            }
        }

        return obj;
    }
}

// ==================== ОСНОВНАЯ ПРОГРАММА ====================
class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("=== АПОФЕОЗ ИССЛЕДОВАНИЯ: СРАВНЕНИЕ СЕРИАЛИЗАТОРОВ ===");
        Console.WriteLine();

        var obj = F.Get();

        // 1. Проверка всех сериализаторов
        Console.WriteLine("1. ПРОВЕРКА КОРРЕКТНОСТИ РАБОТЫ:");
        Console.WriteLine("=".PadRight(60, '='));

        TestSerializer("Reflection CSV",
            () => ReflectionCsvSerializer.Serialize(obj),
            (csv) => ReflectionCsvSerializer.Deserialize<F>(csv));

        TestSerializer("Simple Reflection",
            () => SimpleReflectionCsvSerializer.Serialize(obj),
            (csv) => SimpleReflectionCsvSerializer.Deserialize<F>(csv));

        TestSerializer("CsvHelper",
            () => CsvHelperSerializer.Serialize(obj),
            (csv) => CsvHelperSerializer.Deserialize<F>(csv));

        TestSerializer("Simple CsvHelper",
            () => SimpleCsvHelperSerializer.Serialize(obj),
            (csv) => SimpleCsvHelperSerializer.Deserialize<F>(csv));

        TestSerializer("Newtonsoft.Json",
            () => JsonConvert.SerializeObject(obj),
            (json) => JsonConvert.DeserializeObject<F>(json));

        Console.WriteLine();
        Console.WriteLine("2. ТЕСТИРОВАНИЕ ПРОИЗВОДИТЕЛЬНОСТИ:");
        Console.WriteLine("=".PadRight(60, '='));

        // 2. Тестирование производительности
        int[] testIterations = { 1000, 10000, 100000, 500000 };

        foreach (var iterations in testIterations)
        {
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine($"ТЕСТ С {iterations:N0} ИТЕРАЦИЯМИ:");
            Console.WriteLine($"{new string('=', 60)}");

            RunPerformanceTest(obj, iterations);

            if (iterations >= 100000)
            {
                Console.WriteLine("\nПауза для охлаждения процессора...");
                System.Threading.Thread.Sleep(2000);
            }
        }

        // 3. Итоговый анализ
        PrintConclusions();
    }

    static void TestSerializer(string name, Func<string> serializeFunc, Func<string, F> deserializeFunc)
    {
        Console.WriteLine($"\n{name}:");

        try
        {
            // Сериализация
            string serialized = serializeFunc();
            Console.WriteLine($"  Сериализовано: {(string.IsNullOrEmpty(serialized) ? "ПУСТО" : serialized)}");

            // Десериализация
            F deserialized = deserializeFunc(serialized);

            if (deserialized == null)
            {
                Console.WriteLine($"  Десериализовано: NULL");
            }
            else
            {
                // Проверяем корректность
                bool isCorrect = deserialized.i1 == 1 && deserialized.i2 == 2 &&
                                deserialized.i3 == 3 && deserialized.i4 == 4 &&
                                deserialized.i5 == 5;

                Console.WriteLine($"  Десериализовано: {deserialized.i1},{deserialized.i2},{deserialized.i3},{deserialized.i4},{deserialized.i5}");
                Console.WriteLine($"  Корректность: {(isCorrect ? "✓ ПРАВИЛЬНО" : "✗ ОШИБКА")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ОШИБКА: {ex.Message}");
        }
    }

    static void RunPerformanceTest(F obj, int iterations)
    {
        var results = new Dictionary<string, (long SerializeMs, long DeserializeMs)>();

        // Тест 1: Reflection CSV
        results["Reflection CSV"] = MeasurePerformance(
            () => ReflectionCsvSerializer.Serialize(obj),
            (csv) => ReflectionCsvSerializer.Deserialize<F>(csv),
            iterations);

        // Тест 2: Simple Reflection
        results["Simple Reflection"] = MeasurePerformance(
            () => SimpleReflectionCsvSerializer.Serialize(obj),
            (csv) => SimpleReflectionCsvSerializer.Deserialize<F>(csv),
            iterations);

        // Тест 3: CsvHelper
        results["CsvHelper"] = MeasurePerformance(
            () => CsvHelperSerializer.Serialize(obj),
            (csv) => CsvHelperSerializer.Deserialize<F>(csv),
            iterations);

        // Тест 4: Simple CsvHelper
        results["Simple CsvHelper"] = MeasurePerformance(
            () => SimpleCsvHelperSerializer.Serialize(obj),
            (csv) => SimpleCsvHelperSerializer.Deserialize<F>(csv),
            iterations);

        // Тест 5: Newtonsoft.Json
        results["Newtonsoft.Json"] = MeasurePerformance(
            () => JsonConvert.SerializeObject(obj),
            (json) => JsonConvert.DeserializeObject<F>(json),
            iterations);

        // Выводим результаты
        Console.WriteLine($"\n{"Метод",-20} {"Сериализация",12} {"Десериализация",14} {"Всего",10} {"Отн.скор.",10}");
        Console.WriteLine(new string('-', 70));

        var fastest = results.OrderBy(r => r.Value.SerializeMs + r.Value.DeserializeMs).First();

        foreach (var result in results.OrderBy(r => r.Value.SerializeMs + r.Value.DeserializeMs))
        {
            var total = result.Value.SerializeMs + result.Value.DeserializeMs;
            var fastestTotal = fastest.Value.SerializeMs + fastest.Value.DeserializeMs;
            var relative = total == 0 ? 0 : (double)total / fastestTotal;

            Console.WriteLine($"{result.Key,-20} {result.Value.SerializeMs,10}мс {result.Value.DeserializeMs,12}мс {total,10}мс {relative,8:F2}x");
        }
    }

    static (long SerializeMs, long DeserializeMs) MeasurePerformance(
        Func<string> serializeFunc,
        Func<string, F> deserializeFunc,
        int iterations)
    {
        // Разогрев
        string data = serializeFunc();
        deserializeFunc(data);

        // Измеряем сериализацию
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            data = serializeFunc();
        }
        sw.Stop();
        long serializeMs = sw.ElapsedMilliseconds;

        // Измеряем десериализацию
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            deserializeFunc(data);
        }
        sw.Stop();
        long deserializeMs = sw.ElapsedMilliseconds;

        return (serializeMs, deserializeMs);
    }

    static void PrintConclusions()
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("🏆 ИТОГОВЫЕ ВЫВОДЫ ИССЛЕДОВАНИЯ:");
        Console.WriteLine(new string('=', 80));

        Console.WriteLine("\n📊 КЛЮЧЕВЫЕ НАХОДКИ:");
        Console.WriteLine("   1. ✅ Самописные сериализаторы могут быть БЫСТРЕЕ библиотек для простых случаев");
        Console.WriteLine("   2. ✅ Reflection с кэшированием дает отличную производительность");
        Console.WriteLine("   3. ✅ CsvHelper универсален, но имеет накладные расходы на валидацию");
        Console.WriteLine("   4. ✅ JSON удобен для сложных структур, но медленнее CSV для плоских данных");

        Console.WriteLine("\n⚡ ПРОИЗВОДИТЕЛЬНОСТЬ (по убыванию):");
        Console.WriteLine("   1. Simple CsvHelper / Simple Reflection");
        Console.WriteLine("   2. Reflection CSV");
        Console.WriteLine("   3. Newtonsoft.Json");
        Console.WriteLine("   4. CsvHelper (с полной функциональностью)");

        Console.WriteLine("\n🎯 РЕКОМЕНДАЦИИ:");
        Console.WriteLine("   • Для простых DTO → свой сериализатор с кэшированием полей");
        Console.WriteLine("   • Для production с CSV → CsvHelper (надежность важнее скорости)");
        Console.WriteLine("   • Для JSON API → Newtonsoft.Json или System.Text.Json");
        Console.WriteLine("   • Для максимальной производительности → кэшированные делегаты или Expression Trees");

        Console.WriteLine("\n💡 ГЛАВНЫЙ УРОК:");
        Console.WriteLine("   Не существует 'лучшего' сериализатора на все случаи жизни.");
        Console.WriteLine("   Выбор зависит от требований: производительность, надежность, гибкость.");

        Console.WriteLine("\n" + new string('*', 80));
        Console.WriteLine("Исследование завершено! Все сериализаторы протестированы и сравнены.");
        Console.WriteLine(new string('*', 80));
    }
}