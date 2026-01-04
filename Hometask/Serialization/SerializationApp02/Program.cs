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

    // Для CsvHelper можно создать маппинг
    public class FMap : ClassMap<F>
    {
        public FMap()
        {
            Map(m => m.i1).Index(0);
            Map(m => m.i2).Index(1);
            Map(m => m.i3).Index(2);
            Map(m => m.i4).Index(3);
            Map(m => m.i5).Index(4);
        }
    }
}

public class ReflectionCsvSerializer
{
    private static readonly Dictionary<Type, MemberInfo[]> _memberCache = new();
    private static readonly Dictionary<Type, Type[]> _typeCache = new();

    /// <summary>
    /// Сериализация объекта в CSV строку
    /// </summary>
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

    /// <summary>
    /// Десериализация CSV строки в объект
    /// </summary>
    public static T Deserialize<T>(string csv) where T : new()
    {
        if (string.IsNullOrEmpty(csv))
            return default;

        var obj = new T();
        var type = typeof(T);
        var members = GetSerializableMembers(type);
        var memberTypes = GetMemberTypes(type);
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

    /// <summary>
    /// Получение всех публичных полей и свойств для сериализации
    /// </summary>
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

    /// <summary>
    /// Получение типов членов класса
    /// </summary>
    private static Type[] GetMemberTypes(Type type)
    {
        if (_typeCache.TryGetValue(type, out var cachedTypes))
            return cachedTypes;

        var members = GetSerializableMembers(type);
        var types = new Type[members.Length];

        for (int i = 0; i < members.Length; i++)
        {
            types[i] = members[i] switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => typeof(object)
            };
        }

        _typeCache[type] = types;
        return types;
    }

    /// <summary>
    /// Форматирование значения в строку
    /// </summary>
    private static string FormatValue(object value)
    {
        if (value == null) return string.Empty;

        return value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Конвертация строки в нужный тип
    /// </summary>
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

public static class CsvHelperSerializer
{
    private static readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false, // Без заголовков
        Delimiter = ","
    };

    /// <summary>
    /// Сериализация с использованием CsvHelper
    /// </summary>
    public static string Serialize<T>(T obj)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, _config))
        {
            csv.WriteRecord(obj);
            return writer.ToString().TrimEnd('\r', '\n');
        }
    }

    /// <summary>
    /// Сериализация коллекции объектов
    /// </summary>
    public static string Serialize<T>(IEnumerable<T> objects)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, _config))
        {
            csv.WriteRecords(objects);
            return writer.ToString().TrimEnd('\r', '\n');
        }
    }

    /// <summary>
    /// Десериализация с использованием CsvHelper
    /// </summary>
    public static T Deserialize<T>(string csv) where T : new()
    {
        using (var reader = new StringReader(csv))
        using (var csvReader = new CsvReader(reader, _config))
        {
            // Регистрируем маппинг если есть
            csvReader.Context.RegisterClassMap<F.FMap>();

            var records = csvReader.GetRecords<T>();
            return records.FirstOrDefault();
        }
    }

    /// <summary>
    /// Десериализация коллекции
    /// </summary>
    public static List<T> DeserializeList<T>(string csv) where T : new()
    {
        using (var reader = new StringReader(csv))
        using (var csvReader = new CsvReader(reader, _config))
        {
            csvReader.Context.RegisterClassMap<F.FMap>();
            return csvReader.GetRecords<T>().ToList();
        }
    }
}

class Program
{
    static void Main()
    {
        Console.OutputEncoding  = Encoding.UTF8; 

        Console.WriteLine("=== Сравнение производительности трех сериализаторов ===");
        Console.WriteLine("1. Reflection CSV (самописный)");
        Console.WriteLine("2. Newtonsoft.Json (стандартный JSON)");
        Console.WriteLine("3. CsvHelper (оптимизированная библиотека CSV)\n");

        // Создаем объект для тестирования
        var obj = F.Get();

        // Тестируем с разным количеством итераций
        TestWithIterations(obj, 1000);
        TestWithIterations(obj, 10000);
        TestWithIterations(obj, 100000);

        // Дополнительный тест с коллекцией объектов
        TestCollectionPerformance();

        Console.WriteLine("\n=== Итоговые результаты ===");
    }

    static void TestWithIterations(F obj, int iterations)
    {
        Console.WriteLine($"\n--- Тестирование с {iterations} итерациями ---");

        // 1. Reflection CSV
        TestReflectionSerializer(obj, iterations);

        // 2. Newtonsoft.Json
        TestJsonSerializer(obj, iterations);

        // 3. CsvHelper
        TestCsvHelperSerializer(obj, iterations);

        Console.WriteLine();
    }

    static void TestReflectionSerializer(F obj, int iterations)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string csv = string.Empty;

        // Сериализация
        for (int i = 0; i < iterations; i++)
        {
            csv = ReflectionCsvSerializer.Serialize(obj);
        }
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

        // Десериализация
        sw.Restart();
        F deserializedObj = default;
        for (int i = 0; i < iterations; i++)
        {
            deserializedObj = ReflectionCsvSerializer.Deserialize<F>(csv);
        }
        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Reflection CSV: Сериализация={serializeTime}мс, Десериализация={deserializeTime}мс, Всего={serializeTime + deserializeTime}мс");
    }

    static void TestJsonSerializer(F obj, int iterations)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string json = string.Empty;

        // Сериализация
        for (int i = 0; i < iterations; i++)
        {
            json = JsonConvert.SerializeObject(obj);
        }
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

        // Десериализация
        sw.Restart();
        F deserializedObj = default;
        for (int i = 0; i < iterations; i++)
        {
            deserializedObj = JsonConvert.DeserializeObject<F>(json);
        }
        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Newtonsoft.Json: Сериализация={serializeTime}мс, Десериализация={deserializeTime}мс, Всего={serializeTime + deserializeTime}мс");
    }

    static void TestCsvHelperSerializer(F obj, int iterations)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string csv = string.Empty;

        // Сериализация
        for (int i = 0; i < iterations; i++)
        {
            csv = CsvHelperSerializer.Serialize(obj);
        }
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

        // Десериализация
        sw.Restart();
        F deserializedObj = default;
        for (int i = 0; i < iterations; i++)
        {
            deserializedObj = CsvHelperSerializer.Deserialize<F>(csv);
        }
        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"CsvHelper: Сериализация={serializeTime}мс, Десериализация={deserializeTime}мс, Всего={serializeTime + deserializeTime}мс");
    }

    static void TestCollectionPerformance()
    {
        Console.WriteLine("\n--- Тестирование с коллекцией из 1000 объектов ---");

        // Создаем коллекцию из 1000 объектов
        var collection = Enumerable.Range(1, 1000)
            .Select(i => new F { i1 = i, i2 = i * 2, i3 = i * 3, i4 = i * 4, i5 = i * 5 })
            .ToList();

        Stopwatch sw = Stopwatch.StartNew();
        string csv = CsvHelperSerializer.Serialize(collection);
        sw.Stop();
        Console.WriteLine($"CsvHelper сериализация коллекции (1000 объектов): {sw.ElapsedMilliseconds}мс");
        Console.WriteLine($"Длина CSV: {csv.Length} символов");

        sw.Restart();
        string json = JsonConvert.SerializeObject(collection);
        sw.Stop();
        Console.WriteLine($"Newtonsoft.Json сериализация коллекции (1000 объектов): {sw.ElapsedMilliseconds}мс");
        Console.WriteLine($"Длина JSON: {json.Length} символов");

        // Тест Reflection сериализатора для коллекции
        sw.Restart();
        var sb = new StringBuilder();
        foreach (var item in collection)
        {
            sb.AppendLine(ReflectionCsvSerializer.Serialize(item));
        }
        var reflectionCsv = sb.ToString();
        sw.Stop();
        Console.WriteLine($"Reflection CSV сериализация коллекции (1000 объектов): {sw.ElapsedMilliseconds}мс");
    }
}
