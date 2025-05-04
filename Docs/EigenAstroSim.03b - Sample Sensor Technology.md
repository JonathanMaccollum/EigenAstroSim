// Astrophotography Camera Specifications Database
// For use in EigenAstroSim sensor simulation

namespace AstroSim.Cameras

open System.Collections.Generic

/// Represents the key technical specifications needed for sensor simulation
type SensorSpecification = {
    Name: string
    SensorModel: string
    Resolution: (int * int)    // Width x Height
    PixelSize: float          // microns
    QuantumEfficiency: float  // 0.0 to 1.0
    ReadNoise: float         // electrons RMS
    DarkCurrent: float       // e-/pixel/second at 0°C
    FullWellCapacity: int    // electrons
    ADC: int                 // bits
    GainSettings: Map<int, float>  // Gain setting -> e-/ADU
    RecommendedSettings: OperatingModes
}

and OperatingModes = {
    UnityGain: float * string     // Gain setting, notes
    LowNoiseGain: float * string  // Gain setting for lowest read noise
    GuideSettings: float list     // Common gain settings for guiding
    DeepSkySettings: float list   // Common gain settings for deep sky
}

/// QHY5L-II-M Guide Camera Specifications
let qhy5l2m_specification = {
    Name = "QHY5L-II-M"
    SensorModel = "APTINA MT9M034"
    Resolution = (1280, 960)
    PixelSize = 3.75
    QuantumEfficiency = 0.74    // 74% QE
    ReadNoise = 4.0            // ~4 electrons at minimum gain
    DarkCurrent = 0.05         // Low dark current (estimated)
    FullWellCapacity = 10000   // Estimated based on pixel size
    ADC = 14                   // 14-bit ADC
    GainSettings = Map [
        (0, 7.0);              // Low gain: ~7e-/ADU, ~7e- read noise
        (50, 5.0);             // Medium gain
        (100, 4.0);            // Near "unity": ~4e- read noise
        (175, 2.0);            // High gain: ~2e- read noise
    ]
    RecommendedSettings = {
        UnityGain = (100, "Around gain 100 gives ~1e-/ADU")
        LowNoiseGain = (175, "Lowest read noise at ~2e-")
        GuideSettings = [20; 35; 50]  // Common PHD2 settings
        DeepSkySettings = [100; 139]  // Higher gain for DSO
    }
}

/// ASI1600MM Pro Camera Specifications
let asi1600mm_specification = {
    Name = "ASI1600MM-Pro"
    SensorModel = "Panasonic MN34230"
    Resolution = (4656, 3520)
    PixelSize = 3.8
    QuantumEfficiency = 0.60    // ~60% QE peak
    ReadNoise = 1.8            // 1.8e- at gain 150
    DarkCurrent = 0.005        // Very low dark current when cooled
    FullWellCapacity = 20000   // 20,000 electrons
    ADC = 12                   // 12-bit ADC
    GainSettings = Map [
        (0, 0.2);              // Minimum gain: 0.2e-/ADU
        (76, 0.133);           // Common gain setting
        (139, 0.0625);         // Unity gain: 0.0625e-/ADU
        (200, 0.04);           // High gain for narrowband
        (300, 0.025);          // Very high gain
    ]
    RecommendedSettings = {
        UnityGain = (139, "Unity gain at 139")
        LowNoiseGain = (300, "Lowest read noise ~1.5e-")
        GuideSettings = [20; 35; 50]
        DeepSkySettings = [0; 76; 139; 200]
    }
}

/// QHY600M Camera Specifications  
let qhy600m_specification = {
    Name = "QHY600M"
    SensorModel = "Sony IMX455"
    Resolution = (9576, 6388)  // 61 MP
    PixelSize = 3.76
    QuantumEfficiency = 0.88    // ~88% QE (back-illuminated)
    ReadNoise = 2.0            // Base read noise in standard mode
    DarkCurrent = 0.003        // Very low dark current
    FullWellCapacity = 51000   // 51,000 electrons
    ADC = 16                   // 16-bit ADC
    GainSettings = Map [
        (0, 2.0);              // Photographic DSO mode
        (26, 1.6);             // Balanced mode
        (56, 1.0);             // High gain mode
        (100, 0.5);            // Very high gain
    ]
    RecommendedSettings = {
        UnityGain = (56, "Approximately 1e-/ADU")
        LowNoiseGain = (0, "Extended full well mode")
        GuideSettings = [20; 35]
        DeepSkySettings = [0; 26; 56]
    }
}

/// Common operational parameters that combine sensor specs
type CameraSimulationParams = {
    Sensor: SensorSpecification
    OperatingTemperature: float  // °C
    EffectiveGain: float       // e-/ADU based on gain setting
    EffectiveReadNoise: float  // electrons RMS
    EffectiveDarkCurrent: float // e-/pixel/second
    EffectiveQE: float         // May vary with temperature
}

/// Calculate effective values for a specific gain setting
let calculateEffectiveParams (sensor: SensorSpecification) (gainSetting: int) (temperature: float) : CameraSimulationParams =
    let gain = Map.tryFind gainSetting sensor.GainSettings |> Option.defaultValue 1.0
    
    // Adjust dark current with temperature (doubles for every 6°C)
    let tempFactor = System.Math.Pow(2.0, (temperature - 0.0) / 6.0)
    let adjustedDarkCurrent = sensor.DarkCurrent * tempFactor
    
    // Adjust read noise with gain (empirical approximation)
    let adjustedReadNoise = 
        match sensor.Name with
        | "QHY5L-II-M" when gainSetting > 100 -> 
            7.0 - (float gainSetting / 50.0) |> max 2.0
        | "ASI1600MM-Pro" when gainSetting > 139 ->
            3.8 - (float gainSetting / 150.0) |> max 1.5
        | _ -> sensor.ReadNoise
    
    // Temperature affects QE slightly
    let adjustedQE = sensor.QuantumEfficiency * (1.0 - (temperature - 0.0) / 500.0)
    
    {
        Sensor = sensor
        OperatingTemperature = temperature
        EffectiveGain = gain
        EffectiveReadNoise = adjustedReadNoise
        EffectiveDarkCurrent = adjustedDarkCurrent
        EffectiveQE = adjustedQE
    }

/// Database of all supported cameras
let cameraDatabase: Map<string, SensorSpecification> = 
    Map [
        ("QHY5L-II-M", qhy5l2m_specification)
        ("ASI1600MM-Pro", asi1600mm_specification)
        ("QHY600M", qhy600m_specification)
    ]

/// Get specifications by camera name
let getSensorSpecification (cameraName: string) =
    Map.tryFind cameraName cameraDatabase

/// Common usage example for sensor simulation
let example() =
    let qhy5params = calculateEffectiveParams qhy5l2m_specification 35 (-10.0)
    printfn "QHY5L-II-M at gain 35, -10°C:"
    printfn "  Read noise: %.1f e-" qhy5params.EffectiveReadNoise
    printfn "  Gain: %.3f e-/ADU" qhy5params.EffectiveGain
    printfn "  Dark current: %.5f e-/pixel/sec" qhy5params.EffectiveDarkCurrent
    printfn "  QE: %.1f%%" (qhy5params.EffectiveQE * 100.0)

/// For simulator control panel - get recommended settings
let getRecommendedGuidingSettings (cameraName: string) =
    getSensorSpecification cameraName
    |> Option.map (fun spec -> spec.RecommendedSettings.GuideSettings)
    |> Option.defaultValue []

/// For simulator UI - get common gain ranges
let getGainRangeForCamera (cameraName: string) =
    getSensorSpecification cameraName
    |> Option.map (fun spec -> 
        let gainKeys = spec.GainSettings |> Map.toList |> List.map fst
        let minGain = List.min gainKeys
        let maxGain = List.max gainKeys
        (minGain, maxGain))
    |> Option.defaultValue (0, 300)

/// Generate realistic sensor noise for the simulator
let generateSensorNoise 
    (params: CameraSimulationParams) 
    (exposureTime: float) 
    (pixelValue: float) 
    : float =
    
    let random = System.Random()
    
    // Shot noise (Poisson)
    let photonCount = pixelValue / params.EffectiveGain / params.EffectiveQE
    let shotNoise = sqrt photonCount
    
    // Dark current noise
    let darkElectrons = params.EffectiveDarkCurrent * exposureTime
    let darkNoise = sqrt darkElectrons
    
    // Read noise (Gaussian)
    let readNoise = params.EffectiveReadNoise
    
    // Total noise
    let totalNoise = sqrt (shotNoise ** 2.0 + darkNoise ** 2.0 + readNoise ** 2.0)
    
    // Convert back to ADU
    totalNoise * params.EffectiveGain

/// Generate realistic hot pixel distribution
let generateHotPixels 
    (sensor: SensorSpecification) 
    (temperature: float) 
    (exposureTime: float) : Map<(int * int), float> =
    
    let hotPixelRate = 0.001 * (System.Math.Pow(2.0, (temperature + 20.0) / 10.0))
    let totalPixels = float (fst sensor.Resolution * snd sensor.Resolution)
    let hotPixelCount = int (totalPixels * hotPixelRate)
    
    let random = System.Random()
    
    [for _ in 1..hotPixelCount ->
        let x = random.Next(fst sensor.Resolution)
        let y = random.Next(snd sensor.Resolution)
        let intensity = exposureTime * sensor.DarkCurrent * (random.NextDouble() * 9.0 + 1.0)
        ((x, y), intensity)
    ]
    |> Map.ofList