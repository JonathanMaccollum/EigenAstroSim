namespace EigenAstroSim.UI.Services

open System
open System.Windows.Threading
open EigenAstroSim.UI

/// Simulation timer service that advances the simulation time
type SimulationTimerService(simulationEngine: SimulationEngine) =
    // The timer for simulation updates
    let timer = new DispatcherTimer()
    
    // Default update interval in milliseconds
    let defaultInterval = 100
    
    // Flag to track whether the timer is running
    let mutable isRunning = false
    
    // Initialize the timer
    do
        timer.Interval <- TimeSpan.FromMilliseconds(float defaultInterval)
        timer.Tick.Add(fun _ -> 
            // Calculate elapsed time in seconds
            let elapsedSeconds = float defaultInterval / 1000.0
            
            // Update the simulation
            simulationEngine.PostMessage(AdvanceTime elapsedSeconds)
        )
    
    /// Start the simulation timer
    member this.Start() =
        if not isRunning then
            timer.Start()
            isRunning <- true
    
    /// Stop the simulation timer
    member this.Stop() =
        if isRunning then
            timer.Stop()
            isRunning <- false
    
    /// Set the update interval in milliseconds
    member this.SetInterval(milliseconds: int) =
        timer.Interval <- TimeSpan.FromMilliseconds(float milliseconds)
    
    /// Toggle the timer state
    member this.Toggle() =
        if isRunning then
            this.Stop()
        else
            this.Start()
    
    /// Current running state
    member this.IsRunning = isRunning
    
    interface IDisposable with
        member this.Dispose() =
            this.Stop()