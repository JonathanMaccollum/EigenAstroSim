namespace EigenAstroSim.Domain

type ILogger =
    abstract member Debug : string -> unit
    abstract member Info : string -> unit
    abstract member Warn : string -> unit
    abstract member Error : string -> unit
    abstract member Fatal : string -> unit
    
    abstract member WarnException : exn -> string -> unit
    abstract member ErrorException : exn -> string -> unit
    abstract member FatalException : exn -> string -> unit

module Logger =
    let private noOpLogger =
        { new ILogger with
            member _.Debug message = ()
            member _.Info message = ()
            member _.Warn message = ()
            member _.Error message = ()
            member _.Fatal message = ()
            
            member _.WarnException ex message = ()
            member _.ErrorException ex message = ()
            member _.FatalException ex message = () }
            
    let mutable private loggerFactory = fun (_: string) -> noOpLogger
    let setFactory (factory: string -> ILogger) =
        loggerFactory <- factory
    let getLogger<'T> () = 
        let typeName = typeof<'T>.FullName
        loggerFactory typeName
    let getLoggerByName name = 
        loggerFactory name
    
    // Basic logging methods
    let Debug message =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.Debug message
    let Info message =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.Info message
    let Warn message =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.Warn message
    let Error message =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.Error message
    let Fatal message =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.Fatal message
    
    // Formatted logging methods
    let Debugf format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.Debug s) format
    let Infof format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.Info s) format
    let Warnf format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.Warn s) format
    let Errorf format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.Error s) format
    let Fatalf format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.Fatal s) format
        
    // Exception logging methods
    let WarnException (ex: System.Exception) (message: string) =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.WarnException ex message
    let ErrorException (ex: System.Exception) (message: string) =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.ErrorException ex message
    let FatalException (ex: System.Exception) (message: string) =
        let defaultLogger = getLoggerByName "Default"
        defaultLogger.FatalException ex message
        
    // Formatted exception logging methods
    let WarnExceptionf (ex: System.Exception) format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.WarnException ex s) format
    let ErrorExceptionf (ex: System.Exception) format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.ErrorException ex s) format
    let FatalExceptionf (ex: System.Exception) format =
        let defaultLogger = getLoggerByName "Default"
        Printf.kprintf (fun s -> defaultLogger.FatalException ex s) format
[<AutoOpen>]
module LoggerExtensions =
    // Formatted logging functions
    let Debugf (logger: ILogger) format =
        Printf.kprintf (fun s -> logger.Debug s) format
    let Infof (logger: ILogger) format =
        Printf.kprintf (fun s -> logger.Info s) format
    let Warnf (logger: ILogger) format =
        Printf.kprintf (fun s -> logger.Warn s) format
    let Errorf (logger: ILogger) format =
        Printf.kprintf (fun s -> logger.Error s) format
    let Fatalf (logger: ILogger) format =
        Printf.kprintf (fun s -> logger.Fatal s) format
    
    // Exception logging with optional message
    let WarnExceptionOpt (logger: ILogger) ex messageOpt =
        let message = defaultArg messageOpt ""
        logger.WarnException ex message
    let ErrorExceptionOpt (logger: ILogger) ex messageOpt =
        let message = defaultArg messageOpt ""
        logger.ErrorException ex message
    let FatalExceptionOpt (logger: ILogger) ex messageOpt =
        let message = defaultArg messageOpt ""
        logger.FatalException ex message
    
    // Formatted exception logging functions
    let WarnExceptionf (logger: ILogger) ex format =
        Printf.kprintf (fun s -> logger.WarnException ex s) format
    let ErrorExceptionf (logger: ILogger) ex format =
        Printf.kprintf (fun s -> logger.ErrorException ex s) format
    let FatalExceptionf (logger: ILogger) ex format =
        Printf.kprintf (fun s -> logger.FatalException ex s) format
    
    // Extension methods for ILogger
    type ILogger with
        member this.Debugf format = Debugf this format
        member this.Infof format = Infof this format
        member this.Warnf format = Warnf this format
        member this.Errorf format = Errorf this format
        member this.Fatalf format = Fatalf this format
        
        member this.WarnExceptionOpt ex messageOpt = WarnExceptionOpt this ex messageOpt
        member this.ErrorExceptionOpt ex messageOpt = ErrorExceptionOpt this ex messageOpt
        member this.FatalExceptionOpt ex messageOpt = FatalExceptionOpt this ex messageOpt
        
        member this.WarnExceptionf ex format = WarnExceptionf this ex format
        member this.ErrorExceptionf ex format = ErrorExceptionf this ex format
        member this.FatalExceptionf ex format = FatalExceptionf this ex format