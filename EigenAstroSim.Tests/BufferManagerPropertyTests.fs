namespace EigenAstroSim.Tests

module BufferPoolManagerPropertyTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain.BufferManagement
    
    // Helper to generate random values
    let random = Random()
    
    let randomWidth() = 10 + random.Next(190)  // 10-200 pixels
    let randomHeight() = 10 + random.Next(190) // 10-200 pixels
    let randomValue() = random.NextDouble() * 100.0 // 0-100 value
    
    // Test multiple property instances
    let testProperty iterations property =
        for _ in 1..iterations do
            let result = property()
            result |> should equal true
    
    [<Fact>]
    let ``Property: Reused buffers should always be zeroed`` () =
        testProperty 10 (fun () ->
            // Arrange
            let width = randomWidth()
            let height = randomHeight()
            let value = randomValue()
            
            use manager = new BufferPoolManager()
            let pool = manager.GetBufferPool width height
            
            // Get a buffer and set values
            let buffer1 = pool.GetBuffer()
            for i = 0 to width - 1 do
                for j = 0 to height - 1 do
                    buffer1.[i, j] <- value
                    
            // Return and get again
            pool.ReturnBuffer(buffer1)
            let buffer2 = pool.GetBuffer()
            
            // All values should be zero
            let mutable allZeros = true
            for i = 0 to width - 1 do
                for j = 0 to height - 1 do
                    if buffer2.[i, j] <> 0.0 then
                        allZeros <- false
                    
            allZeros
        )
    
    [<Fact>]
    let ``Property: Multiple buffer managers are isolated`` () =
        testProperty 10 (fun () ->
            // Arrange
            let width = randomWidth()
            let height = randomHeight()
            
            use manager1 = new BufferPoolManager()
            use manager2 = new BufferPoolManager()
            
            let pool1 = manager1.GetBufferPool width height
            let pool2 = manager2.GetBufferPool width height
            
            // Get buffers from both
            let buffer1 = pool1.GetBuffer()
            let buffer2 = pool2.GetBuffer()
            
            // Return to first pool
            pool1.ReturnBuffer(buffer1)
            
            // Get new buffers
            let buffer1b = pool1.GetBuffer()
            let buffer2b = pool2.GetBuffer()
            
            // First should be reused, second should be new
            Object.ReferenceEquals(buffer1, buffer1b) &&
            not (Object.ReferenceEquals(buffer2, buffer2b)) &&
            pool1.Reused = 1 &&
            pool2.Reused = 0
        )
    
    [<Fact>]
    let ``Property: Buffer dimensions always match requested dimensions`` () =
        testProperty 10 (fun () ->
            // Arrange
            let width = randomWidth()
            let height = randomHeight()
            
            use manager = new BufferPoolManager()
            let pool = manager.GetBufferPool width height
            
            let buffer = pool.GetBuffer()
            
            // Dimensions should match
            Array2D.length1 buffer = width && Array2D.length2 buffer = height
        )
        
    [<Fact>]
    let ``Property: Disposed PooledBuffer returns its buffer to the pool`` () =
        testProperty 10 (fun () ->
            // Arrange
            let width = randomWidth()
            let height = randomHeight()
            
            use manager = new BufferPoolManager()
            let pool = manager.GetBufferPool width height
            
            // Create a reference outside the using block to test against later
            let mutable bufferRef = null
            
            // Scope for pooled buffer
            do
                use pooledBuffer = createPooledBuffer manager width height
                bufferRef <- pooledBuffer.Buffer
            
            // Now get another buffer after the pooled one is disposed
            let buffer2 = pool.GetBuffer()
            
            // Should be the same instance and counted as reused
            Object.ReferenceEquals(bufferRef, buffer2) && pool.Reused = 1
        )