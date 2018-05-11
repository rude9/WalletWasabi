﻿using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WalletWasabi.Logging
{
	public static class Logger
    {
		#region PropertiesAndMembers

		private static long On = 1;

		public static LogLevel MinimumLevel { get; private set; } = LogLevel.Critical;

        public static HashSet<LogMode> Modes { get; } = new HashSet<LogMode>();

        public static string FilePath { get; private set; } = "Log.txt";

        public static string EntrySeparator { get; private set; } = Environment.NewLine;

        public static string FileEntryEncryptionPassword { get; private set; } = null;

        private static long _logerFailed = Interlocked.Exchange(ref _logerFailed, 0);

		private static readonly object Lock = new object();

		/// <summary>
		/// KB
		/// </summary>
		public static long MaximumLogFileSize { get; private set; } = 10_000; // approx 10 MB

		#endregion

		#region Initializers

		public static void SetMinimumLevel(LogLevel level) => MinimumLevel = level;

		public static void SetModes(params LogMode[] modes)
        {
            if (Modes.Count != 0)
            {
                Modes.Clear();
            }

            if (modes != null)
            {
                foreach (var mode in modes)
                {
                    Modes.Add(mode);
                }
            }
        }

        public static void SetFilePath(string filePath) => FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);

        public static void SetEntrySeparator(string entrySeparator) => EntrySeparator = Guard.NotNull(nameof(entrySeparator), entrySeparator);

        public static void SetFileEntryEncryptionPassword(string password) => FileEntryEncryptionPassword = password;

		/// <summary>
		/// KB
		/// </summary>
		public static void SetMaximumLogFileSize(long sizeInKb) => MaximumLogFileSize = sizeInKb;

		#endregion

		#region Methods

		public static void TurnOff() => Interlocked.Exchange(ref On, 0);
		public static void TurnOn() => Interlocked.Exchange(ref On, 1);
		public static bool IsOn() => Interlocked.Read(ref On) == 1;

		public static void DecryptLogEntries(string destination)
        {
            var encrypted = File.ReadAllText(FilePath);

            var dir = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(dir);

            foreach(var entry in encrypted.Split(new char[]{','}, StringSplitOptions.RemoveEmptyEntries))
            {
                var decryptedEntry = StringCipher.Decrypt(entry, FileEntryEncryptionPassword);
                File.AppendAllText(destination, $"{decryptedEntry}{EntrySeparator}");
            }
        }

		#endregion

		#region LoggingMethods

		#region GeneralLoggingMethods

		private static void Log(LogLevel level, string message, string category)
        {
            try
            {
                if (Modes.Count == 0 || !IsOn())
                {
                    return;
                }

                if(level < MinimumLevel)
                {
                    return;
                }

                message = string.IsNullOrWhiteSpace(message) ? "" : message;
                category = string.IsNullOrWhiteSpace(category) ? "" : category;

                var finalLogMessage = $"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd hh:mm:ss} {level.ToString().ToUpperInvariant()} {category}: {message}{EntrySeparator}";

				lock (Lock)
				{
					if (Modes.Contains(LogMode.Console))
					{
						lock (Console.Out)
						{
							var color = Console.ForegroundColor;
							switch (level)
							{
								case LogLevel.Warning:
									color = ConsoleColor.Yellow;
									break;
								case LogLevel.Error:
								case LogLevel.Critical:
									color = ConsoleColor.Red;
									break;
							}

							Console.ForegroundColor = color;
							Console.Write(finalLogMessage);
							Console.ResetColor();
						}
					}

					if (Modes.Contains(LogMode.Console))
					{
						Debug.Write(finalLogMessage);
					}

					if (Modes.Contains(LogMode.File))
					{
						var dir = Path.GetDirectoryName(FilePath);
						if (dir != "")
						{
							Directory.CreateDirectory(dir);
						}

						if(File.Exists(FilePath))
						{
							var sizeInBytes = new FileInfo(FilePath).Length;
							if(sizeInBytes > 1000 * MaximumLogFileSize)
							{
								File.Delete(FilePath);
							}
						}

						if (FileEntryEncryptionPassword != null)
						{
							// take the separator down and add a comma (not base64)
							var replacedSeparatorWithCommaMessage = finalLogMessage.Substring(0, finalLogMessage.Length - EntrySeparator.Length);
							var encryptedLogMessage = StringCipher.Encrypt(replacedSeparatorWithCommaMessage, FileEntryEncryptionPassword) + ',';

							File.AppendAllText(FilePath, encryptedLogMessage);
						}
						else
						{
							File.AppendAllText(FilePath, finalLogMessage);
						}
					}
				}
            }
            catch(Exception ex)
            {
                Interlocked.Increment(ref _logerFailed);
                if(Interlocked.Read(ref _logerFailed) > 1)
                {
                    Interlocked.Exchange(ref _logerFailed, 0);
                    return;
                }
                LogDebug($"Logging failed: {ex}", $"{nameof(Logger)}.{nameof(Logging)}.{nameof(Logger)}");
            }
        }

        private static void Log(LogLevel level, string message, Type category)
        {
            if (category == null)
            {
                Log(level, message, "");
            }
            else
            {
                Log(level, message, category.ToString());
            }
        }

        private static void Log<T>(LogLevel level, string message) => Log(level, message, typeof(T).Name);

		#endregion

		#region ExceptionLoggingMethods

		private static void Log(Exception ex, LogLevel level, string category = "")
		{
			Log(level, ex.ToString(), category);
		}

		private static void Log<T>(Exception ex, LogLevel level)
		{
			Log<T>(level, ex.ToString());
		}

		private static void Log(Exception ex, LogLevel level, Type category = null)
		{
			Log(level, ex.ToString(), category);
		}

		#endregion

		#region TraceLoggingMethods

		/// <summary>
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace<T>(string message) => Log<T>(LogLevel.Trace, message);
        /// <summary>
        /// For information that is valuable only to a developer debugging an issue.
        /// These messages may contain sensitive application data and so should not be enabled in a production environment.
        /// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
        /// </summary>
        public static void LogTrace(string message, Type category) => Log(LogLevel.Trace, message, category);
        /// <summary>
        /// For information that is valuable only to a developer debugging an issue.
        /// These messages may contain sensitive application data and so should not be enabled in a production environment.
        /// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
        /// </summary>
        public static void LogTrace(string message, string category = "") => Log(LogLevel.Trace, message, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		/// 
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace<T>(Exception ex) => Log<T>(ex, LogLevel.Trace);
		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		/// 
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(Exception ex, Type category) => Log(ex, LogLevel.Trace, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		/// 
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(Exception ex, string category = "") => Log(ex, LogLevel.Trace, category);

		#endregion

		#region DebugLoggingMethods

		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug<T>(string message) => Log<T>(LogLevel.Debug, message);
		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug(string message, Type category) => Log(LogLevel.Debug, message, category);
		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug(string message, string category = "") => Log(LogLevel.Debug, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Debug level, if only Debug level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Debug, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// <paramref name="toStringLevel"/>Should be lower or equal to Debug level.
		/// </summary>
		public static void LogDebug<T>(Exception ex, LogLevel toStringLevel = LogLevel.Trace) => Log<T>(ex, LogLevel.Debug);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Debug level, if only Debug level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Debug, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// <paramref name="toStringLevel"/>Should be lower or equal to Debug level.
		/// </summary>
		public static void LogDebug(Exception ex, LogLevel toStringLevel = LogLevel.Trace, Type category = null) => Log(ex, LogLevel.Debug, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Debug level, if only Debug level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Debug, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// <paramref name="toStringLevel"/>Should be lower or equal to Debug level.
		/// </summary>
		public static void LogDebug(Exception ex, LogLevel toStringLevel = LogLevel.Trace, string category = "") => Log(ex, LogLevel.Debug, category);

		#endregion

		#region InfoLoggingMethods

		/// <summary>
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo<T>(string message) => Log<T>(LogLevel.Info, message);
        /// <summary>
        /// For tracking the general flow of the application.
        /// These logs typically have some long-term value.
        /// Example: "Request received for path /api/my-controller"
        /// </summary>
        public static void LogInfo(string message, Type category) => Log(LogLevel.Info, message, category);
        /// <summary>
        /// For tracking the general flow of the application.
        /// These logs typically have some long-term value.
        /// Example: "Request received for path /api/my-controller"
        /// </summary>
        public static void LogInfo(string message, string category = "") => Log(LogLevel.Info, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Info level, if only Info level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Info, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// <paramref name="toStringLevel"/>Should be lower or equal to Info level.
		/// </summary>
		public static void LogInfo<T>(Exception ex, LogLevel toStringLevel = LogLevel.Trace) => Log<T>(ex, LogLevel.Info);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Info level, if only Info level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Info, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// <paramref name="toStringLevel"/>Should be lower or equal to Info level.
		/// </summary>
		public static void LogInfo(Exception ex, LogLevel toStringLevel = LogLevel.Trace, Type category = null) => Log(ex, LogLevel.Info, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Info level, if only Info level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Info, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// <paramref name="toStringLevel"/>Should be lower or equal to Info level.
		/// </summary>
		public static void LogInfo(Exception ex, LogLevel toStringLevel = LogLevel.Trace, string category = "") => Log(ex, LogLevel.Info, category);

		#endregion

		#region WarningLoggingMethods

		/// <summary>
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning<T>(string message) => Log<T>(LogLevel.Warning, message);
        /// <summary>
        /// For abnormal or unexpected events in the application flow.
        /// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
        /// Handled exceptions are a common place to use the Warning log level.
        /// Example: "FileNotFoundException for file quotes.txt."
        /// </summary>
        public static void LogWarning(string message, Type category) => Log(LogLevel.Warning, message, category);
        /// <summary>
        /// For abnormal or unexpected events in the application flow.
        /// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
        /// Handled exceptions are a common place to use the Warning log level.
        /// Example: "FileNotFoundException for file quotes.txt."
        /// </summary>
        public static void LogWarning(string message, string category = "") => Log(LogLevel.Warning, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Warning level, if only Warning level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Warning, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// <paramref name="toStringLevel"/>Should be lower or equal to Warning level.
		/// </summary>
		public static void LogWarning<T>(Exception ex, LogLevel toStringLevel = LogLevel.Trace) => Log<T>(ex, LogLevel.Warning);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Warning level, if only Warning level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Warning, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// <paramref name="toStringLevel"/>Should be lower or equal to Warning level.
		/// </summary>
		public static void LogWarning(Exception ex, LogLevel toStringLevel = LogLevel.Trace, Type category = null) => Log(ex, LogLevel.Warning, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Warning level, if only Warning level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Warning, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// <paramref name="toStringLevel"/>Should be lower or equal to Warning level.
		/// </summary>
		public static void LogWarning(Exception ex, LogLevel toStringLevel = LogLevel.Trace, string category = "") => Log(ex, LogLevel.Warning, category);

		#endregion

		#region ErrorLoggingMethods

		/// <summary>
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError<T>(string message) => Log<T>(LogLevel.Error, message);
        /// <summary>
        /// For errors and exceptions that cannot be handled.
        /// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
        /// Example log message: "Cannot insert record due to duplicate key violation."
        /// </summary>
        public static void LogError(string message, Type category) => Log(LogLevel.Error, message, category);
        /// <summary>
        /// For errors and exceptions that cannot be handled.
        /// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
        /// Example log message: "Cannot insert record due to duplicate key violation."
        /// </summary>
        public static void LogError(string message, string category = "") => Log(LogLevel.Error, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Error level, if only Error level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Error, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// <paramref name="toStringLevel"/>Should be lower or equal to Error level.
		/// </summary>
		public static void LogError<T>(Exception ex, LogLevel toStringLevel = LogLevel.Trace) => Log<T>(ex, LogLevel.Error);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Error level, if only Error level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Error, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// <paramref name="toStringLevel"/>Should be lower or equal to Error level.
		/// </summary>
		public static void LogError(Exception ex, LogLevel toStringLevel = LogLevel.Trace, Type category = null) => Log(ex, LogLevel.Error, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Error level, if only Error level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Error, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// <paramref name="toStringLevel"/>Should be lower or equal to Error level.
		/// </summary>
		public static void LogError(Exception ex, LogLevel toStringLevel = LogLevel.Trace, string category = "") => Log(ex, LogLevel.Error, category);

		#endregion

		#region CriticalLoggingMethods

		/// <summary>
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical<T>(string message) => Log<T>(LogLevel.Critical, message);
        /// <summary>
        /// For failures that require immediate attention.
        /// Examples: data loss scenarios, out of disk space.
        /// </summary>
        public static void LogCritical(string message, Type category) => Log(LogLevel.Critical, message, category);
        /// <summary>
        /// For failures that require immediate attention.
        /// Examples: data loss scenarios, out of disk space.
        /// </summary>
        public static void LogCritical(string message, string category = "") => Log(LogLevel.Critical, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level, if only Critical level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Critical, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// <paramref name="toStringLevel"/>Should be lower or equal to Critical level.
		/// </summary>
		public static void LogCritical<T>(Exception ex, LogLevel toStringLevel = LogLevel.Trace) => Log<T>(ex, LogLevel.Critical);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level, if only Critical level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Critical, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// <paramref name="toStringLevel"/>Should be lower or equal to Critical level.
		/// </summary>
		public static void LogCritical(Exception ex, LogLevel toStringLevel = LogLevel.Trace, Type category = null) => Log(ex, LogLevel.Critical, category);
		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level, if only Critical level logging is set.
		/// Logs the <paramref name="ex"/>.ToString() at Critical, if <paramref name="toStringLevel"/> logging is also set.
		/// Note: this will never log at <paramref name="toStringLevel"/>.
		/// 
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// <paramref name="toStringLevel"/>Should be lower or equal to Critical level.
		/// </summary>
		public static void LogCritical(Exception ex, LogLevel toStringLevel = LogLevel.Trace, string category = "") => Log(ex, LogLevel.Critical, category);

		#endregion

		#endregion
	}
}
