namespace EigenAstroSim.UI.Services

open System
open System.IO
open System.Threading

/// Simple file logger for debugging WPF applications
type FileLogger() =
    let logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt")
    let mutable writer: StreamWriter = null
    let lockObj = new Object()
    
    do
        // Create or clear the log file when the logger is initialized
        try
            // Make sure directory exists
            let directory = Path.GetDirectoryName(logFilePath)
            if not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore
                
            // Create or truncate the log file
            writer <- new StreamWriter(logFilePath, false)
            writer.AutoFlush <- true
            
            writer.WriteLine($"Log started at {DateTime.Now}")
            writer.WriteLine("----------------------------------------")
        with
        | ex -> 
            // Write to console as a fallback
            Console.WriteLine($"Error initializing logger: {ex.Message}")
    
    /// Logs a message to the file with timestamp
    member _.Log(message: string) =
        try
            lock lockObj (fun () ->
                if isNull writer || writer.BaseStream = null || not writer.BaseStream.CanWrite then
                    // Reopen file if it was closed
                    writer <- new StreamWriter(logFilePath, true)
                    writer.AutoFlush <- true
                
                let timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                writer.WriteLine($"[{timestamp}] {message}")
            )
        with
        | ex -> 
            // Write to console as a fallback
            Console.WriteLine($"Error writing to log: {ex.Message}")
    
    /// Log a formatted message with arguments
    member this.LogFormat(format: string, [<ParamArray>] args: obj[]) =
        try
            let message = String.Format(format, args)
            this.Log(message)
        with
        | ex -> 
            this.Log($"Error formatting log message: {ex.Message}")
    
    interface IDisposable with
        member _.Dispose() =
            try
                if not (isNull writer) then
                    lock lockObj (fun () ->
                        writer.WriteLine($"Log closed at {DateTime.Now}")
                        writer.WriteLine("----------------------------------------")
                        writer.Flush()
                        writer.Dispose()
                        writer <- null
                    )
            with
            | ex -> 
                Console.WriteLine($"Error disposing logger: {ex.Message}")

/// Static logger instance for easy access throughout the application
module Logger =
    let private logger = new FileLogger()
    
    /// Log a simple message
    let log message = 
        logger.Log(message)
    
    /// Log an object with its string representation
    let logObj (obj: obj) =
        match obj with
        | null -> logger.Log("null")
        | _ -> logger.Log(obj.ToString())
    
    /// Log a formatted message
    let logf format (args: obj[]) =
        logger.LogFormat(format, args)
    
    /// Log an exception with optional context message
    let logException (ex: Exception) (context: string option) =
        match context with
        | Some msg -> logger.Log($"Exception in {msg}: {ex.Message}\n{ex.StackTrace}")
        | None -> logger.Log($"Exception: {ex.Message}\n{ex.StackTrace}")
    
    /// Dispose the logger when application exits
    let dispose() =
        (logger :> IDisposable).Dispose()