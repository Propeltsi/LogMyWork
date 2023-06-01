# .NET 7 Process Logger

This project is a console application that logs the usage time of specified applications running on a Windows operating system. The application was developed using C# and .NET 7.

## Features

- Monitors and logs the usage time of specified applications.
- Logs are generated in JSON format and saved to a file.
- The console application runs in a loop, updating the log file every 5 seconds.
- The log file includes the name of the application, the window title, the start time, and the total time the application was active.

## How it Works

The application uses the Process.GetProcesses method to retrieve information about the currently active processes. It then filters this information based on a list of specified keywords, which represent the names of the applications that should be logged.

The application logs the usage time of each matching process, as well as its window title and start time. This information is then written to a JSON file, which is updated every 5 seconds.

## Usage

Simply run the console application. It will begin monitoring and logging the specified applications immediately.

## Project Status

This project is currently in a Work in Progress (WIP) state. There are plans for further development and improvements.

## Contributions

Contributions are welcome. Please feel free to submit a pull request or open an issue.

## License

This project is licensed under the MIT License. See the LICENSE file for details.
