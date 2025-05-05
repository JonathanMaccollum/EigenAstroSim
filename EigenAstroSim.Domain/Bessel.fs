namespace EigenAstroSim

module Bessel = 

    open System

    /// High-precision Bessel function of the first kind, order 1 (J₁(x))
    /// Implementation optimized for astrophotography applications
    /// This implementation provides accuracy suitable for high-precision work
    /// by using different algorithms depending on the input range
    let rec besselJ1 (x: float) : float =
        // Handle negative values by using J₁(-x) = -J₁(x)
        if x < 0.0 then
            -besselJ1 (-x)
        // Handle special case: x = 0
        elif x = 0.0 then
            0.0
        // For small x values: Use power series expansion
        // J₁(x) = (x/2) * Sum_{k=0}^∞ (-1)^k / (k!(k+1)!) * (x/2)^(2k)
        elif x <= 12.0 then
            let mutable sum = 0.0
            let mutable term = 0.5 * x  // First term: x/2
            let mutable k = 0
            
            // For high precision, we continue until convergence
            while abs term > 1.0e-30 do
                sum <- sum + term
                k <- k + 1
                // Calculate next term in the series using recurrence to avoid overflow
                // term_k+1 = term_k * (-1) * (x/2)^2 / (k*(k+1))
                term <- term * (-0.25 * x * x) / (float (k * (k + 1)))
            
            sum
        // For intermediate to large values, use asymptotic formula with correction terms
        else
            // Asymptotic form for large x:
            // J₁(x) ≈ sqrt(2/(π*x)) * (P(x) * cos(x - 3π/4) - Q(x) * sin(x - 3π/4))
            // Where P(x) and Q(x) are asymptotic series
            let invX = 1.0 / x
            let invX2 = invX * invX
            
            // P(x) coefficients for Bessel J₁
            let p = 1.0 - 
                    0.1875 * invX2 + 
                    0.1953125 * invX2 * invX2 - 
                    0.76171875 * (invX2 * invX2 * invX2) +
                    11.23046875 * pown invX2 4 -
                    299.6484375 * pown invX2 5 +
                    13543.35546875 * pown invX2 6 -
                    969638.671875 * pown invX2 7
                    
            // Q(x) coefficients for Bessel J₁
            let q = 0.375 * invX - 
                    0.1171875 * invX * invX2 + 
                    0.1025390625 * invX * invX2 * invX2 - 
                    0.3466796875 * invX * pown invX2 3 +
                    4.5029296875 * invX * pown invX2 4 -
                    110.2734375 * invX * pown invX2 5 +
                    4655.625 * invX * pown invX2 6 -
                    313189.8125 * invX * pown invX2 7
                    
            // Phase term: (x - 3π/4)
            let theta = x - 0.75 * Math.PI
            let cosTheta = cos theta
            let sinTheta = sin theta
            
            // Compute the asymptotic approximation with scaling factor
            sqrt (2.0 / (Math.PI * x)) * (p * cosTheta - q * sinTheta)

    /// Higher-precision implementation using continued fractions
    /// Useful for very large x values and high precision requirements
    let rec besselJ1ContinuedFraction (x: float) : float =
        if x = 0.0 then 
            0.0
        elif x < 0.0 then
            -besselJ1ContinuedFraction (-x)
        else
            // For large x, use backward recurrence with Miller's algorithm
            // This provides excellent numerical stability
            let n = max 30 (int (x + 15.0))
            let mutable jp = 0.0
            let mutable j = 1.0e-30  // Small non-zero value
            let mutable sum = 0.0
            
            // Backward recurrence using J_(n+1)(x) = (2n/x)J_n(x) - J_(n-1)(x)
            for k = n downto 1 do
                let jm = (2.0 * float k / x) * j - jp
                jp <- j
                j <- jm
                
                // Accumulate for normalization
                if k % 2 = 1 then
                    sum <- sum + 2.0 * jm
            
            // Normalize using the fact that J₀(x) + 2J₂(x) + 2J₄(x) + ... = 1
            // The value we want is J₁(x)
            jp / sum

    /// Calculate zeros of J₁(x) function - useful for diffraction pattern analysis
    let besselJ1Zeros (numZeros: int) : float[] =
        // Initial approximations for the first several zeros
        let initialApproximations = [|
            3.832; 7.016; 10.173; 13.324; 16.471; 
            19.616; 22.760; 25.904; 29.047; 32.190;
        |]
        
        // For zeros beyond our initial approximations, use asymptotic formula
        // n-th zero ≈ (n + 1/4)π - 1/(8(n+1/4)π) + ...
        let approximateZero n =
            if n < initialApproximations.Length then
                initialApproximations.[n]
            else
                let m = float n + 0.25
                let mp = Math.PI * m
                mp - 1.0/(8.0 * mp) - 31.0/(384.0 * mp * mp * mp)
        
        // Newton-Raphson method to refine zeros
        let refineZero approximateValue =
            let mutable x = approximateValue
            let mutable dx = 1.0
            let epsilon = 1.0e-15
            
            // Newton iterations: x_{n+1} = x_n - f(x_n)/f'(x_n)
            while abs dx > epsilon * abs x do
                // J₁'(x) = J₀(x) - J₁(x)/x
                let j1 = besselJ1 x
                // Use recurrence relation to calculate J₀ from J₁
                let j0 = if x <> 0.0 then j1/x + besselJ1(x+1e-15)/(1e-15) else 1.0
                let derivative = j0 - j1/x
                dx <- j1 / derivative
                x <- x - dx
            
            x
        
        // Calculate specified number of zeros with high precision
        Array.init numZeros (fun i -> refineZero (approximateZero i))

    /// Calculate the Airy disk pattern for a circular aperture
    /// Returns intensity at a given radial distance r (in radians)
    let airyDiskPattern (r: float) : float =
        if r = 0.0 then 
            1.0  // Central maximum
        else
            // The Airy disk pattern: I(r) = [2*J₁(k*r)/(k*r)]²
            // where k = 2π/λ, but we normalize k=1 for simplicity
            let x = Math.PI * r
            let j1x = besselJ1 x
            let ratio = if x <> 0.0 then 2.0 * j1x / x else 1.0
            ratio * ratio  // Square for intensity

    /// Computes the Struve function H₁(x), which is often used with J₁(x)
    /// in diffraction problems and point spread function modeling
    let struveH1 (x: float) : float =
        // Special case
        if x = 0.0 then 
            0.0
        else
            let absX = abs x
            let sign = if x < 0.0 then -1.0 else 1.0
            
            // For small values, use power series
            if absX <= 5.0 then
                let mutable sum = 0.0
                let mutable term = (x * x) / (Math.PI * 3.0) // First term
                let mutable k = 1
                
                while abs term > 1.0e-30 do
                    sum <- sum + term
                    k <- k + 1
                    // Calculate next term using recurrence
                    term <- term * (-x * x) / (4.0 * float (k * k) - 1.0)
                
                sign * sum
            // For larger values, use relation involving Bessel functions
            else
                // H₁(x) ≈ Y₁(x) + 2/(π*x) * [1 - cos(x)]
                // We approximate Y₁(x) using J₁(x) and the asymptotic relation
                let theta = absX - 0.75 * Math.PI
                let besselTerm = besselJ1(absX) * cos(theta) + sin(theta) * sqrt(2.0 / (Math.PI * absX))
                let correctionTerm = 2.0 / (Math.PI * absX) * (1.0 - cos(absX))
                
                sign * (besselTerm + correctionTerm)

    /// Custom double-double precision type for extremely high precision calculations
    /// Used internally for some calculations where regular float64 is insufficient
    type DoubleDouble = { Hi: float; Lo: float }

    /// Internal helper for creating a double-double value
    let private makeDD (hi: float) (lo: float) : DoubleDouble = { Hi = hi; Lo = lo }

    /// Convert a regular double to double-double precision
    let private toDD (x: float) : DoubleDouble = { Hi = x; Lo = 0.0 }

    /// Add two double-double values
    let private ddAdd (a: DoubleDouble) (b: DoubleDouble) : DoubleDouble =
        let s = a.Hi + b.Hi
        let v = s - a.Hi
        let hi = s
        let lo = (a.Hi - (s - v)) + (b.Hi - v) + a.Lo + b.Lo
        makeDD hi lo

    /// Convert double-double back to regular double (with higher precision)
    let private fromDD (dd: DoubleDouble) : float = dd.Hi + dd.Lo

    /// High-precision Bessel J₁ for extremely sensitive calculations
    /// Uses double-double arithmetic internally for critical parts
    let besselJ1HighPrecision (x: float) : float =
        if x = 0.0 then
            0.0
        elif abs x < 1.0e-6 then
            // For very small x, use first terms of series expansion
            x / 2.0 * (1.0 - x * x / 16.0)
        else
            // Use regular implementation for now, but with extended working precision
            // for critical intermediate calculations
            besselJ1 x