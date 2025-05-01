namespace EigenAstroSim.UI.Services

open System
open System.Windows.Threading
open EigenAstroSim.UI

type SimulationTimerService(simulationEngine: SimulationEngine) =
    let timer = new DispatcherTimer()
    let defaultInterval = 100
    let mutable isRunning = false
    do
        timer.Interval <- TimeSpan.FromMilliseconds(float defaultInterval)
        timer.Tick.Add(fun _ -> 
            let elapsedSeconds = float defaultInterval / 1000.0
            simulationEngine.PostMessage(AdvanceTime elapsedSeconds)
        )
    member this.Start() =
        if not isRunning then
            printfn "Starting SimulationTimerService"
            timer.Start()
            isRunning <- true
    member this.Stop() =
        if isRunning then
            printfn "Stopping SimulationTimerService"
            timer.Stop()
            isRunning <- false
    member this.SetInterval(milliseconds: int) =
        timer.Interval <- TimeSpan.FromMilliseconds(float milliseconds)
    member this.Toggle() =
        if isRunning then
            this.Stop()
        else
            this.Start()
    member this.IsRunning = isRunning
    interface IDisposable with
        member this.Dispose() =
            this.Stop()