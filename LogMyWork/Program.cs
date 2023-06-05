using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

class Program
{
    private static List<string> keywords;
    private static int sleepDuration;

    static void Main()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow non-ASCII characters
        };

        var configJson = File.ReadAllText("config.json");
        var config = JsonSerializer.Deserialize<Config>(configJson, options);

        keywords = config.Keywords;
        sleepDuration = config.SleepDuration;

        var applicationInfos = new List<ApplicationInfo>();

        while (true) // Infinite loop
        {
            var processes = Process.GetProcesses();

            foreach (var keyword in keywords)
            {
                foreach (var process in processes.Where(p => p.ProcessName == keyword && !string.IsNullOrWhiteSpace(p.MainWindowTitle)))
                {
                    var existingApp = applicationInfos.FirstOrDefault(a => a.Name == process.ProcessName);
                    if (existingApp == null)
                    {
                        existingApp = new ApplicationInfo
                        {
                            Name = process.ProcessName,
                            ProcessInfos = new List<ProcessInfo>()
                        };

                        applicationInfos.Add(existingApp);
                        Console.WriteLine($"Added new application: {existingApp.Name}");
                    }

                    var existingProcess = existingApp.ProcessInfos.FirstOrDefault(p => p.Title == process.MainWindowTitle.Replace("\t", "").Trim());
                    if (existingProcess == null)
                    {
                        var info = new ProcessInfo
                        {
                            Time = $"00:00:00",
                            Title = process.MainWindowTitle.Replace("\t", "").Trim(),
                            StartTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")
                        };

                        existingApp.ProcessInfos.Add(info);
                        Console.WriteLine($"Added new process: {info.Title}");
                    }
                    else
                    {
                        var time = TimeSpan.Parse(existingProcess.Time);
                        time = time.Add(TimeSpan.FromMilliseconds(sleepDuration));
                        existingProcess.Time = $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";

                        Console.WriteLine($"Updated process: {existingProcess.Title}, New time: {existingProcess.Time}");
                    }
                }
            }

            var json = JsonSerializer.Serialize(applicationInfos, options);
            var filename = $"{DateTime.Now:dd-MM-yyyy}_processes.json";
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            File.WriteAllBytes(filename, jsonBytes);

            Console.WriteLine($"JSON file updated: {filename}\n");

            Thread.Sleep(sleepDuration); // Wait for specified duration
        }
    }
}

public class ProcessInfo
{
    public string Time { get; set; }
    public string Title { get; set; }
    public string StartTime { get; set; }
}

public class ApplicationInfo
{
    public string Name { get; set; }
    public List<ProcessInfo> ProcessInfos { get; set; }
}

public class Config
{
    public List<string> Keywords { get; set; }
    public int SleepDuration { get; set; }
}
