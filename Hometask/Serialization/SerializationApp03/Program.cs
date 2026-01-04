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

public class ReflectionCsvSerializer
{
    private static readonly Dictionary<Type, MemberInfo[]> _memberCache = new();
    private static readonly Dictionary<Type, Type[]> _typeCache = new();

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

public static class CsvHelperSerializer
{
    private static readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        Delimiter = ","
    };

    public static string Serialize<T>(T obj)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, _config))
        {
            // Получаем все публичные поля
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
        using (var reader = new StringReader(csv))
        using (var csvReader = new CsvReader(reader, _config))
        {
            csvReader.Read();

            var obj = new T();
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var orderedFields = fields.OrderBy(f => f.Name).ToArray();

            for (int i = 0; i < orderedFields.Length; i++)
            {
                var field = orderedFields[i];
                var valueStr = csvReader.GetField(i);

                if (!string.IsNullOrEmpty(valueStr))
                {
                    var value = ConvertValue(valueStr, field.FieldType);
                    field.SetValue(obj, value);
                }
            }

            return obj;
        }
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

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("=== Сравнение производительности трех сериализаторов ===");
        Console.WriteLine("1. Reflection CSV (самописный)");
        Console.WriteLine("2. Newtonsoft.Json (стандартный JSON)");
        Console.WriteLine("3. CsvHelper (оптимизированная библиотека CSV)\n");

        var obj = F.Get();

        // Предварительный тест для проверки работоспособности
        Console.WriteLine("Предварительная проверка:");
        Console.WriteLine($"Reflection CSV: {ReflectionCsvSerializer.Serialize(obj)}");
        Console.WriteLine($"CsvHelper: {CsvHelperSerializer.Serialize(obj)}");
        Console.WriteLine($"Newtonsoft.Json: {JsonConvert.SerializeObject(obj)}");

        // Основные тесты
        TestWithIterations(obj, 1000);
        TestWithIterations(obj, 10000);
        TestWithIterations(obj, 100000);

        Console.WriteLine("\n=== Итоговые результаты ===");
    }

    static void TestWithIterations(F obj, int iterations)
    {
        Console.WriteLine($"\n--- Тестирование с {iterations} итерациями ---");

        TestReflectionSerializer(obj, iterations);
        TestJsonSerializer(obj, iterations);
        TestCsvHelperSerializer(obj, iterations);

        Console.WriteLine();
    }

    static void TestReflectionSerializer(F obj, int iterations)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string csv = string.Empty;

        for (int i = 0; i < iterations; i++)
        {
            csv = ReflectionCsvSerializer.Serialize(obj);
        }
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

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

        for (int i = 0; i < iterations; i++)
        {
            json = JsonConvert.SerializeObject(obj);
        }
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

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

        for (int i = 0; i < iterations; i++)
        {
            csv = CsvHelperSerializer.Serialize(obj);
        }
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

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
}
