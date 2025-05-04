using EigenAstroSim.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Core;
using System;

namespace EigenAstroSim.UI.Views
{
    public class FSharpLoggerAdapter : EigenAstroSim.Domain.ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public FSharpLoggerAdapter(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Debug(string message)
        {
            _logger.LogDebug(message);
        }

        public void Info(string message)
        {
            _logger.LogInformation(message);
        }

        public void Warn(string message)
        {
            _logger.LogWarning(message);
        }

        public void Error(string message)
        {
            _logger.LogError(message);
        }

        public void Fatal(string message)
        {
            _logger.LogCritical(message);
        }

        public void WarnException(Exception ex, string message)
        {
            _logger.LogWarning(ex, message);
        }

        public void ErrorException(Exception ex, string message)
        {
            _logger.LogError(ex, message);
        }

        public void FatalException(Exception ex, string message)
        {
            _logger.LogCritical(ex, message);
        }
    }
    public static class LoggingExtensions
    {
        public static EigenAstroSim.Domain.ILogger CreateFSharpLogger(this ILoggerFactory loggerFactory, string categoryName)
        {
            var msLogger = loggerFactory.CreateLogger(categoryName);
            return new FSharpLoggerAdapter(msLogger);
        }

        public static void ConfigureFSharpLogging(this ILoggerFactory loggerFactory)
        {
            // Convert C# lambda to F# function type using FuncConvert.FromFunc
            var factory = FuncConvert.FromFunc<string, EigenAstroSim.Domain.ILogger>(
                name => new FSharpLoggerAdapter(loggerFactory.CreateLogger(name))
            );

            Logger.setFactory(factory);
        }
    }
}