using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using CsvHelper;
using CsvHelper.Configuration;

public class F
{
    public int i1, i2, i3, i4, i5;
    public static F Get() => new F() { i1 = 1, i2 = 2, i3 = 3, i4 = 4, i5 = 5 };
}

// Вспомогательные методы расширения
public static class StringExtensions
{
    public static string Repeat(this string str, int count)
    {
        if (count <= 0) return string.Empty;
        if (count == 1) return str;

        var sb = new StringBuilder(str.Length * count);
        for (int i = 0; i < count; i++)
        {
            sb.Append(str);
        }
        return sb.ToString();
    }

    public static string Repeat(this char c, int count)
    {
        return new string(c, count);
    }
}

public class SystemInfo
{
    public string OSVersion { get; set; }
    public string FrameworkVersion { get; set; }
    public string CPUName { get; set; }
    public int CPUCores { get; set; }
    public int CPUThreads { get; set; }
    public string CPUSpeed { get; set; }
    public long TotalMemoryGB { get; set; }
    public long AvailableMemoryGB { get; set; }
    public string DiskType { get; set; }
    public string GPUName { get; set; }
    public string Resolution { get; set; }
    public bool Is64Bit { get; set; }
    public DateTime TestTime { get; set; }

    public static SystemInfo GetCurrent()
    {
        var info = new SystemInfo
        {
            OSVersion = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            FrameworkVersion = RuntimeInformation.FrameworkDescription,
            Is64Bit = Environment.Is64BitOperatingSystem,
            TestTime = DateTime.Now
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GetWindowsHardwareInfo(info);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                GetLinuxHardwareInfo(info);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                GetMacHardwareInfo(info);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении информации о системе: {ex.Message}");
        }

        return info;
    }

    private static void GetWindowsHardwareInfo(SystemInfo info)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.CPUName = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    info.CPUCores = int.TryParse(obj["NumberOfCores"]?.ToString(), out int cores) ? cores : 0;
                    info.CPUThreads = int.TryParse(obj["NumberOfLogicalProcessors"]?.ToString(), out int threads) ? threads : 0;

                    if (double.TryParse(obj["MaxClockSpeed"]?.ToString(), out double speed))
                        info.CPUSpeed = $"{speed} MHz";
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalMemory = ulong.TryParse(obj["TotalVisibleMemorySize"]?.ToString(), out ulong total) ? total : 0;
                    var freeMemory = ulong.TryParse(obj["FreePhysicalMemory"]?.ToString(), out ulong free) ? free : 0;
                    info.TotalMemoryGB = (long)(totalMemory / 1024 / 1024);
                    info.AvailableMemoryGB = (long)(freeMemory / 1024 / 1024);
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE MediaType = 'Fixed hard disk media'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.DiskType = obj["MediaType"]?.ToString() ?? "Unknown";
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.GPUName = obj["Name"]?.ToString() ?? "Unknown";
                    break;
                }
            }
        }
        catch { }
    }

    private static void GetLinuxHardwareInfo(SystemInfo info)
    {
        try
        {
            if (File.Exists("/proc/cpuinfo"))
            {
                var cpuInfo = File.ReadAllText("/proc/cpuinfo");
                var lines = cpuInfo.Split('\n');

                foreach (var line in lines)
                {
                    if (line.StartsWith("model name"))
                    {
                        info.CPUName = line.Split(':')[1].Trim();
                    }
                    else if (line.StartsWith("cpu cores"))
                    {
                        info.CPUCores = int.TryParse(line.Split(':')[1].Trim(), out int cores) ? cores : 0;
                    }
                }

                info.CPUThreads = Environment.ProcessorCount;
            }

            if (File.Exists("/proc/meminfo"))
            {
                var memInfo = File.ReadAllText("/proc/meminfo");
                var lines = memInfo.Split('\n');

                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal"))
                    {
                        var value = line.Split(':')[1].Trim().Replace(" kB", "");
                        if (long.TryParse(value, out long kb))
                            info.TotalMemoryGB = kb / 1024 / 1024;
                    }
                    else if (line.StartsWith("MemAvailable"))
                    {
                        var value = line.Split(':')[1].Trim().Replace(" kB", "");
                        if (long.TryParse(value, out long kb))
                            info.AvailableMemoryGB = kb / 1024 / 1024;
                    }
                }
            }
        }
        catch { }
    }

    private static void GetMacHardwareInfo(SystemInfo info)
    {
        info.CPUThreads = Environment.ProcessorCount;
        info.CPUCores = Environment.ProcessorCount / 2;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ИНФОРМАЦИЯ О СИСТЕМЕ ===");
        sb.AppendLine($"ОС: {OSVersion}");
        sb.AppendLine($"Framework: {FrameworkVersion}");
        sb.AppendLine($"Архитектура: {(Is64Bit ? "64-bit" : "32-bit")}");
        sb.AppendLine($"Процессор: {CPUName}");
        sb.AppendLine($"Ядра/Потоки: {CPUCores}/{CPUThreads}");
        sb.AppendLine($"Частота: {CPUSpeed}");
        sb.AppendLine($"Память ОЗУ: {TotalMemoryGB} GB (доступно: {AvailableMemoryGB} GB)");
        sb.AppendLine($"Диск: {DiskType}");
        sb.AppendLine($"Видеокарта: {GPUName}");
        sb.AppendLine($"Время теста: {TestTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("============================");
        return sb.ToString();
    }
}

public class PerformanceResults
{
    public SystemInfo SystemInfo { get; set; }
    public int Iterations { get; set; }
    public long ReflectionSerializeMs { get; set; }
    public long ReflectionDeserializeMs { get; set; }
    public long JsonSerializeMs { get; set; }
    public long JsonDeserializeMs { get; set; }
    public long CsvHelperSerializeMs { get; set; }
    public long CsvHelperDeserializeMs { get; set; }

    public void PrintResults()
    {
        Console.WriteLine($"\n=== РЕЗУЛЬТАТЫ ТЕСТИРОВАНИЯ ({Iterations:N0} итераций) ===");

        Console.WriteLine("\n1. Reflection CSV сериализатор:");
        Console.WriteLine($"   Сериализация: {ReflectionSerializeMs} мс");
        Console.WriteLine($"   Десериализация: {ReflectionDeserializeMs} мс");
        Console.WriteLine($"   Всего: {ReflectionSerializeMs + ReflectionDeserializeMs} мс");

        Console.WriteLine("\n2. Newtonsoft.Json:");
        Console.WriteLine($"   Сериализация: {JsonSerializeMs} мс");
        Console.WriteLine($"   Десериализация: {JsonDeserializeMs} мс");
        Console.WriteLine($"   Всего: {JsonSerializeMs + JsonDeserializeMs} мс");

        Console.WriteLine("\n3. CsvHelper:");
        Console.WriteLine($"   Сериализация: {CsvHelperSerializeMs} мс");
        Console.WriteLine($"   Десериализация: {CsvHelperDeserializeMs} мс");
        Console.WriteLine($"   Всего: {CsvHelperSerializeMs + CsvHelperDeserializeMs} мс");

        Console.WriteLine("\n=== СРАВНИТЕЛЬНАЯ ТАБЛИЦА ===");
        Console.WriteLine($"| Метод               | Сериализация | Десериализация | Всего      |");
        Console.WriteLine($"|---------------------|--------------|----------------|------------|");
        Console.WriteLine($"| Reflection CSV      | {ReflectionSerializeMs,6} мс    | {ReflectionDeserializeMs,6} мс      | {ReflectionSerializeMs + ReflectionDeserializeMs,6} мс |");
        Console.WriteLine($"| Newtonsoft.Json     | {JsonSerializeMs,6} мс    | {JsonDeserializeMs,6} мс      | {JsonSerializeMs + JsonDeserializeMs,6} мс |");
        Console.WriteLine($"| CsvHelper           | {CsvHelperSerializeMs,6} мс    | {CsvHelperDeserializeMs,6} мс      | {CsvHelperSerializeMs + CsvHelperDeserializeMs,6} мс |");

        var fastestTotal = Math.Min(
            ReflectionSerializeMs + ReflectionDeserializeMs,
            Math.Min(
                JsonSerializeMs + JsonDeserializeMs,
                CsvHelperSerializeMs + CsvHelperDeserializeMs
            )
        );

        Console.WriteLine($"\nОтносительная производительность (чем меньше, тем лучше):");
        Console.WriteLine($"Reflection CSV: {((double)(ReflectionSerializeMs + ReflectionDeserializeMs) / fastestTotal):F2}x");
        Console.WriteLine($"Newtonsoft.Json: {((double)(JsonSerializeMs + JsonDeserializeMs) / fastestTotal):F2}x");
        Console.WriteLine($"CsvHelper: {((double)(CsvHelperSerializeMs + CsvHelperDeserializeMs) / fastestTotal):F2}x");
    }
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
        Delimiter = ",",
    };

    public static string Serialize<T>(T obj)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, _config))
        {
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields.OrderBy(f => f.Name))
            {
                csv.WriteField(field.GetValue(obj)?.ToString() ?? "");
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
                        var value = Convert.ChangeType(valueStr, field.FieldType, CultureInfo.InvariantCulture);
                        field.SetValue(obj, value);
                    }
                    catch
                    {
                        // Если не удалось конвертировать, оставляем значение по умолчанию
                    }
                }
            }

            return obj;
        }
    }
}

//public static class CsvHelperSerializer
//{
//    private static readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
//    {
//        HasHeaderRecord = false,
//        Delimiter = ",",
//        MemberTypes = CsvHelper.Configuration.MemberTypes.Fields
//    };

//    public static string Serialize<T>(T obj)
//    {
//        using (var writer = new StringWriter())
//        using (var csv = new CsvWriter(writer, _config))
//        {
//            csv.WriteRecord(obj);
//            return writer.ToString().TrimEnd('\r', '\n');
//        }
//    }

//    public static T Deserialize<T>(string csv) where T : new()
//    {
//        using (var reader = new StringReader(csv))
//        using (var csvReader = new CsvReader(reader, _config))
//        {
//            var records = csvReader.GetRecords<T>();
//            return records.FirstOrDefault();
//        }
//    }
//}

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Получаем информацию о системе
        var systemInfo = SystemInfo.GetCurrent();
        Console.WriteLine(systemInfo.ToString());

        Console.WriteLine("=== СРАВНЕНИЕ ПРОИЗВОДИТЕЛЬНОСТИ СЕРИАЛИЗАТОРОВ ===");
        Console.WriteLine("1. Reflection CSV (самописный)");
        Console.WriteLine("2. Newtonsoft.Json (стандартный JSON)");
        Console.WriteLine("3. CsvHelper (оптимизированная библиотека CSV)\n");

        var obj = F.Get();

        // Проверяем корректность работы сериализаторов
        Console.WriteLine("Проверка корректности сериализации:");
        var reflectionResult = ReflectionCsvSerializer.Serialize(obj);
        var csvHelperResult = CsvHelperSerializer.Serialize(obj);
        var jsonResult = JsonConvert.SerializeObject(obj);

        Console.WriteLine($"Reflection CSV: {reflectionResult}");
        Console.WriteLine($"CsvHelper: {csvHelperResult}");
        Console.WriteLine($"Newtonsoft.Json: {jsonResult}");

        // Проверяем десериализацию
        var reflectionObj = ReflectionCsvSerializer.Deserialize<F>(reflectionResult);
        var csvHelperObj = CsvHelperSerializer.Deserialize<F>(csvHelperResult);
        var jsonObj = JsonConvert.DeserializeObject<F>(jsonResult);

        Console.WriteLine($"\nПроверка десериализации:");
        Console.WriteLine($"Reflection: {reflectionObj.i1},{reflectionObj.i2},{reflectionObj.i3},{reflectionObj.i4},{reflectionObj.i5}");
        Console.WriteLine($"CsvHelper: {csvHelperObj.i1},{csvHelperObj.i2},{csvHelperObj.i3},{csvHelperObj.i4},{csvHelperObj.i5}");
        Console.WriteLine($"Newtonsoft.Json: {jsonObj.i1},{jsonObj.i2},{jsonObj.i3},{jsonObj.i4},{jsonObj.i5}");

        // Запускаем тесты производительности
        var results = new List<PerformanceResults>();

        int[] testIterations = { 1000, 10000, 100000, 500000 };

        foreach (var iterations in testIterations)
        {
            // Используем new string() для повторения символов
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine($"ТЕСТИРОВАНИЕ С {iterations:N0} ИТЕРАЦИЯМИ");
            Console.WriteLine($"{new string('=', 60)}");

            var result = RunPerformanceTest(obj, iterations);
            result.SystemInfo = systemInfo;
            result.Iterations = iterations;
            result.PrintResults();
            results.Add(result);

            // Пауза между тестами для охлаждения
            if (iterations >= 100000)
            {
                Console.WriteLine("\nПауза 2 секунды для охлаждения процессора...");
                System.Threading.Thread.Sleep(2000);
            }
        }

        // Сохраняем результаты в файл
        SaveResultsToFile(results, systemInfo);

        Console.WriteLine("\nТестирование завершено! Результаты сохранены в файл 'performance_results.csv'");
    }

    static PerformanceResults RunPerformanceTest(F obj, int iterations)
    {
        var result = new PerformanceResults();

        // Тест Reflection CSV
        result.ReflectionSerializeMs = MeasureTime(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                ReflectionCsvSerializer.Serialize(obj);
            }
        });

        string csv = ReflectionCsvSerializer.Serialize(obj);
        result.ReflectionDeserializeMs = MeasureTime(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                ReflectionCsvSerializer.Deserialize<F>(csv);
            }
        });

        // Тест Newtonsoft.Json
        result.JsonSerializeMs = MeasureTime(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                JsonConvert.SerializeObject(obj);
            }
        });

        string json = JsonConvert.SerializeObject(obj);
        result.JsonDeserializeMs = MeasureTime(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                JsonConvert.DeserializeObject<F>(json);
            }
        });

        // Тест CsvHelper
        result.CsvHelperSerializeMs = MeasureTime(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                CsvHelperSerializer.Serialize(obj);
            }
        });

        string csv2 = CsvHelperSerializer.Serialize(obj);
        result.CsvHelperDeserializeMs = MeasureTime(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                CsvHelperSerializer.Deserialize<F>(csv2);
            }
        });

        return result;
    }

    static long MeasureTime(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    static void SaveResultsToFile(List<PerformanceResults> results, SystemInfo systemInfo)
    {
        try
        {
            using (var writer = new StreamWriter("performance_results.csv", false, Encoding.UTF8))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // Записываем информацию о системе
                writer.WriteLine("# ИНФОРМАЦИЯ О СИСТЕМЕ");
                writer.WriteLine($"# ОС: {systemInfo.OSVersion}");
                writer.WriteLine($"# Процессор: {systemInfo.CPUName}");
                writer.WriteLine($"# Ядра/Потоки: {systemInfo.CPUCores}/{systemInfo.CPUThreads}");
                writer.WriteLine($"# Память ОЗУ: {systemInfo.TotalMemoryGB} GB");
                writer.WriteLine($"# Время теста: {systemInfo.TestTime:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                // Записываем результаты
                csv.WriteRecords(results);
            }

            // Создаем HTML отчет
            CreateHtmlReport(results, systemInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении результатов: {ex.Message}");
        }
    }

    static void CreateHtmlReport(List<PerformanceResults> results, SystemInfo systemInfo)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Результаты тестирования сериализаторов</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .system-info {{ background: #f0f0f0; padding: 15px; border-radius: 5px; margin-bottom: 20px; }}
        .results {{ border-collapse: collapse; width: 100%; }}
        .results th, .results td {{ border: 1px solid #ddd; padding: 8px; text-align: right; }}
        .results th {{ background: #4CAF50; color: white; }}
        .results tr:nth-child(even) {{ background: #f2f2f2; }}
        .chart {{ margin: 20px 0; }}
        .winner {{ background: #d4edda !important; }}
        .slowest {{ background: #f8d7da !important; }}
    </style>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
</head>
<body>
    <h1>Результаты тестирования сериализаторов</h1>
    
    <div class=""system-info"">
        <h2>Информация о системе</h2>
        <p><strong>ОС:</strong> {systemInfo.OSVersion}</p>
        <p><strong>Процессор:</strong> {systemInfo.CPUName}</p>
        <p><strong>Ядра/Потоки:</strong> {systemInfo.CPUCores}/{systemInfo.CPUThreads}</p>
        <p><strong>Память ОЗУ:</strong> {systemInfo.TotalMemoryGB} GB</p>
        <p><strong>Время теста:</strong> {systemInfo.TestTime:yyyy-MM-dd HH:mm:ss}</p>
    </div>

    <h2>Результаты тестирования</h2>
    <table class=""results"">
        <tr>
            <th>Итераций</th>
            <th>Reflection CSV (мс)</th>
            <th>Newtonsoft.Json (мс)</th>
            <th>CsvHelper (мс)</th>
            <th>Победитель</th>
        </tr>";

        foreach (var result in results)
        {
            var reflectionTotal = result.ReflectionSerializeMs + result.ReflectionDeserializeMs;
            var jsonTotal = result.JsonSerializeMs + result.JsonDeserializeMs;
            var csvHelperTotal = result.CsvHelperSerializeMs + result.CsvHelperDeserializeMs;

            var minTotal = Math.Min(reflectionTotal, Math.Min(jsonTotal, csvHelperTotal));
            var winner = minTotal == reflectionTotal ? "Reflection CSV" :
                        minTotal == jsonTotal ? "Newtonsoft.Json" : "CsvHelper";

            var reflectionClass = minTotal == reflectionTotal ? "winner" : "slowest";
            var jsonClass = minTotal == jsonTotal ? "winner" : "";
            var csvHelperClass = minTotal == csvHelperTotal ? "winner" : "";

            html += $@"
        <tr>
            <td>{result.Iterations:N0}</td>
            <td class=""{reflectionClass}"">{reflectionTotal}</td>
            <td class=""{jsonClass}"">{jsonTotal}</td>
            <td class=""{csvHelperClass}"">{csvHelperTotal}</td>
            <td>{winner}</td>
        </tr>";
        }

        html += @"
    </table>

    <div class=""chart"">
        <canvas id=""performanceChart"" width=""800"" height=""400""></canvas>
    </div>

    <script>
        const ctx = document.getElementById('performanceChart').getContext('2d');
        const chart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: [" + string.Join(",", results.Select(r => $"'{r.Iterations:N0}'")) + @"],
                datasets: [
                    {
                        label: 'Reflection CSV',
                        data: [" + string.Join(",", results.Select(r => r.ReflectionSerializeMs + r.ReflectionDeserializeMs)) + @"],
                        backgroundColor: 'rgba(255, 99, 132, 0.5)',
                        borderColor: 'rgba(255, 99, 132, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Newtonsoft.Json',
                        data: [" + string.Join(",", results.Select(r => r.JsonSerializeMs + r.JsonDeserializeMs)) + @"],
                        backgroundColor: 'rgba(54, 162, 235, 0.5)',
                        borderColor: 'rgba(54, 162, 235, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'CsvHelper',
                        data: [" + string.Join(",", results.Select(r => r.CsvHelperSerializeMs + r.CsvHelperDeserializeMs)) + @"],
                        backgroundColor: 'rgba(75, 192, 192, 0.5)',
                        borderColor: 'rgba(75, 192, 192, 1)',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: 'Время (мс)'
                        }
                    },
                    x: {
                        title: {
                            display: true,
                            text: 'Количество итераций'
                        }
                    }
                }
            }
        });
    </script>
</body>
</html>";

        File.WriteAllText("performance_report.html", html, Encoding.UTF8);
    }
}
