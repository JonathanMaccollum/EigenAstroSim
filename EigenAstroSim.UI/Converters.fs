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
        member this.Convert(value, targetType, parameter, culture) =
            match value with
            | :? bool as b -> not b :> obj
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(value, targetType, parameter, culture) =
            match value with
            | :? bool as b -> not b :> obj
            | _ -> DependencyProperty.UnsetValue

// Converts a boolean to visibility (true = Visible, false = Collapsed)
type BoolToVisibilityConverter() =
    interface IValueConverter with
        member this.Convert(value, targetType, parameter, culture) =
            match value with
            | :? bool as b when b -> Visibility.Visible :> obj
            | _ -> Visibility.Collapsed :> obj
        
        member this.ConvertBack(value, targetType, parameter, culture) =
            match value with
            | :? Visibility as v -> (v = Visibility.Visible) :> obj
            | _ -> DependencyProperty.UnsetValue

// Converts byte array to an image source
type ByteArrayToImageConverter() =
    interface IValueConverter with
        member this.Convert(value, targetType, parameter, culture) =
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
        
        member this.ConvertBack(value, targetType, parameter, culture) =
            DependencyProperty.UnsetValue

// Converts a right ascension value in degrees to HH:MM:SS.SS format
type RAToHMSConverter() =
    interface IValueConverter with
        member this.Convert(value, targetType, parameter, culture) =
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
        
        member this.ConvertBack(value, targetType, parameter, culture) =
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
        member this.Convert(value, targetType, parameter, culture) =
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
        
        member this.ConvertBack(value, targetType, parameter, culture) =
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
        member this.Convert(value, targetType, parameter, culture) =
            match value with
            | :? float as v -> (v * 100.0).ToString("0") + "%" :> obj
            | _ -> DependencyProperty.UnsetValue
        
        member this.ConvertBack(value, targetType, parameter, culture) =
            match value with
            | :? string as s ->
                try
                    let percentValue = s.TrimEnd([|'%'|])
                    let value = Double.Parse(percentValue, CultureInfo.InvariantCulture)
                    (value / 100.0) :> obj
                with
                | _ -> DependencyProperty.UnsetValue
            | _ -> DependencyProperty.UnsetValue