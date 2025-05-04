namespace EigenAstroSim.UI

open System
open System.Windows.Threading
open EigenAstroSim.UI

type SimulationTimer(simulationEngine: SimulationEngine) =
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
            printfn "Starting SimulationTimer"
            timer.Start()
            isRunning <- true
    member this.Stop() =
        if isRunning then
            printfn "Stopping SimulationTimer"
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