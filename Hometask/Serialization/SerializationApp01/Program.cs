using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

public class F
{
    public int i1, i2, i3, i4, i5;
    public static F Get() => new F() { i1 = 1, i2 = 2, i3 = 3, i4 = 4, i5 = 5 };
}

public class CsvSerializer
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
            .OrderBy(m => m.Name)  // Для стабильного порядка
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

        // Для числовых типов используем инвариантную культуру
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
            // Обработка основных типов
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

            // Общий случай
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

        Console.WriteLine("=== Тестирование CSV сериализатора на Reflection ===");

        // Создаем объект для тестирования
        var obj = F.Get();

        // Количество итераций для тестирования
        int iterations = 10000;

        // Тестируем Reflection сериализатор
        TestReflectionSerializer(obj, iterations);

        // Тестируем стандартный JSON сериализатор
        TestJsonSerializer(obj, iterations);

        Console.WriteLine("\n=== Результаты ===");
    }

    static void TestReflectionSerializer(F obj, int iterations)
    {
        Console.WriteLine($"\n1. Reflection CSV сериализатор ({iterations} итераций)");

        // Замер сериализации
        Stopwatch sw = Stopwatch.StartNew();
        string csv = string.Empty;

        for (int i = 0; i < iterations; i++)
        {
            csv = CsvSerializer.Serialize(obj);
        }

        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

        // Замер времени вывода в консоль
        sw.Restart();
        Console.WriteLine($"CSV строка: {csv}");
        sw.Stop();
        var consoleOutputTime = sw.ElapsedMilliseconds;

        // Замер десериализации
        sw.Restart();
        F deserializedObj = default;

        for (int i = 0; i < iterations; i++)
        {
            deserializedObj = CsvSerializer.Deserialize<F>(csv);
        }

        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        // Проверка корректности
        bool isValid = deserializedObj != null &&
                      deserializedObj.i1 == 1 &&
                      deserializedObj.i2 == 2 &&
                      deserializedObj.i3 == 3 &&
                      deserializedObj.i4 == 4 &&
                      deserializedObj.i5 == 5;

        Console.WriteLine($"Время сериализации: {serializeTime} мс");
        Console.WriteLine($"Время десериализации: {deserializeTime} мс");
        Console.WriteLine($"Время вывода в консоль: {consoleOutputTime} мс");
        Console.WriteLine($"Корректность: {isValid}");
    }

    static void TestJsonSerializer(F obj, int iterations)
    {
        

        Console.WriteLine($"\n2. Стандартный JSON сериализатор ({iterations} итераций)");

        // Замер сериализации JSON
        Stopwatch sw = Stopwatch.StartNew();
        string json = string.Empty;

        for (int i = 0; i < iterations; i++)
        {
            json = JsonConvert.SerializeObject(obj);
        }

        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"JSON строка: {json}");

        // Замер десериализации JSON
        sw.Restart();
        F deserializedObj = default;

        for (int i = 0; i < iterations; i++)
        {
            deserializedObj = JsonConvert.DeserializeObject<F>(json);
        }

        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Время сериализации (JSON): {serializeTime} мс");
        Console.WriteLine($"Время десериализации (JSON): {deserializeTime} мс");
    }
}