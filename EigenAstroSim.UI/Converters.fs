namespace EigenAstroSim.UI.Converters

open System
open System.Windows
open System.Windows.Data
open System.Windows.Media
open System.Windows.Media.Imaging
open System.Globalization
open System.IO

// Converts a boolean to its inverse
type InverseBoolConverter() =
    interface IValueConverter with
        member this.Convert(value, _, _, _) =
            match value with
            | :? bool as b -> not b :> obj
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(value, _, _, _) =
            match value with
            | :? bool as b -> not b :> obj
            | _ -> DependencyProperty.UnsetValue

// Converts a boolean to visibility (true = Visible, false = Collapsed)
type BoolToVisibilityConverter() =
    interface IValueConverter with
        member this.Convert(value, _, _, _) =
            match value with
            | :? bool as b when b -> Visibility.Visible :> obj
            | _ -> Visibility.Collapsed :> obj
        
        member this.ConvertBack(value, _, _, _) =
            match value with
            | :? Visibility as v -> (v = Visibility.Visible) :> obj
            | _ -> DependencyProperty.UnsetValue

// Converts byte array to an image source
type ByteArrayToImageConverter() =
    interface IValueConverter with
        member this.Convert(value, _, parameter, _) =
            match value with
            | :? (byte[]) as bytes when bytes.Length > 0 ->
                try
                    // Get dimensions from parameter if available, otherwise use default
                    let width, height = 
                        match parameter with
                        | :? string as s ->
                            let parts = s.Split('x')
                            if parts.Length = 2 then
                                try
                                    let w = Int32.Parse(parts.[0])
                                    let h = Int32.Parse(parts.[1])
                                    w, h
                                with _ -> 800, 600
                            else
                                800, 600
                        | _ -> 800, 600
                    
                    // Calculate pixel count and create pixel array
                    let pixelCount = width * height
                    let pixelValues = Array.zeroCreate<byte> (pixelCount * 4) // BGRA format (4 bytes per pixel)
                    
                    // Find min/max values for normalization
                    let mutable minVal = Double.MaxValue
                    let mutable maxVal = Double.MinValue
                    
                    // Determine if we're dealing with a float array or a direct image byte array
                    if bytes.Length >= 8 && bytes.Length % 8 = 0 then
                        // Likely a double array from our image generator
                        for i = 0 to Math.Min(bytes.Length / 8 - 1, pixelCount - 1) do
                            let value = BitConverter.ToDouble(bytes, i * 8)
                            if not (Double.IsNaN(value) || Double.IsInfinity(value)) then
                                minVal <- min minVal value
                                maxVal <- max maxVal value
                        
                        // Calculate range for normalization
                        let range = maxVal - minVal
                        
                        // Convert to BGRA format
                        for i = 0 to Math.Min(bytes.Length / 8 - 1, pixelCount - 1) do
                            let value = BitConverter.ToDouble(bytes, i * 8)
                            let pixelIdx = i * 4
                            
                            // Normalize to 0-255 range
                            let normalizedValue = 
                                if range > 0.0 then
                                    int (((value - minVal) / range) * 255.0)
                                else
                                    128 // Default mid-gray if no range
                            
                            let byteValue = byte (Math.Min(Math.Max(normalizedValue, 0), 255))
                            
                            // BGRA format (B, G, R, A)
                            pixelValues.[pixelIdx] <- byteValue     // B
                            pixelValues.[pixelIdx + 1] <- byteValue // G
                            pixelValues.[pixelIdx + 2] <- byteValue // R
                            pixelValues.[pixelIdx + 3] <- 255uy     // A (fully opaque)
                    else
                        // Might be a direct byte array - handle as grayscale
                        let bytesPerPixel = bytes.Length / pixelCount
                        for i = 0 to Math.Min(bytes.Length - 1, pixelCount * bytesPerPixel - 1) / bytesPerPixel do
                            let pixelIdx = i * 4
                            let byteIdx = i * bytesPerPixel
                            
                            if byteIdx < bytes.Length then
                                let byteValue = bytes.[byteIdx]
                                
                                // BGRA format (B, G, R, A)
                                pixelValues.[pixelIdx] <- byteValue     // B
                                pixelValues.[pixelIdx + 1] <- byteValue // G
                                pixelValues.[pixelIdx + 2] <- byteValue // R
                                pixelValues.[pixelIdx + 3] <- 255uy     // A (fully opaque)
                    
                    // Create the bitmap
                    let dpiX, dpiY = 96.0, 96.0
                    let stride = width * 4 // 4 bytes per pixel (BGRA)
                    let bitmap = BitmapSource.Create(
                        width, height, 
                        dpiX, dpiY, 
                        PixelFormats.Bgra32, 
                        null, 
                        pixelValues, 
                        stride)
                    
                    bitmap :> obj
                with ex -> 
                    System.Diagnostics.Debug.WriteLine($"Error converting byte array to image: {ex.Message}")
                    DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(_, _, _, _) =
            DependencyProperty.UnsetValue


/// <summary>
/// Converts a float array to an Image source for display in the UI
/// </summary>
type FloatArrayToImageConverter() =
    interface IValueConverter with
        member this.Convert(value, _, parameter, _) =
            match value with
            | :? (float[]) as floats when floats.Length > 0 ->
                try
                    // Get dimensions from parameter if available, otherwise use default
                    let width, height = 
                        match parameter with
                        | :? string as s ->
                            let parts = s.Split('x')
                            if parts.Length = 2 then
                                try
                                    let w = Int32.Parse(parts.[0])
                                    let h = Int32.Parse(parts.[1])
                                    w, h
                                with _ -> 800, 600
                            else
                                800, 600
                        | _ -> 800, 600
                    
                    // Calculate pixel count and create pixel array
                    let pixelCount = width * height
                    let pixelValues = Array.zeroCreate<byte> (pixelCount * 4) // BGRA format (4 bytes per pixel)
                    
                    // Find min/max values for normalization
                    let mutable minVal = Double.MaxValue
                    let mutable maxVal = Double.MinValue
                    
                    // Get min/max for normalization
                    for i = 0 to Math.Min(floats.Length - 1, pixelCount - 1) do
                        let value = floats.[i]
                        if not (Double.IsNaN(value) || Double.IsInfinity(value)) then
                            minVal <- min minVal value
                            maxVal <- max maxVal value
                    
                    // Calculate range for normalization
                    let range = maxVal - minVal
                    
                    // Convert to BGRA format
                    for i = 0 to Math.Min(floats.Length - 1, pixelCount - 1) do
                        let value = floats.[i]
                        let pixelIdx = i * 4
                        
                        // Normalize to 0-255 range
                        let normalizedValue = 
                            if range > 0.0 then
                                int (((value - minVal) / range) * 255.0)
                            else
                                128 // Default mid-gray if no range
                        
                        let byteValue = byte (Math.Min(Math.Max(normalizedValue, 0), 255))
                        
                        // BGRA format (B, G, R, A)
                        pixelValues.[pixelIdx] <- byteValue     // B
                        pixelValues.[pixelIdx + 1] <- byteValue // G
                        pixelValues.[pixelIdx + 2] <- byteValue // R
                        pixelValues.[pixelIdx + 3] <- 255uy     // A (fully opaque)
                    
                    // Create the bitmap
                    let dpiX, dpiY = 96.0, 96.0
                    let stride = width * 4 // 4 bytes per pixel (BGRA)
                    let bitmap = BitmapSource.Create(
                        width, height, 
                        dpiX, dpiY, 
                        PixelFormats.Bgra32, 
                        null, 
                        pixelValues, 
                        stride)
                    
                    bitmap :> obj
                with ex -> 
                    System.Diagnostics.Debug.WriteLine($"Error converting float array to image: {ex.Message}")
                    DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(_, _, _, _) =
            DependencyProperty.UnsetValue

// Converts a right ascension value in degrees to HH:MM:SS.SS format
type RAToHMSConverter() =
    interface IValueConverter with
        member this.Convert(value, _, _, _) =
            match value with
            | :? float as ra ->
                // Convert degrees to hours (15 degrees = 1 hour)
                let totalHours = ra / 15.0
                
                // Extract hours, minutes, seconds
                let hours = Math.Floor(totalHours)
                let totalMinutes = (totalHours - hours) * 60.0
                let minutes = Math.Floor(totalMinutes)
                let seconds = (totalMinutes - minutes) * 60.0
                
                // Format as HH:MM:SS.SS
                sprintf "%02d:%02d:%05.2f" (int hours) (int minutes) seconds :> obj
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(value, _, _, _) =
            match value with
            | :? string as hms ->
                try
                    // Parse the HH:MM:SS.SS format
                    let parts = hms.Split(':')
                    if parts.Length = 3 then
                        let hours = Double.Parse(parts.[0], CultureInfo.InvariantCulture)
                        let minutes = Double.Parse(parts.[1], CultureInfo.InvariantCulture)
                        let seconds = Double.Parse(parts.[2], CultureInfo.InvariantCulture)
                        
                        // Convert to degrees
                        let totalHours = hours + minutes / 60.0 + seconds / 3600.0
                        (totalHours * 15.0) :> obj
                    else
                        DependencyProperty.UnsetValue
                with
                | _ -> DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue

// Converts a declination value in degrees to DD:MM:SS.SS format
type DecToDMSConverter() =
    interface IValueConverter with
        member this.Convert(value, _, _, _) =
            match value with
            | :? float as dec ->
                // Determine sign
                let sign = if dec >= 0.0 then "+" else "-"
                let absDec = Math.Abs(dec)
                
                // Extract degrees, minutes, seconds
                let degrees = Math.Floor(absDec)
                let totalMinutes = (absDec - degrees) * 60.0
                let minutes = Math.Floor(totalMinutes)
                let seconds = (totalMinutes - minutes) * 60.0
                
                // Format as +/-DD:MM:SS.SS
                sprintf "%s%02d:%02d:%05.2f" sign (int degrees) (int minutes) seconds :> obj
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(value, _, _, _) =
            match value with
            | :? string as dms ->
                try
                    // Parse the +/-DD:MM:SS.SS format
                    let sign = if dms.StartsWith("-") then -1.0 else 1.0
                    let cleanDms = dms.TrimStart([|'+'; '-'|])
                    let parts = cleanDms.Split(':')
                    
                    if parts.Length = 3 then
                        let degrees = Double.Parse(parts.[0], CultureInfo.InvariantCulture)
                        let minutes = Double.Parse(parts.[1], CultureInfo.InvariantCulture)
                        let seconds = Double.Parse(parts.[2], CultureInfo.InvariantCulture)
                        
                        // Convert to decimal degrees
                        let decimalDegrees = degrees + minutes / 60.0 + seconds / 3600.0
                        (sign * decimalDegrees) :> obj
                    else
                        DependencyProperty.UnsetValue
                with
                | _ -> DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue

// Converts a numeric value to a percentage string
type PercentageConverter() =
    interface IValueConverter with
        member this.Convert(value, _, _, _) =
            match value with
            | :? float as v -> (v * 100.0).ToString("0") + "%" :> obj
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(value, _, _, _) =
            match value with
            | :? string as s ->
                try
                    let percentValue = s.TrimEnd([|'%'|])
                    let value = Double.Parse(percentValue, CultureInfo.InvariantCulture)
                    (value / 100.0) :> obj
                with
                | _ -> DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue

/// <summary>
/// Converts a float array to an Image source with user-controlled stretching
/// </summary>
type FloatArrayToImageWithStretchConverter() =
    interface IValueConverter with
        member this.Convert(value, _, parameter, _) =
            match value with
            | :? (float[]) as floats when floats.Length > 0 ->
                try
                    // Parse parameters: format "width,height,logStretch,blackPoint,whitePoint"
                    let width, height, logStretch, blackPoint, whitePoint = 
                        match parameter with
                        | :? string as s ->
                            let parts = s.Split(',')
                            if parts.Length >= 5 then
                                try
                                    let w = Int32.Parse(parts.[0])
                                    let h = Int32.Parse(parts.[1])
                                    let ls = Double.Parse(parts.[2])
                                    let bp = Double.Parse(parts.[3])
                                    let wp = Double.Parse(parts.[4])
                                    w, h, ls, bp, wp
                                with _ -> 800, 600, 1.0, 0.0, 1.0
                            else
                                800, 600, 1.0, 0.0, 1.0
                        | _ -> 800, 600, 1.0, 0.0, 1.0
                    
                    let pixelCount = width * height
                    let pixelValues = Array.zeroCreate<byte> (pixelCount * 4)
                    
                    // Convert to BGRA format with user-controlled stretching
                    for i = 0 to Math.Min(floats.Length - 1, pixelCount - 1) do
                        let value = floats.[i]
                        let pixelIdx = i * 4
                        
                        // Apply black point
                        let adjustedValue = max 0.0 (value - blackPoint)
                        
                        // Apply logarithmic stretch
                        let stretchedValue = 
                            if logStretch > 0.0 then
                                if value <= blackPoint then 0.0
                                else 
                                    let logVal = Math.Log10(adjustedValue + 1.0)
                                    logVal * logStretch
                            else
                                adjustedValue
                        
                        // Scale to 0-255 range using white point
                        let finalValue = 
                            let normalized = stretchedValue / whitePoint
                            int (Math.Min(Math.Max(normalized * 255.0, 0.0), 255.0))
                        
                        let byteValue = byte finalValue
                        
                        // BGRA format
                        pixelValues.[pixelIdx] <- byteValue
                        pixelValues.[pixelIdx + 1] <- byteValue
                        pixelValues.[pixelIdx + 2] <- byteValue
                        pixelValues.[pixelIdx + 3] <- 255uy
                    
                    // Create the bitmap
                    let dpiX, dpiY = 96.0, 96.0
                    let stride = width * 4
                    let bitmap = BitmapSource.Create(
                        width, height, 
                        dpiX, dpiY, 
                        PixelFormats.Bgra32, 
                        null, 
                        pixelValues, 
                        stride)
                    
                    bitmap :> obj
                with ex -> 
                    System.Diagnostics.Debug.WriteLine($"Error converting float array to image: {ex.Message}")
                    DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(_, _, _, _) =
            DependencyProperty.UnsetValue

type FloatArrayWithStretchMultiConverter() =
    interface IMultiValueConverter with
        member this.Convert(values, _, _, _) =
            if values.Length >= 6 && 
               values.[0] :? float[] && 
               values.[1] :? int && 
               values.[2] :? int && 
               values.[3] :? double && 
               values.[4] :? double && 
               values.[5] :? double then
                
                let floats = values.[0] :?> float[]
                let width = values.[1] :?> int
                let height = values.[2] :?> int
                let logStretch = values.[3] :?> double
                let blackPointPercent = values.[4] :?> double // 0-100
                let whitePointPercent = values.[5] :?> double // 0-100
                
                try
                    let pixelCount = width * height
                    let pixelValues = Array.zeroCreate<byte> (pixelCount * 4)
                    
                    let maxVal = 65535.0
                    
                    // Calculate black and white point values based on actual range
                    let blackPointValue = (blackPointPercent / 100.0) * maxVal
                    let whitePointValue = (whitePointPercent / 100.0) * maxVal
                    
                    System.Diagnostics.Debug.WriteLine($"Actual max value: {maxVal}, Black point: {blackPointValue}, White point: {whitePointValue}")
                    
                    // Convert to BGRA format with user-controlled stretching
                    for i = 0 to Math.Min(floats.Length - 1, pixelCount - 1) do
                        let value = floats.[i]
                        let pixelIdx = i * 4
                        
                        // Apply black point and white point
                        let clippedValue = 
                            if value <= blackPointValue then 0.0
                            elif value >= whitePointValue then 1.0
                            elif whitePointValue <= blackPointValue then 0.0 // Avoid division by zero
                            else 
                                // Scale value between black and white points to 0-1 range
                                (value - blackPointValue) / (whitePointValue - blackPointValue)
                        
                        // Apply logarithmic stretch (only if value > 0)
                        let stretchedValue = 
                            if logStretch > 0.0 && clippedValue > 0.0 then
                                // Apply logarithmic transform
                                let stretchFactor = 1.0 + logStretch
                                let logVal = Math.Log(1.0 + clippedValue * (stretchFactor - 1.0)) / Math.Log(stretchFactor)
                                logVal
                            else
                                clippedValue
                        
                        // Convert to 0-255 byte range
                        let finalValue = int (Math.Round(stretchedValue * 255.0))
                        let byteValue = byte (Math.Max(0, Math.Min(255, finalValue)))
                        
                        // BGRA format
                        pixelValues.[pixelIdx] <- byteValue
                        pixelValues.[pixelIdx + 1] <- byteValue
                        pixelValues.[pixelIdx + 2] <- byteValue
                        pixelValues.[pixelIdx + 3] <- 255uy
                    
                    // Create the bitmap
                    let dpiX, dpiY = 96.0, 96.0
                    let stride = width * 4
                    let bitmap = BitmapSource.Create(
                        width, height, 
                        dpiX, dpiY, 
                        PixelFormats.Bgra32, 
                        null, 
                        pixelValues, 
                        stride)
                    
                    bitmap :> obj
                with ex -> 
                    System.Diagnostics.Debug.WriteLine($"Error converting float array to image: {ex.Message}")
                    DependencyProperty.UnsetValue
            else
                DependencyProperty.UnsetValue
        
        member this.ConvertBack(_, targetTypes, _, _) =
            Array.init targetTypes.Length (fun _ -> DependencyProperty.UnsetValue)