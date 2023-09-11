using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

class Program
{
    private static List<string> keywords;
    private static int sleepDuration;

    static void Main()
    {
        SQLitePCL.Batteries.Init();

        InitializeDatabase();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var configJson = File.ReadAllText("config.json");
        var config = JsonSerializer.Deserialize<Config>(configJson, options);

        keywords = config.Keywords;
        sleepDuration = config.SleepDuration;

        var applicationInfos = new List<ApplicationInfo>();

        while (true)
        {
            DateTime now = DateTime.Now;

            if (now.TimeOfDay >= TimeSpan.FromHours(8) && now.TimeOfDay <= TimeSpan.FromHours(17))
            {
                var activeProcess = GetActiveProcess();

                if (keywords.Contains(activeProcess.ProcessName))
                {
                    // Hae ja normalisoi ikkunan otsikko
                    var title = activeProcess.MainWindowTitle.Replace("\t", "").Trim();
                    var normalizedTitle = NormalizeWindowTitle(title);

                    var existingApp = applicationInfos.FirstOrDefault(a => a.Name == activeProcess.ProcessName);
                    if (existingApp == null)
                    {
                        existingApp = new ApplicationInfo
                        {
                            Name = activeProcess.ProcessName,
                            ProcessInfos = new List<ProcessInfo>()
                        };

                        applicationInfos.Add(existingApp);
                        Console.WriteLine($"Added new application: {existingApp.Name}");
                        InsertOrUpdateApplication(existingApp);
                    }

                    var existingProcess = existingApp.ProcessInfos.FirstOrDefault(p => p.Title == activeProcess.MainWindowTitle.Replace("\t", "").Trim());
                    if (existingProcess == null)
                    {
                        var info = new ProcessInfo
                        {
                            Time = $"00:00:05",  // Alusta aika 5 sekunnilla, koska uusi prosessi havaitaan
                            Title = normalizedTitle,
                            StartTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")
                        };

                        existingApp.ProcessInfos.Add(info);
                        Console.WriteLine($"Added new process: {info.Title}");
                        InsertOrUpdateProcessInfo(info, existingApp.Name);
                    }
                    else
                    {
                        var time = TimeSpan.Parse(existingProcess.Time);
                        time = time.Add(TimeSpan.FromSeconds(5)); // Lisää 5 sekuntia, ei millisekuntia
                        existingProcess.Time = $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";

                        Console.WriteLine($"Updated process: {existingProcess.Title}, New time: {existingProcess.Time}");
                        InsertOrUpdateProcessInfo(existingProcess, existingApp.Name);  // Päivitä tietokantaa uudella ajalla
                    }
                }
            }

            Thread.Sleep(sleepDuration);
        }
    }

    public static void InitializeDatabase()
    {
        using (var connection = new SqliteConnection("Data Source=processes.db"))
        {
            connection.Open();

            string createApplicationTable = @"
            CREATE TABLE IF NOT EXISTS Applications (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL
            );";

            string createProcessInfoTable = @"
            CREATE TABLE IF NOT EXISTS ProcessInfos (
                Id INTEGER PRIMARY KEY,
                Time TEXT NOT NULL,
                Title TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                ApplicationId INTEGER,
                FOREIGN KEY(ApplicationId) REFERENCES Applications(Id)
            );";

            using (var command = new SqliteCommand(createApplicationTable, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SqliteCommand(createProcessInfoTable, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public static void InsertOrUpdateApplication(ApplicationInfo appInfo)
    {
        using (var connection = new SqliteConnection("Data Source=processes.db"))
        {
            connection.Open();

            var cmdCheck = new SqliteCommand($"SELECT Id FROM Applications WHERE Name = '{appInfo.Name}'", connection);
            var result = cmdCheck.ExecuteScalar();
            if (result != null)
            {
                // (If needed, add logic to update the existing application)
            }
            else
            {
                var cmdInsert = new SqliteCommand($"INSERT INTO Applications (Name) VALUES ('{appInfo.Name}')", connection);
                cmdInsert.ExecuteNonQuery();
            }
        }
    }

    public static void InsertOrUpdateProcessInfo(ProcessInfo pInfo, string appName)
    {
        using (var connection = new SqliteConnection("Data Source=processes.db"))
        {
            connection.Open();

            var cmdGetAppId = new SqliteCommand($"SELECT Id FROM Applications WHERE Name = '{appName}'", connection);
            var appId = cmdGetAppId.ExecuteScalar();

            if (appId != null)
            {
                var cmdCheck = new SqliteCommand($"SELECT Id FROM ProcessInfos WHERE Title = '{pInfo.Title}' AND ApplicationId = {appId}", connection);
                var result = cmdCheck.ExecuteScalar();
                if (result != null)
                {
                    // Update the existing process info
                    var cmdUpdate = new SqliteCommand($"UPDATE ProcessInfos SET Time = '{pInfo.Time}' WHERE Id = {result}", connection);
                    cmdUpdate.ExecuteNonQuery();
                }
                else
                {
                    var cmdInsert = new SqliteCommand($"INSERT INTO ProcessInfos (Time, Title, StartTime, ApplicationId) VALUES ('{pInfo.Time}', '{pInfo.Title}', '{pInfo.StartTime}', {appId})", connection);
                    cmdInsert.ExecuteNonQuery();
                }
            }
        }
    }

    public static string NormalizeWindowTitle(string title)
    {
        // Poista suluissa olevat osat, kuten "Inbox (1)"
        var cleanTitle = Regex.Replace(title, @"\(\d+\)", "").Trim();

        // Poista "x new item" tai vastaavat
        cleanTitle = Regex.Replace(cleanTitle, @"\d+ new item[s]*", "", RegexOptions.IgnoreCase).Trim();

        // Poista ylimääräiset välilyönnit
        cleanTitle = Regex.Replace(cleanTitle, @"\s+", " ");

        // Poista ylimääräiset "- -"
        cleanTitle = Regex.Replace(cleanTitle, @"- -", "-").Trim();

        return cleanTitle;
    }

    private static Process GetActiveProcess()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        return Process.GetProcessById((int)pid);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

    public static int GetWeekOfMonth(DateTime date)
    {
        DateTime beginningOfMonth = new DateTime(date.Year, date.Month, 1);

        while (date.Date.AddDays(1).DayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
            date = date.AddDays(1);

        return (int)Math.Truncate((double)date.Subtract(beginningOfMonth).TotalDays / 7f) + 1;
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
}